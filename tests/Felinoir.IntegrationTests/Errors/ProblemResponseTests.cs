using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Felinoir.Api.Errors;
using Felinoir.Domain.Common;
using Felinoir.Infrastructure.Persistence;
using Felinoir.IntegrationTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Felinoir.IntegrationTests.Errors;

/// <summary>
/// Error responses are RFC 7807 problem details that keep the <c>error</c> code clients read and
/// carry the request's <c>traceId</c>: business errors through <see cref="ErrorResults"/>,
/// unhandled exceptions through the exception handler, and empty framework 4xx responses.
/// </summary>
public class ProblemResponseTests(ProblemResponseTests.Factory factory) : IClassFixture<ProblemResponseTests.Factory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(ErrorKind.Validation, HttpStatusCode.BadRequest)]
    [InlineData(ErrorKind.NotFound, HttpStatusCode.NotFound)]
    [InlineData(ErrorKind.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ErrorKind.Unprocessable, HttpStatusCode.UnprocessableEntity)]
    [InlineData(ErrorKind.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task A_business_error_is_a_problem_with_its_code(ErrorKind kind, HttpStatusCode status)
    {
        var res = await _client.GetAsync($"/__test/error/{kind}");

        Assert.Equal(status, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("some_code", body.GetProperty("error").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal("the detail", body.GetProperty("detail").GetString());
        Assert.Equal(Single(res, "X-Trace-Id"), body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task An_unhandled_exception_is_a_500_problem()
    {
        var res = await _client.GetAsync("/__test/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("internal_error", body.GetProperty("error").GetString());
        Assert.Equal(Single(res, "X-Trace-Id"), body.GetProperty("traceId").GetString());
        Assert.DoesNotContain("boom", body.GetRawText()); // no exception details leak out
    }

    [Fact]
    public async Task An_unknown_route_is_a_404_problem()
    {
        var res = await _client.GetAsync("/no/such/route");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_malformed_body_is_a_400_problem()
    {
        using var content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json");

        var res = await _client.PostAsync("/felix/chat", content);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("bad_request", body.GetProperty("error").GetString());
    }

    private static string Single(HttpResponseMessage res, string header) =>
        Assert.Single(res.Headers.GetValues(header));

    /// <summary>The real API, plus two endpoints that fail on purpose, reached after the real pipeline.</summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        static Factory()
        {
            Environment.SetEnvironmentVariable(
                "DATABASE_URL", "Host=localhost;Port=5432;Database=felinoir;Username=u;Password=p");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Production, so failures are handled as they are live (no developer exception page).
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveHostedService<ExpiredLobbyCleanupService>();
                services.AddSingleton<IStartupFilter, FailingEndpoints>();
            });
        }
    }

    private sealed class FailingEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map("/__test/throw", b => b.Run(_ => throw new InvalidOperationException("boom")));
            app.Map("/__test/error", b => b.Run(context =>
            {
                var kind = Enum.Parse<ErrorKind>(context.Request.Path.Value!.Trim('/'));
                return new Error("some_code", kind, "the detail").ToProblem().ExecuteAsync(context);
            }));
        };
    }
}
