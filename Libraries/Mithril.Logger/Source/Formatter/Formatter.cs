namespace Mithril.Logger.Formatter;

using Serilog.Events;
using Serilog.Formatting;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

internal abstract class Formatter : ITextFormatter
{
  public const string PropertiesCallBackKey = "__RawPropsCallback";

  protected static JsonWriterOptions jsonOptions;
  protected readonly int maxBufferSize;

  static Formatter()
  {
    jsonOptions = new JsonWriterOptions
    {
      Indented = false,
      SkipValidation = false,
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
  }

  public Formatter(int maxBufferSize)
  {
    this.maxBufferSize = maxBufferSize;
  }

  public abstract void Format(LogEvent logEvent, TextWriter output);
}
