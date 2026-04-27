namespace Mithril.PostgresKit.Migration;

public sealed class FlywayMigrateOptions
{
  public string FlywayPath { get; init; } = "flyway";

  // 예: "flyway.conf" 또는 절대 경로
  public string ConfigFile { get; init; } = "flyway.conf";

  // 필요 시: "-locations=filesystem:db/migration"
  public string? Locations { get; init; }

  // 필요 시: "-table=flyway_schema_history"
  public string? HistoryTable { get; init; }

  public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(3);

  public string WorkingDirectory { get; init; } = string.Empty;
  public string ScriptDirectory { get; init; } = string.Empty;
}
