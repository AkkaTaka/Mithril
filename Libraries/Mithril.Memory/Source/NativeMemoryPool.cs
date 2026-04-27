namespace Mithril.Memory;

using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
/// Work-stealing native memory pool.
/// Each thread owns a Chase-Lev deque per size class.
/// Return (owner push): zero CAS when deque is not full.
/// Rent  (owner pop):   zero CAS when multiple items; one CAS only on last item.
/// Rent  (steal):       one CAS per victim deque tried.
/// Overflow: NativeMemory.Free — keeps per-thread pool bounded without a shared fallback.
/// Underflow: NativeMemory.Alloc.
/// </summary>
public sealed class NativeMemoryPool
{
#if MITHRIL_PROFILE
  public readonly record struct Snapshot(
    long RentCalls,
    long ReturnCalls,
    long PoolHits,
    long NativeAllocations,
    long TotalAllocatedBytes,
    long InUseBuffers,
    long InUseBytes,
    long PooledBuffers,
    long PooledBytes);
#endif

  public const int DefaultMaxBufferSize = 1 << 20; // 1MB

  private const int MinBucketLog = 4; // 2^4 = 16 bytes
  private const int MinBucketSize = 1 << MinBucketLog;

  private static readonly NativeMemoryPool Shared = new();
  public static NativeMemoryPool shared => Shared;

  private readonly ThreadLocal<WorkStealingDeque[]> tlsBuckets;
  private volatile WorkStealingDeque[][]? stealSnapshot;

#if MITHRIL_PROFILE
  private readonly ConcurrentDictionary<nint, AllocationRecord> liveAllocations = new();

  private long rentCalls;
  private long returnCalls;
  private long poolHits;
  private long nativeAllocations;
  private long totalAllocatedBytes;
  private long inUseBuffers;
  private long inUseBytes;
  private long pooledBuffers;
  private long pooledBytes;
#endif

  public NativeMemoryPool(int maxBufferSize = DefaultMaxBufferSize)
  {
    if (maxBufferSize < MinBucketSize)
    {
      throw new ArgumentOutOfRangeException(nameof(maxBufferSize));
    }

    this.MaxBufferSize = (int)BitOperations.RoundUpToPowerOf2((uint)maxBufferSize);
    int maxBucketLog = BitOperations.Log2((uint)this.MaxBufferSize);
    int bucketCount = maxBucketLog - MinBucketLog + 1;

    this.tlsBuckets = new ThreadLocal<WorkStealingDeque[]>(
      () =>
      {
        var deques = new WorkStealingDeque[bucketCount];
        for (int i = 0; i < bucketCount; i++)
        {
          deques[i] = new WorkStealingDeque();
        }
        return deques;
      },
      trackAllValues: true);
  }

  public int MaxBufferSize { get; }

  public unsafe NativeBuffer Rent(int minimumLength)
  {
    if (minimumLength <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(minimumLength));
    }

    int size = Math.Max((int)BitOperations.RoundUpToPowerOf2((uint)minimumLength), MinBucketSize);

    if (size > this.MaxBufferSize)
    {
      throw new ArgumentOutOfRangeException(nameof(minimumLength),
        $"Requested size {minimumLength} exceeds maxBufferSize {this.MaxBufferSize}.");
    }

    int bucketIndex = BitOperations.Log2((uint)size) - MinBucketLog;
    var myDeques = this.GetBuckets();

    // Own deque first — nearly CAS-free
    nint ptr = myDeques[bucketIndex].TryPop();

    if (ptr == 0)
    {
      ptr = this.TrySteal(myDeques, bucketIndex);
    }

    if (ptr == 0)
    {
      ptr = (nint)NativeMemory.Alloc((nuint)size);
#if MITHRIL_PROFILE
      Interlocked.Increment(ref this.nativeAllocations);
      Interlocked.Add(ref this.totalAllocatedBytes, size);
#endif
    }

#if MITHRIL_PROFILE
    else
    {
      Interlocked.Increment(ref this.poolHits);
      Interlocked.Decrement(ref this.pooledBuffers);
      Interlocked.Add(ref this.pooledBytes, -size);
    }
    Interlocked.Increment(ref this.rentCalls);
    Interlocked.Increment(ref this.inUseBuffers);
    Interlocked.Add(ref this.inUseBytes, size);

    this.RegisterAllocation(ptr);
#endif

    return new NativeBuffer(ptr, size);
  }

  public unsafe void Return(NativeBuffer buffer)
  {
    if (buffer.pointer == 0)
    {
      return;
    }

    int bucketIndex = BitOperations.Log2((uint)buffer.length) - MinBucketLog;
    var myDeques = this.GetBuckets();
    bool pushed = myDeques[bucketIndex].TryPush(buffer.pointer);

    if (pushed == false)
    {
      // Deque full — free directly to keep per-thread pool bounded
      NativeMemory.Free((void*)buffer.pointer);
    }

#if MITHRIL_PROFILE
    this.liveAllocations.TryRemove(buffer.pointer, out _);

    Interlocked.Increment(ref this.returnCalls);
    Interlocked.Decrement(ref this.inUseBuffers);
    Interlocked.Add(ref this.inUseBytes, -buffer.length);
    if (pushed)
    {
      Interlocked.Increment(ref this.pooledBuffers);
      Interlocked.Add(ref this.pooledBytes, buffer.length);
    }
#endif
  }

  private WorkStealingDeque[] GetBuckets()
  {
    // On first access by this thread, registration completes before we invalidate the
    // snapshot — ensuring the new deques appear in the next Values snapshot.
    if (this.tlsBuckets.IsValueCreated == false)
    {
      var deques = this.tlsBuckets.Value!;
      this.stealSnapshot = null;
      return deques;
    }

    return this.tlsBuckets.Value!;
  }

  private nint TrySteal(WorkStealingDeque[] myDeques, int bucketIndex)
  {
    var snapshot = this.stealSnapshot;
    if (snapshot == null)
    {
      snapshot = this.tlsBuckets.Values.ToArray();
      this.stealSnapshot = snapshot;
    }

    // Stagger starting position by processor to reduce thundering herd on same victim
    int start = Thread.GetCurrentProcessorId() % snapshot.Length;

    for (int i = 0; i < snapshot.Length; i++)
    {
      var target = snapshot[(start + i) % snapshot.Length];
      if (ReferenceEquals(target, myDeques))
      {
        continue;
      }

      nint ptr = target[bucketIndex].TrySteal();
      if (ptr != 0)
      {
        return ptr;
      }
    }

    return 0;
  }


#if MITHRIL_PROFILE
  public Snapshot TakeSnapshot()
  {
    return new Snapshot(
      RentCalls: Volatile.Read(ref this.rentCalls),
      ReturnCalls: Volatile.Read(ref this.returnCalls),
      PoolHits: Volatile.Read(ref this.poolHits),
      NativeAllocations: Volatile.Read(ref this.nativeAllocations),
      TotalAllocatedBytes: Volatile.Read(ref this.totalAllocatedBytes),
      InUseBuffers: Volatile.Read(ref this.inUseBuffers),
      InUseBytes: Volatile.Read(ref this.inUseBytes),
      PooledBuffers: Volatile.Read(ref this.pooledBuffers),
      PooledBytes: Volatile.Read(ref this.pooledBytes));
  }

  /// <summary>
  /// Returns a snapshot of all currently outstanding (unreturned) allocations.
  /// Key is the native pointer address. Intended for DEBUG leak detection only.
  /// </summary>
  public IReadOnlyDictionary<nint, AllocationRecord> GetLeakReport()
  {
    return this.liveAllocations;
  }

  public sealed class AllocationRecord
  {
    public string StackTrace { get; }
    public DateTime Time { get; }

    internal AllocationRecord(string stackTrace, DateTime time)
    {
      this.StackTrace = stackTrace;
      this.Time = time;
    }
  }

  private void RegisterAllocation(nint ptr)
  {
    var record = new AllocationRecord(
      new StackTrace(skipFrames: 1, fNeedFileInfo: true).ToString(),
      DateTime.UtcNow);

    this.liveAllocations[ptr] = record;
  }
#endif
}
