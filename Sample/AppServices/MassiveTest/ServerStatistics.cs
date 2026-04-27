namespace Mithril.AppServices.MassiveTest;

public sealed class ServerStatistics
{
  private long activeConnections;
  private long totalAccepted;
  private long receivedMessages;
  private long sentMessages;
  private long receivedBytes;
  private long sentBytes;

  public void OnAccepted()
  {
    Interlocked.Increment(ref this.activeConnections);
    Interlocked.Increment(ref this.totalAccepted);
  }

  public void OnClosed()
  {
    Interlocked.Decrement(ref this.activeConnections);
  }

  public void OnMessageReceived(long bytes)
  {
    Interlocked.Increment(ref this.receivedMessages);
    Interlocked.Add(ref this.receivedBytes, bytes);
  }

  public void OnMessageSent(long bytes)
  {
    Interlocked.Increment(ref this.sentMessages);
    Interlocked.Add(ref this.sentBytes, bytes);
  }

  /// <summary>현재 카운터를 읽고 처리량 카운터(메시지/바이트)를 초기화한다.</summary>
  public Snapshot TakeSnapshot()
  {
    return new Snapshot(
      activeConnections: Volatile.Read(ref this.activeConnections),
      totalAccepted: Volatile.Read(ref this.totalAccepted),
      receivedMessages: Interlocked.Exchange(ref this.receivedMessages, 0),
      sentMessages: Interlocked.Exchange(ref this.sentMessages, 0),
      receivedBytes: Interlocked.Exchange(ref this.receivedBytes, 0),
      sentBytes: Interlocked.Exchange(ref this.sentBytes, 0));
  }

  public readonly struct Snapshot
  {
    public Snapshot(
      long activeConnections,
      long totalAccepted,
      long receivedMessages,
      long sentMessages,
      long receivedBytes,
      long sentBytes)
    {
      this.ActiveConnections = activeConnections;
      this.TotalAccepted = totalAccepted;
      this.ReceivedMessages = receivedMessages;
      this.SentMessages = sentMessages;
      this.ReceivedBytes = receivedBytes;
      this.SentBytes = sentBytes;
    }

    public long ActiveConnections { get; }
    public long TotalAccepted { get; }
    public long ReceivedMessages { get; }
    public long SentMessages { get; }
    public long ReceivedBytes { get; }
    public long SentBytes { get; }
  }
}
