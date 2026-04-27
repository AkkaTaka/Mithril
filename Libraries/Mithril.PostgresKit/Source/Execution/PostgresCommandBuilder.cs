namespace Mithril.PostgresKit.Execution;

using Npgsql;
using NpgsqlTypes;

public sealed class PostgresCommandBuilder
{
  private readonly List<NpgsqlParameter> parameters;

  public PostgresCommandBuilder()
  {
    this.parameters = new List<NpgsqlParameter>();
  }

  public int ParameterCount => this.parameters.Count;

  public PostgresCommand Build(string sql)
  {
    return new PostgresCommand(sql, this.parameters);
  }
  
  public void Add(int value)
  {
    this.AddTyped(NpgsqlDbType.Integer, value);
  }

  public void Add(long value)
  {
    this.AddTyped(NpgsqlDbType.Bigint, value);
  }

  public void Add(string value)
  {
    this.AddTyped(NpgsqlDbType.Text, value);
  }

  private void AddTyped(NpgsqlDbType dbType, object value)
  {
    var index = this.parameters.Count + 1;
    var parameter = new NpgsqlParameter($"p{index}", dbType)
    {
      Value = value
    };

    this.parameters.Add(parameter);
  }
}