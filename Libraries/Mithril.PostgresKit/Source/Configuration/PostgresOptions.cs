namespace Mithril.PostgresKit.Configuration;

using Npgsql;

public sealed class PostgresOptions
{
  public string Host { get; init; } = "localhost";
  public int Port { get; init; } = 5432;
  public string Database { get; init; } = string.Empty;
  public string Username { get; init; } = string.Empty;
  public string Password { get; init; } = string.Empty;
  public SslMode SslMode { get; init; } = SslMode.Disable;
  public int CommandTimeoutSeconds { get; init; } = 30;
  public int MaxPoolSize { get; init; } = 100;
  public int MinPoolSize { get; init; } = 0;
  public int ConnectionIdleLifetimeSeconds { get; init; } = 300;
  public string ApplicationName { get; init; } = "PostgresKit";
}