using Felinoir.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Felinoir.Infrastructure.Persistence;

/// <summary>
/// Lets the <c>dotnet ef</c> tooling construct the context at design time without
/// booting the API. Loads <c>.env</c> so <c>DATABASE_URL</c> resolves the same way
/// it does at runtime. Note: <c>migrations add</c> never opens a connection.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.TraversePath().Load();

        var connectionString = DatabaseUrl.ToNpgsqlConnectionString(
            Environment.GetEnvironmentVariable("DATABASE_URL"));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }
}
