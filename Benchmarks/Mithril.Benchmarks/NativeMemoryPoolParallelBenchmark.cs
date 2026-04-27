namespace Mithril.Benchmarks;

using BenchmarkDotNet.Attributes;
using Mithril.Memory;
using System.Buffers;
using System.Collections.Concurrent;

/// <summary>
/// 수신(Rent) 스레드와 처리(Return) 스레드를 분리한 MPMC 시나리오.
/// 생산자 N개는 Rent만, 소비자 N개는 Return만 수행하며 동시에 실행된다.
/// 총 스레드 수 = ThreadCount * 2.
/// </summary>
[MemoryDiagnoser]
public class NativeMemoryPoolParallelBenchmark
{
  private const int OpsPerThread = 500;

  [Params(1, 4, 8, 16)]
  public int ThreadCount;

  [Benchmark(Baseline = true)]
  public void ArrayPool_MPMC()
  {
    var queue = new ConcurrentQueue<byte[]>();
    int totalOps = OpsPerThread * ThreadCount;
    int consumed = 0;
    using var done = new CountdownEvent(ThreadCount * 2);

    for (int i = 0; i < ThreadCount; i++)
    {
      Task.Run(() =>
      {
        for (int j = 0; j < OpsPerThread; j++)
        {
          queue.Enqueue(ArrayPool<byte>.Shared.Rent(1024));
        }

        done.Signal();
      });
    }

    for (int i = 0; i < ThreadCount; i++)
    {
      Task.Run(() =>
      {
        while (true)
        {
          int idx = Interlocked.Increment(ref consumed);

          if (idx > totalOps)
          {
            break;
          }

          byte[]? arr;
          while (queue.TryDequeue(out arr) == false)
          {
            Thread.SpinWait(10);
          }

          ArrayPool<byte>.Shared.Return(arr!);

          if (idx == totalOps)
          {
            break;
          }
        }

        done.Signal();
      });
    }

    done.Wait();
  }

  [Benchmark]
  public void NativePool_MPMC()
  {
    var queue = new ConcurrentQueue<NativeBuffer>();
    int totalOps = OpsPerThread * ThreadCount;
    int consumed = 0;
    using var done = new CountdownEvent(ThreadCount * 2);

    for (int i = 0; i < ThreadCount; i++)
    {
      Task.Run(() =>
      {
        for (int j = 0; j < OpsPerThread; j++)
        {
          queue.Enqueue(NativeMemoryPool.shared.Rent(1024));
        }

        done.Signal();
      });
    }

    for (int i = 0; i < ThreadCount; i++)
    {
      Task.Run(() =>
      {
        while (true)
        {
          int idx = Interlocked.Increment(ref consumed);

          if (idx > totalOps)
          {
            break;
          }

          NativeBuffer buf;
          while (queue.TryDequeue(out buf) == false)
          {
            Thread.SpinWait(10);
          }

          NativeMemoryPool.shared.Return(buf);

          if (idx == totalOps)
          {
            break;
          }
        }

        done.Signal();
      });
    }

    done.Wait();
  }
}
