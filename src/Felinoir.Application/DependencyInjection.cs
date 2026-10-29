using Felinoir.Application.Features.Felix;
using Felinoir.Application.Features.Lobbies;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felinoir.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CorpusSnapshotLoader>();
        services.AddScoped<ICorpusBuilder, CorpusBuilder>();
        services.AddScoped<LobbyViews>();
        services.TryAddSingleton(TimeProvider.System);

        // Request validators, run by ValidationBehavior. The mediator itself (handlers and the
        // pipeline) is registered by the host, where its source generator runs.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Scoped);

        // Each city's Felix context is process-wide; the handler that uses it is per-request.
        services.AddSingleton<FelixContextRegistry>();
        services.AddOptions<FelixOptions>();

        return services;
    }
}
