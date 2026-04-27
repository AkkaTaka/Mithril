namespace Mithril.Network.Packet;

using Google.Protobuf;
using System.Buffers;
using System.Buffers.Binary;

public static class PacketSerializer
{
  // Size(2) + Id(2)
  public const int HeaderSize = 4;

  public static void Serialize(
    IBufferWriter<byte> writer,
    ushort id,
    IMessage protobufMessage,
    int bodySize)
  {
    var totalSize = (ushort)(HeaderSize + bodySize);
    var span = writer.GetSpan(totalSize);
    Serialize(span, id, protobufMessage, bodySize);
    writer.Advance(totalSize);
  }

  public static void Serialize(
    Span<byte> span,
    ushort id,
    IMessage protobufMessage,
    int bodySize)
  {
    var totalSize = (ushort)(HeaderSize + bodySize);
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(0, 2), totalSize);
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(2, 2), id);
    protobufMessage.WriteTo(span.Slice(HeaderSize, bodySize));
  }

  public static bool TryParseHeader(ReadOnlySequence<byte> buffer, out PacketHeader header)
  {
    if (buffer.Length < HeaderSize)
    {
      header = default;
      return false;
    }

    var reader = new SequenceReader<byte>(buffer);

    if (reader.TryReadLittleEndian(out short totalSize) == false ||
        reader.TryReadLittleEndian(out short id) == false)
    {
      header = default;
      return false;
    }

    header = new PacketHeader(
      unchecked((ushort)totalSize),
      unchecked((ushort)id));

    return true;
  }
}