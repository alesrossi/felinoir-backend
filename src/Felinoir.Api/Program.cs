using System.Text.Json;
using System.Text.Json.Serialization;
using Felinoir.Api.Endpoints;
using Felinoir.Api.Errors;
using Felinoir.Api.Observability;
using Felinoir.Application;
using Felinoir.Application.Common.Behaviors;
using Felinoir.Application.Features.Felix;
using Felinoir.Infrastructure;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

// Load .env for local dev / IDE runs. No-op when the file is absent (CI/prod),
// where the real environment already exports DATABASE_URL etc. NoClobber: variables that are
// already set win over .env.
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Captive dependencies (a singleton holding a scoped service) and unresolvable registrations fail
// at startup in every environment, not only in Development.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.AddObservability();
builder.Services.AddProblemResponses();

var port = builder.Configuration["PORT"] ?? "3001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.Configure<FelixOptions>(builder.Configuration.GetSection(FelixOptions.Section));

// Handlers are found at compile time by the source generator. Scoped, because handlers use the
// request's DbContext. The behaviours run in this order around every handler (rework plan, step 3).
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(Felinoir.Application.DependencyInjection)];
    options.PipelineBehaviors =
    [
        typeof(LoggingBehavior<,>),
        typeof(ValidationBehavior<,>),
        typeof(ConflictRetryBehavior<,>),
        typeof(UnitOfWorkBehavior<,>),
    ];
});

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
                + "plus the Felix AI chat assistant and shareable screening lobbies. Public, unauthenticated, CORS open to all origins.",
        };
        return Task.CompletedTask;
    });
});

// CORS: all origins, per spec (public, unauthenticated API).
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders(ObservabilityExtensions.TraceIdHeader)));

// camelCase JSON; null/optional fields omitted to match the spec's "?" optionals.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

app.UseTraceIdHeader();
app.UseSerilogRequestLogging(options => options.GetLevel = RequestLogLevel.For);
app.UseProblemResponses();
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
app.MapLobbyEndpoints();

app.Run();

// Exposed so the integration test project can drive the app via WebApplicationFactory.
public partial class Program;
