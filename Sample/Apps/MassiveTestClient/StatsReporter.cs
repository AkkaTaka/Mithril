namespace Mithril.Apps.MassiveTestClient;

using Mithril.AppServices.MassiveTest;
using Mithril.Logger;
using System.Text;

internal sealed class StatsReporter
{
  private readonly IAppLogger appLogger;
  private readonly Statistics statistics;
  private readonly MassiveTestClientConfig config;
  private readonly DateTime startTime;

  public StatsReporter(IAppLogger appLogger, Statistics statistics, MassiveTestClientConfig config)
  {
    this.appLogger = appLogger;
    this.statistics = statistics;
    this.config = config;
    this.startTime = DateTime.UtcNow;
  }

  public async Task RunAsync(CancellationToken ct)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(this.config.StatsIntervalSeconds));

    while (await timer.WaitForNextTickAsync(ct))
    {
      this.PrintStats();
    }
  }

  public void PrintStats()
  {
    var elapsed = DateTime.UtcNow - this.startTime;
    var snap = this.statistics.TakeSnapshot();
    var interval = this.config.StatsIntervalSeconds;

    this.appLogger.Info(
      $"[+{elapsed:hh\\:mm\\:ss}] [Client]" +
      $"  Conn: {snap.ConnectedCount}/{this.config.TotalConnections}" +
      $" (connected: {snap.TotalConnected} fail: {snap.TotalFailed} drop: {snap.TotalDisconnected})" +
      $"\n            " +
      $"  Sent: {FormatRate(snap.SentMessages, interval)} {FormatBytes(snap.SentBytes, interval)}" +
      $"  |  Recv: {FormatRate(snap.ReceivedMessages, interval)} {FormatBytes(snap.ReceivedBytes, interval)}" +
      $"\n            RTT Total  {FormatLatencySummary(snap.TotalLatency)}" +
#if MITHRIL_PROFILE
      $"\n            RTT C->S   {FormatLatencySummary(snap.ClientToServerLatency)}" +
      $"\n            RTT Server {FormatLatencySummary(snap.ServerProcessingLatency)}" +
      $"\n            RTT S->C   {FormatLatencySummary(snap.ServerToClientLatency)}" +
      $"\n            RTT S->SockRecv {FormatLatencySummary(snap.ServerToSocketReceiveLatency)}" +
      $"\n            RTT SockRecv->Flush {FormatLatencySummary(snap.SocketReceiveToPipeFlushLatency)}" +
      $"\n            RTT S->Pipe {FormatLatencySummary(snap.ServerToClientPipeLatency)}" +
      $"\n            RTT Pipe->App {FormatLatencySummary(snap.ClientPipeToHandlerLatency)}"
#else
      string.Empty
#endif
      );
  }

  public void PrintFinalStats(TimeSpan duration)
  {
    var snap = this.statistics.TakeFinalSnapshot();
    var totalSeconds = duration.TotalSeconds;

    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine("============================================================");
    sb.AppendLine("                     FINAL RESULTS");
    sb.AppendLine("============================================================");
    sb.AppendLine($" Duration      : {duration:hh\\:mm\\:ss}");
    sb.AppendLine($" Connections   : connected {snap.TotalConnected,7} / failed {snap.TotalFailed,5} / dropped {snap.TotalDisconnected,5}");
    sb.AppendLine("------------------------------------------------------------");
    sb.AppendLine($" Total Sent    : {snap.SentMessages,15:N0} msgs  {FormatTotalBytes(snap.SentBytes)}");
    sb.AppendLine($" Total Recv    : {snap.ReceivedMessages,15:N0} msgs  {FormatTotalBytes(snap.ReceivedBytes)}");
    sb.AppendLine("------------------------------------------------------------");
    sb.AppendLine($" Avg Sent      : {FormatAvgRate(snap.SentMessages, totalSeconds),12}  {FormatAvgBytes(snap.SentBytes, totalSeconds),12}");
    sb.AppendLine($" Avg Recv      : {FormatAvgRate(snap.ReceivedMessages, totalSeconds),12}  {FormatAvgBytes(snap.ReceivedBytes, totalSeconds),12}");
    sb.AppendLine("------------------------------------------------------------");
    sb.AppendLine($" RTT Total     : {FormatLatencySummary(snap.TotalLatency)}");
#if MITHRIL_PROFILE
    sb.AppendLine($" RTT C->S      : {FormatLatencySummary(snap.ClientToServerLatency)}");
    sb.AppendLine($" RTT Server    : {FormatLatencySummary(snap.ServerProcessingLatency)}");
    sb.AppendLine($" RTT S->C      : {FormatLatencySummary(snap.ServerToClientLatency)}");
    sb.AppendLine($" RTT S->SockRecv   : {FormatLatencySummary(snap.ServerToSocketReceiveLatency)}");
    sb.AppendLine($" RTT SockRecv->Flush : {FormatLatencySummary(snap.SocketReceiveToPipeFlushLatency)}");
    sb.AppendLine($" RTT S->Pipe   : {FormatLatencySummary(snap.ServerToClientPipeLatency)}");
    sb.AppendLine($" RTT Pipe->App : {FormatLatencySummary(snap.ClientPipeToHandlerLatency)}");
#endif
    sb.Append("============================================================");

    this.appLogger.Info(sb.ToString());
  }

  private static string FormatLatencySummary(Statistics.LatencySummary summary)
  {
    if (summary.AverageUs < 0)
    {
      return "(no data)";
    }

    return
      $"avg: {FormatLatency(summary.AverageUs),8}" +
      $"  min: {FormatLatency(summary.MinUs),8}" +
      $"  P50: {FormatLatency(summary.P50Us),8}" +
      $"  P90: {FormatLatency(summary.P90Us),8}" +
      $"  P99: {FormatLatency(summary.P99Us),8}" +
      $"  max: {FormatLatency(summary.MaxUs),8}";
  }

  private static string FormatTotalBytes(long bytes)
  {
    if (bytes >= 1_073_741_824) { return $"{bytes / 1_073_741_824.0:F2} GB"; }
    if (bytes >= 1_048_576) { return $"{bytes / 1_048_576.0:F2} MB"; }
    if (bytes >= 1_024) { return $"{bytes / 1_024.0:F1} KB"; }
    return $"{bytes} B";
  }

  private static string FormatAvgRate(long total, double seconds)
  {
    if (seconds <= 0) { return "N/A"; }
    var rate = total / seconds;
    if (rate >= 1_000_000) { return $"{rate / 1_000_000.0:F1}M msg/s"; }
    if (rate >= 1_000) { return $"{rate / 1_000.0:F1}K msg/s"; }
    return $"{rate:F0} msg/s";
  }

  private static string FormatAvgBytes(long totalBytes, double seconds)
  {
    if (seconds <= 0) { return "N/A"; }
    var rate = totalBytes / seconds;
    if (rate >= 1_048_576) { return $"{rate / 1_048_576.0:F1} MB/s"; }
    if (rate >= 1_024) { return $"{rate / 1_024.0:F1} KB/s"; }
    return $"{rate:F0} B/s";
  }

  private static string FormatRate(long count, int intervalSeconds)
  {
    var rate = count / intervalSeconds;
    if (rate >= 1_000_000) { return $"{rate / 1_000_000.0:F1}M msg/s"; }
    if (rate >= 1_000) { return $"{rate / 1_000.0:F1}K msg/s"; }
    return $"{rate} msg/s";
  }

  private static string FormatBytes(long bytes, int intervalSeconds)
  {
    var rate = bytes / intervalSeconds;
    if (rate >= 1_048_576) { return $"{rate / 1_048_576.0:F1}MB/s"; }
    if (rate >= 1_024) { return $"{rate / 1_024.0:F1}KB/s"; }
    return $"{rate}B/s";
  }

  private static string FormatLatency(long microseconds)
  {
    if (microseconds < 0)
    {
      return "N/A";
    }

    if (microseconds < 1_000)
    {
      return $"{microseconds}us";
    }

    var milliseconds = microseconds / 1_000.0;
    if (milliseconds < 10)
    {
      return $"{milliseconds:F2}ms";
    }

    if (milliseconds < 100)
    {
      return $"{milliseconds:F1}ms";
    }

    return $"{milliseconds:F0}ms";
  }
}
