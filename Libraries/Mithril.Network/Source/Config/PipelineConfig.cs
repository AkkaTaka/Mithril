namespace Mithril.Network.Config;

using System.Text.Json.Serialization;

public sealed class PipelineConfig
{
  public static readonly PipelineConfig Default;

  static PipelineConfig()
  {
    Default = new PipelineConfig(
      pauseWriterThreshold: 65536,
      resumeWriterThreshold: 32768);
  }

  [JsonConstructor]
  public PipelineConfig(long pauseWriterThreshold, long resumeWriterThreshold)
  {
    if (pauseWriterThreshold <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(pauseWriterThreshold),
        "PauseWriterThreshold must be > 0.");
    }

    if (resumeWriterThreshold <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(resumeWriterThreshold),
        "ResumeWriterThreshold must be > 0.");
    }

    if (resumeWriterThreshold >= pauseWriterThreshold)
    {
      throw new ArgumentException(
        $"ResumeWriterThreshold({resumeWriterThreshold}) must be < PauseWriterThreshold({pauseWriterThreshold}).");
    }

    this.PauseWriterThreshold = pauseWriterThreshold;
    this.ResumeWriterThreshold = resumeWriterThreshold;
  }

  public long PauseWriterThreshold { get; }
  public long ResumeWriterThreshold { get; }
}
