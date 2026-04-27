namespace Mithril.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mithril.Logger;

public sealed class HostTests : IDisposable
{
  private readonly string tempDirectory;

  public HostTests()
  {
    this.tempDirectory = Path.Combine(Path.GetTempPath(), "Mithril.Hosting.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(this.tempDirectory);
    TestServiceBuilder.Reset();
    TestHostedService.Reset();
  }

  public void Dispose()
  {
    TestServiceBuilder.Reset();
    TestHostedService.Reset();

    if (Directory.Exists(this.tempDirectory))
    {
      Directory.Delete(this.tempDirectory, recursive: true);
    }
  }

  [Fact]
  public async Task Run_WhenConfigFileDoesNotExist_ReturnsMinusOne()
  {
    var path = Path.Combine(this.tempDirectory, "missing.json");

    var exitCode = await Host<TestServiceBuilder, TestServiceConfig>.Run(["--configPath", path]);

    Assert.Equal(-1, exitCode);
  }

  [Fact]
  public async Task Run_WhenConfigFileIsInvalidJson_ReturnsMinusOne()
  {
    var path = this.WriteConfig("invalid.json", "{\"logger\":");

    var exitCode = await Host<TestServiceBuilder, TestServiceConfig>.Run(["--configPath", path]);

    Assert.Equal(-1, exitCode);
  }

  [Fact]
  public async Task Run_WhenConfigFileIsNull_ReturnsMinusOne()
  {
    var path = this.WriteConfig("null.json", "null");

    var exitCode = await Host<TestServiceBuilder, TestServiceConfig>.Run(["--configPath", path]);

    Assert.Equal(-1, exitCode);
  }

  [Fact]
  public async Task Run_WhenConfigIsValid_StartsAndStopsHostedService()
  {
    var json =
      """
      {
        "logger": {
          "filePath": "./host-tests.log",
          "minimumLevel": "information",
          "rollingInterval": "day",
          "maxBufferSize": 64
        },
        "name": "unit-test"
      }
      """;

    var path = this.WriteConfig("valid.json", json);

    var exitCode = await Host<TestServiceBuilder, TestServiceConfig>.Run(["--configPath", path]);

    Assert.Equal(0, exitCode);
    Assert.Equal(1, TestServiceBuilder.BuildCallCount);
    Assert.True(TestHostedService.StartCalled);
    Assert.True(TestHostedService.StopCalled);
    Assert.Equal("unit-test", TestHostedService.ObservedName);
  }

  private string WriteConfig(string fileName, string json)
  {
    var path = Path.Combine(this.tempDirectory, fileName);
    File.WriteAllText(path, json);
    return path;
  }

  private sealed class TestServiceBuilder : IServiceBuilder
  {
    public static int BuildCallCount;

    public void Build(HostBuilderContext context, IServiceCollection service)
    {
      Interlocked.Increment(ref BuildCallCount);
      service.AddHostedService<TestHostedService>();
    }

    public static void Reset()
    {
      BuildCallCount = 0;
    }
  }

  private sealed class TestHostedService : IHostedService
  {
    private readonly IHostApplicationLifetime lifetime;
    private readonly TestServiceConfig config;

    public TestHostedService(IHostApplicationLifetime lifetime, TestServiceConfig config)
    {
      this.lifetime = lifetime;
      this.config = config;
    }

    public static bool StartCalled { get; private set; }
    public static bool StopCalled { get; private set; }
    public static string? ObservedName { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      StartCalled = true;
      ObservedName = this.config.Name;
      this.lifetime.StopApplication();
      return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      StopCalled = true;
      return Task.CompletedTask;
    }

    public static void Reset()
    {
      StartCalled = false;
      StopCalled = false;
      ObservedName = null;
    }
  }

  private sealed record TestServiceConfig(LoggerOptions Logger, string Name) : ServiceConfig(Logger);
}
