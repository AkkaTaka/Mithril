namespace Mithril.Memory;

public readonly struct NativeBuffer
{
  internal readonly nint pointer;
  public readonly int length;

  internal NativeBuffer(nint pointer, int length)
  {
    this.pointer = pointer;
    this.length = length;
  }

  public unsafe Span<byte> AsSpan()
  {
    return new Span<byte>((void*)this.pointer, this.length);
  }

  public unsafe Span<byte> AsSpan(int length)
  {
    return new Span<byte>((void*)this.pointer, length);
  }
}
