namespace Mithril.Apps.EchoServer.Service;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mithril.Hosting;

public sealed class EchoServerServiceBuilder : IServiceBuilder
{
  public void Build(HostBuilderContext context, IServiceCollection service)
  {
    // TODO : DI, Add Service
    service.AddHostedService<EchoServerService>();
  }
}
