namespace PostgreSql.ConsoleApp.Function;

using Npgsql;
using PostgreSql.PostgresKit.Execution;
using PostgreSql.PostgresKit.Execution.Query;
using static PostgreSql.ConsoleApp.Function.ListRecentUserJob;

public sealed class ListRecentUserJob : PostgresFunctionMultiQueryJob<UserRow>
{
  private readonly int limit;

  public ListRecentUserJob(int limit)
  {
    this.limit = limit;
  }

  protected override string JobName => "list_recent_users";

  protected override void BindParameters(PostgresCommandBuilder command)
  {
    command.Add(this.limit);
  }

  protected override UserRow ReadRow(NpgsqlDataReader reader)
  {
    // 컬럼 순서: id(0), name(1), created_at(2)
    var id = reader.GetInt64(0);
    var name = reader.GetString(1);
    var createdAt = reader.GetFieldValue<DateTimeOffset>(2);

    return new UserRow(id, name, createdAt);
  }

  public readonly struct UserRow
  {
    public UserRow(long id, string name, DateTimeOffset createdAt)
    {
      this.Id = id;
      this.Name = name;
      this.CreatedAt = createdAt;
    }

    public long Id { get; }
    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }

    public override string ToString()
    {
      return $"{this.Id}, {this.Name}, {this.CreatedAt:O}";
    }
  }
}
