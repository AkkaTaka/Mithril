namespace Mithril.Network;

using Google.Protobuf;
using Mithril.Logger;
using Mithril.Memory;
using Mithril.Network.Config;
using Mithril.Network.Packet;
using Mithril.Utils.Extensions;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;

#if MITHRIL_PROFILE
using System.Diagnostics;
#endif

public sealed partial class Session
{
  private readonly Socket socket;
  private readonly IPacketMetadata packetMetadata;
  private readonly NetworkConfig networkConfig;
  private readonly Pipe receivePipe;
  private readonly MpscByteBuffer sendBuffer;
  private readonly IAppLogger appLogger;
  private readonly Action<Session>? onClosed;
  private readonly SocketSender socketSender;
  private readonly TaskCompletionSource closedTcs;
  private IPacketDispatcher? dispatcher;
  private int closed;
  private long pendingSendBytes;
  /// <summary>
  /// Close()가 최초로 호출될 때 완료되는 Task.
  /// 세션 종료 여부를 await로 감지하는 데 사용한다.
  /// </summary>
  public Task WhenClosed => this.closedTcs.Task;

  public Session(
    IAppLogger appLogger,
    int id,
    Socket socket,
    PipeOptions pipeOptions,
    IPacketMetadata packetMetadata,
    NetworkConfig networkConfig,
    Action<Session>? onClosed)
  {
    this.socket = socket;
    this.packetMetadata = packetMetadata;
    this.networkConfig = networkConfig;
    this.appLogger = appLogger;
    this.receivePipe = new Pipe(pipeOptions);
    this.sendBuffer = new MpscByteBuffer();
    this.onClosed = onClosed;
    this.LocalEndPoint = socket.LocalEndPoint;
    this.RemoteEndPoint = socket.RemoteEndPoint;
    this.Id = id;
    this.closedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    this.socketSender = new SocketSender(socket);
  }

  public int Id { get; }
  public bool IsConnected => Volatile.Read(ref this.closed) == 0;
  public EndPoint? LocalEndPoint { get; }
  public EndPoint? RemoteEndPoint { get; }

  public void Start(IPacketDispatcher dispatcher)
  {
    this.dispatcher = dispatcher;

    this.ExecutePipelineTask(this.FillReceivePipeAsync, "Fill").FireAndForget(this.appLogger);
    this.ExecutePipelineTask(this.ProcessReceivePipeAsync, "Receive").FireAndForget(this.appLogger);
    this.ExecutePipelineTask(this.ProcessSendAsync, "Send").FireAndForget(this.appLogger);
  }

  public void Send<T>(T protoMessage) where T : IMessage
  {
    if (Volatile.Read(ref this.closed) == 1)
    {
      return;
    }

    if (this.packetMetadata.TryGetId<T>(out var id) == false)
    {
      this.appLogger.Error($"Invalid protoMessage. name:{typeof(T).Name}");
      return;
    }

    var bodySize = protoMessage.CalculateSize();
    var totalSize = PacketSerializer.HeaderSize + bodySize;

    if (this.networkConfig.MaxPendingSendBytes > 0)
    {
      var after = Interlocked.Add(ref this.pendingSendBytes, totalSize);

      if (after > this.networkConfig.MaxPendingSendBytes)
      {
        Interlocked.Add(ref this.pendingSendBytes, -totalSize);

        this.appLogger.Warning(
          "SendQueueOverflow. closing session",
          w =>
          {
            w.Write("SessionId", this.Id);
            w.Write("AfterBytes", after - totalSize);
            w.Write("LimitBytes", this.networkConfig.MaxPendingSendBytes);
            w.Write("PacketId", id);
            w.Write("PacketBytes", totalSize);
          });

        this.Close();
        return;
      }
    }

    var nativeBuffer = NativeMemoryPool.shared.Rent(totalSize);
    PacketSerializer.Serialize(nativeBuffer.AsSpan(totalSize), id, protoMessage, bodySize);

    if (this.sendBuffer.Push(nativeBuffer, totalSize) == false)
    {
      NativeMemoryPool.shared.Return(nativeBuffer);
    }
  }

  public void SetDispatcher(IPacketDispatcher dispatcher)
  {
    this.dispatcher = dispatcher;
  }

  private async Task FillReceivePipeAsync()
  {
    var receivePipeWriter = this.receivePipe.Writer;

    while (this.closed == 0)
    {
      var memory = receivePipeWriter.GetMemory(1024);
      var bytesRead = await this.socket.ReceiveAsync(memory, SocketFlags.None, CancellationToken.None);
#if MITHRIL_PROFILE
      this.ProfileOnSocketReceiveCompleted();
#endif
      if (bytesRead == 0 || this.closed != 0)
      {
        break;
      }

      receivePipeWriter.Advance(bytesRead);

      var result = await receivePipeWriter.FlushAsync(CancellationToken.None);
#if MITHRIL_PROFILE
      this.ProfileOnReceivePipeFlushCompleted();
#endif
      if (result.IsCompleted || result.IsCanceled)
      {
        break;
      }
    }
  }

  private async Task ProcessReceivePipeAsync()
  {
    var receivePipeReader = this.receivePipe.Reader;
    const int headerSize = PacketSerializer.HeaderSize;

    while (this.closed == 0)
    {
      var result = await receivePipeReader.ReadAsync(CancellationToken.None);
#if MITHRIL_PROFILE
      this.ProfileOnReceivePipeRead();
#endif
      var buffer = result.Buffer;
      var consumed = buffer.Start;

      while (true)
      {
        if (buffer.Length < headerSize)
        {
          break;
        }

        if (PacketSerializer.TryParseHeader(buffer, out var header) == false)
        {
          this.appLogger.Error(
            "InvalidPacket. Failed to ParseHeader",
            w =>
            {
              w.Write("Length", buffer.Length);
            });

          receivePipeReader.AdvanceTo(consumed, buffer.End);
          this.Close();
          return;
        }

        if (header.size < headerSize)
        {
          this.appLogger.Error(
            "InvalidPacket. too small",
            w =>
            {
              w.Write("BodySize", header.size);
              w.Write("MaxPacketSize", this.networkConfig.MaxPacketSize);
            });

          receivePipeReader.AdvanceTo(consumed, buffer.End);
          this.Close();
          return;
        }

        if (header.size > this.networkConfig.MaxPacketSize)
        {
          this.appLogger.Error(
            "InvalidPacket. too large",
            w =>
            {
              w.Write("BodySize", header.size);
              w.Write("MaxPacketSize", this.networkConfig.MaxPacketSize);
            });

          receivePipeReader.AdvanceTo(consumed, buffer.End);
          this.Close();
          return;
        }

        if (buffer.Length < header.size)
        {
          break;
        }

        var sequence = buffer.Slice(headerSize, header.size - headerSize);
        this.dispatcher?.OnReceived(this, header.id, sequence);

        buffer = buffer.Slice(header.size);
        consumed = buffer.Start;
      }

      receivePipeReader.AdvanceTo(consumed, buffer.End);

      if (result.IsCompleted && buffer.IsEmpty)
      {
        break;
      }
    }
  }

  private async Task ProcessSendAsync()
  {
    try
    {
      while (this.closed == 0)
      {
        await this.sendBuffer.WaitForDataAsync();
        this.sendBuffer.ResetSignal();

        var head = this.sendBuffer.TryDrain();

        if (head == null)
        {
          if (this.sendBuffer.IsCompleted)
          {
            break;
          }
          continue;
        }

        var segment = head;
        while (segment != null)
        {
          var next = segment.next;

          if (await this.socketSender.SendAsync(segment.buffer, segment.length) == false)
          {
            NativeMemoryPool.shared.Return(segment.buffer);
            this.ClearSendBuffer(next);
            return;
          }

          Interlocked.Add(ref this.pendingSendBytes, -segment.length);
          NativeMemoryPool.shared.Return(segment.buffer);
          segment = next;
        }
      }
    }
    finally
    {
      this.ClearSendBuffer(null);
    }
  }

  public void Close()
  {
    if (Interlocked.Exchange(ref this.closed, 1) != 0)
    {
      return;
    }

#if MITHRIL_PROFILE
    this.ProfileOnCloseStarted();
    var closeStart = Stopwatch.GetTimestamp();
#endif

    // LingerResetOnClose(RST) 모드에서는 Shutdown 없이 Close만 호출해도 즉시 RST를 보낸다.
    // Shutdown(Both)는 고부하 환경에서 커널 콜 비용이 높으므로 RST 시에는 생략한다.
    if (this.networkConfig.LingerResetOnClose == false)
    {
      try
      {
        if (this.socket.Connected)
        {
          this.socket.Shutdown(SocketShutdown.Both);
        }
      }
      catch
      {
      }
    }

#if MITHRIL_PROFILE
    var socketCloseStart = Stopwatch.GetTimestamp();
#endif
    try { this.socket.Close(); } catch { }
#if MITHRIL_PROFILE
    this.ProfileOnCloseSocketClosed(Stopwatch.GetTimestamp() - socketCloseStart);
#endif

#if MITHRIL_PROFILE
    var sendBufferCompleteStart = Stopwatch.GetTimestamp();
#endif
    try { this.sendBuffer.Complete(); } catch { }
#if MITHRIL_PROFILE
    this.ProfileOnCloseSendBufferCompleted(Stopwatch.GetTimestamp() - sendBufferCompleteStart);
#endif

#if MITHRIL_PROFILE
    var clearSendBufferStart = Stopwatch.GetTimestamp();
#endif
    this.ClearSendBuffer(null);
#if MITHRIL_PROFILE
    this.ProfileOnCloseSendBufferCleared(Stopwatch.GetTimestamp() - clearSendBufferStart);
#endif

#if MITHRIL_PROFILE
    var receiveWriterCompleteStart = Stopwatch.GetTimestamp();
#endif
    try { this.receivePipe.Writer.Complete(); } catch { }
#if MITHRIL_PROFILE
    this.ProfileOnCloseReceiveWriterCompleted(Stopwatch.GetTimestamp() - receiveWriterCompleteStart);
#endif

#if MITHRIL_PROFILE
    var receiveReaderCompleteStart = Stopwatch.GetTimestamp();
#endif
    try { this.receivePipe.Reader.Complete(); } catch { }
#if MITHRIL_PROFILE
    this.ProfileOnCloseReceiveReaderCompleted(Stopwatch.GetTimestamp() - receiveReaderCompleteStart);
#endif

#if MITHRIL_PROFILE
    var socketSenderDisposeStart = Stopwatch.GetTimestamp();
#endif
    try { this.socketSender.Dispose(); } catch { }
#if MITHRIL_PROFILE
    this.ProfileOnCloseSocketSenderDisposed(Stopwatch.GetTimestamp() - socketSenderDisposeStart);
#endif

#if MITHRIL_PROFILE
    var notifyStart = Stopwatch.GetTimestamp();
#endif
    this.closedTcs.TrySetResult();
    this.onClosed?.Invoke(this);
#if MITHRIL_PROFILE
    this.ProfileOnCloseNotified(Stopwatch.GetTimestamp() - notifyStart);

    var closeElapsed = Stopwatch.GetTimestamp() - closeStart;
    this.ProfileOnCloseCompleted(closeElapsed);
#endif
  }

  private async Task ExecutePipelineTask(Func<Task> action, string taskName)
  {
    try
    {
      await action();
    }
    catch (OperationCanceledException)
    {
    }
    catch (ObjectDisposedException)
    {
    }
    catch (InvalidOperationException)
    {
    }
    catch (SocketException)
    {
    }
    catch (Exception ex)
    {
      this.appLogger.Error(ex, $"{taskName} Error");
    }
    finally
    {
      this.Close();
    }
  }

  private void ClearSendBuffer(MpscByteBuffer.Segment? remaining)
  {
    ReturnSegmentBuffers(remaining);
    ReturnSegmentBuffers(this.sendBuffer.TryDrain());
  }

  private static void ReturnSegmentBuffers(MpscByteBuffer.Segment? segment)
  {
    while (segment != null)
    {
      var next = segment.next;
      NativeMemoryPool.shared.Return(segment.buffer);
      segment = next;
    }
  }

}
