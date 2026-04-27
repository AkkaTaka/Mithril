namespace Mithril.PostgresKit.Extensions;

using Mithril.PostgresKit.Execution;
using Npgsql;

internal static class PostgresCommandExtensions
{
  public static NpgsqlCommand ToNpgsqlCommand(
    this PostgresCommand command,
    NpgsqlConnection connection,
    NpgsqlTransaction? transaction = null)
  {
    var cmd = connection.CreateCommand();
    cmd.CommandText = command.Sql;

    if (transaction != null)
    {
      cmd.Transaction = transaction;
    }

    if (command.CommandTimeoutSeconds.HasValue == true)
    {
      cmd.CommandTimeout = command.CommandTimeoutSeconds.Value;
    }

    foreach (var parameter in command.Parameters)
    {
      cmd.Parameters.Add(parameter);
    }

    return cmd;
  }
}
