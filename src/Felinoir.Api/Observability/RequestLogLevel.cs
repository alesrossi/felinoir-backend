using Serilog.Events;

namespace Felinoir.Api.Observability;

/// <summary>
/// The level of the one line Serilog writes per request. Like Serilog's default (errors for an
/// exception or a 5xx, information otherwise), except that a successful health check is
/// <see cref="LogEventLevel.Verbose"/>, below every configured minimum: Docker probes
/// <c>/health</c> every few seconds, and those lines would drown the rest. A failing health check
/// is still logged.
/// </summary>
public static class RequestLogLevel
{
    public const string HealthPath = "/health";

    public static LogEventLevel For(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            return LogEventLevel.Error;

        if (context.Request.Path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase)
            && context.Response.StatusCode < StatusCodes.Status400BadRequest)
            return LogEventLevel.Verbose;

        return LogEventLevel.Information;
    }
}
