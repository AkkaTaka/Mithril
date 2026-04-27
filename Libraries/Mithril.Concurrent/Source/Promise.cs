namespace Mithril.Concurrent;

using System;

public sealed class Promise<TResult> : IPromise<TResult>
{
  private readonly Future<TResult> future;

  public Promise()
  {
    this.future = new Future<TResult>();
  }

  public void TryComplete(object value)
  {
    if (value is TResult result)
    {
      this.Complete(result);
    }
    else
    {
      this.SetException(
        new InvalidCastException(
          $"attempt invalid type casting : {value.GetType().Name} to {typeof(TResult)}"));
    }
  }

  public void Complete(TResult result)
  {
    this.future.SetResult(result);
  }

  public void SetException(Exception exception)
  {
    this.future.SetException(exception);
  }

  public Future<TResult> GetFuture()
  {
    return this.future;
  }
}