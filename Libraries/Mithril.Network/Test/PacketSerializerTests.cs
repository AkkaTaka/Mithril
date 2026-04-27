namespace Mithril.Network.Tests;

using Google.Protobuf.WellKnownTypes;
using Mithril.Network.Packet;
using System.Buffers;
using System.Buffers.Binary;

public sealed class PacketSerializerTests
{
  [Fact]
  public void Serialize_WritesHeaderAndBody()
  {
    var message = new StringValue { Value = "mithril" };
    var bodySize = message.CalculateSize();
    var writer = new ArrayBufferWriter<byte>();
    const ushort packetId = 77;

    PacketSerializer.Serialize(writer, packetId, message, bodySize);

    var payload = writer.WrittenSpan;
    var totalSize = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(0, 2));
    var id = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(2, 2));

    Assert.Equal(PacketSerializer.HeaderSize + bodySize, totalSize);
    Assert.Equal(packetId, id);

    var parsed = StringValue.Parser.ParseFrom(payload.Slice(PacketSerializer.HeaderSize, bodySize));
    Assert.Equal("mithril", parsed.Value);
  }

  [Fact]
  public void TryParseHeader_WithValidBuffer_ReturnsTrueAndHeader()
  {
    var bytes = new byte[PacketSerializer.HeaderSize];
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), 16);
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), 9);
    var sequence = new ReadOnlySequence<byte>(bytes);

    var ok = PacketSerializer.TryParseHeader(sequence, out var header);

    Assert.True(ok);
    Assert.Equal((ushort)16, header.size);
    Assert.Equal((ushort)9, header.id);
  }

  [Fact]
  public void TryParseHeader_WithTooShortBuffer_ReturnsFalse()
  {
    var bytes = new byte[PacketSerializer.HeaderSize - 1];
    var sequence = new ReadOnlySequence<byte>(bytes);

    var ok = PacketSerializer.TryParseHeader(sequence, out var header);

    Assert.False(ok);
    Assert.Equal(default, header);
  }

  [Fact]
  public void RoundTrip_SerializeAndParseHeader_ReturnsMatchingValues()
  {
    var message = new StringValue { Value = "round-trip" };
    var bodySize = message.CalculateSize();
    const ushort packetId = 123;
    var writer = new ArrayBufferWriter<byte>();

    PacketSerializer.Serialize(writer, packetId, message, bodySize);
    var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
    var ok = PacketSerializer.TryParseHeader(sequence, out var header);

    Assert.True(ok);
    Assert.Equal(packetId, header.id);
    Assert.Equal((ushort)(PacketSerializer.HeaderSize + bodySize), header.size);
  }

  [Fact]
  public void TryParseHeader_WithExactlyHeaderSizeBuffer_ReturnsTrue()
  {
    // 경계값: 정확히 HeaderSize 바이트인 버퍼 → 헤더 파싱 성공
    var bytes = new byte[PacketSerializer.HeaderSize];
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), PacketSerializer.HeaderSize);
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), 1);
    var sequence = new ReadOnlySequence<byte>(bytes);

    var ok = PacketSerializer.TryParseHeader(sequence, out var header);

    Assert.True(ok);
    Assert.Equal((ushort)PacketSerializer.HeaderSize, header.size);
    Assert.Equal((ushort)1, header.id);
  }

  [Fact]
  public void Serialize_ZeroBodyMessage_WritesOnlyHeader()
  {
    // Empty 메시지는 body 0 바이트 → 전체 패킷 크기 == HeaderSize
    var message = new Empty();
    var bodySize = message.CalculateSize();  // 0
    var writer = new ArrayBufferWriter<byte>();
    const ushort packetId = 5;

    PacketSerializer.Serialize(writer, packetId, message, bodySize);

    Assert.Equal(PacketSerializer.HeaderSize, writer.WrittenCount);
    var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
    PacketSerializer.TryParseHeader(sequence, out var header);
    Assert.Equal((ushort)PacketSerializer.HeaderSize, header.size);
    Assert.Equal(packetId, header.id);
  }

  [Fact]
  public void Serialize_MaxPacketId_PreservesUshortMaxValue()
  {
    var message = new StringValue { Value = "x" };
    var bodySize = message.CalculateSize();
    var writer = new ArrayBufferWriter<byte>();
    const ushort maxId = ushort.MaxValue;

    PacketSerializer.Serialize(writer, maxId, message, bodySize);
    var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
    PacketSerializer.TryParseHeader(sequence, out var header);

    Assert.Equal(maxId, header.id);
  }
}
