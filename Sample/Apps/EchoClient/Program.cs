namespace Mithril.Apps.EchoClient;

using Mithril.Logger;
using Mithril.Network;
using Mithril.Network.Config;
using Mithril.Protocol;
using System.Net;

internal sealed class Program
{
  static async Task Main(string[] args)
  {
    var appLogger = CreateLogger();
    var packetMetadata = new PacketMetadata();
    var networkFramework = new NetworkFramework(
      appLogger, 
      new NetworkConfig 
      {
        NoDelay = true,
        MaxPacketSize = 1024 * 64,
      },
      packetMetadata);

    var connector = networkFramework.CreateConnector();

    var ip = IPAddress.Parse("127.0.0.1");
    var port = 22222;
    var endPoint = new IPEndPoint(ip, port);
    var session = await connector.ConnectAsync(endPoint, CancellationToken.None);
    if (session == null)
    {
      appLogger.Error($"Failed to connect. {ip}:{port}");
      return;
    }

    var dispatcher = new ClientDispatcher(appLogger);
    session.Start(dispatcher);

    while (true)
    {
      appLogger.Info("input : ");
      var input = Console.ReadLine();

      if (string.IsNullOrEmpty(input) == false)
      {
        if (input.ToLower() == "exit")
        {
          break;
        }
      }

      session.Send(new EchoReq { Message = input });
    }
  }

  private static IAppLogger CreateLogger()
  {
    return new LoggerBuilder(
      Serilog.Events.LogEventLevel.Debug,
      1024)
      .AddConsoleWriter()
      .Build();
  }
}
