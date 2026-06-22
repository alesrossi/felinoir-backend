using Felinoir.Domain.Entities;

namespace Felinoir.Application.Screenings;

public interface IScreeningService
{
    Task<IReadOnlyList<Screening>> GetAsync(ScreeningQuery query, CancellationToken ct = default);
}
