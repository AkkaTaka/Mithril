namespace Mithril.AppServices.Echo;

using Mithril.Hosting;
using Mithril.Logger;
using Mithril.Network.Config;

public sealed record EchoServerConfig : ServiceConfig
{
  public EchoServerConfig(
    LoggerOptions logger,
    NetworkConfig network,
    ListenerConfig listener)
    : base(logger)
  {
    this.Network = network;
    this.Listener = listener;
  }

  public NetworkConfig Network { get; }
  public ListenerConfig Listener { get; }
}
