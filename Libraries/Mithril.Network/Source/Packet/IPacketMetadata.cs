namespace Mithril.Network.Packet;

using Google.Protobuf;

public interface IPacketMetadata
{
  public bool TryGetId<T>(out ushort id) where T : IMessage;
}
