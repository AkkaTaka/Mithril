namespace Mithril.Diagnostics.Tests;

using Mithril.Diagnostics.Dump;
using Mithril.Logger;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

public sealed class CrashDumperTests : IDisposable
{
  private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
  private readonly List<string> tempDirectories = [];

  public CrashDumperTests()
  {
    this.ResetCrashDumperState();
  }

  public void Dispose()
  {
    this.ResetCrashDumperState();

    foreach (var directory in this.tempDirectories)
    {
      try
      {
        if (Directory.Exists(directory))
        {
          Directory.Delete(directory, recursive: true);
        }
      }
      catch
      {
      }
    }
  }

  [Fact]
  public void Configure_WithDirectory_CreatesDirectory()
  {
    var directory = this.CreateTempDirectoryPath();
    if (Directory.Exists(directory))
    {
      Directory.Delete(directory, recursive: true);
    }

    CrashDumper.Configure(appLogger: null, directory);

    Assert.True(Directory.Exists(directory));
  }

  [Fact]
  public void SanitizeFileName_ReplacesInvalidCharacters()
  {
    var raw = "Unit:Test*Name?.txt";

    var sanitized = (string)this.InvokePrivate("SanitizeFileName", raw)!;
    var invalid = Path.GetInvalidFileNameChars();

    Assert.Contains('_', sanitized);
    Assert.DoesNotContain(sanitized, ch => Array.IndexOf(invalid, ch) >= 0);
  }

  [Fact]
  public void BuildDumpFileName_FormatsExpectedPattern()
  {
    using var process = Process.GetCurrentProcess();
    var utc = new DateTime(2025, 01, 02, 03, 04, 05, 678, DateTimeKind.Utc);

    var fileName = (string)this.InvokePrivate("BuildDumpFileName", process, utc, "Crash:Reason")!;

    Assert.StartsWith($"{process.ProcessName}_{process.Id}_20250102_030405_678_", fileName, StringComparison.Ordinal);
    Assert.EndsWith(".dmp", fileName, StringComparison.Ordinal);
    Assert.DoesNotContain(":", fileName, StringComparison.Ordinal);
  }

  [Fact]
  public void BuildMeta_IncludesCoreFields()
  {
    using var process = Process.GetCurrentProcess();
    var utc = new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc);

    var meta = (string)this.InvokePrivate("BuildMeta", "UnitTestReason", process, utc)!;

    Assert.Contains("Reason: UnitTestReason", meta, StringComparison.Ordinal);
    Assert.Contains($"PID: {process.Id}", meta, StringComparison.Ordinal);
    Assert.Contains($"Process: {process.ProcessName}", meta, StringComparison.Ordinal);
    Assert.Contains("Framework:", meta, StringComparison.Ordinal);
    Assert.Contains("OS:", meta, StringComparison.Ordinal);
    Assert.Contains("CommandLine:", meta, StringComparison.Ordinal);
  }

  [Fact]
  public void TryWriteSidecar_WritesContentToFile()
  {
    var directory = this.CreateTempDirectoryPath();
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "sidecar.txt");
    const string content = "sidecar-content";

    this.InvokePrivate("TryWriteSidecar", path, content);

    Assert.True(File.Exists(path));
    Assert.Equal(content, File.ReadAllText(path));
  }

  [Fact]
  public void TryWriteDump_WhenAlreadyTaken_LogsWarningAndSkipsWrite()
  {
    var logger = new TestLogger();
    var directory = this.CreateTempDirectoryPath();

    CrashDumper.Configure(logger, directory);
    this.SetPrivateField("DumpTaken", 1);

    this.InvokePrivate("TryWriteDump", null, "AlreadyTakenCase");

    Assert.Single(logger.WarningMessages);
    Assert.Contains("already taken", logger.WarningMessages[0], StringComparison.OrdinalIgnoreCase);
    Assert.Empty(Directory.GetFiles(directory));
  }

  private string CreateTempDirectoryPath()
  {
    var path = Path.Combine(Path.GetTempPath(), "Mithril.Diagnostics.Tests", Guid.NewGuid().ToString("N"));
    this.tempDirectories.Add(path);
    return path;
  }

  private void ResetCrashDumperState()
  {
    this.SetPrivateField("DumpTaken", 0);
    this.SetPrivateField("AppLogger", null);
    this.SetPrivateField("DumpDirectory", Path.Combine(AppContext.BaseDirectory, "dumps"));
  }

  private void SetPrivateField(string name, object? value)
  {
    var field = typeof(CrashDumper).GetField(name, PrivateStatic);
    Assert.NotNull(field);
    field!.SetValue(null, value);
  }

  private object? InvokePrivate(string name, params object?[] args)
  {
    var method = typeof(CrashDumper).GetMethod(name, PrivateStatic);
    Assert.NotNull(method);
    return method!.Invoke(null, args);
  }

  private sealed class TestLogger : IAppLogger
  {
    public TestLogger()
    {
      this.InternalLogger = new EmptyLogger().InternalLogger;
    }

    public ILogger InternalLogger { get; }

    public List<string> WarningMessages { get; } = [];

    public void Verbose(string message, JsonWriterCallback? callback = null)
    {
    }

    public void Debug(string message, JsonWriterCallback? callback = null)
    {
    }

    public void Info(string message, JsonWriterCallback? callback = null)
    {
    }

    public void Warning(string message, JsonWriterCallback? callback = null)
    {
      this.WarningMessages.Add(message);
    }

    public void Error(string message, JsonWriterCallback? callback = null)
    {
    }

    public void Fatal(string message, JsonWriterCallback? callback = null)
    {
    }

    public void Error(Exception ex, string message)
    {
    }

    public void Fatal(Exception ex, string message)
    {
    }

    public void Fatal(Exception ex)
    {
    }
  }
}
