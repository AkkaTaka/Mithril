namespace Mithril.Memory.Tests;

using System.Runtime.InteropServices;

public sealed class NativeMemoryPoolTests
{
  [Fact]
  public void Rent_ReturnsValidBuffer()
  {
    var pool = new NativeMemoryPool();
    var buffer = pool.Rent(100);

    Assert.NotEqual(nint.Zero, buffer.pointer);
    Assert.Equal(128, buffer.length); // rounded up to 2^7

    pool.Return(buffer);
  }

  [Fact]
  public void Rent_RoundsUpToPowerOfTwo()
  {
    var pool = new NativeMemoryPool();

    var b1 = pool.Rent(1);
    var b2 = pool.Rent(17);
    var b3 = pool.Rent(256);
    var b4 = pool.Rent(1000);

    Assert.Equal(16, b1.length);    // min bucket
    Assert.Equal(32, b2.length);    // 2^5
    Assert.Equal(256, b3.length);   // exact
    Assert.Equal(1024, b4.length);  // 2^10

    pool.Return(b4);
    pool.Return(b3);
    pool.Return(b2);
    pool.Return(b1);
  }

  [Fact]
  public void Rent_AfterReturn_ReusesBuffer()
  {
    var pool = new NativeMemoryPool();

    var buffer1 = pool.Rent(64);
    var ptr1 = buffer1.pointer;
    pool.Return(buffer1);

    var buffer2 = pool.Rent(64);
    var ptr2 = buffer2.pointer;
    pool.Return(buffer2);

    Assert.Equal(ptr1, ptr2);
  }

  [Fact]
  public void Rent_DifferentSizes_UseDifferentBuckets()
  {
    var pool = new NativeMemoryPool();

    var small = pool.Rent(16);
    var large = pool.Rent(256);

    Assert.NotEqual(small.pointer, large.pointer);
    Assert.Equal(16, small.length);
    Assert.Equal(256, large.length);

    pool.Return(large);
    pool.Return(small);
  }

  [Fact]
  public void AsSpan_CanReadWrite()
  {
    var pool = new NativeMemoryPool();
    var buffer = pool.Rent(32);

    var span = buffer.AsSpan();
    span[0] = 0xAA;
    span[31] = 0xBB;

    Assert.Equal(0xAA, span[0]);
    Assert.Equal(0xBB, span[31]);

    pool.Return(buffer);
  }

  [Fact]
  public void AsSpan_WithLength_ReturnsPartialSpan()
  {
    var pool = new NativeMemoryPool();
    var buffer = pool.Rent(64);

    var span = buffer.AsSpan(10);
    Assert.Equal(10, span.Length);

    pool.Return(buffer);
  }

  [Fact]
  public void Rent_OverMaxBufferSize_Throws()
  {
    var pool = new NativeMemoryPool(maxBufferSize: 1024);

    Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(2048));
  }

  [Fact]
  public void Rent_InvalidLength_Throws()
  {
    var pool = new NativeMemoryPool();

    Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(0));
    Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(-1));
  }

  [Fact]
  public void Constructor_InvalidMaxBufferSize_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new NativeMemoryPool(maxBufferSize: 8));
  }

  [Fact]
  public void MultipleRentReturn_NoLeak()
  {
    var pool = new NativeMemoryPool();
    var buffers = new NativeBuffer[100];

    for (int i = 0; i < buffers.Length; i++)
    {
      buffers[i] = pool.Rent(128);
    }

    for (int i = 0; i < buffers.Length; i++)
    {
      pool.Return(buffers[i]);
    }

    // all should be pooled and reusable
    for (int i = 0; i < buffers.Length; i++)
    {
      buffers[i] = pool.Rent(128);
      Assert.NotEqual(nint.Zero, buffers[i].pointer);
    }

    for (int i = 0; i < buffers.Length; i++)
    {
      pool.Return(buffers[i]);
    }
  }

  [Fact]
  public void ConcurrentRentReturn_IsThreadSafe()
  {
    var pool = new NativeMemoryPool();
    const int threadCount = 8;
    const int opsPerThread = 1000;
    var barrier = new Barrier(threadCount);
    var exceptions = new List<Exception>();

    var threads = Enumerable.Range(0, threadCount).Select(_ => new Thread(() =>
    {
      try
      {
        barrier.SignalAndWait();
        for (int i = 0; i < opsPerThread; i++)
        {
          var buf = pool.Rent(64);
          buf.AsSpan().Fill(0xFF);
          pool.Return(buf);
        }
      }
      catch (Exception ex)
      {
        lock (exceptions) exceptions.Add(ex);
      }
    })).ToArray();

    foreach (var t in threads)
    {
      t.Start();
    }

    foreach (var t in threads)
    {
      t.Join();
    }

    Assert.Empty(exceptions);
  }

  [Fact]
  public void ConcurrentRentReturn_DifferentSizes_IsThreadSafe()
  {
    var pool = new NativeMemoryPool();
    const int threadCount = 8;
    const int opsPerThread = 500;
    var sizes = new[] { 16, 32, 64, 128, 256, 512, 1024 };
    var barrier = new Barrier(threadCount);
    var exceptions = new List<Exception>();

    var threads = Enumerable.Range(0, threadCount).Select(idx => new Thread(() =>
    {
      try
      {
        barrier.SignalAndWait();
        var rng = new Random(idx);
        for (int i = 0; i < opsPerThread; i++)
        {
          int size = sizes[rng.Next(sizes.Length)];
          var buf = pool.Rent(size);
          buf.AsSpan().Fill((byte)(idx & 0xFF));
          pool.Return(buf);
        }
      }
      catch (Exception ex)
      {
        lock (exceptions) exceptions.Add(ex);
      }
    })).ToArray();

    foreach (var t in threads)
    {
      t.Start();
    }

    foreach (var t in threads)
    {
      t.Join();
    }

    Assert.Empty(exceptions);
  }

  [Fact]
  public void SharedInstance_Works()
  {
    var buffer = NativeMemoryPool.shared.Rent(64);
    Assert.NotEqual(nint.Zero, buffer.pointer);
    NativeMemoryPool.shared.Return(buffer);
  }
}
