namespace Mithril.Network;

public interface IListenerEventHandler
{
  public void OnAccepted(Session session);
  public void OnClosed(Session session);
}
