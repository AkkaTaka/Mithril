namespace Mithril.Concurrent;

using System;

public interface IPromise
{
  public void TryComplete(object value);
  public void SetException(Exception exception);
}

public interface IPromise<in TResult> : IPromise
{
  void Complete(TResult result);
}