# Rework Explorations — DDD and Functional

> **Historical.** Written before the rework; the decisions taken are in `docs/REWORK-PLAN.md` and
> `docs/adr/`, and the code now follows `docs/ARCHITECTURE.md`. Several conclusions below (for
> example "no repositories") were deliberately overruled.

Two hypothetical reworks of the Felinoir backend, written as thought experiments.
**No code in this repository has been changed.** Everything below is a sketch of what
the same behaviour would look like under a different set of organising principles.

Read `SPEC.md` for the contract this backend must honour, `CLAUDE.md` for the rules the
current implementation deliberately follows, and `docs/ARCHITECTURE.md` for what exists
today.

---

## 0. Where we are now, honestly

The codebase is clean-architecture-shaped but CRUD-natured. Four projects, dependencies
pointing inward, services injected with `IApplicationDbContext`, endpoints that are three
lines long. It works, and for the three read-only resources it is close to the right
amount of structure.

The important observation before proposing anything: **this backend almost never writes.**

| Resource | Operations exposed | Invariants enforced here |
|---|---|---|
| Movies | list, by id, by slug | none |
| Cinemas | list, by id | none |
| Screenings | filtered list | none |
| Felix | chat turn | cache lifecycle, fallback, corpus shape, filter merging |

Writes happen in the scrapers, which live outside this repository. So `Movie`,
`Cinema` and `Screening` are, in this process, **read models**. There is no state
transition to protect, no command to validate, no aggregate boundary that a
transaction could violate.

That matters a lot for what follows: a faithful DDD rework is *not* mostly about the
movies table. The genuine domain complexity in this repo is concentrated in two places:

1. **The corpus** — `CorpusBuilder` is 200 lines of domain knowledge disguised as string
   formatting: what counts as "in programmazione", which title to display, the rating
   preference chain, how screenings group and get tagged.
2. **Felix readiness** — `FelixService.EnsureReadyAsync` is a small state machine with a
   concurrency gate, a persistence fallback, and a mid-flight expiry recovery path.

Both reworks below are judged mainly on how well they handle *those*, not on how nicely
they can wrap `GetByIdAsync`.

### The rules currently scattered across layers

These are the concrete things each rework has to find a home for. They are the spine of
the comparison.

| # | Rule | Lives today in |
|---|---|---|
| R1 | Display title = `tmdbTitle` else `title` | `CorpusBuilder.AppendMovie` (private static) |
| R2 | Rating preference IMDb → RT → Metacritic | `CorpusBuilder.AppendMovie` (inline `if`s) |
| R3 | "In programmazione" = has ≥1 screening at or after now | `CorpusBuilder.BuildAsync` (`byMovie.ContainsKey`) |
| R4 | Timestamps are fixed-width ISO 8601 UTC, so lexicographic compare == chronological compare | Implicit; a comment in `ScreeningQuery` |
| R5 | Screening identity is `(cinemaId, movieId, datetime)` | DB constraint only |
| R6 | `limit` clamped to `[0, 2000]` / `[0, 100000]`, `offset` ≥ 0 | `MovieEndpoints`, `ScreeningEndpoints` (endpoint layer) |
| R7 | Chat role is exactly `"user"` or `"assistant"` | `string` + a validating `if` in `FelixEndpoints.ParseMessages` |
| R8 | Cache is fresh if `expiresAt > now + 1h`; inline corpus fresh for 24h | `FelixService` private predicates |
| R9 | Open-air flag comes only from `cinemas.yml`, never the DB | `CorpusBuilder` (two dictionaries, by convention) |

R4 and R6 are the two that would actually bite. R4 is a correctness landmine: a single
timestamp written as `2026-06-22T20:30:00.000Z` or `+00:00` silently breaks range
filtering, and nothing in the type system says so. R6 means the API layer owns a business
constraint — call `IMovieService.GetAllAsync(int.MaxValue, -5)` from anywhere else and
the guard is gone.

---

# Rework A — Domain-Driven Design

## A.1 Guiding idea

Stop treating the database schema as the model. Introduce a domain model that speaks the
language of Roman cinema programming, keep it free of persistence concerns, and let the
existing 60-column `movies` table become a *persistence detail* mapped onto it rather than
the thing the whole system is shaped around.

Two bounded contexts, because they have genuinely different models of the same words:

- **Programming** (*Programmazione*) — the catalogue and schedule. A `Film`, a `Venue`,
  a `Showing`. Read-optimised, no commands in this process.
- **Concierge** (*Felix*) — the assistant. It does not care about 60 TMDB columns; it
  cares about a `FilmDossier` (the handful of facts worth telling a user) and a
  `Programme` (what is on, grouped by venue). It has real state and real behaviour.

The word **Screening** is a good example of why two contexts help. In Programming it is a
row with `is3D`, `bookingUrl` and `meta`. In Concierge it is `"Ven 22/06 20:30 [VO]"` at a
venue that may be open-air. Same real-world thing, two models, no shared class trying to
serve both.

## A.2 Project layout

```text
src/
  Felinoir.SharedKernel/              value objects used by both contexts
    Iso8601Utc.cs                     R4 lives here, once
    Slug.cs
    PageRequest.cs                    R6 lives here, once

  Felinoir.Programming.Domain/
    Films/ Film.cs  FilmId.cs  TitleSet.cs  RatingSet.cs  Runtime.cs
    Venues/ Venue.cs  VenueId.cs  City.cs
    Showings/ Showing.cs  ShowingId.cs  ShowingFormat.cs  DateRange.cs
    Programme/ Programme.cs           the "what's on" read model
  Felinoir.Programming.Application/
    IFilmCatalogue.cs  IVenueDirectory.cs  IShowingSchedule.cs
  Felinoir.Programming.Infrastructure/
    Persistence/ records + mapping profiles (MovieRecord -> Film)

  Felinoir.Concierge.Domain/
    Conversation/ Conversation.cs  Turn.cs  Speaker.cs  FilterContext.cs
    Knowledge/ Corpus.cs  FilmDossier.cs  CorpusComposer.cs
    Readiness/ FelixReadiness.cs      the aggregate that owns the state machine
  Felinoir.Concierge.Application/
    AnswerQuestionHandler.cs
  Felinoir.Concierge.Infrastructure/
    Gemini/  Persistence/

  Felinoir.Api/                       unchanged role: thin host, maps to DTOs
```

Note what is *not* there: no `Felinoir.Domain` holding all five entities together. The
split by context is the point.

## A.3 Value objects — where the landmines get defused

R4, the fixed-width-UTC assumption, becomes a type rather than a comment:

```csharp
namespace Felinoir.SharedKernel;

/// <summary>
/// An ISO 8601 UTC instant in the exact fixed-width form the schema stores:
/// "yyyy-MM-ddTHH:mm:ssZ". Fixed width is what makes ordinal comparison
/// equivalent to chronological comparison, so the invariant is enforced at
/// construction and nowhere else has to know.
/// </summary>
public readonly record struct Iso8601Utc : IComparable<Iso8601Utc>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ssZ";
    public string Value { get; }

    private Iso8601Utc(string value) => Value = value;

    public static Iso8601Utc From(DateTimeOffset instant) =>
        new(instant.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));

    public static Iso8601Utc Now() => From(DateTimeOffset.UtcNow);

    public static bool TryParse(string? raw, out Iso8601Utc result)
    {
        result = default;
        if (raw is null) return false;
        if (!DateTimeOffset.TryParseExact(raw, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return false;
        result = From(dto);
        return true;
    }

    // Ordinal, not culture-aware: this is the whole reason the type exists.
    public int CompareTo(Iso8601Utc other) => string.CompareOrdinal(Value, other.Value);
    public static bool operator <(Iso8601Utc a, Iso8601Utc b) => a.CompareTo(b) < 0;
    public static bool operator >(Iso8601Utc a, Iso8601Utc b) => a.CompareTo(b) > 0;

    public DateOnly Date => DateOnly.Parse(Value[..10], CultureInfo.InvariantCulture);
    public override string ToString() => Value;
}
```

An EF `ValueConverter` maps it to the TEXT column, so the schema is untouched and the
`string`-typed properties disappear from the model. The API still serialises `Value`, so
the wire contract is byte-identical.

R6 stops being an endpoint concern:

```csharp
public readonly record struct PageRequest
{
    public int Limit { get; }
    public int Offset { get; }

    private PageRequest(int limit, int offset) => (Limit, Offset) = (limit, offset);

    /// <summary>Clamps rather than rejects — matches today's permissive API behaviour.</summary>
    public static PageRequest Of(int? limit, int? offset, int defaultLimit, int maxLimit) =>
        new(Math.Clamp(limit ?? defaultLimit, 0, maxLimit), Math.Max(offset ?? 0, 0));

    public static PageRequest Films(int? limit, int? offset) => Of(limit, offset, 500, 2000);
    public static PageRequest Showings(int? limit, int? offset) => Of(limit, offset, 50_000, 100_000);
}
```

Now `IFilmCatalogue.List(PageRequest)` is unbypassable — there is no overload taking two
loose `int`s.

R1 and R2 become the behaviour of the value objects that own them:

```csharp
/// <summary>
/// The several titles a film carries. R1: the display title prefers the TMDB
/// Italian title over the scraped one; the alternates are whatever actually differs.
/// </summary>
public sealed record TitleSet(string Scraped, string? Tmdb, string? Original, string? English)
{
    public string Display => FirstNonEmpty(Tmdb, Scraped) ?? Scraped;

    public IReadOnlyList<string> Alternates
    {
        get
        {
            var alts = new List<string>();
            var original = FirstNonEmpty(Original);
            var english  = FirstNonEmpty(English);
            if (original is not null && original != Display) alts.Add(original);
            if (english  is not null && english != Display && english != original) alts.Add($"EN: {english}");
            return alts;
        }
    }

    private static string? FirstNonEmpty(params string?[] vs) =>
        vs.FirstOrDefault(v => !string.IsNullOrEmpty(v));
}

/// <summary>R2: the house preference order is IMDb, then Rotten Tomatoes, then Metacritic.</summary>
public sealed record RatingSet(string? Imdb, string? RottenTomatoes, string? Metacritic, string? Tmdb)
{
    public IEnumerable<(string Source, string Score)> InPreferenceOrder()
    {
        if (!string.IsNullOrEmpty(Imdb))            yield return ("IMDB", Imdb);
        if (!string.IsNullOrEmpty(RottenTomatoes))  yield return ("RT", RottenTomatoes);
        if (!string.IsNullOrEmpty(Metacritic))      yield return ("MC", Metacritic);
    }

    /// <summary>Per SPEC: TMDB is only ever a last resort, never mixed in with the others.</summary>
    public (string Source, string Score)? Headline =>
        InPreferenceOrder().Cast<(string, string)?>().FirstOrDefault()
        ?? (string.IsNullOrEmpty(Tmdb) ? null : ("TMDB", Tmdb));
}
```

`CorpusBuilder` currently owns both of these as private statics, which means nothing else
can reuse them and no unit test can reach them directly. Here they are public, tiny and
independently testable.

## A.4 The Film aggregate and the ACL over TMDB

```csharp
public sealed class Film
{
    public FilmId Id { get; }
    public TitleSet Titles { get; }
    public RatingSet Ratings { get; }
    public Runtime? Runtime { get; }
    public Credits Credits { get; }            // director, writer, cast
    public Classification Classification { get; } // genres, certificate, language, countries
    public Slug? Slug { get; }
    public Iso8601Utc AddedAt { get; }

    // ... constructor enforcing: title non-empty, slug well-formed if present
}
```

The 60 TMDB columns do not appear. They are an **upstream model** and get an
anti-corruption layer in Infrastructure:

```csharp
internal static class MovieRecordMapper
{
    public static Film ToDomain(MovieRecord r) => new(
        new FilmId(r.Id),
        new TitleSet(r.Title, r.TmdbTitle, r.OriginalTitle ?? r.TmdbOriginalTitle, r.TmdbTitleEn),
        new RatingSet(r.ImdbRating, r.RtRating, r.MetacriticScore, r.TmdbRating),
        Runtime.FromMinutes(r.DurationMinutes),
        new Credits(r.Director, r.Writer, r.Actors),
        new Classification(
            Genres: r.TmdbGenre ?? r.Genres,
            Certificate: r.ContentRating,
            OriginalLanguage: r.TmdbLanguage,
            // TMDB JSON blobs are parsed *here*, at the boundary, never in the domain.
            Countries: TmdbJson.Names(r.TmdbProductionCountries),
            Keywords: TmdbJson.Names(r.TmdbKeywords),
            Collection: TmdbJson.Name(r.TmdbCollection)),
        Slug.TryFrom(r.Slug, out var s) ? s : null,
        Iso8601Utc.TryParse(r.CreatedAt, out var c) ? c : Iso8601Utc.Now());
}
```

This is the single largest win available in this codebase and the single largest cost.
The win: the domain stops knowing that `tmdbProductionCountries` is a JSON string. The
cost: **the API must keep returning all 60 fields verbatim** (`SPEC.md` is explicit —
blobs are returned as strings, not parsed). So the API layer needs a separate read path
that projects `MovieRecord` straight to the wire DTO, bypassing the domain entirely.

That is not a flaw, it is CQRS, and it should be stated plainly: **DDD here means
accepting two models of a movie — a rich `Film` for the Concierge, and a flat
`MovieResponse` for the wire.** Pretending one class can be both is how the current
design ended up with `Movie` having 60 properties and a `[JsonIgnore]` navigation.

## A.5 Showing, and identity done properly

```csharp
public sealed class Showing
{
    public ShowingId Id { get; }
    public FilmId FilmId { get; }
    public VenueId VenueId { get; }
    public Iso8601Utc StartsAt { get; }
    public ShowingFormat Format { get; }   // Is3D, IsOriginalVersion, SubtitleLanguage
    public string? Hall { get; }
    public Uri? Booking { get; }

    /// <summary>R5: the natural key, the same triple the DB unique constraint uses.</summary>
    public ShowingKey Key => new(VenueId, FilmId, StartsAt);

    public bool IsUpcoming(Iso8601Utc now) => StartsAt >= now;   // R3, as behaviour
}

public readonly record struct ShowingKey(VenueId Venue, FilmId Film, Iso8601Utc StartsAt);
```

`ShowingFormat` replaces two loose booleans and gives the `[VO]`/`[3D]` tagging a home:

```csharp
public readonly record struct ShowingFormat(bool Is3D, bool IsOriginalVersion, string? SubtitleLanguage)
{
    public IEnumerable<string> Tags()
    {
        if (IsOriginalVersion) yield return "VO";
        if (Is3D) yield return "3D";
    }
}
```

## A.6 The Programme — R3 as a first-class concept

Today "the set of films with a future screening" is an anonymous
`Dictionary<int, List<Screening>>` built inside `CorpusBuilder`. It is the most important
concept in the Felix subsystem and it has no name. Give it one:

```csharp
namespace Felinoir.Programming.Domain.Programme;

/// <summary>
/// What is actually on. R3: a film is "in programmazione" exactly when it has at
/// least one upcoming showing. Constructed from a snapshot; immutable thereafter.
/// </summary>
public sealed class Programme
{
    private readonly IReadOnlyDictionary<FilmId, IReadOnlyList<Showing>> _byFilm;

    public Iso8601Utc AsOf { get; }
    public IReadOnlyCollection<Film> Films { get; }

    public static Programme Compose(
        IEnumerable<Film> catalogue, IEnumerable<Showing> showings, Iso8601Utc now)
    {
        var upcoming = showings
            .Where(s => s.IsUpcoming(now))
            .GroupBy(s => s.FilmId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Showing>)g.ToList());

        var showing = catalogue.Where(f => upcoming.ContainsKey(f.Id)).ToList();
        return new Programme(showing, upcoming, now);
    }

    public IReadOnlyList<Showing> ShowingsOf(FilmId id) => _byFilm[id];

    /// <summary>Venue-grouped, first-seen order preserved to match the Node output byte for byte.</summary>
    public IReadOnlyList<VenueSchedule> ScheduleOf(FilmId id, VenueDirectory venues) { /* ... */ }
}
```

`Programme` is a read-model aggregate, and it is exactly the seam the current code lacks:
the "is this film on?" question is asked by the corpus builder today, and would be asked
by a `/programme` endpoint tomorrow, and by any future "notify me" feature. One place.

## A.7 Felix readiness as an aggregate with an explicit state machine

The current `EnsureReadyAsync` is correct but its states are implied by two nullable
fields and four private predicates. Made explicit:

```csharp
namespace Felinoir.Concierge.Domain.Readiness;

public abstract record KnowledgeState
{
    public sealed record Cold : KnowledgeState;
    public sealed record Cached(CacheHandle Handle) : KnowledgeState;
    public sealed record Inline(Corpus Corpus, DateTimeOffset BuiltAt) : KnowledgeState;
}

public sealed class FelixReadiness
{
    private static readonly TimeSpan RenewalLead = TimeSpan.FromHours(1);  // R8
    private static readonly TimeSpan InlineTtl   = TimeSpan.FromHours(24); // R8

    public KnowledgeState State { get; private set; } = new KnowledgeState.Cold();

    /// <summary>Pure decision: what, if anything, must happen before answering.</summary>
    public ReadinessDecision Decide(DateTimeOffset now) => State switch
    {
        KnowledgeState.Cached(var h) when h.ExpiresAt > now + RenewalLead => ReadinessDecision.Ready,
        KnowledgeState.Inline(_, var built) when now - built < InlineTtl  => ReadinessDecision.Ready,
        KnowledgeState.Cached                                             => ReadinessDecision.Renew,
        _                                                                 => ReadinessDecision.ColdStart,
    };

    public void CachePromoted(CacheHandle h) => State = new KnowledgeState.Cached(h);
    public void FellBackToInline(Corpus c, DateTimeOffset now) => State = new KnowledgeState.Inline(c, now);
    public void CacheRejected() => State = new KnowledgeState.Cold();   // mid-flight 404
}
```

The application handler keeps the `SemaphoreSlim` and the I/O; the aggregate holds only
the decision logic, which is now unit-testable without a fake Gemini client, a fake
`DbContext`, or a clock injection dance. `Decide` is a pure function of state and time.

`Speaker` replaces R7's stringly-typed role:

```csharp
public enum Speaker { User, Assistant }

public sealed record Turn(Speaker Speaker, string Content)
{
    public static Turn? TryCreate(string? role, string? content) => (role, content) switch
    {
        ("user", { Length: > 0 } c)      => new Turn(Speaker.User, c),
        ("assistant", { Length: > 0 } c) => new Turn(Speaker.Assistant, c),
        _                                => null,
    };
}

public sealed record Conversation(IReadOnlyList<Turn> Turns)
{
    /// <summary>
    /// Filters ride along with the last user turn so the model reads one coherent
    /// message instead of a synthetic exchange. Returns an unchanged conversation
    /// when there is nothing to attach.
    /// </summary>
    public Conversation WithFilterContext(FilterContext? filters)
    {
        if (filters is null || Turns.Count == 0) return this;
        if (Turns[^1] is not { Speaker: Speaker.User } last) return this;
        if (filters.Render() is not { } block) return this;

        var turns = Turns.ToArray();
        turns[^1] = last with { Content = $"{last.Content}\n\n{block}" };
        return new Conversation(turns);
    }
}
```

## A.8 The friction with CLAUDE.md

Honest accounting — full DDD contradicts three standing decisions in this repo:

1. **"Do not add a repository or unit-of-work layer on top of EF Core."** A domain model
   that is not the persistence model *requires* something that maps between them.
   `IFilmCatalogue` is a repository in everything but name. There is no way to have
   persistence-ignorant aggregates and also inject `DbContext` into services. This is a
   direct, unavoidable conflict — not a stylistic one.
2. **"Entities matching the shapes in SPEC.md."** Under DDD the domain stops matching the
   spec shapes; the *DTOs* match them. Domain and wire diverge by design.
3. **One `Felinoir.Domain` project.** Bounded contexts mean two domain projects, and the
   shared `Iso8601Utc`/`Slug`/`PageRequest` need a shared kernel.

If DDD were adopted, `CLAUDE.md` would have to be rewritten first. Doing it piecemeal —
aggregates *and* direct `DbContext` injection — produces the worst of both.

## A.9 What DDD buys and costs here

**Buys**

- R4 becomes impossible to get wrong; today it is a comment and a prayer.
- R1/R2/R3 become named, reusable, directly testable, instead of private statics inside a
  string builder.
- The Felix state machine becomes readable at a glance and testable without fakes.
- `Programme` names the central concept the system currently computes anonymously.
- The 60 TMDB columns stop leaking into every layer that touches a film.

**Costs**

- Roughly 3–4× the type count for behaviour the HTTP contract cannot expose.
- A mapping layer that must be kept in sync with a schema this process does not own.
- Two models of a movie, permanently, with the discipline that implies.
- Direct conflict with three explicit rules in `CLAUDE.md`.
- For Movies/Cinemas/Screenings — 3 of the 4 endpoint groups — it buys **almost nothing**,
  because there are no commands and no invariants to protect.

**Verdict:** over-engineering if applied uniformly. Genuinely valuable if applied *only*
to the Concierge context and the shared value objects. See §C.

---

# Rework B — Functional C#

## B.1 Guiding idea

Push all decision-making into pure, total functions over immutable data; confine I/O to a
thin shell at the edges. Errors travel in return values, not exceptions. Composition
replaces inheritance and, largely, replaces DI.

C# can carry most of this now — records, pattern matching, `switch` expressions, static
lambdas, collection expressions. What it cannot do well is also worth naming up front:
no higher-kinded types (so no shared `Traverse`/`Map` across `Option`, `Result`, `Task`),
no do-notation (so monadic chains get verbose), `Task` is eager and not a lawful monad,
and LINQ-to-Entities constrains how clever expression composition can get. Pretending
otherwise produces code that is functional in aspiration and unreadable in practice.

## B.2 Project layout

Fewer projects, organised by *purity*, not by layer:

```text
src/
  Felinoir.Core/                       pure; no I/O, no EF, no HttpClient
    Prelude.cs                         Option, Result, combinators
    Time/ Timestamp.cs
    Catalogue/ Film.cs  Showing.cs  Venue.cs  Queries.cs
    Corpus/ Compose.cs  Render.cs      the whole corpus builder, pure
    Felix/ Readiness.cs  Conversation.cs  Filters.cs
  Felinoir.Shell/                      impure; the only place with side effects
    Db/  Gemini/  Config/
  Felinoir.Api/                        wiring + HTTP translation
```

The test story follows directly: `Felinoir.Core` needs no Testcontainers, no mocks, no
`IApplicationDbContext`, no `WebApplicationFactory`. It is tested with values in, values
out. The integration tests then only have to cover the shell.

## B.3 Prelude

Hand-rolled, ~150 lines, no dependency. (`LanguageExt` is the alternative; it is powerful,
but it imports a large idiom that every future contributor must learn, and its `Aff`/`Eff`
effect types are a much bigger commitment than this codebase justifies.)

```csharp
namespace Felinoir.Core;

public readonly record struct Option<T>
{
    private readonly T? _value;
    public bool IsSome { get; }
    private Option(T v) { _value = v; IsSome = true; }

    public static Option<T> Some(T v) => new(v);
    public static Option<T> None => default;

    public Option<TR> Map<TR>(Func<T, TR> f) => IsSome ? Option<TR>.Some(f(_value!)) : Option<TR>.None;
    public Option<TR> Bind<TR>(Func<T, Option<TR>> f) => IsSome ? f(_value!) : Option<TR>.None;
    public T IfNone(T fallback) => IsSome ? _value! : fallback;
    public TR Match<TR>(Func<T, TR> some, Func<TR> none) => IsSome ? some(_value!) : none();
}

public abstract record Result<T, TErr>
{
    public sealed record Ok(T Value) : Result<T, TErr>;
    public sealed record Err(TErr Error) : Result<T, TErr>;

    public Result<TR, TErr> Map<TR>(Func<T, TR> f) => this switch
    {
        Ok(var v)  => new Result<TR, TErr>.Ok(f(v)),
        Err(var e) => new Result<TR, TErr>.Err(e),
        _ => throw new InvalidOperationException(),
    };

    public Result<TR, TErr> Bind<TR>(Func<T, Result<TR, TErr>> f) => this switch
    {
        Ok(var v)  => f(v),
        Err(var e) => new Result<TR, TErr>.Err(e),
        _ => throw new InvalidOperationException(),
    };

    public TR Match<TR>(Func<T, TR> ok, Func<TErr, TR> err) => this switch
    {
        Ok(var v)  => ok(v),
        Err(var e) => err(e),
        _ => throw new InvalidOperationException(),
    };
}

public static class Prelude
{
    public static Option<T> Some<T>(T v) => Option<T>.Some(v);
    public static Option<T> None<T>() => Option<T>.None;
    public static Result<T, E> Ok<T, E>(T v) => new Result<T, E>.Ok(v);
    public static Result<T, E> Err<T, E>(E e) => new Result<T, E>.Err(e);

    /// <summary>First non-empty string, as an Option. Replaces the ad-hoc JS `||` port.</summary>
    public static Option<string> FirstNonEmpty(params string?[] vs) =>
        vs.FirstOrDefault(v => !string.IsNullOrEmpty(v)) is { } hit ? Some(hit) : None<string>();

    /// <summary>Async bind, since Task is the one monad we cannot abstract over.</summary>
    public static async Task<Result<TR, E>> BindAsync<T, TR, E>(
        this Task<Result<T, E>> task, Func<T, Task<Result<TR, E>>> f) =>
        await (await task).Match(f, e => Task.FromResult(Err<TR, E>(e)));
}
```

That last method is the tax: every `Task`/`Result` composition needs its own helper,
because C# cannot express "monad" generically. Expect a dozen such shims.

## B.4 Data as data

Everything is a record; nothing has a setter; navigation properties are gone.

```csharp
namespace Felinoir.Core.Catalogue;

public readonly record struct FilmId(int Value);
public readonly record struct VenueId(string Value);

public sealed record Film(
    FilmId Id,
    string Title,
    Option<string> TmdbTitle,
    Option<string> OriginalTitle,
    Option<string> EnglishTitle,
    Option<int> DurationMinutes,
    Ratings Ratings,
    Classification Classification,
    Credits Credits,
    Option<string> Plot,
    Timestamp CreatedAt);

public sealed record Showing(
    int Id,
    FilmId FilmId,
    VenueId VenueId,
    Timestamp StartsAt,
    Format Format,
    Option<string> Hall,
    Option<string> BookingUrl);

public readonly record struct Format(bool Is3D, bool IsOriginalVersion)
{
    public string Tag => (IsOriginalVersion, Is3D) switch
    {
        (true, true)  => " [VO] [3D]",
        (true, false) => " [VO]",
        (false, true) => " [3D]",
        _             => "",
    };
}
```

`Option<string>` instead of `string?` is the deliberate choice: `string?` is advisory
(nullable reference types are erased at runtime and can be defeated by `!`), `Option<T>`
is enforced. It costs verbosity at the persistence boundary, where every nullable column
needs wrapping.

R1 and R2 become expressions rather than statements:

```csharp
public static class FilmQueries
{
    // R1
    public static string DisplayTitle(Film f) => f.TmdbTitle.IfNone(f.Title);

    public static IReadOnlyList<string> AlternateTitles(Film f)
    {
        var display = DisplayTitle(f);
        var original = f.OriginalTitle.Match(o => o != display ? Some(o) : None<string>(), None<string>);
        var english  = f.EnglishTitle.Match(
            e => e != display && original.Match(o => e != o, () => true) ? Some($"EN: {e}") : None<string>(),
            None<string>);
        return [.. original.ToEnumerable(), .. english.ToEnumerable()];
    }

    // R2 — the preference chain as a fold over candidates, not a cascade of ifs
    public static IReadOnlyList<string> RatingLabels(Ratings r) =>
        [.. new[] { ("IMDB", r.Imdb), ("RT", r.RottenTomatoes), ("MC", r.Metacritic) }
            .Where(p => !string.IsNullOrEmpty(p.Item2))
            .Select(p => $"{p.Item1}: {p.Item2}")];
}
```

## B.5 The corpus, pure end to end

This is where the functional rework pays for itself. Today `CorpusBuilder` does I/O and
formatting in one class, so testing the formatting requires an in-memory `DbContext`.
Split it:

```csharp
namespace Felinoir.Core.Corpus;

/// <summary>Everything the corpus needs, as plain data. No DbContext, no clock, no config.</summary>
public sealed record CorpusInput(
    IReadOnlyList<Film> Films,
    IReadOnlyList<Showing> Showings,
    IReadOnlyDictionary<VenueId, VenueInfo> Venues,
    Timestamp Now);

public sealed record VenueInfo(string Name, bool OpenAir);

public static class Corpus
{
    /// <summary>Pure: same input, same string, every time. The entire corpus rule set.</summary>
    public static string Render(CorpusInput input)
    {
        var byFilm = input.Showings
            .Where(s => s.StartsAt >= input.Now)                     // R3
            .GroupBy(s => s.FilmId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Showing>)[.. g]);

        var onNow = input.Films.Where(f => byFilm.ContainsKey(f.Id)).ToList();

        return string.Join("\n", [
            $"# Film in programmazione a Roma — {input.Now.Date}",
            $"Totale: {onNow.Count} film",
            "",
            .. onNow.SelectMany(f => RenderFilm(f, byFilm[f.Id], input.Venues).Append("")),
        ]);
    }

    private static IEnumerable<string> RenderFilm(
        Film f, IReadOnlyList<Showing> showings, IReadOnlyDictionary<VenueId, VenueInfo> venues)
    {
        yield return RenderTitleLine(f);
        foreach (var line in RenderMetaBlock(f)) yield return line;
        foreach (var line in RenderSchedule(showings, venues)) yield return line;
    }

    private static IEnumerable<string> RenderSchedule(
        IReadOnlyList<Showing> showings, IReadOnlyDictionary<VenueId, VenueInfo> venues)
    {
        yield return "Proiezioni:";

        // Distinct-by preserves first-seen venue order, matching the Node output.
        foreach (var venueId in showings.Select(s => s.VenueId).Distinct())
        {
            var info = venues.TryGetValue(venueId, out var v) ? v : new VenueInfo(venueId.Value, false);
            var label = info.OpenAir ? $"{info.Name} [Open Air]" : info.Name;   // R9
            var times = showings
                .Where(s => s.VenueId == venueId)
                .Select(s => $"{s.StartsAt.ItalianDate} {s.StartsAt.Time}{s.Format.Tag}");
            yield return $"  {label}: {string.Join(", ", times)}";
        }
    }
}
```

The shell then becomes trivial and, importantly, boring:

```csharp
namespace Felinoir.Shell.Db;

internal static class CorpusGateway
{
    public static async Task<CorpusInput> LoadAsync(
        FelinoirDb db, IReadOnlyList<VenueConfig> config, Timestamp now, CancellationToken ct)
    {
        var films = await db.Movies.AsNoTracking().OrderBy(m => m.Id).Take(1000)
            .Select(MovieRow.ToFilm).ToListAsync(ct);
        var showings = await db.Screenings.AsNoTracking()
            .Where(s => string.Compare(s.Datetime, now.Value) >= 0).Take(100_000)
            .Select(ScreeningRow.ToShowing).ToListAsync(ct);
        var venues = await VenueIndex.BuildAsync(db, config, ct);
        return new CorpusInput(films, showings, venues, now);
    }
}
```

Note `now` is a *parameter*, not `DateTime.UtcNow` read inside the function. That single
change makes the corpus golden-file testable against a frozen date — which the current
implementation, with `DateTime.UtcNow` called inside `BuildAsync`, cannot do.

## B.6 Felix as a state transition function

The current `EnsureReadyAsync` interleaves the decision, the I/O and the mutation. Split
the decision out as a pure function and the shell becomes a dumb interpreter:

```csharp
namespace Felinoir.Core.Felix;

public abstract record Knowledge
{
    public sealed record Cold : Knowledge;
    public sealed record Cached(string CacheName, DateTimeOffset ExpiresAt) : Knowledge;
    public sealed record Inline(string Corpus, DateTimeOffset BuiltAt) : Knowledge;
}

/// <summary>What the shell must do next. A description of an effect, not the effect.</summary>
public abstract record Plan
{
    public sealed record AnswerFromCache(string CacheName) : Plan;
    public sealed record AnswerInline(string Corpus) : Plan;
    public sealed record LoadPersistedCache : Plan;
    public sealed record BuildCorpusAndCache : Plan;
}

public static class Readiness
{
    private static readonly TimeSpan RenewalLead = TimeSpan.FromHours(1);   // R8
    private static readonly TimeSpan InlineTtl   = TimeSpan.FromHours(24);  // R8

    /// <summary>Pure. Given state and time, what is the next step? Fully enumerable in tests.</summary>
    public static Plan Next(Knowledge k, DateTimeOffset now) => k switch
    {
        Knowledge.Cached(var name, var exp) when exp > now + RenewalLead => new Plan.AnswerFromCache(name),
        Knowledge.Inline(var c, var built) when now - built < InlineTtl   => new Plan.AnswerInline(c),
        Knowledge.Cold                                                    => new Plan.LoadPersistedCache,
        _                                                                 => new Plan.BuildCorpusAndCache,
    };

    /// <summary>Pure state transitions. One function per thing that can happen.</summary>
    public static Knowledge OnCacheCreated(string name, DateTimeOffset expiresAt) =>
        new Knowledge.Cached(name, expiresAt);
    public static Knowledge OnCacheUnavailable(string corpus, DateTimeOffset now) =>
        new Knowledge.Inline(corpus, now);
    public static Knowledge OnCacheExpiredMidFlight() => new Knowledge.Cold();
}
```

Testing `Next` is a table of eight cases with no fakes at all. Compare with
`FelixServiceTests` today, which needs a stub `IGeminiClient`, a stub `ICorpusBuilder` and
an in-memory `DbContext` to assert the same eight behaviours.

## B.7 Errors in return position

The `/felix/chat` endpoint currently wraps everything in `try/catch (Exception)`. Made
explicit:

```csharp
namespace Felinoir.Core.Felix;

public abstract record FelixError
{
    public sealed record MalformedRequest : FelixError;
    public sealed record NotConfigured : FelixError;
    public sealed record UpstreamFailed(string Detail) : FelixError;
}
```

And the endpoint becomes a total translation — every case named, nothing swallowed, and
the exhaustiveness checked by the compiler if the hierarchy is `sealed`:

```csharp
app.MapPost("/felix/chat", async (FelixChatRequest? body, FelixHandler felix, CancellationToken ct) =>
{
    var result = await ParseRequest(body)
        .BindAsync(req => felix.AnswerAsync(req, ct));

    return result.Match(
        ok: reply => Results.Ok(new { reply }),
        err: e => e switch
        {
            FelixError.MalformedRequest =>
                Results.BadRequest(new { error = "Richiesta non valida." }),
            FelixError.NotConfigured =>
                Results.Json(new { error = "Felix non è configurato al momento." }, statusCode: 503),
            FelixError.UpstreamFailed =>
                Results.Json(new { error = "Qualcosa è andato storto. Riprova tra qualche secondo." },
                    statusCode: 500),
        });
});
```

## B.8 Composable queries instead of an if-ladder

`ScreeningService.GetAsync` is eight sequential `if`s mutating an `IQueryable`. Functionally,
a filter is a value and the query is a fold:

```csharp
namespace Felinoir.Core.Catalogue;

public sealed record ShowingFilter(
    Option<VenueId> Venue,
    Option<FilmId> Film,
    Option<string> City,
    Option<Timestamp> From,
    Option<Timestamp> To)
{
    /// <summary>Predicates as data. Composed by the shell; kept out of the shell's head.</summary>
    public IEnumerable<Expression<Func<ScreeningRow, bool>>> Predicates()
    {
        if (Venue.IsSome) { var v = Venue.IfNone(default); yield return s => s.CinemaId == v.Value; }
        if (Film.IsSome)  { var f = Film.IfNone(default);  yield return s => s.MovieId == f.Value; }
        if (City.IsSome)  { var c = City.IfNone("");       yield return s => s.Cinema!.City == c; }
        if (From.IsSome)  { var t = From.IfNone(default);  yield return s => string.Compare(s.Datetime, t.Value) >= 0; }
        if (To.IsSome)    { var t = To.IfNone(default);    yield return s => string.Compare(s.Datetime, t.Value) <= 0; }
    }
}

// shell:
var query = filter.Predicates().Aggregate(
    db.Screenings.AsNoTracking(),
    (q, predicate) => q.Where(predicate));
```

This is the clearest example of where C# pushes back. The `Expression<Func<>>` closures
must capture locals, not `Option` fields, or EF cannot translate them — so the "pure"
version still leaks knowledge of the persistence row type into `Felinoir.Core`, or needs a
second mapping layer. Either the purity or the simplicity gives. A pragmatic answer is to
keep `ShowingFilter` pure and let the shell own a small `Apply(ShowingFilter)` translation;
that is what the sketch above actually does, and it is the honest shape.

## B.9 DI by composition

With no mutable services, most classes become static functions, and the container's role
shrinks to supplying the impure bits. Endpoints take delegates:

```csharp
public delegate Task<Result<string, FelixError>> AnswerQuestion(ChatRequest req, CancellationToken ct);

builder.Services.AddScoped<AnswerQuestion>(sp =>
{
    var db = sp.GetRequiredService<FelinoirDb>();
    var gemini = sp.GetRequiredService<GeminiPort>();
    var state = sp.GetRequiredService<FelixStateCell>();
    return (req, ct) => FelixHandler.Answer(state, gemini, db, Clock.System, req, ct);
});
```

Interfaces with one implementation and one method (`ICorpusBuilder`, `IMovieService`,
`ICinemaService`) disappear — a delegate says the same thing with less ceremony. The
five `I*Service`/`I*Builder` interfaces in the current codebase are all in this category.

## B.10 What functional buys and costs here

**Buys**

- The corpus builder, the Felix state machine and every formatting rule become pure and
  trivially testable. The unit test project stops needing an in-memory `DbContext`.
- `now` as a parameter makes time-dependent behaviour deterministic, which it currently
  is not.
- The error envelope is exhaustive and compiler-checked; no `catch (Exception)` deciding
  the HTTP status.
- Fewer types overall than the DDD version, and fewer single-method interfaces than today.
- Immutability removes a whole class of question about `FelixState` being a mutable
  singleton shared across requests.

**Costs**

- `Option<T>` vs `string?` is a real tax at the EF boundary — 60 columns to wrap and
  unwrap on every movie read.
- Every `Task` + `Result` combination needs a bespoke shim; there is no generic `Traverse`.
- `Expression<Func<>>` and EF translation fight abstraction, as §B.8 shows.
- The idiom is unusual in .NET; a future contributor reads `BindAsync` chains more slowly
  than an `if` ladder, and the codebase cannot assume familiarity.
- Records with 12 positional members get unwieldy, and `with`-expression chains are
  allocation-heavy on hot paths (the corpus builds over ~1000 films × ~100k screenings).

**Verdict:** a better fit for this codebase than full DDD, because it targets exactly
where the complexity actually is (corpus + Felix) and leaves the CRUD reads nearly as
simple as they are now. The `Option<T>`-everywhere part is the piece to drop.

---

# C. Side-by-side

| Concern | Today | DDD | Functional |
|---|---|---|---|
| R4 fixed-width UTC | comment | `Iso8601Utc` value object | `Timestamp` record struct |
| R6 pagination clamp | endpoint layer | `PageRequest` VO | smart constructor returning `Result` |
| R1 display title | private static in builder | `TitleSet.Display` | `FilmQueries.DisplayTitle` |
| R2 rating chain | inline `if`s | `RatingSet.InPreferenceOrder` | fold over candidates |
| R3 "in programmazione" | anonymous dictionary | `Programme` aggregate | `Corpus.Render` filter |
| R7 chat role | `string` + validating `if` | `Speaker` enum + `Turn.TryCreate` | `Speaker` DU + parse to `Result` |
| R8 cache freshness | private predicates | `FelixReadiness.Decide` | `Readiness.Next` (pure) |
| Errors | `catch (Exception)` → 500 | domain exceptions + handler | `Result<T, FelixError>` |
| Testing the corpus | needs in-memory `DbContext` | needs `Programme` + fake catalogue | plain values in, string out |
| Testing Felix states | 3 fakes | 0 fakes for `Decide` | 0 fakes for `Next` |
| New types added | — | ~45 | ~20 |
| Conflicts with `CLAUDE.md` | — | repository ban, entity shapes, single Domain project | service-pattern rule (delegates replace `I*Service`) |

## C.1 Recommendation, if any of this were ever acted on

Neither rework wholesale. The three things worth stealing, in order of value per unit of
churn:

1. **`Iso8601Utc` / `Timestamp` as a value object.** Small, self-contained, and it closes
   the one genuine correctness hole in the codebase (R4). Compatible with `CLAUDE.md` as
   written — it is an EF value converter over the same TEXT column, and the JSON output is
   unchanged. This one is worth doing on its own merits.
2. **Make the corpus pure** (§B.5). Split `CorpusBuilder` into a `CorpusInput` loader and a
   pure `Render`, and pass `now` in. Deletes the in-memory-`DbContext` dependency from
   `CorpusBuilderTests`, makes the output golden-file testable, and does not require
   adopting `Option<T>` or any other idiom. No conflict with any existing rule.
3. **Make Felix readiness a pure decision function** (§A.7 / §B.6). The state machine is
   the highest-risk logic in the repo and the hardest to test today. Extracting `Decide`/
   `Next` leaves `FelixService` owning the semaphore and the I/O, which is the right split
   regardless of which philosophy you prefer — both reworks arrived at the same shape here,
   which is a decent signal it is correct.

Things **not** worth taking: bounded-context project splits (two contexts, one of which is
read-only, does not pay for the ceremony), `Option<T>` across 60 TMDB columns, repositories
over EF Core, and a domain `Film` that the API can never return.

The underlying point both reworks make, from opposite directions: the current design is
well-organised for the CRUD it does and under-organised for the one genuinely complex
thing it does. The fix is not a new architecture — it is moving four or five rules out of
`CorpusBuilder` and `FelixService` into places where they can be named and tested.
