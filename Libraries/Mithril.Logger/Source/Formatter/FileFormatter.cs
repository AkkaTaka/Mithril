namespace Mithril.Logger.Formatter;

using Serilog.Events;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;

internal sealed class FileJsonFormatter : Formatter
{
  public FileJsonFormatter(int maxBufferSize)
    : base(maxBufferSize)
  {
  }

  public override void Format(LogEvent logEvent, TextWriter output)
  {
    byte[]? buffer = null;

    try
    {
      buffer = ArrayPool<byte>.Shared.Rent(this.maxBufferSize);

      using var stream = new MemoryStream(buffer, true);
      using (var writer = new Utf8JsonWriter(stream, jsonOptions))
      {
        using (writer.WriteObject())
        {
          writer.WriteString("Time", logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
          writer.WriteString("Level", logEvent.Level.ToString().ToUpper()[..3]);
          writer.WriteString("Message", logEvent.RenderMessage());

          if (logEvent.Properties.TryGetValue(PropertiesCallBackKey, out var callbackProp)
            && callbackProp is ScalarValue { Value: JsonWriterCallback callBack })
          {
            using (writer.WriteObject("Properties"))
            {
              callBack(writer);
            }
          }

          if (logEvent.Exception is Exception ex)
          {
            using (writer.WriteObject("Exception"))
            {
              writer.WriteString("Type", ex.GetType().FullName);
              writer.WriteString("Message", ex.Message);
              writer.WriteString("StackTrace", ex.ToString());
            }
          }
        }
      }

      var json = Encoding.UTF8.GetString(buffer, 0, (int)Math.Min(this.maxBufferSize, stream.Position));
      output.WriteLine(json);
    }
    finally
    {
      if (buffer != null)
      {
        ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
      }
    }
  }
}
