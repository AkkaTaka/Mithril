namespace Mithril.PostgresKit.Execution.Query;

using Mithril.PostgresKit.Extensions;
using Npgsql;
using System.Text;

public abstract class PostgresProcedureJob : PostgresJob
{
  internal sealed override async ValueTask ExecuteAsync(
    NpgsqlConnection connection,
    string schemaName,
    NpgsqlTransaction? transaction,
    CancellationToken ct)
  {
    var command = this.CreateCommand(schemaName);
    await using var npgsqlCommand = command.ToNpgsqlCommand(connection, transaction);

    _ = await npgsqlCommand.ExecuteNonQueryAsync(ct);
  }

  private PostgresCommand CreateCommand(string schemaName)
  {
    var builder = new PostgresCommandBuilder();
    this.BindParameters(builder);

    var key = new SqlCacheKey(this.GetType(), schemaName);
    var parameterCount = builder.ParameterCount;

    var sql = SqlCache.GetOrAdd(
      key,
      _ => BuildCallSql(schemaName, this.JobName, parameterCount));

    return builder.Build(sql);
  }

  private static string BuildCallSql(
    string schemaName,
    string procedureName,
    int parameterCount)
  {
    var sb = new StringBuilder(64);
    sb.Append("CALL ");
    sb.Append($"{schemaName}.{procedureName}");
    sb.Append('(');

    for (var i = 0; i < parameterCount; i++)
    {
      if (i > 0)
      {
        sb.Append(", ");
      }

      sb.Append("@p");
      sb.Append(i + 1);
    }

    sb.Append(");");
    return sb.ToString();
  }
}
