namespace Mithril.Network.Packet;

using Google.Protobuf;

public sealed class PacketMessage
{
  public PacketMessage(ushort id, IMessage protobufMessage)
  {
    this.Id = id;
    this.ProtobufMessage = protobufMessage;
  }

  public ushort Id { get; }
  public IMessage ProtobufMessage { get; }
}
