using Felinoir.Domain.Entities;

namespace Felinoir.Application.Cinemas;

public interface ICinemaService
{
    /// <summary>Cinemas by name; with <paramref name="city"/>, only those listed under that city.</summary>
    Task<IReadOnlyList<Cinema>> GetAllAsync(string? city = null, CancellationToken ct = default);

    Task<Cinema?> GetByIdAsync(string id, CancellationToken ct = default);
}
