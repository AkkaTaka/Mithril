namespace Mithril.PostgresKit.Execution;

using Npgsql;

public sealed class PostgresCommand
{
  private readonly IReadOnlyList<NpgsqlParameter> parameters;

  public PostgresCommand(
    string sql,
    IReadOnlyList<NpgsqlParameter> parameters,
    int? commandTimeoutSeconds = null)
  {
    if (string.IsNullOrWhiteSpace(sql) == true)
    {
      throw new ArgumentException("SQL must not be empty.", nameof(sql));
    }

    this.Sql = sql;
    this.CommandTimeoutSeconds = commandTimeoutSeconds;
    this.parameters = parameters;
  }

  public string Sql { get; }
  public int? CommandTimeoutSeconds { get; }
  public IReadOnlyList<NpgsqlParameter> Parameters => this.parameters;
  public int ParameterCount => this.parameters.Count;
}