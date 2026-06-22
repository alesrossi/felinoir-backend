using Felinoir.Domain.Entities;

namespace Felinoir.Application.Cinemas;

public interface ICinemaService
{
    Task<IReadOnlyList<Cinema>> GetAllAsync(CancellationToken ct = default);

    Task<Cinema?> GetByIdAsync(string id, CancellationToken ct = default);
}
