namespace Mithril.PostgresKit.Execution.Retry;

public interface IRetryPolicy
{
  public ValueTask ExecuteAsync(
    Func<ValueTask> func,
    CancellationToken ct);
}
