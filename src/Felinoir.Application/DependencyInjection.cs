using Felinoir.Application.Cinemas;
using Felinoir.Application.Felix;
using Felinoir.Application.Movies;
using Felinoir.Application.Screenings;
using Microsoft.Extensions.DependencyInjection;

namespace Felinoir.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<ICinemaService, CinemaService>();
        services.AddScoped<IScreeningService, ScreeningService>();
        services.AddScoped<ICorpusBuilder, CorpusBuilder>();

        // Felix readiness state is process-wide; the service that uses it is per-request.
        services.AddSingleton<FelixState>();
        services.AddScoped<IFelixService, FelixService>();

        return services;
    }
}
