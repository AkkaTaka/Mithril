namespace Mithril.Apps.EchoClient;

using Mithril.Logger;
using Mithril.Network;
using Mithril.Network.Packet;
using Mithril.Protocol;
using System.Buffers;

public sealed class ClientDispatcher : IPacketDispatcher
{
  private readonly IAppLogger appLogger;

  public ClientDispatcher(IAppLogger appLogger)
  {
    this.appLogger = appLogger;
  }

  public void OnReceived(Session session, ushort id, ReadOnlySequence<byte> sequence)
  {
    if (EchoRes.Id == id)
    {
      var echoRes = EchoRes.Parser.ParseFrom(sequence);

      this.appLogger.Info($"OnReceived. message:{echoRes.Message}");
    }
    else
    {
      this.appLogger.Warning($"Invalid message received. id:{id}");
    }
  }
}