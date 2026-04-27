namespace Mithril.Network;

using Mithril.Logger;
using Mithril.Network.Config;
using Mithril.Network.Packet;

public sealed class NetworkFramework
{
  private readonly IAppLogger appLogger;
  private readonly NetworkConfig networkConfig;
  private readonly IPacketMetadata packetMetadata;
    
  public NetworkConfig Config => this.networkConfig;

  public NetworkFramework(
    IAppLogger appLogger,
    NetworkConfig networkConfig,
    IPacketMetadata packetMetadata)
  {
    this.appLogger = appLogger;
    this.networkConfig = networkConfig;
    this.packetMetadata = packetMetadata;
  }

  public Listener CreateListener(
    string name,
    ListenerConfig config,
    IListenerEventHandler eventHandler)
  {
    var listener = new Listener(
      this.appLogger, 
      name,
      this.networkConfig,
      config,
      eventHandler,
      this.packetMetadata);

    return listener;
  }

  public Connector CreateConnector()
  {
    return new Connector(
      this.appLogger,
      this.networkConfig,
      this.packetMetadata);
  }
}