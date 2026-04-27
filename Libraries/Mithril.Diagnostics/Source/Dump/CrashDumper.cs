namespace Mithril.Diagnostics.Dump;

using Mithril.Logger;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class CrashDumper
{
  private static int DumpTaken;
  private static IAppLogger? AppLogger;
  private static string DumpDirectory = Path.Combine(AppContext.BaseDirectory, "dumps");

  public static void Configure(IAppLogger? appLogger, string? directory)
  {
    if (string.IsNullOrWhiteSpace(directory) == false)
    {
      DumpDirectory = directory;
    }

    Directory.CreateDirectory(DumpDirectory);
    AppLogger = appLogger;
  }

  public static void RegisterGlobalHandlers()
  {
    AppDomain.CurrentDomain.UnhandledException += thisAppDomainUnhandledException;
    TaskScheduler.UnobservedTaskException += thisUnobservedTaskException;
  }

  public static void FailFastWithDump(Exception ex, string reason)
  {
    TryWriteDump(ex, reason);
    Environment.FailFast(reason, ex);
  }

  private static void TryWriteDump(Exception? exception, string reason)
  {
    if (Interlocked.Exchange(ref DumpTaken, 1) != 0)
    {
      AppLogger?.Warning($"CrashDump skipped (already taken). reason={reason}");
      return;
    }

    try
    {
      var now = DateTime.UtcNow;
      var process = Process.GetCurrentProcess();
      var fileName = BuildDumpFileName(process, now, reason);
      var fullPath = Path.Combine(DumpDirectory, fileName);

      AppLogger?.Fatal($"CrashDump start. reason={reason}, path={fullPath}");

      using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
      var success = Imports.MiniDumpWriteDump(
        process.Handle,
        (uint)process.Id,
        stream.SafeFileHandle,
        MiniDumpType.MiniDumpWithFullMemory,
        IntPtr.Zero,
        IntPtr.Zero,
        IntPtr.Zero);

      if (success == false)
      {
        var error = Marshal.GetLastWin32Error();
        AppLogger?.Fatal($"MiniDumpWriteDump failed. error={error}");
        TryWriteSidecar(fullPath + ".err.txt", $"MiniDumpWriteDump failed. GetLastError={error}");
        return;
      }

      if (exception != null)
      {
        TryWriteSidecar(fullPath + ".exception.txt", exception.ToString());
      }

      TryWriteSidecar(fullPath + ".meta.txt", BuildMeta(reason, process, now));

      AppLogger?.Fatal($"CrashDump completed. path={fullPath}");
    }
    catch (Exception ex)
    {
      AppLogger?.Fatal($"CrashDump internal failure: {ex}");
      TryWriteSidecar(Path.Combine(DumpDirectory, "dump_writer_failed.txt"), ex.ToString());
    }
  }

  private static void thisAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
  {
    var ex = e.ExceptionObject as Exception;
    var reason = "UnhandledException";
    TryWriteDump(ex, reason);
    Environment.FailFast(reason, ex);
  }

  private static void thisUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
  {
    TryWriteDump(e.Exception, "UnobservedTaskException");

    e.SetObserved();
  }

  private static string BuildDumpFileName(Process process, DateTime utcNow, string reason)
  {
    var ts = utcNow.ToString("yyyyMMdd_HHmmss_fff");
    var pid = process.Id;
    var safeReason = SanitizeFileName(reason);
    return $"{process.ProcessName}_{pid}_{ts}_{safeReason}.dmp";
  }

  private static string SanitizeFileName(string value)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var sb = new StringBuilder(value.Length);

    foreach (var ch in value)
    {
      var ok = Array.IndexOf(invalid, ch) < 0;
      sb.Append(ok ? ch : '_');
    }

    return sb.ToString();
  }

  private static string BuildMeta(string reason, Process process, DateTime utcNow)
  {
    var sb = new StringBuilder();
    sb.AppendLine($"Reason: {reason}");
    sb.AppendLine($"UTC: {utcNow:O}");
    sb.AppendLine($"Process: {process.ProcessName}");
    sb.AppendLine($"PID: {process.Id}");
    sb.AppendLine($"MainModule: {TryGetMainModule(process)}");
    sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
    sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
    sb.AppendLine($"OSArch: {RuntimeInformation.OSArchitecture}");
    sb.AppendLine($"ProcArch: {RuntimeInformation.ProcessArchitecture}");
    sb.AppendLine($"CommandLine: {Environment.CommandLine}");
    return sb.ToString();
  }

  private static string TryGetMainModule(Process process)
  {
    try
    {
      return process.MainModule?.FileName ?? "";
    }
    catch
    {
      return "";
    }
  }

  private static void TryWriteSidecar(string path, string content)
  {
    try
    {
      File.WriteAllText(path, content);
    }
    catch
    {
      // 덤프 경로에서 추가 예외로 죽지 않도록 무시
    }
  }
}

