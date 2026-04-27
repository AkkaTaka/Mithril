namespace Mithril.Network.Packet;

using System.Buffers;

public interface IPacketDispatcher
{
  public void OnReceived(Session session, ushort id, ReadOnlySequence<byte> sequence);
}
