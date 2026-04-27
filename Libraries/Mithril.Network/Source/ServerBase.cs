namespace Mithril.Network;

using Mithril.Logger;
using Mithril.Network.Config;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

public abstract class ServerBase : IListenerEventHandler
{
  private readonly IAppLogger appLogger;
  private readonly string name;
  private readonly NetworkFramework networkFramework;
  private readonly Listener listener;
  private readonly ConcurrentDictionary<int, Session> sessionMap;

  public ServerBase(
    IAppLogger appLogger,
    string name,
    NetworkFramework networkFramework,
    ListenerConfig listenerConfig)
  {
    this.appLogger = appLogger;
    this.name = name;
    this.networkFramework = networkFramework;

    this.listener = networkFramework.CreateListener(
      name,
      listenerConfig,
      this);

    this.sessionMap = new ConcurrentDictionary<int, Session>();
  }

  public abstract void OnAcceptedInternal(Session session);
  public abstract void OnClosedInternal(Session session);

  public void Start(CancellationToken ct)
  {
    this.listener.Start(ct);

    this.appLogger.Info(
      $"{this.name} Started",
      writer =>
      {
        writer.Write("Name", this.listener.Name);
        writer.Write("Port", this.listener.Config.Port);
      });
  }

  public async Task StopAsync()
  {
    var sessions = this.sessionMap.Values.ToArray();
    this.sessionMap.Clear();

    foreach (var session in sessions)
    {
      session.Close();
    }

    await this.listener.StopAsync();

    this.appLogger.Info($"{this.name} Stopped");
  }

  public bool TryGetSession(
    int id,
    [MaybeNullWhen(false)] out Session session)
  {
    return this.sessionMap.TryGetValue(id, out session);
  }

  public void OnAccepted(Session session)
  {
    if (this.networkFramework.Config.LogConnectionEvents)
    {
      this.appLogger.Info(
        "OnAccepted",
        writer =>
        {
          writer.Write("Name", this.listener.Name);
          writer.Write("Session", session);
        });
    }

    this.sessionMap[session.Id] = session;

    this.OnAcceptedInternal(session);
  }

  public void OnClosed(Session session)
  {
    if (this.networkFramework.Config.LogConnectionEvents)
    {
      this.appLogger.Info(
        "OnClosed",
        writer =>
        {
          writer.Write("Name", this.listener.Name);
          writer.Write("Session", session);
        });
    }

    this.sessionMap.TryRemove(session.Id, out _);

    this.OnClosedInternal(session);
  }
}
