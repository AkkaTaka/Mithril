namespace Mithril.AppServices.Echo;

using Mithril.Logger;
using Mithril.Network;
using Mithril.Network.Packet;
using Mithril.Protocol;
using System.Buffers;

internal sealed class EchoServerPacketDispatcher : IPacketDispatcher
{
  delegate void Handler(Session session, ushort id, ReadOnlySequence<byte> sequence);

  private static readonly Handler?[] Handlers;
  private readonly IAppLogger appLogger;

  static EchoServerPacketDispatcher()
  {
    Handlers = new Handler[ushort.MaxValue + 1];
    Handlers[EchoReq.Id] = HandleEchoReq;
  }

  public EchoServerPacketDispatcher(IAppLogger appLogger)
  {
    this.appLogger = appLogger;
  }

  private static void HandleEchoReq(Session session, ushort id, ReadOnlySequence<byte> sequence)
  {
    var echoReq = EchoReq.Parser.ParseFrom(sequence);

    session.Send(new EchoRes { Message = echoReq.Message });
  }

  private static void OnReceivedInvalidMessage(Session session, ushort id, ReadOnlySequence<byte> sequence)
  {
    session.Close();
  }

  public void OnReceived(Session session, ushort id, ReadOnlySequence<byte> sequence)
  {
    this.appLogger.Info($"OnReceived - id:{id} Length:{sequence.Length}");

    var handler = Handlers[id] ?? OnReceivedInvalidMessage;

    handler(session, id, sequence);
  }
}
