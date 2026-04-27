namespace Mithril.Apps.EchoServer.Service;

using Microsoft.Extensions.Hosting;
using Mithril.AppServices.Echo;
using Mithril.Logger;
using System.Threading;
using System.Threading.Tasks;
using EchoServer = Mithril.AppServices.Echo.Server.EchoServer;

internal sealed class EchoServerService : IHostedService
{
  private readonly IAppLogger appLogger;
  private readonly EchoServer server;

  public EchoServerService(IAppLogger appLogger, EchoServerConfig config)
  {
    this.appLogger = appLogger;
    this.server = new EchoServer(appLogger, config);
  }

  public Task StartAsync(CancellationToken ct)
  {
    this.server.Start(ct);

    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken ct)
  {
    return this.server.StopAsync();
  }
}
