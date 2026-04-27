namespace Mithril.Logger;

using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using System.Text.Json;

public delegate void JsonWriterCallback(Utf8JsonWriter writer);

public interface IAppLogger
{
  public ILogger InternalLogger { get; }

  public void Verbose(string message, JsonWriterCallback? callback = null);
  public void Debug(string message, JsonWriterCallback? callback = null);
  public void Info(string message, JsonWriterCallback? callback = null);
  public void Warning(string message, JsonWriterCallback? callback = null);
  public void Error(string message, JsonWriterCallback? callback = null);
  public void Fatal(string message, JsonWriterCallback? callback = null);
  public void Error(Exception ex, string message);
  public void Fatal(Exception ex, string message);
  public void Fatal(Exception ex);
}

internal sealed class AppLogger : IAppLogger
{
  private static readonly MessageTemplateParser MessageTemplate = new MessageTemplateParser();
  private readonly ILogger logger;

  private static MessageTemplate ParseTemplate(string message)
  {
    return MessageTemplate.Parse(message);
  }

  public AppLogger(ILogger logger)
  {
    this.logger = logger;
  }

  public ILogger InternalLogger => this.logger;

  public void Verbose(string message, JsonWriterCallback? callback = null)
  {
    this.Write(message, LogEventLevel.Verbose, callback);
  }

  public void Debug(string message, JsonWriterCallback? callback = null)
  {
    this.Write(message, LogEventLevel.Debug, callback);
  }

  public void Info(string message, JsonWriterCallback? callback = null)
  {
    this.Write(message, LogEventLevel.Information, callback);
  }

  public void Warning(string message, JsonWriterCallback? callback = null)
  {
    this.Write(message, LogEventLevel.Warning, callback);
  }

  public void Error(string message, JsonWriterCallback? callback = null)
  {
    this.Write(message, LogEventLevel.Error, callback);
  }

  public void Fatal(string message, JsonWriterCallback? callback = null)
  {
    this.Write(message, LogEventLevel.Fatal, callback);
  }

  public void Error(Exception ex, string message)
  {
    this.Write(message, LogEventLevel.Error, null, ex);
  }

  public void Fatal(Exception ex, string message)
  {
    this.Write(message, LogEventLevel.Fatal, null, ex);
  }

  public void Fatal(Exception ex)
  {
    this.Write(string.Empty, LogEventLevel.Fatal, null, ex);
  }

  private void Write(
    string message,
    LogEventLevel level,
    JsonWriterCallback? callback = null,
    Exception? ex = null)
  {
    var properties = Array.Empty<LogEventProperty>();
    if (callback != null)
    {
      properties =
      [
        new LogEventProperty(Formatter.Formatter.PropertiesCallBackKey, new ScalarValue(callback))
      ];
    }

    var logEvent = new LogEvent(
      timestamp: DateTimeOffset.Now,
      level: level,
      exception: ex,
      messageTemplate: ParseTemplate(message),
      properties: properties);

    this.logger.Write(logEvent);
  }
}
