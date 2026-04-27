namespace Mithril.Apps.EchoServer;

using Mithril.Apps.EchoServer.Service;
using Mithril.AppServices.Echo;
using Mithril.Hosting;

internal sealed class Program
{
  static async Task Main(string[] args)
  {
    await Host<EchoServerServiceBuilder, EchoServerConfig>.Run(args);
  }
}
