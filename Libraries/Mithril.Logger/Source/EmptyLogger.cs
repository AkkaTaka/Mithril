namespace Mithril.Logger;

using Serilog;
using Serilog.Events;
using System;

public sealed class EmptyLogger : IAppLogger
{
  private class EmptyInternalLogger : ILogger
  {
    public void Write(LogEvent logEvent)
    {
    }
  }

  public EmptyLogger()
  {
    this.InternalLogger = new EmptyInternalLogger();
  }

  public ILogger InternalLogger { get; }

  public void Debug(string message, JsonWriterCallback? callback = null)
  {
  }

  public void Error(string message, JsonWriterCallback? callback = null)
  {
  }

  public void Error(Exception ex, string message)
  {
  }

  public void Fatal(string message, JsonWriterCallback? callback = null)
  {
  }

  public void Fatal(Exception ex, string message)
  {
  }

  public void Fatal(Exception ex)
  {
  }

  public void Info(string message, JsonWriterCallback? callback = null)
  {
  }

  public void Verbose(string message, JsonWriterCallback? callback = null)
  {
  }

  public void Warning(string message, JsonWriterCallback? callback = null)
  {
  }
}
