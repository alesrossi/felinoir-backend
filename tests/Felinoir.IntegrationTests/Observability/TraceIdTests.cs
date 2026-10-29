using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Observability;

/// <summary>
/// The W3C trace id is the correlation id: a caller's <c>traceparent</c> is continued, and every
/// response reports the trace id in <c>X-Trace-Id</c> (also exposed to browsers by CORS).
/// </summary>
public class TraceIdTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    [Fact]
    public async Task Every_response_carries_a_trace_id()
    {
        var res = await factory.CreateClient().GetAsync("/health");

        var traceId = Assert.Single(res.Headers.GetValues("X-Trace-Id"));
        Assert.Matches("^[0-9a-f]{32}$", traceId);
    }

    [Fact]
    public async Task An_incoming_traceparent_is_continued()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("traceparent", TraceParent);

        var res = await factory.CreateClient().SendAsync(request);

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", Assert.Single(res.Headers.GetValues("X-Trace-Id")));
    }

    [Fact]
    public async Task Each_request_without_a_traceparent_starts_its_own_trace()
    {
        var client = factory.CreateClient();

        var first = (await client.GetAsync("/health")).Headers.GetValues("X-Trace-Id").Single();
        var second = (await client.GetAsync("/health")).Headers.GetValues("X-Trace-Id").Single();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Browsers_may_read_the_trace_id()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://felinoir.it");

        var res = await factory.CreateClient().SendAsync(request);

        Assert.Contains("X-Trace-Id", res.Headers.GetValues("Access-Control-Expose-Headers"));
    }
}
