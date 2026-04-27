namespace Mithril.PostgresKit.Execution.Query;

using Npgsql;
using System.Collections.Concurrent;

public abstract class PostgresJob
{
  protected static readonly ConcurrentDictionary<SqlCacheKey, string> SqlCache = new();

  protected abstract string JobName { get; }
  protected abstract void BindParameters(PostgresCommandBuilder command);

  internal abstract ValueTask ExecuteAsync(
    NpgsqlConnection connection,
    string schemaName,
    NpgsqlTransaction? transaction,
    CancellationToken ct);

  protected readonly record struct SqlCacheKey(
    Type JobType,
    string SchemaName);
}
