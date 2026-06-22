namespace Felinoir.Application.Felix;

/// <summary>
/// Builds the Felix knowledge corpus: a Markdown document listing every film with at
/// least one future screening in Rome, with metadata, ratings, keywords, plot and
/// screenings grouped by cinema. Fed to Gemini as cached or inline context.
/// </summary>
public interface ICorpusBuilder
{
    Task<string> BuildAsync(CancellationToken ct = default);
}
