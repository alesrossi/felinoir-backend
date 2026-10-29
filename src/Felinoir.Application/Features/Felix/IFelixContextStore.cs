namespace Felinoir.Application.Features.Felix;

/// <summary>
/// Keeps each city's cached <see cref="FelixContext"/> across restarts (the <c>felix_cache</c> table),
/// so a new process reuses Gemini's cache instead of uploading the corpus again. Best effort: a
/// failure is logged and Felix carries on with what it has in memory.
/// </summary>
public interface IFelixContextStore
{
    /// <summary>The persisted context of the city, or null (none, or it couldn't be read).</summary>
    Task<FelixContext?> LoadAsync(string cityId, CancellationToken cancellationToken = default);

    /// <summary>Persists a cached context (inline ones are never persisted).</summary>
    Task SaveAsync(string cityId, FelixContext context, CancellationToken cancellationToken = default);
}
