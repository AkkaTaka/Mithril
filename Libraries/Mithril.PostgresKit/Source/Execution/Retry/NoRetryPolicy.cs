
namespace Mithril.PostgresKit.Execution.Retry;

public sealed class NoRetryPolicy : IRetryPolicy
{
  public ValueTask ExecuteAsync(Func<ValueTask> func, CancellationToken ct)
  {
    return func();
  }
}
