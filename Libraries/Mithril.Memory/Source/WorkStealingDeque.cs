namespace Mithril.Memory;

internal sealed class WorkStealingDeque
{
  private const int DequeCapacity = 64;
  private const int Mask = DequeCapacity - 1;

  private readonly nint[] items = new nint[DequeCapacity];
  private int head; // stealers advance via CAS
  private int tail; // owner advances without CAS

  public bool TryPush(nint ptr)
  {
    int t = this.tail;
    if (t - Volatile.Read(ref this.head) >= DequeCapacity)
    {
      return false;
    }

    this.items[t & Mask] = ptr;
    Volatile.Write(ref this.tail, t + 1);
    return true;
  }

  // Owner thread only
  public nint TryPop()
  {
    int t = this.tail - 1;
    Volatile.Write(ref this.tail, t);
    Thread.MemoryBarrier();
    int h = Volatile.Read(ref this.head);

    if (h < t)
    {
      return this.items[t & Mask];
    }

    Volatile.Write(ref this.tail, t + 1);

    if (h == t)
    {
      nint val = this.items[t & Mask];
      if (Interlocked.CompareExchange(ref this.head, h + 1, h) == h)
      {
        return val;
      }
    }

    return 0;
  }

  public nint TrySteal()
  {
    int h = Volatile.Read(ref this.head);
    // Barrier ensures we read tail after head, preventing a stale-empty false negative
    Thread.MemoryBarrier();
    int t = Volatile.Read(ref this.tail);

    if (h >= t)
    {
      return 0;
    }

    nint val = this.items[h & Mask];

    if (Interlocked.CompareExchange(ref this.head, h + 1, h) != h)
    {
      return 0;
    }

    return val;
  }
}