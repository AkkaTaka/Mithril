namespace Mithril.PostgresKit.Execution;

public readonly struct DbValue
{
  private readonly object? value;

  internal DbValue(object? value)
  {
    this.value = value;
  }

  public bool IsNull => this.value is null || this.value is DBNull;

  public long ToInt64()
  {
    if (this.value is null || this.value is DBNull)
    {
      throw new InvalidOperationException("DB returned NULL, expected bigint.");
    }

    if (this.value is long l)
    {
      return l;
    }

    if (this.value is int i)
    {
      return i;
    }

    throw new InvalidCastException($"DB value '{this.value.GetType().Name}' cannot be converted to Int64.");
  }

  public int ToInt32()
  {
    if (this.value is null || this.value is DBNull)
    {
      throw new InvalidOperationException("DB returned NULL, expected int.");
    }

    if (this.value is int i)
    {
      return i;
    }

    if (this.value is short s)
    {
      return s;
    }

    throw new InvalidCastException($"DB value '{this.value.GetType().Name}' cannot be converted to Int32.");
  }

  public string ToStringNotNull()
  {
    if (this.value is null || this.value is DBNull)
    {
      throw new InvalidOperationException("DB returned NULL, expected string.");
    }

    return (string)this.value;
  }
}
