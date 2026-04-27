namespace Mithril.PostgresKit.Execution.Query;

using Mithril.PostgresKit.Extensions;
using Npgsql;
using System.Data;
using System.Text;

public abstract class PostgresFunctionSingleQueryJob<T> : PostgresFunctionQueryJob
{
  private T result;

  public PostgresFunctionSingleQueryJob()
  {
    this.result = default!;
  }

  public T Result => this.result;

  protected abstract T ReadResult(NpgsqlDataReader reader);

  protected sealed override string BuildSelectSql(string schemaName, int parameterCount)
  {
    var sb = new StringBuilder(64);
    sb.Append("SELECT ");
    sb.Append($"{schemaName}.{this.JobName}");
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

  internal sealed override async ValueTask ExecuteAsync(
    NpgsqlConnection connection,
    string schemaName,
    NpgsqlTransaction? transaction,
    CancellationToken ct)
  {
    var command = this.CreateCommand(schemaName);
    await using var npgsqlCommand = command.ToNpgsqlCommand(connection, transaction);

    await using var reader = await npgsqlCommand.ExecuteReaderAsync(
      CommandBehavior.SingleRow | CommandBehavior.SingleResult,
      ct);

    if (await reader.ReadAsync(ct) == false)
    {
      throw new InvalidOperationException($"Function '{this.GetType().Name}' returned no rows.");
    }

    this.Read(reader);
  }

  private void Read(NpgsqlDataReader reader)
  {
    this.result = this.ReadResult(reader);
  }
}
