namespace Mithril.Network;

using Mithril.Memory;
using System.Threading;
using System.Threading.Tasks.Sources;

public sealed class MpscByteBuffer : IValueTaskSource<bool>
{
  public sealed class Segment
  {
    public NativeBuffer buffer;
    public int length;
    public Segment? next;
  }

  private Segment? head;
  private volatile bool completed;
  private ManualResetValueTaskSourceCore<bool> core;
  private int signaled;

  public MpscByteBuffer()
  {
    this.core.RunContinuationsAsynchronously = true;
  }

  public bool IsCompleted => this.completed;

  /// <summary>
  /// Lock-free push. Called by multiple producers concurrently.
  /// </summary>
  public bool Push(NativeBuffer buffer, int length)
  {
    if (this.completed)
    {
      return false;
    }

    var segment = new Segment { buffer = buffer, length = length };

    Segment? oldHead;
    do
    {
      oldHead = this.head;
      segment.next = oldHead;
    }
    while (Interlocked.CompareExchange(ref this.head, segment, oldHead) != oldHead);

    if (Interlocked.Exchange(ref this.signaled, 1) == 0)
    {
      this.core.SetResult(true);
    }

    return true;
  }

  /// <summary>
  /// Async wait for data. No thread consumption while waiting.
  /// </summary>
  public ValueTask<bool> WaitForDataAsync()
  {
    return new ValueTask<bool>(this, this.core.Version);
  }

  /// <summary>
  /// Reset signal for next wait cycle.
  /// Re-signals if data or completion arrived during reset.
  /// </summary>
  public void ResetSignal()
  {
    this.core.Reset();
    Volatile.Write(ref this.signaled, 0);

    if (this.completed || Volatile.Read(ref this.head) != null)
    {
      if (Interlocked.Exchange(ref this.signaled, 1) == 0)
      {
        this.core.SetResult(true);
      }
    }
  }

  /// <summary>
  /// Non-blocking drain. Returns head of FIFO-ordered linked list.
  /// </summary>
  public Segment? TryDrain()
  {
    var stolen = Interlocked.Exchange(ref this.head, null);
    if (stolen == null)
    {
      return null;
    }

    return Reverse(stolen);
  }

  public void Complete()
  {
    this.completed = true;
    if (Interlocked.Exchange(ref this.signaled, 1) == 0)
    {
      this.core.SetResult(true);
    }
  }

  bool IValueTaskSource<bool>.GetResult(short token)
  {
    return this.core.GetResult(token);
  }

  ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
  {
    return this.core.GetStatus(token);
  }

  void IValueTaskSource<bool>.OnCompleted(
    Action<object?> continuation,
    object? state,
    short token,
    ValueTaskSourceOnCompletedFlags flags)
  {
    this.core.OnCompleted(continuation, state, token, flags);
  }

  private static Segment Reverse(Segment head)
  {
    Segment? prev = null;
    Segment? current = head;
    while (current != null)
    {
      var next = current.next;
      current.next = prev;
      prev = current;
      current = next;
    }
    return prev!;
  }
}
