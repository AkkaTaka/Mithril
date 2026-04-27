namespace PostgreSql.ConsoleApp.Procedure;

using PostgreSql.PostgresKit.Execution;
using PostgreSql.PostgresKit.Execution.Query;

public sealed class RebuildIndexesJob : PostgresProcedureJob
{
  public RebuildIndexesJob()
  {
  }

  protected override string JobName => "rebuild_indexes";

  protected override void BindParameters(PostgresCommandBuilder command)
  {
    // no args
  }
}