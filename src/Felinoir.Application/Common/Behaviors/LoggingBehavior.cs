using System.Diagnostics;
using Felinoir.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Felinoir.Application.Common.Behaviors;

/// <summary>
/// First in the pipeline: a <see cref="FelinoirTelemetry"/> span per request, and one log line with
/// the request's name, duration and outcome. It never logs the request or the response, which can
/// hold device tokens and chat messages. Exceptions are only recorded on the span; whoever sent the
/// request (the HTTP exception handler, a background job) logs them.
/// </summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(
    ILogger<LoggingBehavior<TMessage, TResponse>> logger,
    TimeProvider time) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private static readonly string RequestName = typeof(TMessage).Name;

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = FelinoirTelemetry.ActivitySource.StartActivity(RequestName);
        var started = time.GetTimestamp();

        try
        {
            var response = await next(message, cancellationToken);
            var elapsedMs = time.GetElapsedTime(started).TotalMilliseconds;

            if (response is Result { IsSuccess: false } failed)
            {
                activity?.SetTag("felinoir.error", failed.Error.Code);
                LogFailed(logger, RequestName, failed.Error.Code, elapsedMs);
            }
            else
            {
                LogHandled(logger, RequestName, elapsedMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Request} handled in {ElapsedMs:0.0} ms")]
    private static partial void LogHandled(ILogger logger, string request, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Request} failed with {Error} in {ElapsedMs:0.0} ms")]
    private static partial void LogFailed(ILogger logger, string request, string error, double elapsedMs);
}
