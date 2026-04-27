namespace Mithril.Utils.Extensions;

using Mithril.Logger;

public static class TaskExtensions
{
  public static void FireAndForget(this Task task, IAppLogger? appLogger = null)
  {
    task.ContinueWith(t =>
    {
      if (t.IsFaulted)
      {
        appLogger?.Error($"Unhandled Task Exception: {t.Exception?.GetBaseException().Message}");
      }
    }, TaskContinuationOptions.OnlyOnFaulted);
  }
}