namespace Mithril.Concurrent.Tests;

public class FutureTests
{
  [Fact]
  public void FromResult_Then_InvokesCallbackImmediately()
  {
    var future = Future<int>.FromResult(42);
    var actual = 0;

    future.Then(value => actual = value);

    Assert.Equal(42, actual);
  }

  [Fact]
  public void Then_CanBeRegisteredOnlyOnce()
  {
    var future = new Future<int>();

    future.Then(_ => { });

    var ex = Assert.Throws<InvalidOperationException>(() => future.Then(_ => { }));
    Assert.Contains("registered only once", ex.Message);
  }

  [Fact]
  public void SetResult_CanCompleteOnlyOnce()
  {
    var promise = new Promise<int>();

    promise.Complete(1);

    var ex = Assert.Throws<InvalidOperationException>(() => promise.Complete(2));
    Assert.Contains("registered only once", ex.Message);
  }

  [Fact]
  public void Then_And_Complete_RaceConcurrently_CallbackInvokedExactlyOnce()
  {
    // Then()과 Complete()가 동시에 실행될 때 콜백이 정확히 1회만 호출되어야 한다
    const int iterations = 500;

    for (var i = 0; i < iterations; i++)
    {
      var promise = new Promise<int>();
      var future = promise.GetFuture();
      var invokeCount = 0;
      var start = new ManualResetEventSlim(false);

      var thenThread = new Thread(() =>
      {
        start.Wait();
        future.Then(_ => Interlocked.Increment(ref invokeCount));
      });

      var completeThread = new Thread(() =>
      {
        start.Wait();
        promise.Complete(42);
      });

      thenThread.Start();
      completeThread.Start();
      start.Set();
      thenThread.Join();
      completeThread.Join();

      Assert.Equal(1, Volatile.Read(ref invokeCount));
    }
  }
}
