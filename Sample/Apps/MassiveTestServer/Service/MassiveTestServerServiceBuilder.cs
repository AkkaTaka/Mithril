namespace Mithril.Apps.MassiveTestServer.Service;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mithril.Hosting;

public sealed class MassiveTestServerServiceBuilder : IServiceBuilder
{
  public void Build(HostBuilderContext context, IServiceCollection service)
  {
    service.AddHostedService<MassiveTestServerService>();
  }
}
