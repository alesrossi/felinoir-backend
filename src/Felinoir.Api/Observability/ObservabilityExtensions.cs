using System.Diagnostics;
using Felinoir.Application.Common;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Felinoir.Api.Observability;

/// <summary>
/// Logging and tracing (rework plan, step 3). The correlation id is the W3C trace id: ASP.NET
/// continues an incoming <c>traceparent</c>, every log line carries the trace and span ids, and
/// every response reports the trace id in <see cref="TraceIdHeader"/>.
/// </summary>
public static class ObservabilityExtensions
{
    public const string TraceIdHeader = "X-Trace-Id";

    private const string ServiceName = "felinoir-api";

    /// <summary>
    /// Serilog, configured from the <c>Serilog</c> configuration section (levels, overrides,
    /// enrichers, sinks). Without a <c>Serilog:WriteTo</c> it writes to <see cref="LogOutputs"/>:
    /// readable console lines, and Elastic Common Schema JSON files when a path is configured.
    ///
    /// OpenTelemetry traces ASP.NET Core, outgoing HTTP (Gemini), Npgsql and
    /// <see cref="FelinoirTelemetry"/>, plus their metrics. Nothing is exported until
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (Elastic APM accepts OTLP directly).
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services.AddSerilog((services, logger) =>
        {
            logger.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            if (!configuration.GetSection("Serilog:WriteTo").Exists())
                logger.WriteToConfiguredOutputs(configuration);
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(FelinoirTelemetry.SourceName)
                // Docker probes /health every few seconds; those traces would drown the rest.
                .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                    !context.Request.Path.Equals(RequestLogLevel.HealthPath, StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation());

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }

    /// <summary>
    /// Writes the request's trace id to <see cref="TraceIdHeader"/> on every response, so a
    /// caller (or a user reporting an error) can quote it. Add before anything that responds.
    /// </summary>
    public static IApplicationBuilder UseTraceIdHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (Activity.Current?.TraceId.ToString() is { } traceId)
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[TraceIdHeader] = traceId;
                    return Task.CompletedTask;
                });
            }

            await next(context);
        });
}
