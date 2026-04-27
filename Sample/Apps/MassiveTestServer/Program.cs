namespace Mithril.Apps.MassiveTestServer;

using Mithril.Apps.MassiveTestServer.Service;
using Mithril.AppServices.MassiveTest;
using Mithril.Hosting;

internal sealed class Program
{
  static async Task Main(string[] args)
  {
    await Host<MassiveTestServerServiceBuilder, MassiveTestServerConfig>.Run(args);
  }
}
