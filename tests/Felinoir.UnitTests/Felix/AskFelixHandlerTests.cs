using Felinoir.Application.Common.Interfaces;
using Felinoir.Application.Features.Felix;
using Felinoir.Domain.Common;
using Felinoir.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Felinoir.UnitTests.Felix;

/// <summary>The Felix handler with Gemini, the corpus and the store faked.</summary>
public class AskFelixHandlerTests
{
    /// <summary>2026-06-22 10:00 in Rome, after today's 05:00 rebuild time.</summary>
    private static readonly DateTimeOffset Now = new(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider _clock = new(Now);
    private readonly Mock<IGeminiClient> _gemini = new();
    private readonly StubCorpus _corpus = new();
    private readonly InMemoryStore _store = new();
    private readonly FelixContextRegistry _registry = new();

    public AskFelixHandlerTests() => _gemini.SetupGet(g => g.IsConfigured).Returns(true);

    private sealed class StubCorpus : ICorpusBuilder
    {
        public int Builds { get; private set; }

        public async Task<string> BuildAsync(string cityId, CancellationToken ct = default)
        {
            await Task.Yield();
            Builds++;
            return $"CORPUS {cityId} #{Builds}";
        }
    }

    private sealed class InMemoryStore : IFelixContextStore
    {
        public Dictionary<string, FelixContext> Rows { get; } = [];

        public Task<FelixContext?> LoadAsync(string cityId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.GetValueOrDefault(cityId));

        public Task SaveAsync(string cityId, FelixContext context, CancellationToken cancellationToken = default)
        {
            Rows[cityId] = context;
            return Task.CompletedTask;
        }
    }

    private AskFelixHandler Handler() => new(
        _gemini.Object, _corpus, _store, _registry, Mock.Of<IReadDbContext>(), _clock,
        Options.Create(new FelixOptions()), NullLogger<AskFelixHandler>.Instance);

    private static AskFelixCommand Ask(string question, FelixFilters? filters = null) =>
        new(null, [new FelixMessageInput("user", question)], filters);

    private void CacheCreationReturns(GeminiCacheRef? cache) =>
        _gemini.Setup(g => g.CreateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(cache);

    private void InlineReplies(string reply) =>
        _gemini.Setup(g => g.GenerateInlineAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reply);

    [Fact]
    public async Task Without_an_api_key_felix_is_unavailable()
    {
        _gemini.SetupGet(g => g.IsConfigured).Returns(false);

        var result = await Handler().Handle(Ask("ciao"), default);

        Assert.Same(FelixErrors.NotConfigured, result.Error);
        Assert.Equal(0, _corpus.Builds);
    }

    [Fact]
    public async Task A_cold_start_caches_the_corpus_persists_it_and_answers_from_the_cache()
    {
        CacheCreationReturns(new GeminiCacheRef("cachedContents/abc", Now.AddHours(24)));
        _gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "cachedContents/abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cached reply");

        var result = await Handler().Handle(Ask("ciao"), default);

        Assert.Equal("cached reply", result.Value.Reply);
        Assert.Equal("cachedContents/abc", _store.Rows["rome"].CacheName);
        Assert.Equal(Now, _store.Rows["rome"].BuiltAt);
        Assert.Equal(1, _corpus.Builds);
    }

    [Fact]
    public async Task Without_caching_the_corpus_goes_inline_and_is_not_persisted()
    {
        CacheCreationReturns(null);
        InlineReplies("inline reply");

        var first = await Handler().Handle(Ask("ciao"), default);
        var second = await Handler().Handle(Ask("ancora"), default);

        Assert.Equal("inline reply", first.Value.Reply);
        Assert.Equal("inline reply", second.Value.Reply);
        Assert.Equal(1, _corpus.Builds); // the inline corpus is reused
        Assert.Empty(_store.Rows);
        _gemini.Verify(g => g.GenerateInlineAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "CORPUS rome #1", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task A_cache_that_expired_meanwhile_falls_back_to_the_corpus_inline()
    {
        CacheCreationReturns(new GeminiCacheRef("cachedContents/gone", Now.AddHours(24)));
        _gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "cachedContents/gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        InlineReplies("inline reply");

        var result = await Handler().Handle(Ask("ciao"), default);

        Assert.Equal("inline reply", result.Value.Reply);
        Assert.Null(_registry.For("rome").Current); // forgotten, so the next question rebuilds
    }

    [Fact]
    public async Task A_cache_persisted_by_an_earlier_process_is_reused_without_rebuilding()
    {
        _store.Rows["rome"] = FelixContext.Cached("cachedContents/from-db", Now.AddHours(20), builtAt: Now.AddHours(-1));
        _gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "cachedContents/from-db", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cached reply");

        var result = await Handler().Handle(Ask("ciao"), default);

        Assert.Equal("cached reply", result.Value.Reply);
        Assert.Equal(0, _corpus.Builds);
        _gemini.Verify(g => g.CreateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_persisted_cache_built_before_the_nights_scrape_is_rebuilt()
    {
        // Built yesterday at 23:00 Rome time (21:00Z), before today's 05:00 rebuild time.
        _store.Rows["rome"] = FelixContext.Cached("cachedContents/old", Now.AddHours(20), builtAt: Now.AddHours(-11));
        CacheCreationReturns(new GeminiCacheRef("cachedContents/new", Now.AddHours(24)));
        _gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "cachedContents/new", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fresh reply");

        var result = await Handler().Handle(Ask("ciao"), default);

        Assert.Equal("fresh reply", result.Value.Reply);
        Assert.Equal("cachedContents/new", _store.Rows["rome"].CacheName);
    }

    [Fact]
    public async Task The_first_question_after_the_rebuild_time_rebuilds_a_context_from_the_day_before()
    {
        CacheCreationReturns(null);
        InlineReplies("reply");
        _clock.Now = new DateTimeOffset(2026, 6, 22, 2, 0, 0, TimeSpan.Zero); // 04:00 in Rome
        await Handler().Handle(Ask("ciao"), default);

        _clock.Now = new DateTimeOffset(2026, 6, 22, 3, 30, 0, TimeSpan.Zero); // 05:30 in Rome
        await Handler().Handle(Ask("ciao"), default);

        Assert.Equal(2, _corpus.Builds);
    }

    [Fact]
    public async Task Concurrent_cold_starts_build_the_corpus_once()
    {
        CacheCreationReturns(null);
        InlineReplies("reply");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Handler().Handle(Ask("ciao"), default).AsTask()));

        Assert.Equal(1, _corpus.Builds);
    }

    [Fact]
    public async Task The_filter_context_is_appended_to_the_last_user_message()
    {
        CacheCreationReturns(null);
        IReadOnlyList<ChatTurn>? sent = null;
        _gemini.Setup(g => g.GenerateInlineAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ChatTurn>, string, CancellationToken>((turns, _, _) => sent = turns)
            .ReturnsAsync("reply");

        await Handler().Handle(Ask("Cosa c'è?", new FelixFilters(Ov: true)), default);

        var last = Assert.Single(sent!);
        Assert.Equal(Speaker.User, last.Speaker);
        Assert.StartsWith("Cosa c'è?\n\n[Note: I currently have these filters active on felinoir.it]", last.Content);
        Assert.Contains("- Original version (no dubbing) only", last.Content);
    }

    [Fact]
    public async Task A_failure_to_answer_is_felixs_message_not_an_exception()
    {
        CacheCreationReturns(null);
        _gemini.Setup(g => g.GenerateInlineAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Gemini is down"));

        var result = await Handler().Handle(Ask("ciao"), default);

        Assert.Same(FelixErrors.Failed, result.Error);
        Assert.Equal(ErrorKind.Unexpected, result.Error!.Kind);
    }

    [Theory]
    [InlineData("user", "hi", true)]
    [InlineData("assistant", "hello", true)]
    [InlineData("system", "hi", false)]
    [InlineData("User", "hi", false)]
    [InlineData("user", "", false)]
    [InlineData(null, "hi", false)]
    public void A_turn_needs_an_exact_role_and_content(string? role, string? content, bool valid) =>
        Assert.Equal(valid, ChatTurn.TryParse(role, content, out _));
}
