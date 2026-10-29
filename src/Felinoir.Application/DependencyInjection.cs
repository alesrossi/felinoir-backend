using Felinoir.Application.Felix;
using Felinoir.Application.Lobbies;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felinoir.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICorpusBuilder, CorpusBuilder>();
        services.AddScoped<ILobbyService, LobbyService>();
        services.TryAddSingleton(TimeProvider.System);

        // Request validators, run by ValidationBehavior. The mediator itself (handlers and the
        // pipeline) is registered by the host, where its source generator runs.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Scoped);

        // Felix readiness state is process-wide; the service that uses it is per-request.
        services.AddSingleton<FelixState>();
        services.AddScoped<IFelixService, FelixService>();

        return services;
    }
}
