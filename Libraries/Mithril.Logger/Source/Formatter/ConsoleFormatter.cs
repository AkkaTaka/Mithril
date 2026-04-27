namespace Mithril.Logger.Formatter;

using Serilog.Events;
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;

internal sealed class ConsoleFormatter : Formatter
{
  public ConsoleFormatter(int maxBufferSize)
     : base(maxBufferSize)
  {
  }

  private static ConsoleColor GetColor(LogEventLevel level)
  {
    return level switch
    {
      LogEventLevel.Verbose => ConsoleColor.Gray,
      LogEventLevel.Debug => ConsoleColor.DarkGray,
      LogEventLevel.Information => ConsoleColor.White,
      LogEventLevel.Warning => ConsoleColor.Yellow,
      LogEventLevel.Error => ConsoleColor.Red,
      LogEventLevel.Fatal => ConsoleColor.Magenta,
      _ => ConsoleColor.White
    };
  }

  public override void Format(LogEvent logEvent, TextWriter output)
  {
    var origin = Console.ForegroundColor;
    Console.ForegroundColor = GetColor(logEvent.Level);

    var sb = new StringBuilder();

    sb.Append('[')
      .Append(logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
      .Append("] [")
      .Append(logEvent.Level.ToString().ToUpper()[..3])
      .Append("] ")
      .Append(logEvent.RenderMessage());

    if (logEvent.Properties.TryGetValue(PropertiesCallBackKey, out var callbackProp)
      && callbackProp is ScalarValue { Value: JsonWriterCallback callBack })
    {
      sb.Append(" | ");

      byte[]? buffer = null;
      try
      {
        buffer = ArrayPool<byte>.Shared.Rent(this.maxBufferSize);
        using var stream = new MemoryStream(buffer, true);
        using (var writer = new Utf8JsonWriter(stream, jsonOptions))
        {
          writer.WriteStartObject();
          callBack(writer);
          writer.WriteEndObject();
          writer.Flush();
        }

        var json = Encoding.UTF8.GetString(buffer, 0, (int)Math.Min(this.maxBufferSize, stream.Position));
        sb.Append(json);
      }
      finally
      {
        if (buffer != null)
        {
          ArrayPool<byte>.Shared.Return(buffer);
        }
      }
    }

    if (logEvent.Exception != null)
    {
      sb.AppendLine();
      sb.Append("Exception");
      sb.AppendLine();
      sb.AppendLine(logEvent.Exception.ToString());
    }

    Console.WriteLine(sb.ToString());
    Console.ForegroundColor = origin;

  }
}
