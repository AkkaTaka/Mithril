namespace Mithril.Apps.MassiveTestClient;

using Mithril.AppServices.MassiveTest;
using Mithril.Logger;
using Mithril.Network;
using System.Net;

/// <summary>
/// 단일 TCP 연결의 생명주기를 관리한다.
/// Phase 1(ConnectAsync) → Phase 2(RunAsync) 순서로 호출해야 한다.
/// </summary>
internal sealed class ConnectionRunner
{
  private readonly IAppLogger appLogger;
  private readonly Connector connector;
  private readonly IPEndPoint endPoint;
  private readonly MassiveTestClientConfig config;
  private readonly Statistics statistics;
  private Session? session;
  private int closeRequested;

  public ConnectionRunner(
    IAppLogger appLogger,
    Connector connector,
    IPEndPoint endPoint,
    MassiveTestClientConfig config,
    Statistics statistics)
  {
    this.appLogger = appLogger;
    this.connector = connector;
    this.endPoint = endPoint;
    this.config = config;
    this.statistics = statistics;
  }

  /// <summary>
  /// Phase 1: 연결 수립. 성공/실패를 Statistics에 기록하고 세션을 내부에 저장한다.
  /// </summary>
  public async Task ConnectAsync(CancellationToken ct)
  {
    this.session = await this.ConnectWithRetryAsync(ct);

    if (this.session == null)
    {
      this.statistics.RecordFailed();
    }
    else
    {
      this.statistics.RecordConnected();
    }
  }

  /// <summary>
  /// Phase 2: 연결된 세션에서 패킷 송수신 루프를 실행한다.
  /// ConnectAsync에서 연결에 실패한 경우 즉시 반환한다.
  /// </summary>
  public async Task RunAsync(CancellationToken ct)
  {
    if (this.session == null)
    {
      return;
    }

    var dispatcher = new ClientDispatcher(this.statistics, this.config.Mode);
    this.session.Start(dispatcher);

    try
    {
      if (this.config.Mode == SendMode.Flood)
      {
        await this.RunFloodLoopAsync(this.session, dispatcher, ct);
      }
      else
      {
        await this.RunRateLimitedLoopAsync(this.session, dispatcher, ct);
      }
    }
    finally
    {
      this.Close();
    }
  }

  public void Close()
  {
    if (Interlocked.Exchange(ref this.closeRequested, 1) != 0)
    {
      return;
    }

    var session = this.session;
    if (session == null)
    {
      return;
    }

    session.Close();
    this.statistics.RecordDisconnected();
  }

  private async Task<Session?> ConnectWithRetryAsync(CancellationToken ct)
  {
    var maxRetries = this.config.ConnectMaxRetries > 0 ? this.config.ConnectMaxRetries : 1;

    for (var attempt = 0; attempt < maxRetries; attempt++)
    {
      if (ct.IsCancellationRequested)
      {
        return null;
      }

      var session = await this.connector.ConnectAsync(this.endPoint, ct);

      if (session != null)
      {
        return session;
      }

      // 마지막 시도가 아니면 잠시 대기 후 재시도 (로그 없이 조용히)
      if (attempt < maxRetries - 1)
      {
        try
        {
          await Task.Delay(10, ct);
        }
        catch (OperationCanceledException)
        {
          return null;
        }
      }
    }

    // 임계값 초과 후 에러 로그 출력
    this.appLogger.Error($"Connect failed after {maxRetries} retries: {this.endPoint}");
    return null;
  }

  private async Task RunFloodLoopAsync(Session session, ClientDispatcher dispatcher, CancellationToken ct)
  {
    dispatcher.SendNext(session);

    // ct 취소 시 캔슬레이션 스레드에서 직접 Close()를 호출한다.
    // WhenClosed.WaitAsync(ct)는 ct 콜백이 스레드풀을 경유하기 때문에
    // Flood 트래픽으로 스레드풀이 포화된 상태에서 1분 이상 대기하는 문제가 발생한다.
    // UnsafeRegister는 캔슬레이션 스레드에서 동기적으로 Close()를 실행하여
    // closed=1로 설정하고 Flood를 즉시 중단시킨 뒤 WhenClosed를 완료시킨다.
    using var reg = ct.UnsafeRegister(static s => ((Session)s!).Close(), session);
    await session.WhenClosed;
  }

  private async Task RunRateLimitedLoopAsync(Session session, ClientDispatcher dispatcher, CancellationToken ct)
  {
    var intervalMs = 1000.0 / this.config.RatePerSecond;
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

    while (session.IsConnected)
    {
      try
      {
        if (await timer.WaitForNextTickAsync(ct) == false)
        {
          break;
        }
      }
      catch (OperationCanceledException)
      {
        break;
      }

      dispatcher.SendNext(session);
    }
  }
}
