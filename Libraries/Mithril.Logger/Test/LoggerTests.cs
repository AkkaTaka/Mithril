namespace Mithril.Logger.Tests;

using Serilog;
using Serilog.Events;
using System.Text;
using System.Text.Json;

public sealed class LoggerTests
{
  [Fact]
  public void AddFileWriter_WritesStructuredLog_WithCustomProperties()
  {
    var logPath = GetTempLogPath();
    try
    {
      var logger = new LoggerBuilder(LogEventLevel.Verbose, 4096)
        .AddFileWriter(logPath, RollingInterval.Infinite)
        .Build();

      logger.Info("hello logger", writer =>
      {
        writer.Write("Id", 7);
        writer.Write("Name", "mithril");
      });

      DisposeInternalLogger(logger);

      var line = WaitForSingleLine(logPath);
      using var json = JsonDocument.Parse(line);
      var root = json.RootElement;

      Assert.Equal("INF", root.GetProperty("Level").GetString());
      Assert.Equal("hello logger", root.GetProperty("Message").GetString());
      Assert.Equal(7, root.GetProperty("Properties").GetProperty("Id").GetInt32());
      Assert.Equal("mithril", root.GetProperty("Properties").GetProperty("Name").GetString());
      Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("Time").GetString()));
    }
    finally
    {
      CleanupLogDirectory(logPath);
    }
  }

  [Fact]
  public void MinimumLevel_FiltersOutLowerLevelLogs()
  {
    var logPath = GetTempLogPath();
    try
    {
      var logger = new LoggerBuilder(LogEventLevel.Warning, 4096)
        .AddFileWriter(logPath, RollingInterval.Infinite)
        .Build();

      logger.Info("should not be written");
      logger.Warning("should be written");

      DisposeInternalLogger(logger);

      var lines = WaitForLines(logPath, expectedCount: 1);
      using var json = JsonDocument.Parse(lines[0]);
      var root = json.RootElement;

      Assert.Equal("WAR", root.GetProperty("Level").GetString());
      Assert.Equal("should be written", root.GetProperty("Message").GetString());
    }
    finally
    {
      CleanupLogDirectory(logPath);
    }
  }

  [Fact]
  public void Error_WithException_WritesExceptionObject()
  {
    var logPath = GetTempLogPath();
    try
    {
      var logger = new LoggerBuilder(LogEventLevel.Verbose, 4096)
        .AddFileWriter(logPath, RollingInterval.Infinite)
        .Build();

      var ex = new InvalidOperationException("invalid state");
      logger.Error(ex, "failed operation");

      DisposeInternalLogger(logger);

      var line = WaitForSingleLine(logPath);
      using var json = JsonDocument.Parse(line);
      var root = json.RootElement;
      var exception = root.GetProperty("Exception");

      Assert.Equal("ERR", root.GetProperty("Level").GetString());
      Assert.Equal("failed operation", root.GetProperty("Message").GetString());
      Assert.Equal(typeof(InvalidOperationException).FullName, exception.GetProperty("Type").GetString());
      Assert.Equal("invalid state", exception.GetProperty("Message").GetString());
      Assert.Contains("InvalidOperationException", exception.GetProperty("StackTrace").GetString());
    }
    finally
    {
      CleanupLogDirectory(logPath);
    }
  }

  [Fact]
  public void EmptyLogger_Methods_DoNotThrow()
  {
    var logger = new EmptyLogger();
    var ex = Record.Exception(() =>
    {
      logger.Verbose("v");
      logger.Debug("d");
      logger.Info("i", writer => writer.Write("Count", 1));
      logger.Warning("w");
      logger.Error("e");
      logger.Error(new Exception("err"), "error");
      logger.Fatal("f");
      logger.Fatal(new Exception("fatal"), "fatal");
      logger.Fatal(new Exception("fatal only"));
    });

    Assert.Null(ex);
    Assert.NotNull(logger.InternalLogger);
  }

  [Fact]
  public void Utf8JsonWriterExtensions_WriteObjectAndValues_Works()
  {
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
      using (writer.WriteObject())
      {
        writer.Write("Name", "mithril");
        writer.Write("Count", 3);
        writer.Write("Enabled", true);
        writer.WriteNull("Optional");
      }
    }

    var jsonText = Encoding.UTF8.GetString(stream.ToArray());
    using var json = JsonDocument.Parse(jsonText);
    var root = json.RootElement;

    Assert.Equal("mithril", root.GetProperty("Name").GetString());
    Assert.Equal(3, root.GetProperty("Count").GetInt32());
    Assert.True(root.GetProperty("Enabled").GetBoolean());
    Assert.Equal(JsonValueKind.Null, root.GetProperty("Optional").ValueKind);
  }

  private static string GetTempLogPath()
  {
    var dir = Path.Combine(Path.GetTempPath(), "Mithril.Logger.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "app.log");
  }

  private static void DisposeInternalLogger(IAppLogger logger)
  {
    if (logger.InternalLogger is IDisposable disposable)
    {
      disposable.Dispose();
    }
  }

  private static string WaitForSingleLine(string path)
  {
    return WaitForLines(path, expectedCount: 1)[0];
  }

  private static string[] WaitForLines(string path, int expectedCount)
  {
    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (DateTime.UtcNow < deadline)
    {
      if (File.Exists(path))
      {
        var lines = File.ReadAllLines(path)
          .Where(line => !string.IsNullOrWhiteSpace(line))
          .ToArray();
        if (lines.Length >= expectedCount)
        {
          return lines;
        }
      }

      Thread.Sleep(50);
    }

    throw new Xunit.Sdk.XunitException($"Expected at least {expectedCount} log lines in '{path}'.");
  }

  private static void CleanupLogDirectory(string logPath)
  {
    var dir = Path.GetDirectoryName(logPath);
    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
    {
      return;
    }

    try
    {
      Directory.Delete(dir, recursive: true);
    }
    catch
    {
      // Ignore cleanup failures in tests.
    }
  }
}
