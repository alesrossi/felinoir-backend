namespace Felinoir.Application.Felix;

/// <summary>
/// Process-wide Felix readiness state, shared across requests as a singleton (the Node
/// backend kept this on <c>globalThis</c>). Holds either an active Gemini context-cache
/// reference or an inline corpus fallback, plus a gate that serialises (de-duplicates)
/// concurrent initialisation so the corpus is built/cached at most once at a time.
/// </summary>
public sealed class FelixState
{
    /// <summary>Serialises cold-start initialisation across concurrent requests.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Active Gemini context cache, or null when falling back to inline context.</summary>
    public GeminiCacheRef? Cache { get; set; }

    /// <summary>Inline corpus fallback, used when context caching is unavailable.</summary>
    public InlineCorpus? Inline { get; set; }
}

/// <summary>Corpus held in memory for inline use, with the time it was built (for 24 h refresh).</summary>
public sealed record InlineCorpus(string Corpus, DateTimeOffset BuiltAt);
