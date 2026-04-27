namespace Mithril.Memory;

using System.Buffers;

public sealed unsafe class ReusableMemoryManager : MemoryManager<byte>
{
  private byte* pointer;
  private int length;

  public void Reset(NativeBuffer buffer, int offset, int length)
  {
    this.pointer = (byte*)buffer.pointer + offset;
    this.length = length;
  }

  public override Span<byte> GetSpan() => new Span<byte>(this.pointer, this.length);

  public override MemoryHandle Pin(int elementIndex = 0) => new MemoryHandle(this.pointer + elementIndex);

  public override void Unpin() { }

  protected override void Dispose(bool disposing) { }
}