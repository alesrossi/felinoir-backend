namespace Felinoir.Application.Common.Interfaces;

/// <summary>
/// Static cinema metadata not stored in the DB, sourced from <c>cinemas.yml</c>.
/// Used by the Felix corpus builder to tag open-air venues and to fall back on a
/// display name when a cinema id isn't present in the database.
/// </summary>
public interface ICinemaConfigProvider
{
    IReadOnlyList<CinemaConfigEntry> GetAll();
}

/// <summary>One venue entry from <c>cinemas.yml</c>. The <see cref="Id"/> matches <c>cinema.id</c> in the DB.</summary>
public sealed record CinemaConfigEntry(string Id, string Name, bool OpenAir);
