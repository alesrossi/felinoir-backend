using System.Linq.Expressions;
using Felinoir.Application.Features.Cinemas;
using Felinoir.Application.Features.Movies;
using Felinoir.Domain.Entities;

namespace Felinoir.Application.Features.Screenings;

/// <summary>
/// A screening as the API returns it. <see cref="Datetime"/> is the stored Italian wall-clock
/// text, unchanged. <see cref="Movie"/> and <see cref="Cinema"/> are only filled with
/// <c>withRelations</c> (and omitted from the JSON otherwise).
/// </summary>
public sealed record ScreeningResponse
{
    public required int Id { get; init; }
    public required int MovieId { get; init; }
    public required string CinemaId { get; init; }
    public required string Datetime { get; init; }
    public string? Hall { get; init; }
    public required bool Is3D { get; init; }
    public required bool IsOV { get; init; }
    public string? SubtitleLanguage { get; init; }
    public string? BookingUrl { get; init; }
    public string? Meta { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
    public MovieResponse? Movie { get; init; }
    public CinemaResponse? Cinema { get; init; }

    /// <summary>The screening alone, as an expression EF translates.</summary>
    public static readonly Expression<Func<Screening, ScreeningResponse>> Projection = s => new ScreeningResponse
    {
        Id = s.Id,
        MovieId = s.MovieId,
        CinemaId = s.CinemaId,
        Datetime = s.Datetime,
        Hall = s.Hall,
        Is3D = s.Is3D,
        IsOV = s.IsOV,
        SubtitleLanguage = s.SubtitleLanguage,
        BookingUrl = s.BookingUrl,
        Meta = s.Meta,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    private static readonly Func<Screening, ScreeningResponse> Map = Projection.Compile();

    /// <summary>A screening loaded with its movie and its cinema (and the cinema's city).</summary>
    public static ScreeningResponse WithRelations(Screening screening) => Map(screening) with
    {
        Movie = MovieResponse.From(screening.Movie!),
        Cinema = CinemaResponse.From(screening.Cinema!),
    };
}
