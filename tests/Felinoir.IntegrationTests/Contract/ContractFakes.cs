using System.Globalization;
using System.Text;
using Felinoir.Application.Felix;

namespace Felinoir.IntegrationTests.Contract;

/// <summary>A clock frozen at <see cref="Now"/>, which a scenario may move forward.</summary>
internal sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Stands in for Gemini and writes down every call, so a snapshot pins exactly what Felix sends
/// (corpus, messages with the filter context) as well as what the endpoint returns.
/// </summary>
internal sealed class RecordingGeminiClient : IGeminiClient
{
    /// <summary>A message with this content makes generation throw.</summary>
    public const string FailTrigger = "<fail>";

    private readonly StringBuilder _log = new();

    public bool IsConfigured { get; set; } = true;

    /// <summary>When set, cache creation succeeds with this reference; otherwise Felix goes inline.</summary>
    public GeminiCacheRef? CacheToCreate { get; set; }

    public Task<GeminiCacheRef?> CreateCacheAsync(string corpus, CancellationToken ct = default)
    {
        _log.AppendLine(CultureInfo.InvariantCulture, $"CreateCache -> {CacheToCreate?.Name ?? "null (caching unavailable)"}");
        _log.AppendLine("corpus:").AppendLine(corpus).AppendLine();
        return Task.FromResult(CacheToCreate);
    }

    public Task<string?> GenerateWithCacheAsync(
        IReadOnlyList<ChatMessage> messages, string cacheName, CancellationToken ct = default)
    {
        _log.AppendLine(CultureInfo.InvariantCulture, $"GenerateWithCache({cacheName})");
        AppendMessages(messages);
        return Task.FromResult<string?>(Reply(messages));
    }

    public Task<string> GenerateInlineAsync(
        IReadOnlyList<ChatMessage> messages, string corpus, CancellationToken ct = default)
    {
        _log.AppendLine(CultureInfo.InvariantCulture, $"GenerateInline(corpus: {corpus.Length} chars)");
        AppendMessages(messages);
        return Task.FromResult(Reply(messages));
    }

    /// <summary>Returns the calls recorded since the last call to this method.</summary>
    public string TakeLog()
    {
        var text = _log.Length == 0 ? "(no Gemini calls)" : _log.ToString();
        _log.Clear();
        return text;
    }

    private void AppendMessages(IReadOnlyList<ChatMessage> messages)
    {
        foreach (var m in messages)
            _log.AppendLine(CultureInfo.InvariantCulture, $"  [{m.Role}] {m.Content.Replace("\n", "\n    ")}");
    }

    private static string Reply(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(m => m.Content == FailTrigger)
            ? throw new HttpRequestException("Gemini exploded")
            : $"Risposta di Felix al messaggio {messages.Count}.";
}
