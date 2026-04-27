namespace Mithril.PostgresKit.Execution;

using Mithril.PostgresKit.Execution.Query;
using Mithril.PostgresKit.Execution.Retry;
using Mithril.PostgresKit.Extensions;
using Npgsql;
using System.Threading;

public sealed class PostgresClient
{
  private readonly NpgsqlDataSource dataSource;
  private readonly string schemaName;
  private readonly IRetryPolicy retryPolicy;

  public PostgresClient(
    NpgsqlDataSource dataSource, 
    string schemaName,
    IRetryPolicy? retryPolicy = null)
  {
    this.dataSource = dataSource;
    this.schemaName = schemaName;
    this.retryPolicy = retryPolicy ?? new NoRetryPolicy();
  }

  public async Task<int> ExecuteNonQueryAsync(
    PostgresCommand command,
    CancellationToken ct = default)
  {
    await using var connection = await this.dataSource.OpenConnectionAsync(ct);
    await using var npgsqlCommand = command.ToNpgsqlCommand(connection, null);

    return await npgsqlCommand.ExecuteNonQueryAsync(ct);
  }

  public async Task<DbValue> ExecuteScalarAsync(
    PostgresCommand command,
    CancellationToken ct = default)
  {
    await using var connection = await this.dataSource.OpenConnectionAsync(ct);
    await using var npgsqlCommand = command.ToNpgsqlCommand(connection, null);

    var value = await npgsqlCommand.ExecuteScalarAsync(ct);
    return new DbValue(value);
  }

  public async Task CallAsync(
    PostgresJob job,
    CancellationToken ct = default)
  {
    await this.retryPolicy.ExecuteAsync(async () =>
    {
      await this.CallAsyncInternal(job, ct);
    }, ct);
  }

  public async Task CallAsync(
    PostgresJob[] jobs,
    CancellationToken ct = default)
  {
    await this.retryPolicy.ExecuteAsync(async () =>
    {
      await this.CallAsyncInternal(jobs, ct);
    }, ct);
  }

  private async Task CallAsyncInternal(
    PostgresJob job,
    CancellationToken ct = default)
  {
    await using var connection = await this.dataSource.OpenConnectionAsync(ct);
    await job.ExecuteAsync(connection, this.schemaName, null, ct);
  }

  private async Task CallAsyncInternal(
    PostgresJob[] jobs,
    CancellationToken ct = default)
  {
    if (jobs.Length == 0)
    {
      return;
    }

    await using var connection = await this.dataSource.OpenConnectionAsync(ct);
    await using var transaction = await connection.BeginTransactionAsync(ct);

    try
    {
      foreach (var job in jobs)
      {
        await job.ExecuteAsync(connection, this.schemaName, transaction, ct);
      }

      await transaction.CommitAsync(ct);
    }
    catch
    {
      try
      {
        await transaction.RollbackAsync(ct);
      }
      catch
      {
        // rollback 실패는 원 예외를 가리지 않는다.
      }

      throw;
    }
  }
}
