namespace Mithril.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mithril.Logger;
using Serilog;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoggerBuilder = Logger.LoggerBuilder;

public static class Host<TServiceConfigurator, TServiceConfig>
  where TServiceConfigurator : class, IServiceBuilder, new()
  where TServiceConfig : ServiceConfig
{
  public static async Task<int> Run(string[] args)
  {
    var configPathOption = new Option<string>(
        name: "--configPath",
        getDefaultValue: () => $"./{AppDomain.CurrentDomain.FriendlyName}.json");

    var root = new RootCommand("Starts the specified hosted service");
    root.AddOption(configPathOption);

    root.SetHandler(
      async context =>
      {
        var configPath = context.ParseResult.GetValueForOption(configPathOption);

        if (string.IsNullOrWhiteSpace(configPath) || File.Exists(configPath) == false)
        {
          Console.WriteLine($"Cannot find config file. path:{configPath}");
          context.ExitCode = -1;
          return;
        }

        TServiceConfig? config = null!;

        try
        {
          config = JsonSerializer.Deserialize<TServiceConfig>(
            File.ReadAllText(configPath),
            new JsonSerializerOptions
            {
              PropertyNameCaseInsensitive = true,
              Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
            });
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.ToString());
          context.ExitCode = -1;
          return;
        }

        if (config == null)
        {
          Console.WriteLine("config is null");
          context.ExitCode = -1;
          return;
        }

        var appLogger = CreateLogger(config);

        var host = Host.CreateDefaultBuilder(args)
          .UseSerilog(appLogger.InternalLogger)
          .ConfigureServices(
            (context, services) =>
            {
              services.AddSingleton(appLogger);
              services.AddSingleton(config);

              var configurator = new TServiceConfigurator();

              configurator.Build(context, services);
            })
          .Build();

        try
        {
          var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

          lifetime.ApplicationStopping.Register(() =>
          {
            appLogger.Info("Application is stopping...");
          });

          lifetime.ApplicationStopped.Register(() =>
          {
            try
            {
              Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
              appLogger.Fatal(ex);
            }
          });

          await host.RunAsync();
        }
        catch (Exception ex)
        {
          appLogger.Fatal(ex, "Exception occurred.");
        }
      });

    return await root.InvokeAsync(args);
  }

  private static IAppLogger CreateLogger(ServiceConfig config)
  {
    var loggerConfig = config.Logger;

    return new LoggerBuilder(
      loggerConfig.MinimumLevel,
      loggerConfig.MaxBufferSize)
      .AddConsoleWriter()
      .AddFileWriter(
      loggerConfig.FilePath,
      loggerConfig.RollingInterval)
      .Build();
  }
}
