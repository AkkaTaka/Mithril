namespace Mithril.Network.Config;

using Mithril.Network.Packet;

public sealed class NetworkConfig
{
  private int maxPacketSize;
  private long maxPendingSendBytes;

  public bool NoDelay { get; init; }

  public int MaxPacketSize
  {
    get => this.maxPacketSize;
    init
    {
      if (value < PacketSerializer.HeaderSize)
      {
        throw new ArgumentOutOfRangeException(nameof(this.MaxPacketSize),
          $"MaxPacketSize must be at least HeaderSize({PacketSerializer.HeaderSize}).");
      }

      this.maxPacketSize = value;
    }
  }

  public long MaxPendingSendBytes
  {
    get => this.maxPendingSendBytes;
    init
    {
      if (value < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(this.MaxPendingSendBytes),
          "MaxPendingSendBytes must be >= 0. Use 0 to disable the limit.");
      }

      this.maxPendingSendBytes = value;
    }
  }

  /// <summary>
  /// true이면 소켓 종료 시 FIN 대신 RST를 전송하여 TIME_WAIT를 건너뛴다.
  /// 대규모 부하 테스트처럼 포트 고갈이 우려되는 클라이언트 환경에서 사용한다.
  /// 미전송 데이터가 유실될 수 있으므로 프로덕션에서는 사용하지 않는다.
  /// </summary>
  public bool LingerResetOnClose { get; init; }

  /// <summary>
  /// true이면 연결/연결 해제 이벤트를 Info 레벨로 로깅한다.
  /// 대규모 연결 환경에서는 false로 설정해 로그 폭주를 방지한다.
  /// </summary>
  public bool LogConnectionEvents { get; init; }
}
