namespace Mithril.PostgresKit.Execution.Retry;

using Npgsql;

public sealed class ExponentialRetryPolicy : IRetryPolicy
{
  private readonly int maxRetry;
  private readonly TimeSpan baseDelay;

  public ExponentialRetryPolicy(
    int maxRetry = 3,
    TimeSpan? baseDelay = null)
  {
    this.maxRetry = maxRetry;
    this.baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(50);
  }

  private static bool IsTransient(PostgresException ex)
  {
    return ex.SqlState switch
    {
      "40001" => true, // serialization_failure
      "40P01" => true, // deadlock_detected
      "57014" => true, // query_canceled (timeout)
      "08006" => true, // connection_failure
      _ => false
    };
  }

  public async ValueTask ExecuteAsync(
    Func<ValueTask> func,
    CancellationToken ct)
  {
    var attempt = 0;

    while (true)
    {
      try
      {
        await func();
        return;
      }
      catch (PostgresException ex) when (IsTransient(ex) && attempt < this.maxRetry)
      {
        attempt++;
        await Task.Delay(this.ComputeDelay(attempt), ct);
      }
    }
  }

  private TimeSpan ComputeDelay(int attempt)
  {
    var maxDelayMs = this.baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
    var jitter = Random.Shared;
    var half = maxDelayMs / 2;
    var jitterMs = jitter.NextDouble() * half;

    return TimeSpan.FromMilliseconds(half + jitterMs);
  }
}
