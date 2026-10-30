using Felinoir.Api.Observability;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace Felinoir.UnitTests.Configuration;

public class RequestLogLevelTests
{
    private static DefaultHttpContext Request(string path, int status)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;
        return context;
    }

    [Theory]
    [InlineData("/health", 200)]
    [InlineData("/HEALTH", 200)]
    public void A_successful_health_check_is_below_every_minimum_level(string path, int status) =>
        Assert.Equal(LogEventLevel.Verbose, RequestLogLevel.For(Request(path, status), 0.1, null));

    [Fact]
    public void A_failing_health_check_is_still_logged() =>
        Assert.Equal(LogEventLevel.Error, RequestLogLevel.For(Request("/health", 503), 0.1, null));

    [Theory]
    [InlineData("/movies", 200, LogEventLevel.Information)]
    [InlineData("/lobbies/x", 404, LogEventLevel.Information)]
    [InlineData("/health/extra", 200, LogEventLevel.Information)]
    [InlineData("/felix/chat", 500, LogEventLevel.Error)]
    public void Other_requests_keep_serilogs_default_levels(string path, int status, LogEventLevel expected) =>
        Assert.Equal(expected, RequestLogLevel.For(Request(path, status), 12, null));

    [Fact]
    public void An_exception_is_an_error_even_on_the_health_check() =>
        Assert.Equal(LogEventLevel.Error, RequestLogLevel.For(Request("/health", 200), 1, new InvalidOperationException()));
}
