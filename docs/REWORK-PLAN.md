# Felinoir Backend — Rework Plan

The plan for the rework described in `new.md`, as agreed in the design session of
2026-10-28/29. The work is split into steps that each leave the `refactoring` branch building
and green.

Related documents:

- `CONTEXT.md` — the domain language (City, Cinema, Movie, Screening, Lobby, Participant,
  Device, Claim, Felix, Corpus, In programmazione, Felix Context). Use these terms in code.
- `docs/adr/0001-backend-owns-database-schema.md`
- `docs/adr/0002-clean-architecture-with-cqrs-slices.md`
- `docs/SCRAPERS-SCHEMA-MIGRATION.md` — what changes in the scrapers repo, and the production
  cutover.
- `alignment.md` — what the scrapers, the frontend and the server must change or check, kept up to
  date at every step.

---

## Ground rules

- **Development only.** All work happens on `refactoring` (each step as its own branch and PR into
  `refactoring`). `main` stays as the current production image and is untouched until the
  cutover. Compatibility with the deployed scrapers and the old API image does **not** need to
  be kept along the way.
- **One production cutover at the end** (see `docs/SCRAPERS-SCHEMA-MIGRATION.md`): database
  backup, migrator, new API and new scrapers deployed together.
- **The HTTP contract stays frozen**, except for additive changes. The characterisation
  snapshots from step 0 enforce this. The only frontend change in scope is observability
  (sending `traceparent`, reading `traceId`). Any other mismatch with the frontend is handled
  later, case by case.
- **Every migration has a working `Down()`.**
- `CLAUDE.md` rules that conflict with this plan (service pattern, no repository/UoW, schema
  owned by the scrapers, `cinemas.yml`) are superseded now and are rewritten in step 7.

---

## Step 0 — Safety net

Goal: prove later steps change structure, not behaviour.

- **Characterisation snapshots** with Verify: seed a fixed dataset, call every endpoint
  (`/health`, `/movies` incl. slug/id, `/cinemas`, `/screenings` with each filter and
  `withRelations`, `/felix/chat` with a faked `IGeminiClient`, every `/lobbies` route and error
  code), and snapshot status, relevant headers and JSON body. Taken on the current code, before
  anything else moves.
- **Test infrastructure:** one Postgres Testcontainer per test run (keep the host-network
  workaround), **Respawn** to reset data between tests.
- **Production schema reference:** commit a `pg_dump --schema-only` of the current database
  (`tests/schema/production.sql`) as the baseline for drift checks.

Done when: the snapshot suite passes against the current code and runs in CI.

## Step 1 — Schema ownership (ADR 0001) — done 2026-10-29

What the drift test found changed the approach. Neither production nor the local database had
an `__EFMigrationsHistory` table: every table was created by the scrapers' `schema.ts`, and the
five old migrations had only ever run in test containers. They also never reproduced
production: different constraint and index names, `identity` instead of `serial`, no column
defaults, a unique index instead of the screenings unique constraint, and no `idx_movies_unique`,
`idx_movies_title`, `idx_movies_external_id`, `idx_screenings_*`, `pg_trgm` or scraper-only
columns. Marking them as applied would have left the EF model disagreeing with production on
every name. So the history was squashed instead:

- **The EF model describes production exactly:** constraint and index names, `serial` ids,
  column defaults (`SqlDefaults.UtcNowText` for the TEXT timestamps), the screenings unique
  constraint as an alternate key, every index, `pg_trgm` and the trigram GIN index. The
  scraper-only movie columns (`canonical_title`, `tmdb_match_attempts`, `tmdb_last_attempt_at`,
  `has_matches` with its CHECK) are **shadow properties** on `Movie`. `tmdb_lookup_cache` and
  `ai_match_verdicts` are persistence-only types in `Infrastructure/Persistence/ScraperTables`.
- **`Baseline` migration:** production's schema exactly. Generated from the model; expression
  indexes (`idx_movies_unique`, `lobby_participants_name_uniq`), functions and triggers are
  verbatim raw SQL. Three objects stay in production's shape here (see the next point).
- **`NormaliseKeys` migration**, the first ordinary one: the unique indexes of
  `tmdb_lookup_cache` and `ai_match_verdicts` become their primary keys, and the
  `lobby_participants_one_per_device` constraint becomes a unique index of the same name, so the
  database matches the model. The scrapers' `ON CONFLICT` targets are column lists and still match.
- **`DatabaseMigrator`** (Infrastructure) adopts a scraper-built database (scraper tables, no
  history) by recording `Baseline` as applied without running it, then applies pending
  migrations. It refuses a scraper schema older than Baseline (no `lobbies` table). EF's history
  table uses snake_case columns (`migration_id`, `product_version`) because of the naming
  convention.
- **Migrator:** `src/Felinoir.Migrator`, a console app running `DatabaseMigrator` on
  `DATABASE_URL`, built as the `migrator` stage of the Dockerfile (`docker build --target
  migrator`) and published by CI as `alesrossi/cinema-backend-migrator` with the API's tags. In
  both compose files it runs once after `db` is healthy, and the API and the scraper wait for it
  with `service_completed_successfully`. The API never migrates at startup.
- **Drift tests** (`tests/Felinoir.IntegrationTests/Persistence/SchemaDriftTests.cs`), comparing
  schemas through catalog facts, not dump text:
  - `Baseline` alone equals `tests/schema/production.sql`;
  - adopting a copy of production ends on the same schema and history as a fresh database;
  - the model has no changes without a migration;
  - running the migrator twice changes nothing;
  - an older scraper schema is refused;
  - every `Down()` reverts cleanly.
- **The local database is migrated** (history: `Baseline`, `NormaliseKeys`). Its schema is
  identical to a production copy run through the migrator. Until step 1s, the dev scraper's
  bootstrap will add back the two old unique indexes beside the new primary keys: harmless
  drift, gone once the scrapers stop issuing DDL.
- `.env` loading uses `NoClobber`, so a `DATABASE_URL` already in the environment wins over
  `.env` (it used to be overridden, which sent a migrator run meant for a scratch database to the
  local one).

## Step 2 — Cities and cinema facts — done 2026-10-29

Done in the **current** architecture with minimal code, so the schema is final before the
refactor starts. Multi-city support is in place under the hood; Rome is the only city.

- **`Cities` migration:**
  - a `cities` table (`id` slug, `name`, `created_at`, `updated_at`) seeded with `rome` / `Roma`;
    new cities are always added by backend migrations (reference data)
  - `cinemas.city_id` (NOT NULL, FK → `cities`, index `idx_cinemas_city_id`), every existing
    cinema listed under `rome`; the free-text `cinemas.city` and `idx_cinemas_city` are dropped
    (on the dev data, ten cinemas outside the municipality, e.g. Velletri and Tivoli, are now
    Rome cinemas; their town stays in `address`)
  - `cinemas.latitude`, `longitude`, `open_air` (default false), filled from `cinemas.yml` for the
    53 cinemas listed there
  - `felix_cache` keyed by `city_id` (PK, FK → `cities`); the existing row becomes Rome's
  - checked on a copy of the dev data in both directions; `Down()` restores `city` as the city's
    name (`Roma`), since the original town names aren't recoverable
- **Domain:** a `City` entity (`City.RomeId`), `Cinema.CityId` / `City` / `Latitude` / `Longitude`
  / `OpenAir`, `FelixCache.CityId`. The API JSON is unchanged: `Cinema.city` is still a string,
  now the city's name (`CityName`, serialised as `city`; queries returning cinemas include
  `City`). Coordinates and `openAir` are not exposed yet.
- **`?city=`** on `/screenings`, `/movies` (movies with a screening in that city) and `/cinemas`:
  a city id (`rome`) or display name (`Roma`), ignoring case. Without it, results are as before;
  an unknown city gives `[]`.
- **Felix** builds its corpus per city (`ICorpusBuilder.BuildAsync(cityId)`: that city's cinemas,
  screenings and name) and persists its cache per city, always Rome for now. Accepting `?city=` on
  `/felix/chat` waits for step 6: today's process-wide `FelixState` can only serve one city.
- **`ScreeningTime`** (Domain value object): Italian wall-clock time, `Europe/Rome`.
  - Parses the stored forms strictly: `yyyy-MM-ddTHH:mm:ss`, and `yyyy-MM-ddTHH:mm`, which
    the Troisi scraper writes (73 of 3978 dev rows). Stored values are not rewritten: the
    scrapers upsert on the exact text, so normalising them would create duplicates until that
    scraper is fixed.
  - Replaces `RomeTime`; `UtcText` formats the UTC timestamps.
  - **Fixes the corpus bug:** "now" was UTC and compared with Italian times, so Felix offered
    screenings that had started up to 2 h earlier. The corpus now uses the injected
    `TimeProvider` in Italian time.
  - **`?from=` / `?to=`** with `Z` or an offset are converted to Italian time; values without
    one are Italian time, as stored; unparseable values are compared as given, as before.
  - Still a plain `string` on the entity: mapping `Screening.Datetime` to `ScreeningTime` with an
    EF value converter moves to step 4, when entities stop being serialised directly.
- **The backend's `cinemas.yml` is deleted**, with `CinemasYamlConfigProvider`,
  `CinemasConfigLocator`, `ICinemaConfigProvider` and YamlDotNet; the corpus reads `open_air` from
  the database. `cinemas.yml` now lives only in the scrapers repo.
- **Not applied to the local database:** the running dev images still read and write
  `cinemas.city`; the migrator applies `Cities` when the new stack starts.

## Step 3 — Foundations (ADR 0002) — done 2026-10-29

As built (details below are the original plan, which held):

- **Build:** `Directory.Build.props` (warnings as errors, analysers `latest-recommended`),
  `Directory.Packages.props`, `.editorconfig`. The NuGet audit flagged two high-severity
  advisories, fixed by upgrading `Microsoft.AspNetCore.OpenApi` (10.0.14) and Testcontainers
  (4.16.0). Logging uses source-generated `[LoggerMessage]` methods.
- **Observability:** `ObservabilityExtensions` (Serilog + ECS, OpenTelemetry, `X-Trace-Id`).
  Verified by running the API in both environments; `traceparent` is continued.
- **Errors:** `Error` / `Result<T>` in Application, `ErrorResults` and `ProblemDetailsSetup` in
  Api. The frontend reads errors with `res.json()` / `data.error` and passes `Content-Type`
  through, so `application/problem+json` is safe. Existing endpoints keep their bodies until
  their slice is migrated; empty framework errors already get problem bodies.
- **Mediator:** registered in the Api (where its source generator runs) with **scoped**
  lifetime; behaviours fail through `IFailureResult<T>` (static abstract, no reflection).
  `/health` is the first slice and proves the wiring.
- **Deferred:** dispatching domain events. `AggregateRoot` records them; the dispatcher is built
  with the first event that has a handler.
- **Not committed:** `appsettings.Development.json` (debug log levels) is excluded by the repo's
  `.gitignore` rule for `appsettings*.json`.


- **Build hygiene:** `Directory.Build.props` (nullable, `TreatWarningsAsErrors`, .NET
  analyzers at `Recommended`, implicit usings), `.editorconfig`, central package management
  with `Directory.Packages.props`. Nullable annotations matter even more now: EF reads
  `string` vs `string?` as `NOT NULL` vs nullable when it generates migrations.
- **Mediator:** `martinothamar/Mediator` (source-generated, MIT). Pipeline behaviours, in
  order:
  1. `LoggingBehavior` — request name, duration and outcome, inside a scope. Never logs the
     payload (device tokens and chat contents are sensitive).
  2. `ValidationBehavior` — runs FluentValidation validators for the request's shape.
  3. `UnitOfWorkBehavior` — commands only: commits through `IUnitOfWork` after the handler
     succeeds, then dispatches domain events.
- **Building blocks** (Domain / Application):
  - `Entity<TId>`, `AggregateRoot<TId>` (collects domain events), `ValueObject` where a record
    isn't enough
  - strongly-typed ids as `readonly record struct` (`LobbyId`, `ParticipantId`, `CityId`,
    `CinemaId`, `MovieId`, `ScreeningId`)
  - `Result<T>` with `Error(Code, Kind)`, where `Kind` is NotFound, Conflict, Validation or
    Unprocessable. In-house, about 40 lines.
  - `PageRequest` (`readonly record struct`) owns the limit/offset clamps, so the rule leaves
    the endpoints.
- **Persistence abstractions:** `IUnitOfWork`, `I<Aggregate>Repository` (aggregates only), and
  `IReadDbContext` (no-tracking, for queries).
- **Errors:** a single `Error.Kind` → HTTP status mapping in Api. Responses become **RFC 7807
  ProblemDetails** that keep the old code as an extension: `{ type, title, status, traceId,
  error: "name_taken" }`. This is a superset of today's `{ error }`. Check that the frontend
  doesn't look at the `Content-Type` exactly (`application/problem+json`). A global
  `IExceptionHandler` returns a ProblemDetails 500 for real exceptions.
- **Observability:**
  - **W3C trace context** is the correlation id. ASP.NET reads incoming `traceparent`. The trace
    id goes into every error body (`traceId`) and an `X-Trace-Id` response header.
  - **OpenTelemetry** tracing and metrics: ASP.NET Core, `HttpClient` and Npgsql
    instrumentation, plus `ActivitySource("Felinoir")` for the mediator pipeline and Felix
    rebuilds. Background work starts its own root activity. The OTLP exporter is off until
    configured; Elastic accepts OTLP directly, so connecting Elastic later is configuration only.
  - **Serilog**, configured entirely through `ReadFrom.Configuration` (appsettings or environment
    variables: levels, per-namespace overrides, sinks). The **Elastic Common Schema** JSON
    formatter goes to stdout in production and a readable console in development, with
    `trace.id` and `span.id` on every line.
- **Architecture tests** (NetArchTest):
  - Domain references nothing.
  - Endpoints never reference `DbContext`.
  - Query handlers never reference repositories.
  - Every command has a validator.
- **Lifetimes:**
  - `DbContext`, repositories, the unit of work and handlers are scoped.
  - The singletons are `TimeProvider`, the Felix context registry and options.
  - Typed `HttpClient`s are transient and are never held by a singleton.
  - DI validation is on (`ValidateScopes` and `ValidateOnBuild`), so captive dependencies fail
    at startup.

## Step 4 — Catalogue slices (Movies, Cinemas, Screenings) — done 2026-10-29

As built: six queries under `Application/Features/{Movies,Cinemas,Screenings}`, each response
record with one `Projection` expression (translated in `Select`, and compiled for entities loaded
with `Include` when `withRelations` is set). JSON is byte-identical; 404/400 became problem
responses keeping their old `error` values (`"Not found"`, `"Invalid id"`, in `CatalogueErrors`).

**Deviation:** `Screening.Datetime` stays a `string` in the entity rather than being mapped to
`ScreeningTime` with an EF value converter. A converter would make one malformed scraper row fail
whole queries, and it would rewrite the 73 Troisi rows stored without seconds when returned
(`…T16:15` → `…T16:15:00`). `ScreeningTime` keeps being used where times are parsed or compared.
Revisit once the Troisi scraper fix and its normalising migration are in (see `alignment.md`).


- One query plus handler per use case under `Features/<Area>/<UseCase>/`: `GetMovies`,
  `GetMovieById`, `GetMovieBySlug`, `GetCinemas`, `GetCinemaById`, `GetScreenings`.
- Handlers project straight to `sealed record` responses inside `Select(...)` with
  `AsNoTracking`, and `Include` relations only when `withRelations` is set. No AutoMapper, no
  Mapperly.
- Catalogue entities stay plain: no public setters, no repositories, no behaviour invented for
  the sake of DDD.
- Endpoints become one line each (`sender.Send(...)`), and `/movies/slug/{slug}` is still
  registered before `/movies/{id}`.
- `IMovieService`, `ICinemaService`, `IScreeningService` and their implementations are deleted.

## Step 5 — Lobbies (the write model) — done 2026-10-29

As built, and where it differs from the plan below:

- `Result` / `Error` moved from Application to `Domain.Common`, so aggregates can return them.
- `StartsAt` is a property of `Lobby`, not of `ScreeningSnapshot`: EF can't index a column inside
  a complex type, and `lobbies_starts_at_idx` backs `delete_expired_lobbies()`.
- **Version numbering:** a new lobby starts at 1 and every change adds exactly 1 (the triggers made
  creation 2 and a claim +2). The frontend only needs versions to increase (`lobby-view.tsx`
  ignores lower ones), which they do; existing lobbies continue from their current value.
- **Claims and the device constraint:** `lobby_participants_one_per_device` is now a `DEFERRABLE
  INITIALLY DEFERRED` unique constraint (migration only; EF can't model it). A claim moves a device
  between two rows in one save, and EF orders the two UPDATEs by key, so an immediate check would
  fail half the time.
- **Concurrency:** a stale version or a unique violation makes `ConflictRetryBehavior` run the
  command once more on fresh data; a second loss is a 409 `conflict`.
- Lobby ids are generated by the domain (GUID v7), so the keys are `ValueGeneratedNever`; with a
  store-generated key, EF saved a participant added to a loaded lobby as an UPDATE.


- **Aggregate root `Lobby`** (`LobbyId`):
  - `ScreeningSnapshot`: a value object (EF complex type)
  - `LobbyNote`: at most 500 characters
  - a collection of `Participant` entities, each with a `ParticipantName` (trimmed, 1–40
    characters, no control characters) and an optional `DeviceHash`
- **Behaviour on the root, returning `Result`:**
  - `Create(snapshot, creatorName, device, now)` rejects screenings that have already started.
  - `Join`, `Rename`, `Remove` and `Claim` enforce "unique name ignoring case" and "one entry
    per device". `Claim` moves the device off its other entry.
  - `ChangeNote` updates the note.
  - `IsExpiredAt(now)` is true 48 h after the start.
- **Versioning moves to the domain:**
  - Every change increments `Version`, which EF maps as an **optimistic concurrency token**.
    A conflict returns a 409, or the handler retries once.
  - A migration drops the `lobby_participants_bump_version` and `lobbies_bump_version_on_note`
    triggers.
  - `delete_expired_lobbies()` stays a database function, called by the hourly cleanup.
- **Constraint backstops:** the database unique constraints stay as protection against
  concurrent requests. A 23505 is translated into the same domain errors (`NameTaken`,
  `AlreadyJoined`).
- **Slices:**
  - `CreateLobby`, `GetLobby` (keeps the cheap version/`starts_at` head lookup that answers 304)
  - `ChangeLobbyNote`
  - `JoinLobby`, `RenameParticipant`, `RemoveParticipant`, `ClaimParticipant`
  - each with an `ILobbyRepository` and a FluentValidation validator
- `LobbyService`, `LobbyResult`, `LobbyError` and `LobbyInput` are deleted.

## Step 6 — Felix — done 2026-10-29

As built (`Application/Features/Felix`): `AskFelixCommand` + validator + handler; `CorpusSnapshotLoader`
and the pure `CorpusRenderer`; `FelixReadiness` (pure) with `FelixContext` and the per-city
`FelixContextRegistry`; `IFelixContextStore` (upserts `felix_cache`, which now records the build time
in `created_at`); a typed, resilient `GeminiClient`. `?city=` is accepted on `/felix/chat`. EF
InMemory is gone. The corpus text and every recorded Gemini call are unchanged.

Kept as they were, as agreed: the Gemini settings (base URL, model, TTL, temperature, tokens) are
constants in `GeminiClient`, and the system prompt there still says Rome; a second city needs a
per-city prompt. New setting: `Felix:DailyRebuildAt` (default 05:00). The 24 h inline limit is now
only a safety net: any 24 h window contains a daily rebuild time.


- **Slice `AskFelix`:** `AskFelixCommand(CityId, Messages, Filters)`. A chat turn's speaker
  becomes a validated value object in place of the `"user"`/`"assistant"` string checks.
- **The corpus is split:**
  - `CorpusSnapshotQuery` loads plain data for one City: Movies that are in programmazione
    (by `ScreeningTime`), their upcoming Screenings, and Cinemas with `IsOpenAir`.
  - `CorpusRenderer` is a **pure** function from snapshot to Markdown. The corpus rules live
    here as named, tested methods: display title (TMDB title, else the title), preferred rating
    (IMDb, then RT, then Metacritic) and in programmazione. They are **Felix rules, not Movie
    rules**. Tests use golden files and need no database.
- **Readiness as a pure decision:** `FelixReadiness.Decide(state, now)` returns `UseCache`,
  `UseInline`, `LoadPersisted` or `Rebuild`. The rules:
  - A cache stays fresh until 1 h before it expires, and an inline corpus for 24 h, **and**
  - **a Felix Context built before the most recent daily cutoff is stale.** The cutoff is
    configurable, default 05:00 Italian time, after the nightly scrape. That way the first chat
    of the day rebuilds from that night's data, instead of the rebuild time drifting with
    traffic.

  The handler carries out the decision. Everything reads `TimeProvider`, with no
  `DateTimeOffset.UtcNow`.
- **Per-City state:** `FelixState` becomes a singleton `FelixContextRegistry` mapping each
  `CityId` to its own state and semaphore, so one City rebuilding never blocks another. Each
  City is persisted in `felix_cache` by `city_id`.
- **Gemini:** registered with `AddHttpClient<IGeminiClient, GeminiClient>()` plus the standard
  resilience handler (timeout, retry with jitter). Settings are bound through
  `IOptions<GeminiOptions>` and validated at startup. Swallowed exceptions are now logged.

## Step 7 — Wrap-up — done 2026-10-29

`IApplicationDbContext` removed (`FelixCache` became read-only too); `SPEC.md`,
`docs/ARCHITECTURE.md` and `CLAUDE.md` rewritten for the new design (`CLAUDE.md` is gitignored, so
its rewrite is local); `docs/REWORK-EXPLORATIONS.md` marked historical; the cutover in
`docs/SCRAPERS-SCHEMA-MIGRATION.md` is now a server runbook with the expected migrator output and a
rollback that pins the previous image digests. The k6 comparison was dropped with the performance
work.


- Remove `IApplicationDbContext` and anything else the slices replaced.
- Rewrite `CLAUDE.md` (new architecture rules, schema ownership, migrator, testing layers),
  `docs/ARCHITECTURE.md` and `SPEC.md` (screening time is Italian local time, `cities`, the new
  cinema columns, `felix_cache` by City, triggers removed, ProblemDetails, `?city=`).
- Mark `docs/REWORK-EXPLORATIONS.md` as historical.
- Prepare the cutover described in `docs/SCRAPERS-SCHEMA-MIGRATION.md`.

## Step 8 — After the cutover

- **Extra targeted logging** where the default pipeline logging turns out to be thin, for
  example Felix rebuild reasons, Gemini fallbacks and lobby conflicts.
- **Frontend observability:** the Next.js server propagates `traceparent` (`instrumentation.ts`
  / `@vercel/otel`) and shows or logs `traceId` from error responses.

---

## Deferred (decided against for now)

- **Performance work:** output caching (5-min catalogue TTL was judged acceptable), response
  compression, `AddDbContextPool`, compiled queries, reducing the 50 000 default screening
  limit, BenchmarkDotNet and k6. Projections in `Select` are **not** deferred — they are how
  query slices are written.
- **Breaking contract changes:** for example `city` becoming required once there are several
  Cities, or a `/v2` route group.
- **"Cinemas near me"**: a distance-based feature using cinema coordinates, separate from City.

## Decision log

| # | Decision |
|---|---|
| Q1 | EF migrations are the only source of DDL. A one-shot migrator runs before the API and the scrapers. The scrapers stop issuing DDL and check the migration version at startup. → ADR 0001 |
| Q2 | The EF model and migrations must describe production exactly; misalignment is a bug. (Implemented by squashing the old migrations into `Baseline`, see step 1.) |
| Q3–Q4 | A City is the Italian city a Cinema is listed under. Every current Cinema is a Rome Cinema, the Velletri one included. The `?city=` filter is optional groundwork. |
| Q5 | The `cities` table, `cinemas.city_id`, the free-text `city` dropped, JSON `city` from `cities.name`, `felix_cache` by City, and `?city=` accepting the id or the name. |
| Q6 | Cinema coordinates and `open_air` move to the database. `cinemas.yml` is scraper-only. |
| Q7 | Clean architecture, CQRS-lite feature slices, `martinothamar/Mediator`, repositories and the unit of work for aggregates only, queries projecting from a read context. → ADR 0002 |
| Q8 | The Lobby aggregate, and versioning moved from triggers to an EF concurrency token. |
| Q9–Q10 | Catalogue entities stay plain. The corpus rules belong to Felix. `ScreeningTime` is Italian local time. Scraper-only data stays out of Domain. `PageRequest`. |
| Q11 | Felix: a pure renderer, a pure readiness decision, a per-City registry, a typed resilient `HttpClient`. |
| Q12 | A daily cutoff rule for Felix Context staleness. |
| Q13 | W3C trace context, OpenTelemetry, Serilog + ECS configured entirely from configuration. The frontend adapts for observability only. |
| Q14 | The contract is frozen except for additive changes. |
| Q15 | Characterisation snapshots first, EF InMemory removed, real Postgres for handlers, schema-drift and architecture tests. |
| Q16 | Hand-written mapping, records and record structs, strongly-typed ids, in-house `Result`, FluentValidation, interfaces only at boundaries, nullable plus warnings as errors. |
| Q17 | Performance work deferred. |
| Q18–Q20 | The step order above. Step PRs into `refactoring`. Development only, so compatibility may break. One production cutover at the end. |
