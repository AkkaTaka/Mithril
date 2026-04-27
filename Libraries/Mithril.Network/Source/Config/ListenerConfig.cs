namespace Mithril.Network.Config;

using System.Net;
using System.Text.Json.Serialization;

public sealed class ListenerConfig
{
  public static readonly ListenerConfig Default;

  static ListenerConfig()
  {
    Default = new ListenerConfig(22222, 100, PipelineConfig.Default);
  }

  [JsonConstructor]
  public ListenerConfig(
    int port,
    int backlog,
    PipelineConfig pipeline)
  {
    if (port is < 1 or > 65535)
    {
      throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
    }

    if (backlog <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(backlog), "Backlog must be > 0.");
    }

    this.Port = port;
    this.EndPoint = new IPEndPoint(IPAddress.Any, port);
    this.Backlog = backlog;
    this.Pipeline = pipeline;
  }

  [JsonIgnore]
  public IPEndPoint EndPoint { get; }
  public int Backlog { get; }
  public int Port { get; }
  public PipelineConfig Pipeline { get; }

  public override string ToString()
  {
    return $"0.0.0.0:{this.Port}/{this.Backlog}";
  }
}
