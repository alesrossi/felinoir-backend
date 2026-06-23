using Felinoir.Application.Felix;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felinoir.IntegrationTests.Support;

/// <summary>
/// Boots the real API on an in-memory TestServer for endpoint tests that don't need a
/// database (validation, OpenAPI, the Felix 503 path).
///
/// The app boots fully, so <c>AddInfrastructure</c> parses <c>DATABASE_URL</c>: a
/// parseable dummy is provided via a real environment variable (the only configuration
/// source reliably applied before Program reads it under the minimal hosting model).
/// These tests never connect to the database.
///
/// Felix is swapped for an unconfigured stub so the 503 branch is deterministic
/// regardless of whether a real GEMINI_API_KEY is present in the environment.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    static TestApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "DATABASE_URL", "Host=localhost;Port=5432;Database=felinoir;Username=u;Password=p");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFelixService>();
            services.AddScoped<IFelixService, UnconfiguredFelix>();
        });
    }

    private sealed class UnconfiguredFelix : IFelixService
    {
        public bool IsConfigured => false;

        public Task<string> GenerateAsync(
            IReadOnlyList<ChatMessage> messages, string? filterContext = null, CancellationToken ct = default) =>
            throw new InvalidOperationException("Felix is intentionally unconfigured in tests.");
    }
}
