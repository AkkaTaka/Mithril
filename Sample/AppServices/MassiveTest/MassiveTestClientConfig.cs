namespace Mithril.AppServices.MassiveTest;

using System.Text.Json.Serialization;

public enum SendMode
{
  Flood,
  RateLimited,
}

public enum ClientShutdownMode
{
  Graceful,
  SnapshotThenExit,
}

public sealed record MassiveTestClientConfig
{
  [JsonConstructor]
  public MassiveTestClientConfig(
    string serverAddress,
    int serverPort,
    int totalConnections,
    int connectBatchSize,
    int connectIntervalMs,
    SendMode mode,
    int ratePerSecond,
    int statsIntervalSeconds,
    int durationSeconds,
    int connectMaxRetries,
    int closeFanoutParallelism,
    ClientShutdownMode shutdownMode)
  {
    this.ServerAddress = serverAddress;
    this.ServerPort = serverPort;
    this.TotalConnections = totalConnections;
    this.ConnectBatchSize = connectBatchSize;
    this.ConnectIntervalMs = connectIntervalMs;
    this.Mode = mode;
    this.RatePerSecond = ratePerSecond;
    this.StatsIntervalSeconds = statsIntervalSeconds;
    this.DurationSeconds = durationSeconds;
    this.ConnectMaxRetries = connectMaxRetries;
    this.CloseFanoutParallelism = closeFanoutParallelism;
    this.ShutdownMode = shutdownMode;
  }

  public string ServerAddress { get; }
  public int ServerPort { get; }
  public int TotalConnections { get; }
  public int ConnectBatchSize { get; }
  public int ConnectIntervalMs { get; }
  public SendMode Mode { get; }
  public int RatePerSecond { get; }
  public int StatsIntervalSeconds { get; }
  public int DurationSeconds { get; }
  public int ConnectMaxRetries { get; }
  public int CloseFanoutParallelism { get; }
  public ClientShutdownMode ShutdownMode { get; }
}
