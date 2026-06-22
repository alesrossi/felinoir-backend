using System.Globalization;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Felix;

/// <summary>
/// Felix orchestration, ported from the Node <c>gemini.ts</c> module.
///
/// Readiness (<see cref="EnsureReadyAsync"/>) is checked in order: in-memory cache →
/// in-memory inline corpus → <c>felix_cache</c> DB row → cold-start build. Cold start
/// builds the corpus and tries to create a Gemini context cache; if caching is
/// unavailable it falls back to inline context. Generation prefers the cache and falls
/// through to inline if the cache has expired mid-flight.
/// </summary>
public sealed class FelixService : IFelixService
{
    private static readonly TimeSpan CacheLead = TimeSpan.FromHours(1);
    private static readonly TimeSpan InlineTtl = TimeSpan.FromHours(24);
    private const int CacheRowId = 1;

    private readonly FelixState _state;
    private readonly IGeminiClient _gemini;
    private readonly ICorpusBuilder _corpus;
    private readonly IApplicationDbContext _db;

    public FelixService(FelixState state, IGeminiClient gemini, ICorpusBuilder corpus, IApplicationDbContext db)
    {
        _state = state;
        _gemini = gemini;
        _corpus = corpus;
        _db = db;
    }

    public bool IsConfigured => _gemini.IsConfigured;

    public async Task<string> GenerateAsync(
        IReadOnlyList<ChatMessage> messages, string? filterContext = null, CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct);

        var contents = WithFilterContext(messages, filterContext);

        var cache = _state.Cache;
        if (cache is not null)
        {
            var reply = await _gemini.GenerateWithCacheAsync(contents, cache.Name, ct);
            if (reply is not null) return reply;
            _state.Cache = null; // cache expired mid-flight — fall through to inline
        }

        var corpus = _state.Inline?.Corpus ?? await _corpus.BuildAsync(ct);
        return await _gemini.GenerateInlineAsync(contents, corpus, ct);
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (IsCacheFresh() || IsInlineFresh()) return;

        await _state.Gate.WaitAsync(ct);
        try
        {
            // Re-check now that we hold the gate — another request may have initialised.
            if (IsCacheFresh() || IsInlineFresh()) return;

            var fromDb = await LoadCacheFromDbAsync(ct);
            if (fromDb is not null)
            {
                _state.Cache = fromDb;
                _state.Inline = null;
                return;
            }

            // Cold start — build the corpus and attempt to cache it.
            var corpus = await _corpus.BuildAsync(ct);
            var created = await _gemini.CreateCacheAsync(corpus, ct);

            if (created is not null)
            {
                await SaveCacheToDbAsync(created, ct);
                _state.Cache = created;
                _state.Inline = null;
            }
            else
            {
                _state.Cache = null;
                _state.Inline = new InlineCorpus(corpus, DateTimeOffset.UtcNow);
            }
        }
        finally
        {
            _state.Gate.Release();
        }
    }

    private bool IsCacheFresh() =>
        _state.Cache is { } c && c.ExpiresAt > DateTimeOffset.UtcNow + CacheLead;

    private bool IsInlineFresh() =>
        _state.Inline is { } i && DateTimeOffset.UtcNow - i.BuiltAt < InlineTtl;

    private async Task<GeminiCacheRef?> LoadCacheFromDbAsync(CancellationToken ct)
    {
        try
        {
            var row = await _db.FelixCache.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == CacheRowId, ct);
            if (row is null) return null;

            if (!TryParseTimestamp(row.ExpiresAt, out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow)
                return null;

            return new GeminiCacheRef(row.CacheName, expiresAt);
        }
        catch (Exception)
        {
            // A DB hiccup shouldn't block Felix — fall through to a cold start.
            return null;
        }
    }

    private async Task SaveCacheToDbAsync(GeminiCacheRef cache, CancellationToken ct)
    {
        try
        {
            var expiresAt = FormatTimestamp(cache.ExpiresAt);
            var existing = await _db.FelixCache.FirstOrDefaultAsync(f => f.Id == CacheRowId, ct);
            if (existing is null)
            {
                _db.FelixCache.Add(new FelixCache
                {
                    Id = CacheRowId,
                    CacheName = cache.Name,
                    ExpiresAt = expiresAt,
                    CreatedAt = FormatTimestamp(DateTimeOffset.UtcNow),
                });
            }
            else
            {
                existing.CacheName = cache.Name;
                existing.ExpiresAt = expiresAt;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // Persistence is best-effort; the in-memory cache still works this run.
        }
    }

    /// <summary>
    /// Appends the filter context to the last user message so it reads as one coherent
    /// turn, rather than injecting a fake user/model exchange that confuses refusal logic.
    /// </summary>
    private static IReadOnlyList<ChatMessage> WithFilterContext(
        IReadOnlyList<ChatMessage> messages, string? filterContext)
    {
        if (string.IsNullOrEmpty(filterContext) || messages.Count == 0) return messages;

        var last = messages[^1];
        if (last.Role != "user") return messages;

        var updated = messages.ToList();
        updated[^1] = last with { Content = $"{last.Content}\n\n{filterContext}" };
        return updated;
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static bool TryParseTimestamp(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}
