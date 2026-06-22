using System.Text.Json;
using System.Text.Json.Serialization;
using Felinoir.Api.Endpoints;
using Felinoir.Application;
using Felinoir.Infrastructure;

// Load .env for local dev / IDE runs. No-op when the file is absent (CI/prod),
// where the real environment already exports DATABASE_URL etc.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"] ?? "3001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

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

app.MapHealthEndpoints();
app.MapMovieEndpoints();
app.MapCinemaEndpoints();
app.MapScreeningEndpoints();
app.MapFelixEndpoints();

app.Run();

// Exposed so the integration test project can drive the app via WebApplicationFactory.
public partial class Program;
