namespace Mithril.Apps.MassiveTestClient;

using System.Diagnostics;

internal sealed class Statistics
{
  internal readonly record struct LatencySummary(
    long AverageUs,
    long MinUs,
    long P50Us,
    long P90Us,
    long P99Us,
    long MaxUs);

  private static readonly long[] LatencyBucketUpperBoundUs =
  {
    50,
    100,
    200,
    300,
    400,
    500,
    750,
    1_000,
    1_500,
    2_000,
    3_000,
    5_000,
    7_500,
    10_000,
    20_000,
    50_000,
    100_000,
    200_000,
    500_000,
    1_000_000,
    long.MaxValue,
  };

  private readonly LatencyTracker totalLatency = new();
#if MITHRIL_PROFILE
  private readonly LatencyTracker clientToServerLatency = new();
  private readonly LatencyTracker serverProcessingLatency = new();
  private readonly LatencyTracker serverToClientLatency = new();
  private readonly LatencyTracker serverToSocketReceiveLatency = new();
  private readonly LatencyTracker socketReceiveToPipeFlushLatency = new();
  private readonly LatencyTracker serverToClientPipeLatency = new();
  private readonly LatencyTracker clientPipeToHandlerLatency = new();
#endif

  private long connectedCount;
  private long totalConnected;
  private long totalFailed;
  private long totalDisconnected;
  private long sentMessages;
  private long receivedMessages;
  private long sentBytes;
  private long receivedBytes;
  private long cumulativeSentMessages;
  private long cumulativeReceivedMessages;
  private long cumulativeSentBytes;
  private long cumulativeReceivedBytes;
  private int measurementFrozen;

  public void RecordConnected()
  {
    if (Volatile.Read(ref this.measurementFrozen) != 0)
    {
      return;
    }

    Interlocked.Increment(ref this.connectedCount);
    Interlocked.Increment(ref this.totalConnected);
  }

  public void RecordFailed()
  {
    if (Volatile.Read(ref this.measurementFrozen) != 0)
    {
      return;
    }

    Interlocked.Increment(ref this.totalFailed);
  }

  public void RecordDisconnected()
  {
    if (Volatile.Read(ref this.measurementFrozen) != 0)
    {
      return;
    }

    Interlocked.Decrement(ref this.connectedCount);
    Interlocked.Increment(ref this.totalDisconnected);
  }

  public void RecordSent(long bytes)
  {
    if (Volatile.Read(ref this.measurementFrozen) != 0)
    {
      return;
    }

    Interlocked.Increment(ref this.sentMessages);
    Interlocked.Add(ref this.sentBytes, bytes);
    Interlocked.Increment(ref this.cumulativeSentMessages);
    Interlocked.Add(ref this.cumulativeSentBytes, bytes);
  }

  public void RecordReceived(long bytes, long totalRttTicks)
  {
    if (Volatile.Read(ref this.measurementFrozen) != 0)
    {
      return;
    }

    Interlocked.Increment(ref this.receivedMessages);
    Interlocked.Add(ref this.receivedBytes, bytes);
    Interlocked.Increment(ref this.cumulativeReceivedMessages);
    Interlocked.Add(ref this.cumulativeReceivedBytes, bytes);
    this.totalLatency.Record(totalRttTicks);
  }

#if MITHRIL_PROFILE
  public void RecordReceived(
    long bytes,
    long totalRttTicks,
    long clientToServerTicks,
    long serverProcessingTicks,
    long serverToClientTicks,
    long serverToSocketReceiveTicks,
    long socketReceiveToPipeFlushTicks,
    long serverToClientPipeTicks,
    long clientPipeToHandlerTicks)
  {
    this.RecordReceived(bytes, totalRttTicks);
    this.clientToServerLatency.Record(clientToServerTicks);
    this.serverProcessingLatency.Record(serverProcessingTicks);
    this.serverToClientLatency.Record(serverToClientTicks);
    this.serverToSocketReceiveLatency.Record(serverToSocketReceiveTicks);
    this.socketReceiveToPipeFlushLatency.Record(socketReceiveToPipeFlushTicks);
    this.serverToClientPipeLatency.Record(serverToClientPipeTicks);
    this.clientPipeToHandlerLatency.Record(clientPipeToHandlerTicks);
  }
#endif

  public void FreezeMeasurements()
  {
    Interlocked.Exchange(ref this.measurementFrozen, 1);
  }

  public Snapshot TakeSnapshot()
  {
    return new Snapshot(
      connectedCount: Volatile.Read(ref this.connectedCount),
      totalConnected: Volatile.Read(ref this.totalConnected),
      totalFailed: Volatile.Read(ref this.totalFailed),
      totalDisconnected: Volatile.Read(ref this.totalDisconnected),
      sentMessages: Interlocked.Exchange(ref this.sentMessages, 0),
      receivedMessages: Interlocked.Exchange(ref this.receivedMessages, 0),
      sentBytes: Interlocked.Exchange(ref this.sentBytes, 0),
      receivedBytes: Interlocked.Exchange(ref this.receivedBytes, 0),
      totalLatency: this.totalLatency.TakeSnapshot()
#if MITHRIL_PROFILE
      ,
      clientToServerLatency: this.clientToServerLatency.TakeSnapshot(),
      serverProcessingLatency: this.serverProcessingLatency.TakeSnapshot(),
      serverToClientLatency: this.serverToClientLatency.TakeSnapshot(),
      serverToSocketReceiveLatency: this.serverToSocketReceiveLatency.TakeSnapshot(),
      socketReceiveToPipeFlushLatency: this.socketReceiveToPipeFlushLatency.TakeSnapshot(),
      serverToClientPipeLatency: this.serverToClientPipeLatency.TakeSnapshot(),
      clientPipeToHandlerLatency: this.clientPipeToHandlerLatency.TakeSnapshot()
#endif
      );
  }

  public FinalSnapshot TakeFinalSnapshot()
  {
    return new FinalSnapshot(
      totalConnected: Volatile.Read(ref this.totalConnected),
      totalFailed: Volatile.Read(ref this.totalFailed),
      totalDisconnected: Volatile.Read(ref this.totalDisconnected),
      sentMessages: Volatile.Read(ref this.cumulativeSentMessages),
      receivedMessages: Volatile.Read(ref this.cumulativeReceivedMessages),
      sentBytes: Volatile.Read(ref this.cumulativeSentBytes),
      receivedBytes: Volatile.Read(ref this.cumulativeReceivedBytes),
      totalLatency: this.totalLatency.TakeFinalSnapshot()
#if MITHRIL_PROFILE
      ,
      clientToServerLatency: this.clientToServerLatency.TakeFinalSnapshot(),
      serverProcessingLatency: this.serverProcessingLatency.TakeFinalSnapshot(),
      serverToClientLatency: this.serverToClientLatency.TakeFinalSnapshot(),
      serverToSocketReceiveLatency: this.serverToSocketReceiveLatency.TakeFinalSnapshot(),
      socketReceiveToPipeFlushLatency: this.socketReceiveToPipeFlushLatency.TakeFinalSnapshot(),
      serverToClientPipeLatency: this.serverToClientPipeLatency.TakeFinalSnapshot(),
      clientPipeToHandlerLatency: this.clientPipeToHandlerLatency.TakeFinalSnapshot()
#endif
      );
  }

  private static long StopwatchTicksToMicroseconds(long stopwatchTicks)
  {
    return stopwatchTicks * 1_000_000 / Stopwatch.Frequency;
  }

  private static void UpdateMin(ref long target, long value)
  {
    while (true)
    {
      var current = Volatile.Read(ref target);
      if (value >= current)
      {
        return;
      }

      if (Interlocked.CompareExchange(ref target, value, current) == current)
      {
        return;
      }
    }
  }

  private static void UpdateMax(ref long target, long value)
  {
    while (true)
    {
      var current = Volatile.Read(ref target);
      if (value <= current)
      {
        return;
      }

      if (Interlocked.CompareExchange(ref target, value, current) == current)
      {
        return;
      }
    }
  }

  private static LatencySummary BuildLatencySummary(LatencySnapshot snapshot)
  {
    if (snapshot.Count <= 0)
    {
      return new LatencySummary(-1, -1, -1, -1, -1, -1);
    }

    return new LatencySummary(
      AverageUs: StopwatchTicksToMicroseconds(snapshot.TotalStopwatchTicks) / snapshot.Count,
      MinUs: snapshot.MinStopwatchTicks == long.MaxValue ? -1 : StopwatchTicksToMicroseconds(snapshot.MinStopwatchTicks),
      P50Us: CalculatePercentileUs(snapshot.Buckets, 50),
      P90Us: CalculatePercentileUs(snapshot.Buckets, 90),
      P99Us: CalculatePercentileUs(snapshot.Buckets, 99),
      MaxUs: snapshot.MaxStopwatchTicks == 0 ? -1 : StopwatchTicksToMicroseconds(snapshot.MaxStopwatchTicks));
  }

  private static long CalculatePercentileUs(long[] buckets, int percentile)
  {
    long total = 0;
    for (var i = 0; i < buckets.Length; i++)
    {
      total += buckets[i];
    }

    if (total == 0)
    {
      return -1;
    }

    var threshold = (long)Math.Ceiling(total * percentile / 100.0);
    long cumulative = 0;
    for (var i = 0; i < buckets.Length; i++)
    {
      cumulative += buckets[i];
      if (cumulative >= threshold)
      {
        return LatencyBucketUpperBoundUs[i];
      }
    }

    return LatencyBucketUpperBoundUs[^1];
  }

  private readonly struct LatencySnapshot
  {
    public LatencySnapshot(long count, long totalStopwatchTicks, long minStopwatchTicks, long maxStopwatchTicks, long[] buckets)
    {
      this.Count = count;
      this.TotalStopwatchTicks = totalStopwatchTicks;
      this.MinStopwatchTicks = minStopwatchTicks;
      this.MaxStopwatchTicks = maxStopwatchTicks;
      this.Buckets = buckets;
    }

    public long Count { get; }
    public long TotalStopwatchTicks { get; }
    public long MinStopwatchTicks { get; }
    public long MaxStopwatchTicks { get; }
    public long[] Buckets { get; }
  }

  private sealed class LatencyTracker
  {
    private readonly long[] buckets = new long[LatencyBucketUpperBoundUs.Length];
    private readonly long[] cumulativeBuckets = new long[LatencyBucketUpperBoundUs.Length];
    private long count;
    private long totalStopwatchTicks;
    private long minStopwatchTicks = long.MaxValue;
    private long maxStopwatchTicks;
    private long cumulativeCount;
    private long cumulativeTotalStopwatchTicks;
    private long cumulativeMinStopwatchTicks = long.MaxValue;
    private long cumulativeMaxStopwatchTicks;

    public void Record(long elapsedStopwatchTicks)
    {
      var bucket = GetBucketIndex(elapsedStopwatchTicks);
      Interlocked.Increment(ref this.count);
      Interlocked.Add(ref this.totalStopwatchTicks, elapsedStopwatchTicks);
      UpdateMin(ref this.minStopwatchTicks, elapsedStopwatchTicks);
      UpdateMax(ref this.maxStopwatchTicks, elapsedStopwatchTicks);
      Interlocked.Increment(ref this.buckets[bucket]);

      Interlocked.Increment(ref this.cumulativeCount);
      Interlocked.Add(ref this.cumulativeTotalStopwatchTicks, elapsedStopwatchTicks);
      UpdateMin(ref this.cumulativeMinStopwatchTicks, elapsedStopwatchTicks);
      UpdateMax(ref this.cumulativeMaxStopwatchTicks, elapsedStopwatchTicks);
      Interlocked.Increment(ref this.cumulativeBuckets[bucket]);
    }

    public LatencySummary TakeSnapshot()
    {
      var buckets = new long[LatencyBucketUpperBoundUs.Length];
      for (var i = 0; i < buckets.Length; i++)
      {
        buckets[i] = Interlocked.Exchange(ref this.buckets[i], 0);
      }

      return BuildLatencySummary(
        new LatencySnapshot(
          count: Interlocked.Exchange(ref this.count, 0),
          totalStopwatchTicks: Interlocked.Exchange(ref this.totalStopwatchTicks, 0),
          minStopwatchTicks: Interlocked.Exchange(ref this.minStopwatchTicks, long.MaxValue),
          maxStopwatchTicks: Interlocked.Exchange(ref this.maxStopwatchTicks, 0),
          buckets: buckets));
    }

    public LatencySummary TakeFinalSnapshot()
    {
      var buckets = new long[LatencyBucketUpperBoundUs.Length];
      for (var i = 0; i < buckets.Length; i++)
      {
        buckets[i] = Volatile.Read(ref this.cumulativeBuckets[i]);
      }

      return BuildLatencySummary(
        new LatencySnapshot(
          count: Volatile.Read(ref this.cumulativeCount),
          totalStopwatchTicks: Volatile.Read(ref this.cumulativeTotalStopwatchTicks),
          minStopwatchTicks: Volatile.Read(ref this.cumulativeMinStopwatchTicks),
          maxStopwatchTicks: Volatile.Read(ref this.cumulativeMaxStopwatchTicks),
          buckets: buckets));
    }

    private static int GetBucketIndex(long elapsedStopwatchTicks)
    {
      var elapsedUs = StopwatchTicksToMicroseconds(elapsedStopwatchTicks);
      for (var i = 0; i < LatencyBucketUpperBoundUs.Length; i++)
      {
        if (elapsedUs <= LatencyBucketUpperBoundUs[i])
        {
          return i;
        }
      }

      return LatencyBucketUpperBoundUs.Length - 1;
    }
  }

  public readonly struct Snapshot
  {
    public Snapshot(
      long connectedCount,
      long totalConnected,
      long totalFailed,
      long totalDisconnected,
      long sentMessages,
      long receivedMessages,
      long sentBytes,
      long receivedBytes,
      LatencySummary totalLatency
#if MITHRIL_PROFILE
      ,
      LatencySummary clientToServerLatency,
      LatencySummary serverProcessingLatency,
      LatencySummary serverToClientLatency,
      LatencySummary serverToSocketReceiveLatency,
      LatencySummary socketReceiveToPipeFlushLatency,
      LatencySummary serverToClientPipeLatency,
      LatencySummary clientPipeToHandlerLatency
#endif
      )
    {
      this.ConnectedCount = connectedCount;
      this.TotalConnected = totalConnected;
      this.TotalFailed = totalFailed;
      this.TotalDisconnected = totalDisconnected;
      this.SentMessages = sentMessages;
      this.ReceivedMessages = receivedMessages;
      this.SentBytes = sentBytes;
      this.ReceivedBytes = receivedBytes;
      this.TotalLatency = totalLatency;
#if MITHRIL_PROFILE
      this.ClientToServerLatency = clientToServerLatency;
      this.ServerProcessingLatency = serverProcessingLatency;
      this.ServerToClientLatency = serverToClientLatency;
      this.ServerToSocketReceiveLatency = serverToSocketReceiveLatency;
      this.SocketReceiveToPipeFlushLatency = socketReceiveToPipeFlushLatency;
      this.ServerToClientPipeLatency = serverToClientPipeLatency;
      this.ClientPipeToHandlerLatency = clientPipeToHandlerLatency;
#endif
    }

    public long ConnectedCount { get; }
    public long TotalConnected { get; }
    public long TotalFailed { get; }
    public long TotalDisconnected { get; }
    public long SentMessages { get; }
    public long ReceivedMessages { get; }
    public long SentBytes { get; }
    public long ReceivedBytes { get; }
    public LatencySummary TotalLatency { get; }
#if MITHRIL_PROFILE
    public LatencySummary ClientToServerLatency { get; }
    public LatencySummary ServerProcessingLatency { get; }
    public LatencySummary ServerToClientLatency { get; }
    public LatencySummary ServerToSocketReceiveLatency { get; }
    public LatencySummary SocketReceiveToPipeFlushLatency { get; }
    public LatencySummary ServerToClientPipeLatency { get; }
    public LatencySummary ClientPipeToHandlerLatency { get; }
#endif
  }

  public readonly struct FinalSnapshot
  {
    public FinalSnapshot(
      long totalConnected,
      long totalFailed,
      long totalDisconnected,
      long sentMessages,
      long receivedMessages,
      long sentBytes,
      long receivedBytes,
      LatencySummary totalLatency
#if MITHRIL_PROFILE
      ,
      LatencySummary clientToServerLatency,
      LatencySummary serverProcessingLatency,
      LatencySummary serverToClientLatency,
      LatencySummary serverToSocketReceiveLatency,
      LatencySummary socketReceiveToPipeFlushLatency,
      LatencySummary serverToClientPipeLatency,
      LatencySummary clientPipeToHandlerLatency
#endif
      )
    {
      this.TotalConnected = totalConnected;
      this.TotalFailed = totalFailed;
      this.TotalDisconnected = totalDisconnected;
      this.SentMessages = sentMessages;
      this.ReceivedMessages = receivedMessages;
      this.SentBytes = sentBytes;
      this.ReceivedBytes = receivedBytes;
      this.TotalLatency = totalLatency;
#if MITHRIL_PROFILE
      this.ClientToServerLatency = clientToServerLatency;
      this.ServerProcessingLatency = serverProcessingLatency;
      this.ServerToClientLatency = serverToClientLatency;
      this.ServerToSocketReceiveLatency = serverToSocketReceiveLatency;
      this.SocketReceiveToPipeFlushLatency = socketReceiveToPipeFlushLatency;
      this.ServerToClientPipeLatency = serverToClientPipeLatency;
      this.ClientPipeToHandlerLatency = clientPipeToHandlerLatency;
#endif
    }

    public long TotalConnected { get; }
    public long TotalFailed { get; }
    public long TotalDisconnected { get; }
    public long SentMessages { get; }
    public long ReceivedMessages { get; }
    public long SentBytes { get; }
    public long ReceivedBytes { get; }
    public LatencySummary TotalLatency { get; }
#if MITHRIL_PROFILE
    public LatencySummary ClientToServerLatency { get; }
    public LatencySummary ServerProcessingLatency { get; }
    public LatencySummary ServerToClientLatency { get; }
    public LatencySummary ServerToSocketReceiveLatency { get; }
    public LatencySummary SocketReceiveToPipeFlushLatency { get; }
    public LatencySummary ServerToClientPipeLatency { get; }
    public LatencySummary ClientPipeToHandlerLatency { get; }
#endif
  }
}
