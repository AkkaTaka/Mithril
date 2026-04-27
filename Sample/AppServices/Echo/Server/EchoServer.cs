namespace Mithril.AppServices.Echo.Server;

using Mithril.AppServices.Echo;
using Mithril.Logger;
using Mithril.Network;
using Mithril.Network.Config;
using Mithril.Network.Packet;
using Mithril.Protocol;

public sealed class EchoServer : ServerBase
{
  private readonly IAppLogger appLogger;

  public EchoServer(
    IAppLogger appLogger,
    EchoServerConfig config)
    : base(
      appLogger,
      nameof(EchoServer),
      new NetworkFramework(appLogger, config.Network, new PacketMetadata()),
      config.Listener)
  {
    this.appLogger = appLogger;
  }

  public override void OnAcceptedInternal(Session session)
  {
    session.Start(new EchoServerPacketDispatcher(this.appLogger));
  }

  public override void OnClosedInternal(Session session)
  {
  }
}
