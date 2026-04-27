namespace Mithril.Network;

using System.Diagnostics;
using System.Threading;

#if MITHRIL_PROFILE
public sealed partial class Session
{
  public readonly record struct CloseProfileSnapshot(
    long Started,
    long Completed,
    long InProgress,
    double TotalCloseMs,
    double MaxCloseMs,
    double SocketCloseTotalMs,
    double SocketCloseMaxMs,
    double SendBufferCompleteTotalMs,
    double SendBufferCompleteMaxMs,
    double ClearSendBufferTotalMs,
    double ClearSendBufferMaxMs,
    double ReceiveWriterCompleteTotalMs,
    double ReceiveWriterCompleteMaxMs,
    double ReceiveReaderCompleteTotalMs,
    double ReceiveReaderCompleteMaxMs,
    double SocketSenderDisposeTotalMs,
    double SocketSenderDisposeMaxMs,
    double NotifyTotalMs,
    double NotifyMaxMs);

  public readonly record struct ReceiveProfileSnapshot(
    long SocketReceiveCompletedTicks,
    long ReceivePipeFlushCompletedTicks,
    long ReceivePipeReadTicks);

  private static readonly CloseProfiler Profiler = new();
  private readonly ReceiveProfiler receiveProfiler = new();

  public static void ResetCloseProfile()
  {
    Profiler.Reset();
  }

  public static CloseProfileSnapshot TakeCloseProfileSnapshot()
  {
    return Profiler.TakeSnapshot();
  }

  public ReceiveProfileSnapshot TakeReceiveProfileSnapshot()
  {
    return this.receiveProfiler.TakeSnapshot();
  }

  private void ProfileOnSocketReceiveCompleted()
  {
    this.receiveProfiler.socketReceiveCompletedTicks = Stopwatch.GetTimestamp();
  }

  private void ProfileOnReceivePipeFlushCompleted()
  {
    this.receiveProfiler.receivePipeFlushCompletedTicks = Stopwatch.GetTimestamp();
  }

  private void ProfileOnReceivePipeRead()
  {
    this.receiveProfiler.receivePipeReadTicks = Stopwatch.GetTimestamp();
  }

  private void ProfileOnCloseStarted()
  {
    Profiler.OnCloseStarted();
  }

  private void ProfileOnCloseSocketClosed(long elapsedTicks)
  {
    Profiler.OnSocketClosed(elapsedTicks);
  }

  private void ProfileOnCloseSendBufferCompleted(long elapsedTicks)
  {
    Profiler.OnSendBufferCompleted(elapsedTicks);
  }

  private void ProfileOnCloseSendBufferCleared(long elapsedTicks)
  {
    Profiler.OnSendBufferCleared(elapsedTicks);
  }

  private void ProfileOnCloseReceiveWriterCompleted(long elapsedTicks)
  {
    Profiler.OnReceiveWriterCompleted(elapsedTicks);
  }

  private void ProfileOnCloseReceiveReaderCompleted(long elapsedTicks)
  {
    Profiler.OnReceiveReaderCompleted(elapsedTicks);
  }

  private void ProfileOnCloseSocketSenderDisposed(long elapsedTicks)
  {
    Profiler.OnSocketSenderDisposed(elapsedTicks);
  }

  private void ProfileOnCloseNotified(long elapsedTicks)
  {
    Profiler.OnNotified(elapsedTicks);
  }

  private void ProfileOnCloseCompleted(long elapsedTicks)
  {
    Profiler.OnCloseCompleted(elapsedTicks);
  }

  private sealed class ReceiveProfiler
  {
    public long socketReceiveCompletedTicks;
    public long receivePipeFlushCompletedTicks;
    public long receivePipeReadTicks;

    public ReceiveProfileSnapshot TakeSnapshot()
    {
      return new ReceiveProfileSnapshot(
        Volatile.Read(ref this.socketReceiveCompletedTicks),
        Volatile.Read(ref this.receivePipeFlushCompletedTicks),
        Volatile.Read(ref this.receivePipeReadTicks));
    }
  }

  private sealed class CloseProfiler
  {
    private CloseAccumulator accumulator;

    public void Reset()
    {
      this.accumulator = default;
    }

    public void OnCloseStarted()
    {
      Interlocked.Increment(ref this.accumulator.started);
      Interlocked.Increment(ref this.accumulator.inProgress);
    }

    public void OnSocketClosed(long elapsedTicks) => RecordStep(ref this.accumulator.socketCloseTotalTicks, ref this.accumulator.socketCloseMaxTicks, elapsedTicks);
    public void OnSendBufferCompleted(long elapsedTicks) => RecordStep(ref this.accumulator.sendBufferCompleteTotalTicks, ref this.accumulator.sendBufferCompleteMaxTicks, elapsedTicks);
    public void OnSendBufferCleared(long elapsedTicks) => RecordStep(ref this.accumulator.clearSendBufferTotalTicks, ref this.accumulator.clearSendBufferMaxTicks, elapsedTicks);
    public void OnReceiveWriterCompleted(long elapsedTicks) => RecordStep(ref this.accumulator.receiveWriterCompleteTotalTicks, ref this.accumulator.receiveWriterCompleteMaxTicks, elapsedTicks);
    public void OnReceiveReaderCompleted(long elapsedTicks) => RecordStep(ref this.accumulator.receiveReaderCompleteTotalTicks, ref this.accumulator.receiveReaderCompleteMaxTicks, elapsedTicks);
    public void OnSocketSenderDisposed(long elapsedTicks) => RecordStep(ref this.accumulator.socketSenderDisposeTotalTicks, ref this.accumulator.socketSenderDisposeMaxTicks, elapsedTicks);
    public void OnNotified(long elapsedTicks) => RecordStep(ref this.accumulator.notifyTotalTicks, ref this.accumulator.notifyMaxTicks, elapsedTicks);

    public void OnCloseCompleted(long elapsedTicks)
    {
      RecordStep(ref this.accumulator.closeTotalTicks, ref this.accumulator.closeMaxTicks, elapsedTicks);
      Interlocked.Decrement(ref this.accumulator.inProgress);
      Interlocked.Increment(ref this.accumulator.completed);
    }

    public CloseProfileSnapshot TakeSnapshot()
    {
      return new CloseProfileSnapshot(
        Started: Volatile.Read(ref this.accumulator.started),
        Completed: Volatile.Read(ref this.accumulator.completed),
        InProgress: Volatile.Read(ref this.accumulator.inProgress),
        TotalCloseMs: TicksToMs(Volatile.Read(ref this.accumulator.closeTotalTicks)),
        MaxCloseMs: TicksToMs(Volatile.Read(ref this.accumulator.closeMaxTicks)),
        SocketCloseTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.socketCloseTotalTicks)),
        SocketCloseMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.socketCloseMaxTicks)),
        SendBufferCompleteTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.sendBufferCompleteTotalTicks)),
        SendBufferCompleteMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.sendBufferCompleteMaxTicks)),
        ClearSendBufferTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.clearSendBufferTotalTicks)),
        ClearSendBufferMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.clearSendBufferMaxTicks)),
        ReceiveWriterCompleteTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.receiveWriterCompleteTotalTicks)),
        ReceiveWriterCompleteMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.receiveWriterCompleteMaxTicks)),
        ReceiveReaderCompleteTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.receiveReaderCompleteTotalTicks)),
        ReceiveReaderCompleteMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.receiveReaderCompleteMaxTicks)),
        SocketSenderDisposeTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.socketSenderDisposeTotalTicks)),
        SocketSenderDisposeMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.socketSenderDisposeMaxTicks)),
        NotifyTotalMs: TicksToMs(Volatile.Read(ref this.accumulator.notifyTotalTicks)),
        NotifyMaxMs: TicksToMs(Volatile.Read(ref this.accumulator.notifyMaxTicks)));
    }

    private static void RecordStep(ref long totalTicksField, ref long maxTicksField, long elapsedTicks)
    {
      Interlocked.Add(ref totalTicksField, elapsedTicks);

      while (true)
      {
        var currentMax = Volatile.Read(ref maxTicksField);
        if (elapsedTicks <= currentMax)
        {
          return;
        }

        if (Interlocked.CompareExchange(ref maxTicksField, elapsedTicks, currentMax) == currentMax)
        {
          return;
        }
      }
    }

    private static double TicksToMs(long ticks)
    {
      return ticks * 1000.0 / Stopwatch.Frequency;
    }
  }

  private struct CloseAccumulator
  {
    public long started;
    public long completed;
    public long inProgress;
    public long closeTotalTicks;
    public long closeMaxTicks;
    public long socketCloseTotalTicks;
    public long socketCloseMaxTicks;
    public long sendBufferCompleteTotalTicks;
    public long sendBufferCompleteMaxTicks;
    public long clearSendBufferTotalTicks;
    public long clearSendBufferMaxTicks;
    public long receiveWriterCompleteTotalTicks;
    public long receiveWriterCompleteMaxTicks;
    public long receiveReaderCompleteTotalTicks;
    public long receiveReaderCompleteMaxTicks;
    public long socketSenderDisposeTotalTicks;
    public long socketSenderDisposeMaxTicks;
    public long notifyTotalTicks;
    public long notifyMaxTicks;
  }
}
#endif

