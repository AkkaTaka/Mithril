namespace Mithril.Concurrent;

public sealed class Signal
{
  public static readonly Signal Completed;
  private static readonly Action ContinuationCompleted = () => { };

  private readonly ContinuationMode mode;
  private Action? continuation;

  static Signal()
  {
    Completed = new Signal(ContinuationMode.Inline)
    {
      continuation = ContinuationCompleted
    };
  }

  public Signal(ContinuationMode mode = ContinuationMode.Inline)
  {
    this.mode = mode;
  }

  public bool IsCompleted => ReferenceEquals(this.continuation, ContinuationCompleted);

  public void OnCompleted(Action continuation)
  {
    _ = continuation ?? throw new NullReferenceException();

    var oldValue = Interlocked.CompareExchange(ref this.continuation, continuation, null);

    if (ReferenceEquals(oldValue, ContinuationCompleted))
    {
      this.Invoke(continuation);
    }
  }

  public void Reset()
  {
    Volatile.Write(ref this.continuation, null);
  }

  public void Set()
  {
    var continuation = Interlocked.Exchange(ref this.continuation, ContinuationCompleted);
    if (continuation != null && ReferenceEquals(continuation, ContinuationCompleted) == false)
    {
      this.Invoke(continuation);
    }
  }

  private void Invoke(Action continuation)
  {
    switch (this.mode)
    {
      case ContinuationMode.Inline:
        continuation();
        break;
      case ContinuationMode.ThreadPool:
        ThreadPool.UnsafeQueueUserWorkItem(_ => continuation(), null);
        break;
      default:
        throw new ArgumentOutOfRangeException();
    }
  }
}
