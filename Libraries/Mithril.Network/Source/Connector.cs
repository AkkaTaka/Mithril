namespace Mithril.Network;

using Mithril.Logger;
using Mithril.Network.Config;
using Mithril.Network.Packet;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

public sealed class Connector
{
  private readonly IAppLogger appLogger;
  private readonly NetworkConfig config;
  private readonly IPacketMetadata packetMetadata;
  private int idSeed;

  public Connector(
    IAppLogger appLogger, 
    NetworkConfig config,
    IPacketMetadata packetMetadata)
  {
    this.appLogger = appLogger;
    this.config = config;
    this.packetMetadata = packetMetadata;
  }

  public async Task<Session?> ConnectAsync(
    IPEndPoint endPoint,
    CancellationToken ct,
    PipeOptions? pipeOptions = null)
  {
    try
    {
      var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
      socket.NoDelay = this.config.NoDelay;

      if (this.config.LingerResetOnClose)
      {
        // RST로 종료 → TIME_WAIT 없이 포트 즉시 반환 (부하 테스트 용도)
        socket.LingerState = new System.Net.Sockets.LingerOption(true, 0);
      }

      await socket.ConnectAsync(endPoint, ct);

      if (ct.IsCancellationRequested)
      {
        socket.Close();
        return null;
      }

      if (this.config.LogConnectionEvents)
      {
        this.appLogger.Info($"Connected to server: {endPoint}");
      }

      return new Session(
        this.appLogger,
        Interlocked.Increment(ref this.idSeed),
        socket,
        pipeOptions ?? PipeOptions.Default,
        this.packetMetadata,
        this.config,
        null);
    }
    catch (Exception)
    {
      // 연결 실패는 호출자(ConnectionRunner)가 재시도 횟수 초과 후 로깅한다.
      return null;
    }
  }
}
