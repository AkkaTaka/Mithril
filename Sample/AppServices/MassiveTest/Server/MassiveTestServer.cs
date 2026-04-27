namespace Mithril.AppServices.MassiveTest.Server;

using Mithril.Logger;
using Mithril.Network;
using Mithril.Protocol;

public sealed class MassiveTestServer : ServerBase
{
  private readonly ServerStatistics statistics;

  public MassiveTestServer(
    IAppLogger appLogger,
    MassiveTestServerConfig config)
    : base(
      appLogger,
      nameof(MassiveTestServer),
      new NetworkFramework(appLogger, config.Network, new PacketMetadata()),
      config.Listener)
  {
    this.statistics = new ServerStatistics();
  }

  public ServerStatistics Statistics => this.statistics;

  public override void OnAcceptedInternal(Session session)
  {
    this.statistics.OnAccepted();
    session.Start(new MassiveTestServerPacketDispatcher(this.statistics));
  }

  public override void OnClosedInternal(Session session)
  {
    this.statistics.OnClosed();
  }
}
