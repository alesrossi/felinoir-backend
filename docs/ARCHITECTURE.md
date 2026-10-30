# Felinoir Backend — Architecture

A .NET 10 backend for felinoir.it: the films, screenings and cinemas of a city (Rome today),
shareable group outings to a screening (**Lobbies**), and **Felix**, a Gemini-backed chat
assistant. The HTTP contract is in `SPEC.md`, the vocabulary in `CONTEXT.md`, the decisions in
`docs/adr/`. This document is the map.

---

## 1. Projects and the dependency rule

Clean architecture (ADR 0002): dependencies point inward; Domain depends on nothing.

```text
   +--------------------------+      +---------------------------+
   |  Felinoir.Api  (host)    |      |  Felinoir.Migrator        |
   |  endpoints . pipeline    |      |  one-shot schema migrator |
   |  errors . observability  |      |  (own image)              |
   +------------+-------------+      +-------------+-------------+
                |                                  |
                v                                  v
   +--------------------------+      +---------------------------+
   |  Felinoir.Application    |<-----|  Felinoir.Infrastructure  |
   |  feature slices          |      |  EF Core . migrations     |
   |  behaviours . ports      |      |  repositories . Gemini    |
   +------------+-------------+      +-------------+-------------+
                |                                  |
                v                                  v
   +-------------------------------------------------------------+
   |  Felinoir.Domain   aggregates . entities . value objects    |
   |  Result / Error . no framework dependencies                 |
   +-------------------------------------------------------------+
```

| Project | Holds | Depends on |
|---|---|---|
| **Domain** | The Lobby aggregate, the read-only catalogue entities (`Movie`, `Cinema`, `Screening`, `City`, `FelixCache`), value objects (`ScreeningTime`, `ParticipantName`, …), `Result` / `Error`, `Entity` / `AggregateRoot`. | — |
| **Application** | Feature slices (`Features/<Area>/`: request + validator + handler), pipeline behaviours, ports (`IReadDbContext`, `IUnitOfWork`, `ILobbyRepository`, `IGeminiClient`, `IFelixContextStore`), response records. | Domain |
| **Infrastructure** | `ApplicationDbContext`, entity configurations, migrations, `DatabaseMigrator`, repositories, `GeminiClient`, `FelixContextStore`, the lobby cleanup job, `DATABASE_URL` parsing. Scraper-only tables (`ScraperTables/`). | Application, Domain |
| **Api** | Minimal-API endpoints (one file per resource), mediator registration, problem responses, observability, CORS, OpenAPI + Scalar. | Application, Infrastructure |
| **Migrator** | Console app: runs `DatabaseMigrator` on `DATABASE_URL` and exits 0/1. | Infrastructure |

The rule is enforced by architecture tests (`tests/Felinoir.UnitTests/Architecture`).

---

## 2. Project structure

```text
src/
|-- Felinoir.Domain/
|   |-- Common/          Entity, AggregateRoot, Result, Error, IFailureResult
|   |-- Entities/        Movie, Cinema, Screening, City, FelixCache   (read-only, init-only)
|   |-- Lobbies/         Lobby (aggregate root), Participant, LobbyId, ParticipantId,
|   |                    ParticipantName, LobbyNote, DeviceHash, ScreeningSnapshot, LobbyErrors
|   `-- ValueObjects/    ScreeningTime
|-- Felinoir.Application/
|   |-- Common/          Behaviors/, Interfaces/, Exceptions/, PageRequest, UtcText, FelinoirTelemetry
|   |-- Cities/          CityLookup  (?city= by id or name)
|   `-- Features/
|       |-- Health/      GetHealth
|       |-- Movies/      GetMovies, GetMovie(ById|BySlug), MovieResponse
|       |-- Cinemas/     GetCinemas, GetCinemaById, CinemaResponse
|       |-- Screenings/  GetScreenings, ScreeningResponse
|       |-- Lobbies/     CreateLobby, GetLobby, LobbyCommands (note/join/rename/remove/claim),
|       |                LobbyViews, ILobbyRepository
|       `-- Felix/       AskFelix, FelixReadiness + FelixContext, FelixContextRegistry,
|                        CorpusSnapshotLoader + CorpusRenderer, IGeminiClient, IFelixContextStore
|-- Felinoir.Infrastructure/
|   |-- Persistence/     ApplicationDbContext, Configurations/, Migrations/, DatabaseMigrator,
|   |                    LobbyRepository, ExpiredLobbyCleanupService, ScraperTables/
|   |-- Felix/           GeminiClient, FelixContextStore
|   `-- Configuration/   DatabaseUrl
|-- Felinoir.Api/        Endpoints/, Errors/, Observability/, Program.cs
`-- Felinoir.Migrator/   Program.cs

tests/
|-- Felinoir.UnitTests/         domain, behaviours, readiness, renderer, Felix handler, architecture
|-- Felinoir.IntegrationTests/  real Postgres (Testcontainers): contract snapshots, schema drift,
|                               query handlers, lobby persistence, Felix store, errors, tracing
`-- schema/production.sql       the schema the scrapers had built (Baseline's reference)
```

---

## 3. A request

Endpoints only turn HTTP into a request and send it. Every handler runs inside the mediator
pipeline (source-generated `martinothamar/Mediator`, scoped):

```text
  HTTP -> endpoint -> ISender.Send(request)
                         |
                         v
          LoggingBehavior         span "<Request>" + one log line (name, duration, outcome)
          ValidationBehavior      FluentValidation; a failure returns Error.Validation(code)
          ConflictRetryBehavior   commands: a lost race runs the command once more, else 409
          UnitOfWorkBehavior      commands: one SaveChanges, never after a failed Result
                         |
                         v
                      handler  ->  Result<T>  ->  endpoint: 200 / 201 / 304, or a problem response
```

Behaviours apply by constraint: queries get Logging (and Validation when they return a `Result`);
commands get all four.

**Queries** (the catalogue, `GetLobby`) read through `IReadDbContext` (untracked) and project into
response records in `Select`: each record has one `Projection` expression, also compiled for entities
loaded with `Include`. **Commands** load an aggregate through its repository, change it through its
methods and let the unit of work commit. Query handlers never touch repositories or the unit of
work (architecture test).

Routes (documented at `/scalar`, spec at `/openapi/v1.json`):

```text
GET  /health
GET  /movies   GET /movies/slug/{slug}   GET /movies/{id}       (?city=, limit, offset)
GET  /cinemas  GET /cinemas/{id}                                 (?city=)
GET  /screenings                                                 (?cinemaId, movieId, city, from, to, withRelations, limit, offset)
POST /lobbies  GET|PATCH /lobbies/{id}  POST /lobbies/{id}/participants
PATCH|DELETE /lobbies/{id}/participants/{pid}  POST /lobbies/{id}/participants/{pid}/claim
POST /felix/chat                                                 (?city=)
```

---

## 4. Domain

- **Lobby** is the one write model: an aggregate root owning its `Participant`s and every rule
  (screening not started, unique names ignoring case, one entry per device, claiming releases the
  device's other entry, 48 h expiry). Each change increments `Version`, which is also EF's
  concurrency token.
- **Catalogue entities** are read models written by the scrapers: init-only, no behaviour invented
  for its own sake, no repositories. Scraper-only columns are EF shadow properties; scraper-only
  tables live in Infrastructure.
- **`ScreeningTime`**: screening times are Italian wall clock (`Europe/Rome`, the only zone).
- **Errors** are values: `Error(Code, Kind)`; `Kind` maps to one HTTP status in `ErrorResults`.

---

## 5. Persistence and the schema

- **This repository owns the schema** (ADR 0001). EF migrations are the only DDL: `Baseline`
  (the schema the scrapers had built, verified against `tests/schema/production.sql`),
  `NormaliseKeys`, `Cities`, `LobbyVersionInDomain`.
- **The migrator** runs before the API and the scrapers (compose `service_completed_successfully`).
  A database the scrapers built, with no migration history, is adopted by recording `Baseline` as
  applied. The API never migrates.
- **Drift tests** compare schemas through catalog facts: Baseline equals production, adopting
  production equals a fresh database, the model has no unmigrated changes, every `Down()` works.
- Objects EF can't model live only in migrations: `idx_movies_unique`, `lobby_participants_name_uniq`
  (expression indexes), the deferrable `lobby_participants_one_per_device`, `delete_expired_lobbies()`.
- `ApplicationDbContext` translates EF's concurrency exception and unique violations (also a deferred
  one at commit) into Application exceptions, which `ConflictRetryBehavior` handles.

---

## 6. Felix

```text
  POST /felix/chat?city=
      |  validated: messages present, each "user"|"assistant" with content
      v
  AskFelixHandler
      |  Gemini not configured -> 503
      |  FelixContextRegistry[city]: fresh context in memory? ---------------------+
      |  else, under the city's lock: re-check memory -> felix_cache (store) ------+
      |  else rebuild: CorpusSnapshotLoader -> CorpusRenderer -> Gemini cache,     |
      |       or inline if Gemini won't cache; persist a cache (upsert)            |
      v                                                                            v
  Gemini generateContent (cached, or inline; an expired cache falls back inline) -> { reply }
```

Freshness (`FelixReadiness`, pure): built after the latest daily rebuild time (05:00 Italian time by
default) and a cache with more than 1 h left, or an inline corpus under 24 h old. `GeminiClient` is a
typed `HttpClient` with the standard resilience handler (30 s per attempt, 2 retries, 75 s total).
Model, URL and generation settings are constants in `GeminiClient`; the system prompt is Rome's.

---

## 7. Cross-cutting

- **Errors**: RFC 7807 problem details with `error` (the code clients read) and `traceId`; empty
  framework errors get a body; unhandled exceptions are logged and answered with a 500.
- **Observability**: the W3C trace id is the correlation id (incoming `traceparent` continued,
  `X-Trace-Id` on every response). Serilog from configuration (`LogOutputs`): readable console
  lines, or ECS JSON with `LOG_CONSOLE_FORMAT=ecs`; with `LOG_FILE_PATH`, also rolling Elastic
  Common Schema JSON files (daily and at 100 MB, at most a month / 5 GB). OpenTelemetry traces ASP.NET Core, HttpClient, Npgsql
  and the `Felinoir` source; exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- **DI**: scopes validated at startup; singletons hold no scoped services (the Felix registry, the
  `TimeProvider`); typed clients are transient.
- **Build**: warnings as errors, .NET analysers, central package versions.

---

## 8. Deployment

```text
  db (Postgres 16)
    -> cinema-migrator   alesrossi/cinema-backend-migrator   (one-shot; exits 0)
         -> cinema-backend   alesrossi/cinema-backend         (API)
         -> cinema-scraper   alesrossi/cinema-scrapers        (cron: docker compose run --rm)

  logs/backend/*.json (API, ECS JSON) + logs/scraper.log
    -> alloy -> loki (31 days, <= 5 GB via log-retention) -> grafana   https://logs.felinoir.it
```

One Dockerfile builds both backend images (`docker build .` for the API, `--target migrator` for the
migrator); CI publishes them with the same tags from `main`. What the scrapers, the frontend and the
server need for the cutover is in `alignment.md` and `docs/SCRAPERS-SCHEMA-MIGRATION.md`; the log
services' configuration is in `server/logging/`.
