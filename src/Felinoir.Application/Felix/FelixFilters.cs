namespace Felinoir.Application.Felix;

/// <summary>
/// Active UI filters from felinoir.it, sent alongside a chat message. Formatted into a
/// plain-text context block by <see cref="FelixFilterFormatter"/> and appended to the
/// last user message.
/// </summary>
public sealed record FelixFilters(
    IReadOnlyList<string>? Cinemas = null,
    IReadOnlyList<string>? Dates = null,
    IReadOnlyList<string>? Times = null,
    IReadOnlyList<string>? Genres = null,
    bool? Ov = null,
    string? Q = null);
