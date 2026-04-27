namespace Mithril.Network;

using Mithril.Logger;
using Mithril.Network;
using System.Net;
using System.Text.Json;

public static class Utf8JsonWriterExtensions
{
  public static void Write(this Utf8JsonWriter writer, string key, Session session)
  {
    using (writer.WriteObject(key))
    {
      writer.Write("Id", session.Id);
      writer.Write("IsConnected", session.IsConnected);

      if (session.RemoteEndPoint is IPEndPoint remoteEndPoint)
      {
        using (writer.WriteObject("RemoteEndPoint"))
        {
          writer.Write("Ip", remoteEndPoint.Address.ToString());
          writer.Write("Port", remoteEndPoint.Port);
        }
      }

      if (session.LocalEndPoint is IPEndPoint localEndPoint)
      {
        using (writer.WriteObject("LocalEndPoint"))
        {
          writer.Write("Ip", localEndPoint.Address.ToString());
          writer.Write("Port", localEndPoint.Port);
        }
      }
    }
  }
}
