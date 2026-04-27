namespace Mithril.Logger;

using System.Text.Json;

public static class Utf8JsonWriterExtensions
{
  public static void Write(this Utf8JsonWriter writer, string key, string value)
  {
    writer.WritePropertyName(key);
    writer.WriteStringValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, int value)
  {
    writer.WritePropertyName(key);
    writer.WriteNumberValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, long value)
  {
    writer.WritePropertyName(key);
    writer.WriteNumberValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, float value)
  {
    writer.WritePropertyName(key);
    writer.WriteNumberValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, double value)
  {
    writer.WritePropertyName(key);
    writer.WriteNumberValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, decimal value)
  {
    writer.WritePropertyName(key);
    writer.WriteNumberValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, bool value)
  {
    writer.WritePropertyName(key);
    writer.WriteBooleanValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, DateTime value)
  {
    writer.WritePropertyName(key);
    writer.WriteStringValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, Guid value)
  {
    writer.WritePropertyName(key);
    writer.WriteStringValue(value);
  }

  public static void Write(this Utf8JsonWriter writer, string key, byte[] value)
  {
    writer.WritePropertyName(key);
    writer.WriteBase64StringValue(value);
  }

  public static void WriteNull(this Utf8JsonWriter writer, string key)
  {
    writer.WritePropertyName(key);
    writer.WriteNullValue();
  }

  public static Scope WriteObject(this Utf8JsonWriter writer, string propertyName)
  {
    writer.WritePropertyName(propertyName);
    writer.WriteStartObject();

    return new Scope(writer);
  }

  public static Scope WriteObject(this Utf8JsonWriter writer)
  {
    writer.WriteStartObject();

    return new Scope(writer);
  }

  public readonly struct Scope : IDisposable
  {
    private readonly Utf8JsonWriter writer;

    public Scope(Utf8JsonWriter writer)
    {
      this.writer = writer;
    }

    public void Dispose()
    {
      this.writer.WriteEndObject();
    }
  }
}

