using Mediator;

namespace Felinoir.Application.Features.Health;

/// <summary>Liveness: answers as long as the process runs; touches nothing else.</summary>
public sealed record GetHealthQuery : IQuery<HealthStatus>;

/// <summary><c>{ "ok": true }</c>.</summary>
public sealed record HealthStatus(bool Ok);

public sealed class GetHealthHandler : IQueryHandler<GetHealthQuery, HealthStatus>
{
    private static readonly HealthStatus Up = new(true);

    public ValueTask<HealthStatus> Handle(GetHealthQuery query, CancellationToken cancellationToken) => new(Up);
}
