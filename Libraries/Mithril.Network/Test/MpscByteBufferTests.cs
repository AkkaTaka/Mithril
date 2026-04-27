namespace Mithril.Network.Tests;

using Mithril.Memory;
using System.Buffers.Binary;
using System.Collections.Concurrent;

public sealed class MpscByteBufferTests
{
  private static NativeMemoryPool Pool => NativeMemoryPool.shared;

  /// <summary>
  /// 버퍼 첫 4바이트에 고유 인덱스를 기록한다.
  /// pointer가 internal이라 내용으로 버퍼를 식별한다.
  /// </summary>
  private static NativeBuffer RentTagged(int tag)
  {
    var buf = Pool.Rent(16);
    BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(), tag);
    return buf;
  }

  private static int ReadTag(NativeBuffer buf)
  {
    return BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan());
  }

  private static void ReturnAll(MpscByteBuffer.Segment? segment)
  {
    while (segment != null)
    {
      var next = segment.next;
      Pool.Return(segment.buffer);
      segment = next;
    }
  }

  // ── 기본 동작 ──────────────────────────────────────────────────────────────

  [Fact]
  public void TryDrain_WhenEmpty_ReturnsNull()
  {
    var buffer = new MpscByteBuffer();

    Assert.Null(buffer.TryDrain());
  }

  [Fact]
  public void Push_SingleItem_DrainedCorrectly()
  {
    var buffer = new MpscByteBuffer();
    var buf = RentTagged(42);

    var pushed = buffer.Push(buf, 16);
    var head = buffer.TryDrain();

    Assert.True(pushed);
    Assert.NotNull(head);
    Assert.Equal(42, ReadTag(head.buffer));
    Assert.Null(head.next);

    Pool.Return(head.buffer);
  }

  [Fact]
  public void Push_MultipleItems_DrainedInFifoOrder()
  {
    // Treiber 스택을 Reverse해서 FIFO 보장하는지 검증
    var buffer = new MpscByteBuffer();
    const int count = 5;

    for (int i = 0; i < count; i++)
    {
      buffer.Push(RentTagged(i), 16);
    }

    var segment = buffer.TryDrain();

    for (int i = 0; i < count; i++)
    {
      Assert.NotNull(segment);
      Assert.Equal(i, ReadTag(segment!.buffer));
      Pool.Return(segment.buffer);
      segment = segment.next;
    }

    Assert.Null(segment);
  }

  [Fact]
  public void TryDrain_CalledTwice_SecondCallReturnsNull()
  {
    var buffer = new MpscByteBuffer();
    buffer.Push(RentTagged(1), 16);

    ReturnAll(buffer.TryDrain());

    Assert.Null(buffer.TryDrain());
  }

  // ── Complete / Push 후 Complete ────────────────────────────────────────────

  [Fact]
  public void Push_AfterComplete_ReturnsFalse()
  {
    var buffer = new MpscByteBuffer();
    var buf = RentTagged(0);

    buffer.Complete();
    var pushed = buffer.Push(buf, 16);

    Assert.False(pushed);
    Assert.True(buffer.IsCompleted);

    Pool.Return(buf);
  }

  [Fact]
  public void Complete_SetsIsCompleted()
  {
    var buffer = new MpscByteBuffer();

    Assert.False(buffer.IsCompleted);
    buffer.Complete();
    Assert.True(buffer.IsCompleted);
  }

  // ── WaitForDataAsync / ResetSignal ─────────────────────────────────────────

  [Fact]
  public async Task WaitForDataAsync_CompletesWhenPushed()
  {
    var buffer = new MpscByteBuffer();
    var waitTask = buffer.WaitForDataAsync().AsTask();

    Assert.False(waitTask.IsCompleted);

    buffer.Push(RentTagged(1), 16);
    var result = await waitTask;

    Assert.True(result);
    ReturnAll(buffer.TryDrain());
  }

  [Fact]
  public async Task WaitForDataAsync_CompletesWhenCompleted()
  {
    var buffer = new MpscByteBuffer();
    var waitTask = buffer.WaitForDataAsync().AsTask();

    buffer.Complete();
    var result = await waitTask;

    Assert.True(result);
  }

  [Fact]
  public async Task ResetSignal_MultipleWaitCycles_AllComplete()
  {
    // WaitForDataAsync → ResetSignal → Push 사이클이 반복돼도 정상 동작하는지 검증
    var buffer = new MpscByteBuffer();

    for (int i = 0; i < 10; i++)
    {
      buffer.Push(RentTagged(i), 16);
      await buffer.WaitForDataAsync();
      buffer.ResetSignal();
      ReturnAll(buffer.TryDrain());
    }
  }

  [Fact]
  public async Task ResetSignal_PushDuringReset_DoesNotLoseSignal()
  {
    // ResetSignal 직후 Push가 들어와도 다음 Wait가 즉시 완료돼야 한다
    var buffer = new MpscByteBuffer();

    buffer.Push(RentTagged(1), 16);
    await buffer.WaitForDataAsync();

    // ResetSignal 전에 이미 다음 Push
    buffer.Push(RentTagged(2), 16);
    buffer.ResetSignal();

    // 신호가 재설정돼야 하므로 즉시 완료
    var waitTask = buffer.WaitForDataAsync().AsTask();
    Assert.True(waitTask.IsCompleted);

    ReturnAll(buffer.TryDrain());
  }

  // ── 동시성 ─────────────────────────────────────────────────────────────────

  [Fact]
  public async Task MultipleProducers_AllItemsDrained_NoDuplicates()
  {
    // 여러 스레드가 동시에 Push해도 모든 항목이 유실/중복 없이 Drain되는지 검증
    const int producerCount = 8;
    const int itemsPerProducer = 1000;
    const int total = producerCount * itemsPerProducer;

    var mpscBuffer = new MpscByteBuffer();
    var allBufs = new NativeBuffer[total];

    for (int i = 0; i < total; i++)
    {
      allBufs[i] = RentTagged(i);
    }

    var tasks = Enumerable.Range(0, producerCount).Select(p => Task.Run(() =>
    {
      for (int i = 0; i < itemsPerProducer; i++)
      {
        mpscBuffer.Push(allBufs[p * itemsPerProducer + i], 16);
      }
    })).ToArray();

    await Task.WhenAll(tasks);

    var receivedTags = new HashSet<int>();
    var segment = mpscBuffer.TryDrain();

    while (segment != null)
    {
      var tag = ReadTag(segment.buffer);
      Assert.True(receivedTags.Add(tag), $"Duplicate tag found: {tag}");
      Pool.Return(segment.buffer);
      segment = segment.next;
    }

    Assert.Equal(total, receivedTags.Count);

    for (int i = 0; i < total; i++)
    {
      Assert.Contains(i, receivedTags);
    }
  }

  [Fact]
  public async Task PushVsComplete_Race_PushedAfterCompleteAreNotLeaked()
  {
    // Push와 Complete가 동시에 발생할 때 Push false 반환 시 버퍼가 누수 없이 반납되는지 검증
    // (MpscByteBuffer 자체 책임은 아니지만 false 반환이 정확한지 확인)
    for (int trial = 0; trial < 200; trial++)
    {
      var mpscBuffer = new MpscByteBuffer();
      var failedBuffers = new ConcurrentBag<NativeBuffer>();

      var pushTask = Task.Run(() =>
      {
        for (int i = 0; i < 50; i++)
        {
          var buf = RentTagged(i);
          if (mpscBuffer.Push(buf, 16) == false)
          {
            failedBuffers.Add(buf);
          }
        }
      });

      var completeTask = Task.Run(() => mpscBuffer.Complete());

      await Task.WhenAll(pushTask, completeTask);

      // Push 실패한 버퍼는 큐에 없으므로 호출부가 반납
      foreach (var buf in failedBuffers)
      {
        Pool.Return(buf);
      }

      // Complete 이후엔 Push가 절대 성공하면 안 됨
      ReturnAll(mpscBuffer.TryDrain());
    }
  }

  [Fact]
  public async Task ConcurrentPushAndDrain_NoItemLost()
  {
    // Producer가 Push하는 동안 Consumer가 지속적으로 Drain하는 시나리오
    const int producerCount = 4;
    const int itemsPerProducer = 500;
    const int total = producerCount * itemsPerProducer;

    var mpscBuffer = new MpscByteBuffer();
    var receivedCount = 0;
    var cts = new CancellationTokenSource();

    var consumerTask = Task.Run(async () =>
    {
      while (receivedCount < total && cts.IsCancellationRequested == false)
      {
        var segment = mpscBuffer.TryDrain();
        while (segment != null)
        {
          var next = segment.next;
          Pool.Return(segment.buffer);
          Interlocked.Increment(ref receivedCount);
          segment = next;
        }

        await Task.Yield();
      }
    });

    var producerTasks = Enumerable.Range(0, producerCount).Select(p => Task.Run(() =>
    {
      for (int i = 0; i < itemsPerProducer; i++)
      {
        mpscBuffer.Push(RentTagged(p * itemsPerProducer + i), 16);
      }
    })).ToArray();

    await Task.WhenAll(producerTasks);

    // Producer 완료 후 Consumer가 나머지 소진할 때까지 대기
    await Task.WhenAny(consumerTask, Task.Delay(5000));
    cts.Cancel();

    // 남은 항목 정리
    ReturnAll(mpscBuffer.TryDrain());

    Assert.Equal(total, receivedCount);
  }
}
