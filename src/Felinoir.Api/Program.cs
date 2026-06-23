using System.Text.Json;
using System.Text.Json.Serialization;
using Felinoir.Api.Endpoints;
using Felinoir.Application;
using Felinoir.Infrastructure;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

// Load .env for local dev / IDE runs. No-op when the file is absent (CI/prod),
// where the real environment already exports DATABASE_URL etc.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"] ?? "3001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// OpenAPI document describing every endpoint, surfaced through the Scalar UI.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Felinoir API",
            Version = "v1",
            Description =
                "Cinema backend for felinoir.it — films, screenings and cinemas in Rome, "
                + "plus the Felix AI chat assistant. Public, unauthenticated, CORS open to all origins.",
        };
        return Task.CompletedTask;
    });
});

// CORS: all origins, per spec (public, unauthenticated API).
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// camelCase JSON; null/optional fields omitted to match the spec's "?" optionals.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

app.UseCors();

// OpenAPI JSON at /openapi/v1.json; interactive Scalar reference at /scalar.
app.MapOpenApi();
app.MapScalarApiReference(options => options
    .WithTitle("Felinoir API")
    .WithTheme(ScalarTheme.Purple));

app.MapHealthEndpoints();
app.MapMovieEndpoints();
app.MapCinemaEndpoints();
app.MapScreeningEndpoints();
app.MapFelixEndpoints();

app.Run();

// Exposed so the integration test project can drive the app via WebApplicationFactory.
public partial class Program;
