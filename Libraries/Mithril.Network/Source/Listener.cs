namespace Mithril.Network;

using Mithril.Logger;
using Mithril.Network.Config;
using Mithril.Network.Packet;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;

public sealed class Listener
{
  private readonly Socket listenSocket;
  private readonly PipeOptions pipeOptions;
  private readonly IAppLogger appLogger;
  private readonly string name;
  private readonly NetworkConfig networkConfig;
  private readonly ListenerConfig config;
  private readonly IListenerEventHandler eventHandler;
  private readonly IPacketMetadata packetMetadata;
  private int idSeed;
  private Task? acceptLoopTask;

  public Listener(
    IAppLogger appLogger,
    string name,
    NetworkConfig networkConfig,
    ListenerConfig listenerConfig,
    IListenerEventHandler eventHandler,
    IPacketMetadata packetMetadata)
  {
    this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    this.listenSocket.NoDelay = networkConfig.NoDelay;

    this.pipeOptions = new PipeOptions(
        pool: MemoryPool<byte>.Shared,
        readerScheduler: PipeScheduler.ThreadPool,
        writerScheduler: PipeScheduler.ThreadPool,
        pauseWriterThreshold: listenerConfig.Pipeline.PauseWriterThreshold,
        resumeWriterThreshold: listenerConfig.Pipeline.ResumeWriterThreshold,
        useSynchronizationContext: false
    );

    this.appLogger = appLogger;
    this.name = name;
    this.networkConfig = networkConfig;
    this.config = listenerConfig;
    this.eventHandler = eventHandler;
    this.packetMetadata = packetMetadata;
  }

  public string Name => this.name;
  public ListenerConfig Config => this.config;

  public async Task StopAsync()
  {
    this.listenSocket.Dispose();

    if (this.acceptLoopTask != null)
    {
      await this.acceptLoopTask;
    }
  }

  public void Start(CancellationToken ct)
  {
    var endPoint = this.config.EndPoint;
    this.listenSocket.Bind(endPoint);
    this.listenSocket.Listen(this.config.Backlog);

    this.appLogger.Info($"[{this.name}] Listener Started on {endPoint.ToString()}");

    this.acceptLoopTask = Task.Run(() => this.AcceptLoopAsync(ct));
  }

  private async Task AcceptLoopAsync(CancellationToken ct)
  {
    try
    {
      while (ct.IsCancellationRequested == false)
      {
        try
        {
          var socket = await this.listenSocket.AcceptAsync(ct);
          this.Accept(socket, ct);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (ObjectDisposedException)
        {
          break;
        }
        catch (Exception ex)
        {
          this.appLogger.Error($"[{this.name}] Accept Error: {ex.Message}");
        }
      }
    }
    finally
    {
      this.listenSocket.Dispose();
      this.appLogger.Info($"[{this.name}] Listener Stopped");
    }
  }

  private void Accept(Socket socket, CancellationToken ct)
  {
    var session = new Session(
      this.appLogger,
      Interlocked.Increment(ref this.idSeed),
      socket,
      this.pipeOptions,
      this.packetMetadata,
      this.networkConfig,
      this.OnSessionClosed);

    this.eventHandler.OnAccepted(session);
  }

  private void OnSessionClosed(Session session)
  {
    this.eventHandler.OnClosed(session);
  }
}
