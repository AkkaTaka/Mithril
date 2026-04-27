namespace Mithril.Benchmarks;

using BenchmarkDotNet.Attributes;
using Mithril.Memory;
using System.Buffers;

/// <summary>
/// 단일 스레드에서 NativeMemoryPool과 ArrayPool의 Rent/Return 비용 비교.
/// 경합 없는 조건에서의 순수 오버헤드를 측정한다.
/// </summary>
[MemoryDiagnoser]
public class NativeMemoryPoolBenchmark
{
  [Params(64, 1024)]
  public int Size;

  [Benchmark(Baseline = true)]
  public void RentReturn_ArrayPool()
  {
    var arr = ArrayPool<byte>.Shared.Rent(Size);
    ArrayPool<byte>.Shared.Return(arr);
  }

  [Benchmark]
  public void RentReturn_NativePool()
  {
    var buf = NativeMemoryPool.shared.Rent(Size);
    NativeMemoryPool.shared.Return(buf);
  }
}
