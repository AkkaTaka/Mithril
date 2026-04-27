namespace Mithril.Apps.MassiveTestServer.Service;

using Microsoft.Extensions.Hosting;
using Mithril.AppServices.MassiveTest;
using Mithril.AppServices.MassiveTest.Server;
using Mithril.Logger;
using Mithril.Memory;

internal sealed class MassiveTestServerService : IHostedService
{
  private readonly IAppLogger appLogger;
  private readonly MassiveTestServer server;
  private readonly int statsIntervalSeconds;
  private CancellationTokenSource? statsCts;
  private Task? statsTask;

  public MassiveTestServerService(IAppLogger appLogger, MassiveTestServerConfig config)
  {
    this.appLogger = appLogger;
    this.server = new MassiveTestServer(appLogger, config);
    this.statsIntervalSeconds = config.StatsIntervalSeconds;
  }

  public Task StartAsync(CancellationToken ct)
  {
    // 대량 접속/종료 시 소켓 completion과 파이프라인 continuation이
    // ThreadPool에 몰리므로 서버도 최소 워커 수를 미리 확보한다.
    var minThreads = Math.Max(Environment.ProcessorCount * 32, 1024);
    ThreadPool.SetMinThreads(minThreads, minThreads);

    this.server.Start(ct);
    this.statsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    this.statsTask = this.RunStatsLoopAsync(this.statsCts.Token);
    this.appLogger.Info($"MassiveTestServer ThreadPool min threads set to {minThreads}");
    return Task.CompletedTask;
  }

  public async Task StopAsync(CancellationToken ct)
  {
    try
    {
      this.statsCts?.Cancel();
    }
    catch
    {
    }

    await this.server.StopAsync();

    if (this.statsTask != null)
    {
      try
      {
        await this.statsTask;
      }
      catch (OperationCanceledException)
      {
      }
    }
  }

  private async Task RunStatsLoopAsync(CancellationToken ct)
  {
    var startTime = DateTime.UtcNow;
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(this.statsIntervalSeconds));

    while (await timer.WaitForNextTickAsync(ct))
    {
      var elapsed = DateTime.UtcNow - startTime;
      var snap = this.server.Statistics.TakeSnapshot();
      var interval = this.statsIntervalSeconds;

      var message =
        $"[+{elapsed:hh\\:mm\\:ss}] [Server]" +
        $"  Conn: {snap.ActiveConnections} active (total: {snap.TotalAccepted})" +
        $"  |  Recv: {FormatRate(snap.ReceivedMessages, interval)} {FormatBytes(snap.ReceivedBytes, interval)}" +
        $"  |  Sent: {FormatRate(snap.SentMessages, interval)} {FormatBytes(snap.SentBytes, interval)}";

#if MITHRIL_PROFILE
      var native = NativeMemoryPool.shared.TakeSnapshot();
      message +=
        $"\n            Native: in-use {native.InUseBuffers} ({FormatBytes(native.InUseBytes)})" +
        $"  |  pooled {native.PooledBuffers} ({FormatBytes(native.PooledBytes)})" +
        $"  |  alloc {native.NativeAllocations} ({FormatBytes(native.TotalAllocatedBytes)})" +
        $"  |  hit {native.PoolHits}/{native.RentCalls} ({FormatPercent(native.PoolHits, native.RentCalls)}, miss {native.RentCalls - native.PoolHits})";
#endif

      this.appLogger.Info(message);
    }
  }

  private static string FormatRate(long count, int intervalSeconds)
  {
    var rate = count / intervalSeconds;
    if (rate >= 1_000_000)
    {
      return $"{rate / 1_000_000.0:F1}M msg/s";
    }

    if (rate >= 1_000)
    {
      return $"{rate / 1_000.0:F1}K msg/s";
    }

    return $"{rate} msg/s";
  }

  private static string FormatBytes(long bytes, int intervalSeconds)
  {
    var rate = bytes / intervalSeconds;
    if (rate >= 1_048_576)
    {
      return $"{rate / 1_048_576.0:F1}MB/s";
    }

    if (rate >= 1_024)
    {
      return $"{rate / 1_024.0:F1}KB/s";
    }

    return $"{rate}B/s";
  }

  private static string FormatBytes(long bytes)
  {
    if (bytes >= 1_073_741_824)
    {
      return $"{bytes / 1_073_741_824.0:F2}GB";
    }

    if (bytes >= 1_048_576)
    {
      return $"{bytes / 1_048_576.0:F2}MB";
    }

    if (bytes >= 1_024)
    {
      return $"{bytes / 1_024.0:F1}KB";
    }

    return $"{bytes}B";
  }

  private static string FormatPercent(long numerator, long denominator)
  {
    if (denominator <= 0)
    {
      return "N/A";
    }

    return $"{numerator * 100.0 / denominator:F4}%";
  }
}
