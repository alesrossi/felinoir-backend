using Felinoir.Infrastructure;
using Felinoir.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// One-shot: brings the database on DATABASE_URL to the current schema, then exits. Runs before
// the API and the scrapers start (ADR 0001). Exit code 0 on success, 1 on failure, so compose's
// `service_completed_successfully` holds the other services back when it fails.

// Load .env for local runs. No-op when the file is absent (containers), where the real
// environment already exports DATABASE_URL. NoClobber: a variable that is already set wins over
// .env, so DATABASE_URL=... on the command line really targets that database.
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPersistence(builder.Configuration);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    await using var scope = host.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
    return 0;
}
catch (Exception ex)
{
    Log.MigrationFailed(logger, ex);
    return 1;
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Migration failed")]
    public static partial void MigrationFailed(ILogger logger, Exception exception);
}
