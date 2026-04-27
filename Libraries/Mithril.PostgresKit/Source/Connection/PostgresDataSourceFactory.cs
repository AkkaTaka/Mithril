namespace Mithril.PostgresKit.Connection;

using Configuration;
using Npgsql;

public sealed class PostgresDataSourceFactory
{
  public NpgsqlDataSource Create(PostgresOptions options)
  {
    if (options is null)
    {
      throw new ArgumentNullException(nameof(options));
    }

    if (string.IsNullOrWhiteSpace(options.Host))
    {
      throw new ArgumentException("PostgresOptions.Host is required.", nameof(options));
    }

    if (options.Port <= 0)
    {
      throw new ArgumentException("PostgresOptions.Port must be greater than 0.", nameof(options));
    }

    if (string.IsNullOrWhiteSpace(options.Database))
    {
      throw new ArgumentException("PostgresOptions.Database is required.", nameof(options));
    }

    if (string.IsNullOrWhiteSpace(options.Username))
    {
      throw new ArgumentException("PostgresOptions.Username is required.", nameof(options));
    }

    if (options.MaxPoolSize <= 0)
    {
      throw new ArgumentException("PostgresOptions.MaxPoolSize must be greater than 0.", nameof(options));
    }

    if (options.MinPoolSize < 0)
    {
      throw new ArgumentException("PostgresOptions.MinPoolSize must be 0 or greater.", nameof(options));
    }

    if (options.MinPoolSize > options.MaxPoolSize)
    {
      throw new ArgumentException("PostgresOptions.MinPoolSize must be less than or equal to MaxPoolSize.", nameof(options));
    }

    if (options.CommandTimeoutSeconds <= 0)
    {
      throw new ArgumentException("PostgresOptions.CommandTimeoutSeconds must be greater than 0.", nameof(options));
    }

    if (options.ConnectionIdleLifetimeSeconds < 0)
    {
      throw new ArgumentException("PostgresOptions.ConnectionIdleLifetimeSeconds must be 0 or greater.", nameof(options));
    }

    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
      Host = options.Host,
      Port = options.Port,
      Database = options.Database,
      Username = options.Username,
      Password = options.Password,
      SslMode = options.SslMode,

      // Pooling is enabled by default in Npgsql, but being explicit is clearer.
      Pooling = true,
      MaxPoolSize = options.MaxPoolSize,
      MinPoolSize = options.MinPoolSize,

      // Seconds.
      ConnectionIdleLifetime = options.ConnectionIdleLifetimeSeconds,

      // Seconds.
      CommandTimeout = options.CommandTimeoutSeconds,

      ApplicationName = options.ApplicationName,

      MaxAutoPrepare = 256,
      AutoPrepareMinUsages = 2
    };

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);

    // Keep defaults conservative:
    // - No parameter logging
    // - No detailed errors
    //
    // If you later want richer diagnostics in Development only, we can add an option flag
    // and toggle these:
    // dataSourceBuilder.EnableParameterLogging();
    // dataSourceBuilder.EnableDetailedErrors();

    return dataSourceBuilder.Build();
  }
}
