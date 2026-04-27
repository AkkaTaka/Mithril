namespace Mithril.Concurrent.Tests;

public class SignalTests
{
  [Fact]
  public void Completed_IsAlreadyCompleted()
  {
    Assert.True(Signal.Completed.IsCompleted);
  }

  [Fact]
  public void OnCompleted_WithNull_Throws()
  {
    var signal = new Signal();

    Assert.Throws<NullReferenceException>(() => signal.OnCompleted(null!));
  }

  [Fact]
  public void Set_AfterOnCompleted_InvokesContinuation()
  {
    var signal = new Signal();
    var called = false;

    signal.OnCompleted(() => called = true);
    signal.Set();

    Assert.True(called);
  }

  [Fact]
  public void OnCompleted_AfterSet_InvokesImmediately()
  {
    var signal = new Signal();
    var called = false;

    signal.Set();
    signal.OnCompleted(() => called = true);

    Assert.True(called);
  }

  [Fact]
  public void Set_MultipleTimes_InvokesContinuationOnlyOnce()
  {
    var signal = new Signal();
    var count = 0;

    signal.OnCompleted(() => Interlocked.Increment(ref count));
    signal.Set();
    signal.Set();

    Assert.Equal(1, count);
  }

  [Fact]
  public void OnCompleted_MultipleRegistrations_KeepsFirstContinuation()
  {
    var signal = new Signal();
    var first = 0;
    var second = 0;

    signal.OnCompleted(() => Interlocked.Increment(ref first));
    signal.OnCompleted(() => Interlocked.Increment(ref second));
    signal.Set();

    Assert.Equal(1, first);
    Assert.Equal(0, second);
  }

  [Fact]
  public void Reset_AllowsReuse()
  {
    var signal = new Signal();
    var count = 0;

    signal.OnCompleted(() => Interlocked.Increment(ref count));
    signal.Set();
    signal.Reset();
    signal.OnCompleted(() => Interlocked.Increment(ref count));
    signal.Set();

    Assert.Equal(2, count);
  }

  [Fact]
  public void ThreadPoolMode_RunsContinuationAsynchronously()
  {
    var signal = new Signal(ContinuationMode.ThreadPool);
    using var waitHandle = new ManualResetEventSlim(false);

    signal.OnCompleted(() => waitHandle.Set());
    signal.Set();

    Assert.True(waitHandle.Wait(TimeSpan.FromSeconds(2)));
  }
}
