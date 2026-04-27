namespace Mithril.Logger;

using Serilog;
using Serilog.Events;

public sealed record LoggerOptions(
    string FilePath,
    LogEventLevel MinimumLevel,
    RollingInterval RollingInterval,
    int MaxBufferSize
);
