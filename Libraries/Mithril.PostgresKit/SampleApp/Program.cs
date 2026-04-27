
using PostgreSql.ConsoleApp.Function;
using PostgreSql.ConsoleApp.Procedure;
using PostgreSql.PostgresKit.Configuration;
using PostgreSql.PostgresKit.Connection;
using PostgreSql.PostgresKit.Execution;
using PostgreSql.PostgresKit.Execution.Retry;
using PostgreSql.PostgresKit.Migration;

var options = new PostgresOptions
{
  Host = "localhost",
  Port = 5432,
  Database = "gameDb",
  Username = "postgres",
  Password = "0308",
  ApplicationName = "PostgresKit.ConsoleTest"
};

var schemaName = "schema_sample";
var factory = new PostgresDataSourceFactory();

await BootstrapperEnsureDatabaseTest(factory, options, options.Database, schemaName);
await MigrationTest(schemaName);

await using var dataSource = factory.Create(options);
var postgresClient = new PostgresClient(dataSource, schemaName, new ExponentialRetryPolicy());

await JobTest(postgresClient);
await SingleQueryQueryTest(postgresClient, 2, "lee");
await MultiQueryTest(postgresClient);

static async Task BootstrapperEnsureDatabaseTest(
  PostgresDataSourceFactory factory,
  PostgresOptions options,
  string dbName,
  string schemaName)
{
  var adminOptions = new PostgresOptions
  {
    Host = options.Host,
    Port = options.Port,
    Database = "postgres",
    Username = options.Username,
    Password = options.Password,
    SslMode = options.SslMode,
    CommandTimeoutSeconds = options.CommandTimeoutSeconds,
    MaxPoolSize = options.MaxPoolSize,
    MinPoolSize = options.MinPoolSize,
    ConnectionIdleLifetimeSeconds = options.ConnectionIdleLifetimeSeconds,
    ApplicationName = options.ApplicationName
  };

  await using (var adminDataSource = factory.Create(adminOptions))
  {
    var adminClient = new PostgresClient(adminDataSource, schemaName);
    await PostgresDatabaseBootstrapper.EnsureDatabaseExistsAsync(adminClient, dbName);
  }
}

static async Task MigrationTest(string schema)
{
  var options = new FlywayMigrateOptions
  {
    FlywayPath = @"D:\flyway\flyway.cmd",
    ConfigFile = "flyway.conf",
    Locations = "filesystem:db/migration",
    HistoryTable = "flyway_schema_history",
    Timeout = TimeSpan.FromMinutes(3),
    WorkingDirectory = "./",
    ScriptDirectory = "./Script"
  };

  await FlywayMigrator.MigrateAsync(schema, options);
}

static async Task JobTest(PostgresClient postgresClient)
{
  var job = new CreateUserJob("kim");
  await postgresClient.CallAsync(job);
  await postgresClient.CallAsync(new RebuildIndexesJob());

  var userId = job.Result;
  Console.WriteLine($"Result: {userId}");
}

static async Task SingleQueryQueryTest(
  PostgresClient postgresClient,
  long userId,
  string newName)
{
  var job = new RenameUserJob(userId, newName);
  await postgresClient.CallAsync(job);
  var affected = job.Result;
  Console.WriteLine($"Rename affected: {affected}");
}

static async Task MultiQueryTest(PostgresClient postgresClient)
{
  var job = new ListRecentUserJob(10);
  await postgresClient.CallAsync(job);
  var users = job.Results;
  Console.WriteLine(string.Join(", ", users));
}