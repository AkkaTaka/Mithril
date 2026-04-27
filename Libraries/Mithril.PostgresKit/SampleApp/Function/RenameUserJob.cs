namespace PostgreSql.ConsoleApp.Function;

using PostgreSql.PostgresKit.Execution;
using PostgreSql.PostgresKit.Execution.Query;

public sealed class RenameUserJob : PostgresFunctionIntScalarQueryJob
{
  private readonly long userId;
  private readonly string name;

  public RenameUserJob(long userId, string name)
  {
    this.userId = userId;
    this.name = name;
  }

  protected override string JobName => "rename_user";

  protected override void BindParameters(PostgresCommandBuilder command)
  {
    command.Add(this.userId);
    command.Add(this.name);
  }
}