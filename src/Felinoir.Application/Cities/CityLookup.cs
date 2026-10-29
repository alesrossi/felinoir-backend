using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Cities;

public static class CityLookup
{
    /// <summary>
    /// The id of the city a <c>?city=</c> value names: its id (<c>rome</c>) or its display name
    /// (<c>Roma</c>), ignoring case. Null when no city matches.
    /// </summary>
    public static Task<string?> FindIdAsync(this IQueryable<City> cities, string city, CancellationToken ct = default)
    {
        var value = city.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL lower(); .NET cultures don't apply.
        return cities.AsNoTracking()
            .Where(c => c.Id.ToLower() == value || c.Name.ToLower() == value)
#pragma warning restore CA1304, CA1311, CA1862
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
    }
}
