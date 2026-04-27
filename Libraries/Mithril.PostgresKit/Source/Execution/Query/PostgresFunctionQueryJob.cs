namespace Mithril.PostgresKit.Execution.Query;

public abstract class PostgresFunctionQueryJob : PostgresJob
{
  protected abstract string BuildSelectSql(string schemaName, int parameterCount);

  internal PostgresCommand CreateCommand(string schemaName)
  {
    var builder = new PostgresCommandBuilder();
    this.BindParameters(builder);

    var key = new SqlCacheKey(this.GetType(), schemaName);
    var parameterCount = builder.ParameterCount;

    var sql = SqlCache.GetOrAdd(
      key,
      _ => this.BuildSelectSql(schemaName, parameterCount));

    return builder.Build(sql);
  }
}
