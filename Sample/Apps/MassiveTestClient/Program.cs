namespace Mithril.Apps.MassiveTestClient;

using Mithril.AppServices.MassiveTest;
using Mithril.Logger;
using Mithril.Network;
using Mithril.Network.Config;
using Mithril.Protocol;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class Program
{
  static async Task Main(string[] args)
  {
    var appLogger = new LoggerBuilder(
      Serilog.Events.LogEventLevel.Information,
      1024)
      .AddConsoleWriter()
      .Build();

    var configPath = args.Length > 0
      ? args[0]
      : $"./{AppDomain.CurrentDomain.FriendlyName}.json";

    if (File.Exists(configPath) == false)
    {
      appLogger.Error($"Config file not found: {configPath}");
      return;
    }

    ClientSettings? settings;

    try
    {
      settings = JsonSerializer.Deserialize<ClientSettings>(
        File.ReadAllText(configPath),
        new JsonSerializerOptions
        {
          PropertyNameCaseInsensitive = true,
          Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        });
    }
    catch (Exception ex)
    {
      appLogger.Error($"Config parse error: {ex.Message}");
      return;
    }

    if (settings == null)
    {
      appLogger.Error("Config is null.");
      return;
    }

    // Phase 2 시작 시 10,000개 세션이 동시에 송수신을 시작한다.
    // ThreadPool이 서서히 스레드를 추가하면(기본 1개/500ms) 수분이 걸릴 수 있으므로
    // 미리 충분한 수의 스레드를 확보해 즉시 처리되도록 한다.
    var minThreads = Math.Max(settings.Client.TotalConnections / 8, Environment.ProcessorCount * 4);
    ThreadPool.SetMinThreads(minThreads, minThreads);

    var networkFramework = new NetworkFramework(appLogger, settings.Network, new PacketMetadata());
    var connector = networkFramework.CreateConnector();

    if (IPAddress.TryParse(settings.Client.ServerAddress, out var ip) == false)
    {
      appLogger.Error($"Invalid server address: {settings.Client.ServerAddress}");
      return;
    }

    var endPoint = new IPEndPoint(ip, settings.Client.ServerPort);
    var config = settings.Client;
    var statistics = new Statistics();
    var reporter = new StatsReporter(appLogger, statistics, config);

    using var cts = new CancellationTokenSource();

    Console.CancelKeyPress += (_, e) =>
    {
      e.Cancel = true;
      appLogger.Info("Stopping...");
      cts.Cancel();
    };

    var durationLabel = config.DurationSeconds > 0
      ? $"duration: {config.DurationSeconds}s"
      : "duration: unlimited";

    appLogger.Info(
      $"Starting MassiveTestClient" +
      $" → {endPoint}" +
      $"  connections: {config.TotalConnections}" +
      $"  mode: {config.Mode}" +
      (config.Mode == SendMode.RateLimited ? $"  rate: {config.RatePerSecond}/s" : string.Empty) +
      $"  shutdown: {config.ShutdownMode}" +
      $"  close fanout: {(config.CloseFanoutParallelism > 0 ? config.CloseFanoutParallelism.ToString() : "auto")}" +
      $"  {durationLabel}");

    var reporterTask = Task.Run(async () =>
    {
      try
      {
        await reporter.RunAsync(cts.Token);
      }
      catch (OperationCanceledException)
      {
      }
    });

    // ── Phase 1: 연결 수립 ──────────────────────────────────────────────────
    // DurationSeconds 타이머를 아직 시작하지 않는다.
    // 연결 중 ThreadPool 포화로 ConnectAsync 완료가 지연되는 문제를 방지하기 위해
    // 모든 연결이 완료된 이후에 트래픽(Phase 2)을 시작한다.
    var connectStart = DateTime.UtcNow;
    var runners = new List<ConnectionRunner>(config.TotalConnections);
    var connectTasks = new List<Task>(config.TotalConnections);

    for (var i = 0; i < config.TotalConnections; i += config.ConnectBatchSize)
    {
      if (cts.Token.IsCancellationRequested)
      {
        break;
      }

      var batchEnd = Math.Min(i + config.ConnectBatchSize, config.TotalConnections);

      for (var j = i; j < batchEnd; j++)
      {
        var runner = new ConnectionRunner(appLogger, connector, endPoint, config, statistics);
        runners.Add(runner);
        connectTasks.Add(runner.ConnectAsync(cts.Token));
      }

      if (batchEnd < config.TotalConnections && config.ConnectIntervalMs > 0)
      {
        try
        {
          await Task.Delay(config.ConnectIntervalMs, cts.Token);
        }
        catch (OperationCanceledException)
        {
          break;
        }
      }
    }

    await Task.WhenAll(connectTasks);

    var connectDuration = DateTime.UtcNow - connectStart;
    var snap = statistics.TakeSnapshot();

    appLogger.Info(
      $"Phase 1 complete: {snap.TotalConnected}/{config.TotalConnections} connected" +
      $"  fail: {snap.TotalFailed}" +
      $"  elapsed: {connectDuration.TotalSeconds:F1}s" +
      $"  → Starting {durationLabel} traffic...");

    // ── Phase 2: 트래픽 시작 ─────────────────────────────────────────────────
    // 연결이 모두 완료된 이후 DurationSeconds 타이머를 시작한다.
    if (config.DurationSeconds > 0)
    {
      cts.CancelAfter(TimeSpan.FromSeconds(config.DurationSeconds));
    }

#if MITHRIL_PROFILE
    Session.ResetCloseProfile();
#endif
    var testStart = DateTime.UtcNow;
    var runTasks = runners.Select(r => r.RunAsync(cts.Token)).ToList();
    var allRunsTask = Task.WhenAll(runTasks);
    var closeFanoutTask = WaitForCancellationAndCloseAsync(appLogger, runners, config, cts.Token);
    var snapshotExitTask = RunSnapshotThenExitAsync(
      appLogger,
      reporter,
      statistics,
      config,
      testStart,
      cts.Token);

    if (config.ShutdownMode == ClientShutdownMode.SnapshotThenExit)
    {
      await snapshotExitTask;
      return;
    }

    try
    {
      await closeFanoutTask;
    }
    catch (OperationCanceledException)
    {
    }

    if (cts.IsCancellationRequested && allRunsTask.IsCompleted == false)
    {
      var gracePeriod = TimeSpan.FromSeconds(3);
      appLogger.Info($"Run task drain wait: grace={gracePeriod.TotalSeconds:F0}s pending={runTasks.Count(t => t.IsCompleted == false)}");

      await Task.WhenAny(allRunsTask, Task.Delay(gracePeriod));

      if (allRunsTask.IsCompleted == false)
      {
        appLogger.Warning($"Run task drain timeout: pending={runTasks.Count(t => t.IsCompleted == false)}");
      }
    }
    else
    {
      await allRunsTask;
    }

    var actualDuration = DateTime.UtcNow - testStart;
    appLogger.Info($"Shutdown complete: {actualDuration.TotalSeconds:F1}s elapsed since traffic start");
#if MITHRIL_PROFILE
    var closeProfile = Session.TakeCloseProfileSnapshot();
    appLogger.Info(
      "Client Session.Close profile" +
      $"  started: {closeProfile.Started}" +
      $"  completed: {closeProfile.Completed}" +
      $"  in-progress: {closeProfile.InProgress}" +
      $"  total: {closeProfile.TotalCloseMs:F1}ms" +
      $"  max: {closeProfile.MaxCloseMs:F1}ms" +
      $"\n            socket.Close: total {closeProfile.SocketCloseTotalMs:F1}ms max {closeProfile.SocketCloseMaxMs:F1}ms" +
      $"  |  sendBuffer.Complete: total {closeProfile.SendBufferCompleteTotalMs:F1}ms max {closeProfile.SendBufferCompleteMaxMs:F1}ms" +
      $"  |  ClearSendBuffer: total {closeProfile.ClearSendBufferTotalMs:F1}ms max {closeProfile.ClearSendBufferMaxMs:F1}ms" +
      $"\n            receivePipe.Writer.Complete: total {closeProfile.ReceiveWriterCompleteTotalMs:F1}ms max {closeProfile.ReceiveWriterCompleteMaxMs:F1}ms" +
      $"  |  receivePipe.Reader.Complete: total {closeProfile.ReceiveReaderCompleteTotalMs:F1}ms max {closeProfile.ReceiveReaderCompleteMaxMs:F1}ms" +
      $"  |  socketSender.Dispose: total {closeProfile.SocketSenderDisposeTotalMs:F1}ms max {closeProfile.SocketSenderDisposeMaxMs:F1}ms" +
      $"  |  notify: total {closeProfile.NotifyTotalMs:F1}ms max {closeProfile.NotifyMaxMs:F1}ms");
#endif

    try
    {
      await reporterTask;
    }
    catch (OperationCanceledException)
    {
    }

    reporter.PrintFinalStats(actualDuration);
  }

  private static async Task WaitForCancellationAndCloseAsync(
    IAppLogger appLogger,
    List<ConnectionRunner> runners,
    MassiveTestClientConfig config,
    CancellationToken ct)
  {
    if (config.ShutdownMode == ClientShutdownMode.SnapshotThenExit)
    {
      return;
    }

    try
    {
      await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
    catch (OperationCanceledException)
    {
    }

    if (ct.IsCancellationRequested == false)
    {
      return;
    }

    var parallelism = ResolveCloseFanoutParallelism(config);
    var fanoutStart = DateTime.UtcNow;
    appLogger.Info(
      $"Close fanout start: runners={runners.Count} parallelism={parallelism}");

    var nextIndex = 0;
    var workers = new Task[parallelism];
    for (var i = 0; i < workers.Length; i++)
    {
      workers[i] = Task.Factory.StartNew(
        () => CloseWorker(runners, ref nextIndex),
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default);
    }

    await Task.WhenAll(workers);

    var fanoutElapsed = DateTime.UtcNow - fanoutStart;
    appLogger.Info(
      $"Close fanout complete: runners={runners.Count} parallelism={parallelism} elapsed={fanoutElapsed.TotalMilliseconds:F1}ms");
  }

  private static int ResolveCloseFanoutParallelism(MassiveTestClientConfig config)
  {
    if (config.CloseFanoutParallelism > 0)
    {
      return config.CloseFanoutParallelism;
    }

    return Math.Min(
      config.TotalConnections,
      Math.Max(Environment.ProcessorCount * 8, 64));
  }

  private static void CloseWorker(List<ConnectionRunner> runners, ref int nextIndex)
  {
    while (true)
    {
      var index = Interlocked.Increment(ref nextIndex) - 1;
      if (index >= runners.Count)
      {
        return;
      }

      runners[index].Close();
    }
  }

  private static async Task RunSnapshotThenExitAsync(
    IAppLogger appLogger,
    StatsReporter reporter,
    Statistics statistics,
    MassiveTestClientConfig config,
    DateTime testStartUtc,
    CancellationToken ct)
  {
    if (config.ShutdownMode != ClientShutdownMode.SnapshotThenExit)
    {
      return;
    }

    try
    {
      await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
    catch (OperationCanceledException)
    {
    }

    if (ct.IsCancellationRequested == false)
    {
      return;
    }

    statistics.FreezeMeasurements();
    var measuredDuration = DateTime.UtcNow - testStartUtc;
    appLogger.Warning("SnapshotThenExit enabled. Freezing measurements and exiting immediately.");
    reporter.PrintFinalStats(measuredDuration);
    Environment.Exit(0);
  }
}

/// <summary>클라이언트 JSON 설정 루트</summary>
internal sealed class ClientSettings
{
  public NetworkConfig Network { get; init; } = new NetworkConfig { NoDelay = true, MaxPacketSize = 65536 };
  public MassiveTestClientConfig Client { get; init; } = null!;
}
