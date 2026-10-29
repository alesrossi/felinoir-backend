using Felinoir.Application.Cities;
using Felinoir.Application.Common;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Common;
using Felinoir.Domain.Entities;
using Felinoir.Domain.ValueObjects;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Felinoir.Application.Features.Felix;

/// <summary>
/// A question to Felix: the conversation so far (the last turn is the user's question), the active
/// UI filters, and the city whose cinemas Felix answers about (a city id or name; Rome by default).
/// A command rather than a query: answering may build and cache the city's corpus.
/// </summary>
public sealed record AskFelixCommand(string? City, IReadOnlyList<FelixMessageInput?>? Messages, FelixFilters? Filters)
    : ICommand<Result<FelixReply>>;

/// <summary>A conversation turn as it arrives; <see cref="ChatTurn.TryParse"/> makes it a <see cref="ChatTurn"/>.</summary>
public sealed record FelixMessageInput(string? Role, string? Content);

public sealed class AskFelixValidator : AbstractValidator<AskFelixCommand>
{
    public AskFelixValidator()
    {
        RuleFor(c => c.Messages).NotEmpty()
            .WithErrorCode(FelixErrors.InvalidRequest.Code)
            .WithMessage("A conversation has at least one message.");

        RuleForEach(c => c.Messages)
            .Must(m => m is not null && ChatTurn.TryParse(m.Role, m.Content, out _))
            .WithErrorCode(FelixErrors.InvalidRequest.Code)
            .WithMessage("Every message has a role, \"user\" or \"assistant\", and some content.");
    }
}

public sealed partial class AskFelixHandler(
    IGeminiClient gemini,
    ICorpusBuilder corpus,
    IFelixContextStore store,
    FelixContextRegistry registry,
    IReadDbContext db,
    TimeProvider time,
    IOptions<FelixOptions> options,
    ILogger<AskFelixHandler> logger) : ICommandHandler<AskFelixCommand, Result<FelixReply>>
{
    private TimeOnly DailyRebuildAt => options.Value.DailyRebuildAt;

    public async ValueTask<Result<FelixReply>> Handle(AskFelixCommand command, CancellationToken cancellationToken)
    {
        if (!gemini.IsConfigured) return FelixErrors.NotConfigured;

        var cityId = command.City is null
            ? City.RomeId
            : await db.Cities.FindIdAsync(command.City, cancellationToken);
        if (cityId is null) return FelixErrors.InvalidRequest;

        // The validator has checked every message.
        var turns = command.Messages!.Select(m => ChatTurn.TryParse(m!.Role, m.Content, out var turn)
            ? turn
            : throw new InvalidOperationException("AskFelixValidator lets only valid turns through.")).ToList();
        var today = DateOnly.FromDateTime(ScreeningTime.FromInstant(time.GetUtcNow()).WallClock);
        var question = WithFilterContext(turns, FelixFilterFormatter.Format(command.Filters, today));

        try
        {
            var state = registry.For(cityId);
            var context = await EnsureReadyAsync(cityId, state, cancellationToken);
            return new FelixReply(await AnswerAsync(cityId, state, context, question, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, cityId);
            return FelixErrors.Failed;
        }
    }

    /// <summary>
    /// A fresh context for the city: the one in memory, else the one persisted by an earlier process,
    /// else a new one (see <see cref="FelixReadiness"/>). Rebuilds happen once at a time per city.
    /// </summary>
    private async Task<FelixContext> EnsureReadyAsync(
        string cityId, FelixContextRegistry.CityState state, CancellationToken ct)
    {
        if (Fresh(state.Current) is { } current) return current;

        await state.Gate.WaitAsync(ct);
        try
        {
            // Another request may have got here first.
            if (Fresh(state.Current) is { } ready) return ready;

            var persisted = await store.LoadAsync(cityId, ct);
            if (persisted is not null
                && FelixReadiness.Decide(persisted, persistedLoaded: true, time.GetUtcNow(), DailyRebuildAt)
                    == FelixReadinessAction.UseCurrent)
            {
                return state.Current = persisted;
            }

            return state.Current = await RebuildAsync(cityId, ct);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private FelixContext? Fresh(FelixContext? context) =>
        FelixReadiness.Decide(context, persistedLoaded: false, time.GetUtcNow(), DailyRebuildAt)
            == FelixReadinessAction.UseCurrent ? context : null;

    /// <summary>Builds the corpus and caches it at Gemini, or keeps it inline when Gemini won't cache it.</summary>
    private async Task<FelixContext> RebuildAsync(string cityId, CancellationToken ct)
    {
        using var activity = FelinoirTelemetry.ActivitySource.StartActivity("FelixRebuild");
        activity?.SetTag("felinoir.city", cityId);

        var builtAt = time.GetUtcNow();
        var text = await corpus.BuildAsync(cityId, ct);
        var cache = await gemini.CreateCacheAsync(text, ct);

        if (cache is null)
        {
            LogRebuilt(logger, cityId, "inline");
            return FelixContext.Inline(text, builtAt);
        }

        var context = FelixContext.Cached(cache.Name, cache.ExpiresAt, builtAt);
        await store.SaveAsync(cityId, context, ct);
        LogRebuilt(logger, cityId, "cached");
        return context;
    }

    /// <summary>Asks Gemini with the context; a cache that expired meanwhile falls back to the corpus inline.</summary>
    private async Task<string> AnswerAsync(
        string cityId, FelixContextRegistry.CityState state, FelixContext context, IReadOnlyList<ChatTurn> turns,
        CancellationToken ct)
    {
        if (context.CacheName is { } cacheName)
        {
            var reply = await gemini.GenerateWithCacheAsync(turns, cacheName, ct);
            if (reply is not null) return reply;

            if (state.Current == context) state.Current = null;
        }

        var text = context.InlineCorpus ?? await corpus.BuildAsync(cityId, ct);
        return await gemini.GenerateInlineAsync(turns, text, ct);
    }

    /// <summary>
    /// Appends the filter context to the last user message so it reads as one coherent turn, rather
    /// than injecting a fake user/model exchange that confuses refusal logic.
    /// </summary>
    private static List<ChatTurn> WithFilterContext(List<ChatTurn> turns, string? filterContext)
    {
        if (string.IsNullOrEmpty(filterContext) || turns[^1].Speaker != Speaker.User) return turns;

        turns[^1] = turns[^1] with { Content = $"{turns[^1].Content}\n\n{filterContext}" };
        return turns;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Felix context for {City} rebuilt ({Kind})")]
    private static partial void LogRebuilt(ILogger logger, string city, string kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Felix could not answer for {City}")]
    private static partial void LogFailed(ILogger logger, Exception exception, string city);
}
