namespace Mithril.Network.Packet;

public readonly struct PacketHeader
{
  public readonly ushort size;
  public readonly ushort id;

  public PacketHeader(ushort size, ushort id)
  {
    this.size = size;
    this.id = id;
  }
}
