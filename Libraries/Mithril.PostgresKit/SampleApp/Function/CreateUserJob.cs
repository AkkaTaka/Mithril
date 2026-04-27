namespace PostgreSql.ConsoleApp.Function;

using Npgsql;
using PostgreSql.PostgresKit.Execution;
using PostgreSql.PostgresKit.Execution.Query;

public sealed class CreateUserJob : PostgresFunctionSingleQueryJob<long>
{
  private readonly string name;

  public CreateUserJob(string name)
  {
    this.name = name;
  }

  protected override string JobName => "create_user";

  protected override void BindParameters(PostgresCommandBuilder command)
  {
    command.Add(this.name);
  }

  protected override long ReadResult(NpgsqlDataReader reader)
  {
    return reader.GetFieldValue<long>(0);
  }
}
