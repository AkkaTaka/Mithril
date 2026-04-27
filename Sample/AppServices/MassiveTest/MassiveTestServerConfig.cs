namespace Mithril.AppServices.MassiveTest;

using Mithril.Hosting;
using Mithril.Logger;
using Mithril.Network.Config;

public sealed record MassiveTestServerConfig : ServiceConfig
{
  public MassiveTestServerConfig(
    LoggerOptions logger,
    NetworkConfig network,
    ListenerConfig listener,
    int statsIntervalSeconds)
    : base(logger)
  {
    this.Network = network;
    this.Listener = listener;
    this.StatsIntervalSeconds = statsIntervalSeconds;
  }

  public NetworkConfig Network { get; }
  public ListenerConfig Listener { get; }

  /// <summary>통계 출력 주기 (초)</summary>
  public int StatsIntervalSeconds { get; }
}
