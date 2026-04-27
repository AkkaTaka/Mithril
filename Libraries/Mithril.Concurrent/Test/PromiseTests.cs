namespace Mithril.Concurrent.Tests;

public class PromiseTests
{
  [Fact]
  public void TryComplete_WithValidType_CompletesFuture()
  {
    var promise = new Promise<int>();
    var future = promise.GetFuture();
    var actual = 0;

    future.Then(value => actual = value);
    promise.TryComplete(7);

    Assert.Equal(7, actual);
  }

  [Fact]
  public void TryComplete_WithInvalidType_ThrowsInvalidCastException_WhenFailureHandlerIsMissing()
  {
    var promise = new Promise<int>();

    var ex = Assert.Throws<InvalidCastException>(() => promise.TryComplete("wrong"));

    Assert.Contains("String", ex.Message);
    Assert.Contains("System.Int32", ex.Message);
  }

  [Fact]
  public void SetException_WithFailureHandler_InvokesFailureCallback()
  {
    var promise = new Promise<int>();
    var future = promise.GetFuture();
    var expected = new InvalidOperationException("boom");
    Exception? actual = null;

    future.Then(_ => { }, ex => actual = ex);
    promise.SetException(expected);

    Assert.Same(expected, actual);
  }

  [Fact]
  public void SetException_WithoutFailureHandler_ThrowsException()
  {
    var promise = new Promise<int>();
    var future = promise.GetFuture();
    var expected = new InvalidOperationException("boom");

    future.Then(_ => { });

    var thrown = Assert.Throws<InvalidOperationException>(() => promise.SetException(expected));
    Assert.Same(expected, thrown);
  }

  [Fact]
  public void TryComplete_CalledTwice_ThrowsInvalidOperationExceptionOnSecondCall()
  {
    var promise = new Promise<int>();
    var future = promise.GetFuture();
    future.Then(_ => { });

    promise.TryComplete(1);

    Assert.Throws<InvalidOperationException>(() => promise.TryComplete(2));
  }

  [Fact]
  public void SetException_BeforeThen_ThrowsExceptionImmediately()
  {
    // Then()이 등록되기 전에 SetException이 호출되면 예외가 호출 시점에 즉시 throw된다
    var promise = new Promise<int>();
    var expected = new InvalidOperationException("early exception");

    var thrown = Assert.Throws<InvalidOperationException>(() => promise.SetException(expected));

    Assert.Same(expected, thrown);
  }
}
