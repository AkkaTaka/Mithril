namespace Mithril.PostgresKit.Execution.Query;

using Mithril.PostgresKit.Extensions;
using Npgsql;
using System.Text;

public abstract class PostgresFunctionMultiQueryJob<T> : PostgresFunctionQueryJob
{
  private T[] results;

  public PostgresFunctionMultiQueryJob()
  {
    this.results = Array.Empty<T>();
  }

  public T[] Results => this.results;

  protected abstract T ReadRow(NpgsqlDataReader reader);

  protected sealed override string BuildSelectSql(string schemaName, int parameterCount)
  {
    var sb = new StringBuilder(64);
    sb.Append("SELECT * FROM ");
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
    await using var reader = await npgsqlCommand.ExecuteReaderAsync(ct);

    await this.ReadAsync(reader, ct);
  }

  private async Task ReadAsync(NpgsqlDataReader reader, CancellationToken ct)
  {
    var results = new List<T>(64);

    while (await reader.ReadAsync(ct))
    {
      results.Add(this.ReadRow(reader));
    }

    this.results = results.ToArray();
  }
}
