namespace Mithril.Network;

using Mithril.Memory;
using System.Net.Sockets;
using System.Threading.Tasks.Sources;

internal sealed class SocketSender
{
  private readonly Socket socket;
  private readonly SocketAsyncEventArgs args;
  private readonly ReusableMemoryManager memoryManager;
  private readonly SendVtsWrapper vts;
  private NativeBuffer sendingBuffer;
  private int totalLength;
  private int offset;

  public SocketSender(Socket socket)
  {
    this.socket = socket;
    this.memoryManager = new ReusableMemoryManager();
    this.vts = new SendVtsWrapper();
    this.args = new SocketAsyncEventArgs();
    this.args.Completed += this.OnCompleted;
  }

  public ValueTask<bool> SendAsync(NativeBuffer buffer, int length)
  {
    this.sendingBuffer = buffer;
    this.totalLength = length;
    this.offset = 0;
    this.vts.Reset();
    this.BeginSend();

    return new ValueTask<bool>(this.vts, this.vts.Version);
  }

  public void Dispose()
  {
    this.args.Dispose();
  }

  private void BeginSend()
  {
    int remaining = this.totalLength - this.offset;
    this.memoryManager.Reset(this.sendingBuffer, this.offset, remaining);
    this.args.SetBuffer(this.memoryManager.Memory);

    if (this.socket.SendAsync(this.args) == false)
    {
      this.HandleResult();
    }
  }

  private void OnCompleted(object? sender, SocketAsyncEventArgs args)
  {
    this.HandleResult();
  }

  private void HandleResult()
  {
    if (this.args.SocketError != SocketError.Success ||
        this.args.BytesTransferred <= 0)
    {
      this.vts.SetResult(false);
      return;
    }

    this.offset += this.args.BytesTransferred;

    if (this.offset >= this.totalLength)
    {
      this.vts.SetResult(true);
      return;
    }

    this.BeginSend();
  }

  private sealed class SendVtsWrapper : IValueTaskSource<bool>
  {
    private ManualResetValueTaskSourceCore<bool> core;

    public SendVtsWrapper()
    {
      this.core.RunContinuationsAsynchronously = true;
    }

    public short Version => this.core.Version;

    public void Reset() => this.core.Reset();

    public void SetResult(bool result) => this.core.SetResult(result);

    bool IValueTaskSource<bool>.GetResult(short token) => this.core.GetResult(token);

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => this.core.GetStatus(token);

    void IValueTaskSource<bool>.OnCompleted(
      Action<object?> continuation,
      object? state,
      short token,
      ValueTaskSourceOnCompletedFlags flags) => this.core.OnCompleted(continuation, state, token, flags);
  }
}
