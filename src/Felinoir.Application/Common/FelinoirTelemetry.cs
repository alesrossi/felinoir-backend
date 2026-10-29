using System.Diagnostics;

namespace Felinoir.Application.Common;

/// <summary>
/// The application's own tracing source, for spans around work the framework doesn't trace by
/// itself (request handlers, Felix rebuilds, background jobs). Registered with OpenTelemetry by
/// the host under <see cref="SourceName"/>.
/// </summary>
public static class FelinoirTelemetry
{
    public const string SourceName = "Felinoir";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
