namespace Felinoir.Application.Felix;

/// <summary>
/// Builds the Felix knowledge corpus of one city: a Markdown document listing every film
/// with at least one future screening there, with metadata, ratings, keywords, plot and
/// screenings grouped by cinema. Fed to Gemini as cached or inline context.
/// </summary>
public interface ICorpusBuilder
{
    Task<string> BuildAsync(string cityId, CancellationToken ct = default);
}
