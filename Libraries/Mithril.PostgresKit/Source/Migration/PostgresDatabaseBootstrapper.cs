namespace Mithril.PostgresKit.Migration;

using Mithril.PostgresKit.Execution;
using Npgsql;

public static class PostgresDatabaseBootstrapper
{
  public static async Task EnsureDatabaseExistsAsync(
    PostgresClient client,
    string databaseName,
    CancellationToken ct = default)
  {
    if (client is null)
    {
      throw new ArgumentNullException(nameof(client));
    }

    if (string.IsNullOrWhiteSpace(databaseName) == true)
    {
      throw new ArgumentException("databaseName is required.", nameof(databaseName));
    }

    // 1) 존재 확인
    var existsCommand = new PostgresCommand(
      "SELECT 1 FROM pg_database WHERE datname = @p1;",
      [new NpgsqlParameter("p1", databaseName)]);

    var exists = await client.ExecuteScalarAsync(existsCommand, ct);
    if (exists.IsNull == false)
    {
      return;
    }

    // 2) 생성
    var createSql = $"CREATE DATABASE {databaseName};";
    var createCommand = new PostgresCommand(createSql, []);

    try
    {
      await client.ExecuteNonQueryAsync(createCommand, ct);
    }
    catch (PostgresException ex) when (ex.SqlState == "42P04")
    {
      // duplicate_database (레이스: 다른 인스턴스가 먼저 만든 경우)
      return;
    }
  }
}