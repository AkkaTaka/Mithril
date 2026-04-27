namespace Mithril.PostgresKit.Execution.Query;

using Npgsql;

public abstract class PostgresFunctionIntScalarQueryJob : PostgresFunctionSingleQueryJob<int>
{
  protected sealed override int ReadResult(NpgsqlDataReader reader)
  {
    return reader.GetFieldValue<int>(0);
  }
}
