using System.Linq.Expressions;
using Felinoir.Domain.Entities;

namespace Felinoir.Application.Features.Cinemas;

/// <summary>
/// A cinema as the API returns it. <see cref="City"/> is the name of the city the cinema is listed
/// under (<c>Roma</c>), which is what the field held before cities had their own table.
/// </summary>
public sealed record CinemaResponse
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Chain { get; init; }
    public string? Address { get; init; }
    public string City { get; init; } = default!;
    public string Country { get; init; } = default!;
    public string Website { get; init; } = default!;
    public string ScheduleUrl { get; init; } = default!;
    public string CreatedAt { get; init; } = default!;
    public string UpdatedAt { get; init; } = default!;

    /// <summary>The mapping, as an expression EF translates (joining the city for its name).</summary>
    public static readonly Expression<Func<Cinema, CinemaResponse>> Projection = c => new CinemaResponse
    {
        Id = c.Id,
        Name = c.Name,
        Chain = c.Chain,
        Address = c.Address,
        City = c.City!.Name,
        Country = c.Country,
        Website = c.Website,
        ScheduleUrl = c.ScheduleUrl,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };

    private static readonly Func<Cinema, CinemaResponse> Map = Projection.Compile();

    /// <summary>The same mapping in memory, for a cinema loaded with its <see cref="Cinema.City"/>.</summary>
    public static CinemaResponse From(Cinema cinema) => Map(cinema);
}
