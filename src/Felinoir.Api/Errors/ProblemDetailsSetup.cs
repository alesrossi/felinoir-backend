using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace Felinoir.Api.Errors;

/// <summary>
/// Every error response is RFC 7807 problem details with two extra members: <c>traceId</c> (the
/// same id as the <c>X-Trace-Id</c> header) and <c>error</c>, the machine-readable code clients
/// read. That keeps the original <c>{ "error": "..." }</c> shape as a subset. Framework errors that
/// used to come back empty (malformed JSON, unknown routes) get the same body, and unhandled
/// exceptions become a 500 through <see cref="UnhandledExceptionHandler"/>.
/// </summary>
public static class ProblemDetailsSetup
{
    public const string ErrorMember = "error";
    public const string TraceIdMember = "traceId";

    public static IServiceCollection AddProblemResponses(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;

            // The framework's default traceId is the whole traceparent; use the bare trace id.
            if (Activity.Current is { } activity)
                problem.Extensions[TraceIdMember] = activity.TraceId.ToString();

            if (!problem.Extensions.ContainsKey(ErrorMember))
                problem.Extensions[ErrorMember] = CodeFor(problem.Status ?? context.HttpContext.Response.StatusCode);
        });
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }

    /// <summary>Problem responses for unhandled exceptions and for empty 4xx/5xx responses.</summary>
    public static IApplicationBuilder UseProblemResponses(this IApplicationBuilder app) =>
        app.UseExceptionHandler().UseStatusCodePages();

    /// <summary>The <c>error</c> code of a response that has no business error behind it.</summary>
    private static string CodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "bad_request",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        StatusCodes.Status500InternalServerError => "internal_error",
        _ => "error",
    };
}

/// <summary>Logs an unhandled exception and answers with a 500 problem response.</summary>
public sealed partial class UnhandledExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
