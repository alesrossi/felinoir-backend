using System.Globalization;
using Elastic.CommonSchema.Serilog;
using Serilog;

namespace Felinoir.Api.Observability;

/// <summary>
/// Where the API's logs go, unless <c>Serilog:WriteTo</c> configures the sinks itself:
/// <list type="bullet">
/// <item>the console, as readable lines, or as Elastic Common Schema JSON with
/// <c>LOG_CONSOLE_FORMAT=ecs</c> (for a platform that ships stdout);</item>
/// <item>with <c>LOG_FILE_PATH</c>, also a rolling file of ECS JSON lines, which the server's
/// Filebeat ships to Elasticsearch. Files roll daily and at <see cref="FileSizeLimitBytes"/>, and
/// only the last month, at most 5 GB, is kept.</item>
/// </list>
/// </summary>
public static class LogOutputs
{
    public const string ConsoleFormatKey = "LOG_CONSOLE_FORMAT";
    public const string FilePathKey = "LOG_FILE_PATH";

    public const long FileSizeLimitBytes = 100L * 1024 * 1024;
    public const int RetainedFileCountLimit = 50; // 50 × 100 MB = 5 GB
    public static readonly TimeSpan RetainedFileTimeLimit = TimeSpan.FromDays(31);

    private const string TextTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}  {TraceId}{NewLine}{Exception}";

    public static LoggerConfiguration WriteToConfiguredOutputs(
        this LoggerConfiguration logger, IConfiguration configuration)
    {
        if (string.Equals(configuration[ConsoleFormatKey], "ecs", StringComparison.OrdinalIgnoreCase))
            logger.WriteTo.Console(new EcsTextFormatter());
        else
            logger.WriteTo.Console(outputTemplate: TextTemplate, formatProvider: CultureInfo.InvariantCulture);

        if (configuration[FilePathKey] is { Length: > 0 } path)
        {
            logger.WriteTo.File(
                new EcsTextFormatter(),
                path,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCountLimit,
                retainedFileTimeLimit: RetainedFileTimeLimit);
        }

        return logger;
    }
}
