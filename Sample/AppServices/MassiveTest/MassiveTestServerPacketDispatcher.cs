namespace Mithril.AppServices.MassiveTest;

using Mithril.Network;
using Mithril.Network.Packet;
using Mithril.Protocol;
using System.Buffers;
using System.Diagnostics;

internal sealed class MassiveTestServerPacketDispatcher : IPacketDispatcher
{
  private readonly ServerStatistics statistics;

  public MassiveTestServerPacketDispatcher(ServerStatistics statistics)
  {
    this.statistics = statistics;
  }

  public void OnReceived(Session session, ushort id, ReadOnlySequence<byte> sequence)
  {
    // 헤더 크기(4)를 포함한 총 수신 바이트 추적
    this.statistics.OnMessageReceived(PacketSerializer.HeaderSize + sequence.Length);

    if (id == MassTestReq.Id)
    {
      this.HandleMassTestReq(session, sequence);
    }
    else
    {
      session.Close();
    }
  }

  private void HandleMassTestReq(Session session, ReadOnlySequence<byte> sequence)
  {
    var serverRecvTicks = Stopwatch.GetTimestamp();
    var req = MassTestReq.Parser.ParseFrom(sequence);
    var serverSendTicks = Stopwatch.GetTimestamp();
    var res = new MassTestRes
    {
      Sequence = req.Sequence,
      ClientSendTicks = req.SendTicks,
      ServerRecvTicks = serverRecvTicks,
      ServerSendTicks = serverSendTicks,
    };

    var responseBodySize = res.CalculateSize();
    session.Send(res);

    this.statistics.OnMessageSent(PacketSerializer.HeaderSize + responseBodySize);
  }
}
