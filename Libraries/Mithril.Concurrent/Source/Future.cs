namespace Mithril.Concurrent;

using System;

public sealed class Future<TResult>
{
  private static readonly Action<TResult> CompletedAction = _ => { };

  private volatile Action<TResult>? onSuccess;
  private volatile Action<Exception>? onFailure;
  private TResult? result;

  public static Future<TResult> FromResult(TResult result)
  {
    var future = new Future<TResult>
    {
      result = result,
      onSuccess = CompletedAction
    };

    return future;
  }

  public void Then(Action<TResult> onSuccess, Action<Exception> onFailure)
  {
    this.onFailure = onFailure;

    this.Then(onSuccess);
  }

  public void Then(Action<TResult> onSuccess)
  {
    var old = Interlocked.Exchange(ref this.onSuccess, onSuccess);
    if (old == null)
    {
      return;
    }

    if (ReferenceEquals(old, CompletedAction))
    {
      var result = this.result!;
      onSuccess(result);
      return;
    }

    throw new InvalidOperationException("Future.Then can be registered only once.");
  }

  internal void SetResult(TResult result)
  {
    this.result = result;

    var old = Interlocked.Exchange(ref this.onSuccess, CompletedAction);
    if (old == CompletedAction)
    {
      throw new InvalidOperationException("Future.SetResult can be registered only once.");
    }

    old?.Invoke(result);
  }

  internal void SetException(Exception exception)
  {
    var old = Interlocked.Exchange(ref this.onSuccess, CompletedAction);
    if (old != null && old != CompletedAction)
    {
      var captured = this.onFailure;
      if (captured != null)
      {
        captured(exception);
        return;
      }
    }

    throw exception;
  }
}