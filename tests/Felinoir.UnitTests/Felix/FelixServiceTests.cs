using Felinoir.Application.Felix;
using Felinoir.Domain.Entities;
using Felinoir.Infrastructure.Persistence;
using Felinoir.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Felinoir.UnitTests.Felix;

public class FelixServiceTests
{
    private static readonly DateTimeOffset FarFuture = DateTimeOffset.UtcNow.AddHours(24);

    private sealed class StubCorpus(string corpus) : ICorpusBuilder
    {
        public int Calls { get; private set; }
        public Task<string> BuildAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(corpus);
        }
    }

    private static FelixService NewService(
        ApplicationDbContext db, Mock<IGeminiClient> gemini, ICorpusBuilder corpus, FelixState? state = null) =>
        new(state ?? new FelixState(), gemini.Object, corpus, db);

    private static Mock<IGeminiClient> ConfiguredGemini()
    {
        var m = new Mock<IGeminiClient>();
        m.SetupGet(g => g.IsConfigured).Returns(true);
        return m;
    }

    private static List<ChatMessage> UserMsg(string content) => [new ChatMessage("user", content)];

    [Fact]
    public async Task Cold_start_creates_cache_persists_it_and_replies_from_cache()
    {
        using var db = InMemoryDb.Create();
        var gemini = ConfiguredGemini();
        gemini.Setup(g => g.CreateCacheAsync("CORPUS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiCacheRef("cachedContents/abc", FarFuture));
        gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), "cachedContents/abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cached reply");

        var reply = await NewService(db, gemini, new StubCorpus("CORPUS")).GenerateAsync(UserMsg("ciao"));

        Assert.Equal("cached reply", reply);
        gemini.Verify(g => g.CreateCacheAsync("CORPUS", It.IsAny<CancellationToken>()), Times.Once);
        var row = await db.FelixCache.SingleAsync();
        Assert.Equal("cachedContents/abc", row.CacheName);
    }

    [Fact]
    public async Task Falls_back_to_inline_when_cache_creation_unavailable()
    {
        using var db = InMemoryDb.Create();
        var gemini = ConfiguredGemini();
        gemini.Setup(g => g.CreateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeminiCacheRef?)null);
        gemini.Setup(g => g.GenerateInlineAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), "CORPUS", It.IsAny<CancellationToken>()))
            .ReturnsAsync("inline reply");

        var reply = await NewService(db, gemini, new StubCorpus("CORPUS")).GenerateAsync(UserMsg("ciao"));

        Assert.Equal("inline reply", reply);
        Assert.Empty(await db.FelixCache.ToListAsync());
        gemini.Verify(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_inline_when_cache_expires_mid_flight()
    {
        using var db = InMemoryDb.Create();
        var gemini = ConfiguredGemini();
        gemini.Setup(g => g.CreateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiCacheRef("cachedContents/abc", FarFuture));
        gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null); // 404 — cache gone
        gemini.Setup(g => g.GenerateInlineAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("inline reply");

        var state = new FelixState();
        var reply = await NewService(db, gemini, new StubCorpus("CORPUS"), state).GenerateAsync(UserMsg("ciao"));

        Assert.Equal("inline reply", reply);
        Assert.Null(state.Cache);
    }

    [Fact]
    public async Task Reuses_cache_persisted_in_db_without_recreating()
    {
        using var db = InMemoryDb.Create();
        db.FelixCache.Add(new FelixCache
        {
            Id = 1,
            CacheName = "cachedContents/from-db",
            ExpiresAt = FarFuture.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            CreatedAt = "2026-01-01T00:00:00Z",
        });
        await db.SaveChangesAsync();

        var gemini = ConfiguredGemini();
        gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), "cachedContents/from-db", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cached reply");

        var corpus = new StubCorpus("CORPUS");
        var reply = await NewService(db, gemini, corpus).GenerateAsync(UserMsg("ciao"));

        Assert.Equal("cached reply", reply);
        gemini.Verify(g => g.CreateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, corpus.Calls); // corpus never built when DB cache is reused
    }

    [Fact]
    public async Task Appends_filter_context_to_last_user_message()
    {
        using var db = InMemoryDb.Create();
        var gemini = ConfiguredGemini();
        gemini.Setup(g => g.CreateCacheAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiCacheRef("cachedContents/abc", FarFuture));

        IReadOnlyList<ChatMessage>? captured = null;
        gemini.Setup(g => g.GenerateWithCacheAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ChatMessage>, string, CancellationToken>((msgs, _, _) => captured = msgs)
            .ReturnsAsync("ok");

        await NewService(db, gemini, new StubCorpus("CORPUS"))
            .GenerateAsync(UserMsg("Cosa c'è stasera?"), filterContext: "[Note: filters]\n- Genres: Horror");

        Assert.NotNull(captured);
        Assert.Equal("user", captured![^1].Role);
        Assert.Contains("Cosa c'è stasera?", captured[^1].Content);
        Assert.Contains("[Note: filters]", captured[^1].Content);
    }

    [Fact]
    public void IsConfigured_reflects_gemini_client()
    {
        using var db = InMemoryDb.Create();
        var gemini = new Mock<IGeminiClient>();
        gemini.SetupGet(g => g.IsConfigured).Returns(false);

        Assert.False(NewService(db, gemini, new StubCorpus("x")).IsConfigured);
    }
}
