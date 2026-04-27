namespace Mithril.Logger;

using Mithril.Logger.Formatter;
using Serilog;
using Serilog.Events;

public sealed class LoggerBuilder
{
  private readonly LogEventLevel minimumLevel;
  private readonly int maxBufferSize;
  private readonly LoggerConfiguration configuration;

  public LoggerBuilder(
    LogEventLevel minimumLevel,
    int maxBufferSize)
  {
    this.minimumLevel = minimumLevel;
    this.maxBufferSize = maxBufferSize;
    this.configuration = new LoggerConfiguration();
  }

  public LoggerBuilder AddFileWriter(
    string logPath,
    RollingInterval rollingInterval)
  {
    this.configuration.WriteTo.File(
        formatter: new FileJsonFormatter(this.maxBufferSize),
        path: logPath,
        restrictedToMinimumLevel: this.minimumLevel,
        rollingInterval: rollingInterval,
        shared: true);

    return this;
  }

  public LoggerBuilder AddFileAsyncWriter(
    string logPath,
    RollingInterval rollingInterval)
  {
    this.configuration.WriteTo.Async(config => config.File(
        formatter: new FileJsonFormatter(this.maxBufferSize),
        path: logPath,
        restrictedToMinimumLevel: this.minimumLevel,
        rollingInterval: rollingInterval,
        shared: true));

    return this;
  }

  public LoggerBuilder AddConsoleWriter()
  {
    this.configuration.WriteTo.Console(
        formatter: new ConsoleFormatter(this.maxBufferSize),
        restrictedToMinimumLevel: this.minimumLevel);

    return this;
  }

  public LoggerBuilder AddConsoleAsyncWriter()
  {
    this.configuration.WriteTo.Async(config => config.Console(
      formatter: new ConsoleFormatter(this.maxBufferSize),
      restrictedToMinimumLevel: this.minimumLevel));

    return this;
  }

  public IAppLogger Build()
  {
    return new AppLogger(this.configuration.CreateLogger());
  }
}
