namespace Mithril.Apps.MassiveTestClient;

using Mithril.AppServices.MassiveTest;
using Mithril.Network;
using Mithril.Network.Packet;
using Mithril.Protocol;
using System.Buffers;
using System.Diagnostics;

internal sealed class ClientDispatcher : IPacketDispatcher
{
  private readonly Statistics statistics;
  private readonly SendMode mode;
  private long sequence;

  public ClientDispatcher(Statistics statistics, SendMode mode)
  {
    this.statistics = statistics;
    this.mode = mode;
  }

  public void OnReceived(Session session, ushort id, ReadOnlySequence<byte> sequence)
  {
    if (id != MassTestRes.Id)
    {
      session.Close();
      return;
    }

    var res = MassTestRes.Parser.ParseFrom(sequence);
    var clientRecvTicks = Stopwatch.GetTimestamp();
    var totalRttTicks = clientRecvTicks - res.ClientSendTicks;

#if MITHRIL_PROFILE
    var receiveProfile = session.TakeReceiveProfileSnapshot();
    var clientToServerTicks = res.ServerRecvTicks - res.ClientSendTicks;
    var serverProcessingTicks = res.ServerSendTicks - res.ServerRecvTicks;
    var serverToClientTicks = clientRecvTicks - res.ServerSendTicks;
    var serverToSocketReceiveTicks = receiveProfile.SocketReceiveCompletedTicks - res.ServerSendTicks;
    var socketReceiveToPipeFlushTicks = receiveProfile.ReceivePipeFlushCompletedTicks - receiveProfile.SocketReceiveCompletedTicks;
    var serverToClientPipeTicks = receiveProfile.ReceivePipeReadTicks - res.ServerSendTicks;
    var clientPipeToHandlerTicks = clientRecvTicks - receiveProfile.ReceivePipeReadTicks;

    this.statistics.RecordReceived(
      PacketSerializer.HeaderSize + sequence.Length,
      totalRttTicks,
      clientToServerTicks,
      serverProcessingTicks,
      serverToClientTicks,
      serverToSocketReceiveTicks,
      socketReceiveToPipeFlushTicks,
      serverToClientPipeTicks,
      clientPipeToHandlerTicks);
#else
    this.statistics.RecordReceived(PacketSerializer.HeaderSize + sequence.Length, totalRttTicks);
#endif

    // Flood 모드: 응답을 받는 즉시 다음 요청 전송
    if (this.mode == SendMode.Flood)
    {
      this.SendNext(session);
    }
  }

  public void SendNext(Session session)
  {
    if (session.IsConnected == false)
    {
      return;
    }

    var seq = Interlocked.Increment(ref this.sequence);
    var req = new MassTestReq { Sequence = seq, SendTicks = Stopwatch.GetTimestamp() };
    var packetSize = PacketSerializer.HeaderSize + req.CalculateSize();

    session.Send(req);
    this.statistics.RecordSent(packetSize);
  }
}
