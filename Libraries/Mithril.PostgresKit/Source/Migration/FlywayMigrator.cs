namespace Mithril.PostgresKit.Migration;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static class FlywayMigrator
{
  public static async Task MigrateAsync(
    string schemaName,
    FlywayMigrateOptions options,
    CancellationToken ct = default)
  {
    // Flyway migration is a development-only tool.
    // We intentionally restrict execution to Windows to avoid
    // maintaining cross-platform process invocation logic.
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
    {
      throw new PlatformNotSupportedException("Only Windows OS is supported.");
    }

    var scriptDir = Path.Combine(AppContext.BaseDirectory, options.ScriptDirectory);
    var flywayCmd = options.FlywayPath;

    if (Directory.Exists(scriptDir) == false)
    {
      throw new DirectoryNotFoundException($"Script directory not found: {scriptDir}");
    }

    var confPath = Path.Combine(scriptDir, "flyway.conf"); // 실제 conf 위치로 맞춰라

    var arguments =
      $"/c \"\"{flywayCmd}\" -configFiles=\"{confPath}\" -schemas=\"{schemaName}\" -placeholders.schema=\"{schemaName}\" migrate\"";

    var startInfo = new ProcessStartInfo
    {
      FileName = "cmd.exe",
      Arguments = arguments,
      WorkingDirectory = scriptDir,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(startInfo);
    if (process is null)
    {
      throw new InvalidOperationException("Failed to start flyway process.");
    }

    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
      throw new InvalidOperationException(
        $"Flyway failed. ExitCode={process.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    Console.WriteLine(stdout);
    if (string.IsNullOrWhiteSpace(stderr) == false)
    {
      Console.WriteLine(stderr);
    }
  }
}