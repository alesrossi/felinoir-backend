# Felinoir Backend — Architecture Schema

A .NET 10 reimplementation of the Felinoir cinema backend (films, screenings and
cinemas in Rome, plus the **Felix** Gemini-backed chat assistant). Clean architecture,
minimal API, EF Core onto a pre-existing Postgres schema. See `SPEC.md` for the contract
and `CLAUDE.md` for the implementation rules.

> Diagrams below are plain ASCII so they render in any viewer. (An ER diagram of the
> database tables can be added on request.)

---

## 1. Layers & dependency direction

Four projects; dependencies point **inward** only. `Domain` knows nothing about the
others; the API host wires everything together at the edge.

```text
              +-----------------------------------------------------+
              |  Felinoir.Api   (host)                              |
              |  minimal API . endpoints . DI . OpenAPI / Scalar    |
              +----------------+-----------------------+------------+
                               |                       |
                      depends  |                       | depends
                       on      v                       v   on
        +----------------------------+     +------------------------------+
        |  Felinoir.Application      |<----|  Felinoir.Infrastructure     |
        |  services . DTOs . Felix   | dep |  EF Core . Gemini . cinemas  |
        |  orchestration             | on  |  .yml . DATABASE_URL parse   |
        +-------------+--------------+     +---------------+--------------+
                      |                                    |
              depends |                                    | depends
               on     v                                    v   on
              +-------------------------------------------------------+
              |  Felinoir.Domain    entities . no framework deps      |
              +-------------------------------------------------------+

        Dependencies point inward  ->  Domain depends on nothing.
```

| Project | Responsibility | Depends on |
|---|---|---|
| **Domain** | Entities (`Movie`, `Cinema`, `Screening`, `FelixCache`, `TmdbLookupCache`). Plain POCOs. | — |
| **Application** | Service interfaces + implementations, DTOs, business rules, Felix orchestration. Talks to the DB via the `IApplicationDbContext` abstraction. | Domain |
| **Infrastructure** | EF Core `DbContext` + entity configs + migrations, the Gemini REST client, the `cinemas.yml` reader, `DATABASE_URL` parsing. | Application, Domain |
| **Api** | ASP.NET Core minimal API host: one endpoint file per resource, DI wiring, CORS `*`, camelCase JSON, OpenAPI document + Scalar UI. Thin endpoints delegate to services. | Application, Infrastructure |

---

## 2. Project structure

```text
src/
|-- Felinoir.Domain/
|   `-- Entities/            Movie, Cinema, Screening, FelixCache, TmdbLookupCache
|-- Felinoir.Application/
|   |-- Common/Interfaces/   IApplicationDbContext, ICinemaConfigProvider
|   |-- Movies/ Cinemas/ Screenings/   I*Service + *Service (service pattern)
|   |-- Felix/               ICorpusBuilder/CorpusBuilder, IFelixService/FelixService,
|   |                        IGeminiClient, FelixState, FelixFilters(+Formatter), ChatMessage
|   `-- DependencyInjection.cs
|-- Felinoir.Infrastructure/
|   |-- Persistence/         ApplicationDbContext, Configurations/, Migrations/ (baseline)
|   |-- Felix/               GeminiClient, CinemasYamlConfigProvider, CinemasConfigLocator
|   |-- Configuration/       DatabaseUrl  (URI -> Npgsql keyword string)
|   `-- DependencyInjection.cs
`-- Felinoir.Api/
    |-- Endpoints/           Health, Movie, Cinema, Screening, Felix (+ request/response DTOs)
    `-- Program.cs           host, CORS, JSON, OpenAPI + Scalar, endpoint mapping

tests/
|-- Felinoir.UnitTests/          service logic in isolation (EF InMemory + Moq)
`-- Felinoir.IntegrationTests/   real Postgres via Testcontainers + WebApplicationFactory

Root:  Dockerfile . .dockerignore . docker-compose.yml (DB only) . cinemas.yml
       .github/workflows/ci.yml . Felinoir.slnx . SPEC.md . CLAUDE.md
```

---

## 3. Request flow (read endpoints)

Endpoints never touch EF Core directly — they delegate to a service, which queries the
`DbContext` through `IApplicationDbContext` (the EF unit of work itself, deliberately not
wrapped in a repository).

```text
  Client            Endpoint          Service           DbContext          Postgres
 (frontend)          (Api)         (Application)     (Infrastructure)
     |                  |                |                  |                  |
     |  GET /movies     |                |                  |                  |
     |----------------->| GetAllAsync()  |                  |                  |
     |                  |--------------->| LINQ (AsNoTrack) |                  |
     |                  |                |----------------->|  SELECT ...      |
     |                  |                |                  |----------------->|
     |                  |                |                  |     rows         |
     |                  |                |   List<Movie>    |<-----------------|
     |                  | IReadOnlyList  |<-----------------|                  |
     |  200 JSON        |<---------------|                  |                  |
     |<-----------------|                |                  |                  |

  Routes (documented at /scalar, spec at /openapi/v1.json):
    GET /health   GET /movies   GET /movies/slug/{slug}   GET /movies/{id}
    GET /cinemas  GET /cinemas/{id}   GET /screenings   POST /felix/chat
```

---

## 4. Felix subsystem (POST /felix/chat)

The most involved flow. `FelixService` orchestrates a two-layer context strategy; the
`GeminiClient` owns HTTP/payload details; readiness state is process-wide (`FelixState`).

```text
  POST /felix/chat
        |
        v
  messages valid? ------- no ------> 400  "Richiesta non valida."
        | yes
        v
  GEMINI_API_KEY set? ---- no ------> 503  "Felix non e configurato al momento."
        | yes
        v
  FelixService.GenerateAsync
        |
        v
  EnsureReady():
     |-- in-memory cache fresh?  --- yes --.
     |-- in-memory inline fresh? --- yes --|
     |-- felix_cache DB row valid? ------- |---> READY
     `-- else COLD START:                  |
            build corpus (DB + cinemas.yml)|
            Gemini cachedContents create   |
               |-- ok            -> persist cache_name to felix_cache
               `-- too small/fail-> keep corpus inline (24h)
        |
        v
  append active filter context to the last user message
        |
        v
  cache available? -- yes --> generateContent(cachedContent)
        |                          |-- 404 expired --.
        | no                       `-- ok -> 200 { reply }
        v                                            |
  generateContent(inline corpus) <-------------------'
        |
        v
  200 { reply }          (any exception along the way -> 500 generic error)
```

- **Corpus** = Markdown of every film with a future screening (TMDB-preferred titles,
  IMDb -> RT -> MC ratings, keywords, plot, screenings grouped per cinema with
  `[VO]` / `[3D]` / `[Open Air]` tags).
- **Gemini**: `gemini-2.5-flash-lite` REST, `maxOutputTokens: 700`, `temperature: 0.4`.
  Context caching needs >= 2048 tokens; below that it falls back to inline context.
- **`felix_cache`** (single-row table) persists the active `cache_name` across restarts.

---

## 5. External dependencies & data

```text
   Frontend                +--------------------------+   HTTPS    Google Gemini
   felinoir.it  --/api/--->  |                          | --------> generativelanguage
   (Next.js BFF)  felix      |    Felinoir.Api  :3001   |           /v1beta
                             |                          | --reads-> cinemas.yml
                             +------------+-------------+           (coords / openAir)
                                          | EF Core / Npgsql
                                          v
                             Postgres (pre-existing schema)  <--writes--  Node scraper
                                                                          (separate service)
```

- The Postgres schema **already exists with production data**; EF maps onto it (snake_case,
  TEXT timestamps as `string`) and never creates/drops it. Migrations use a baseline
  (`InitialCreate` marked already-applied; its DDL is never run against prod).
- TMDB / OMDb enrichment and scraping are handled by the separate Node scraper, not this server.

---

## 6. Testing & delivery

```text
  UnitTests  (EF InMemory + Moq) ----.
                                      |
  IntegrationTests                    +--> CI (GitHub Actions, push to main)
  (Testcontainers Postgres   --------'         |
   + WebApplicationFactory)                    v
                                      build + dotnet test
                                               |
                                          all pass?
                                               | yes
                                               v
                                      docker build & push
                                               |
                                               v
                                   Docker Hub: alesrossi/cinema-backend
                                       tags:  latest , sha-<commit>
```

- **Unit tests** — service logic in isolation (corpus builder, Felix orchestration, filter
  formatting, YAML parsing, `DATABASE_URL` parsing).
- **Integration tests** — real Postgres via Testcontainers (host-network workaround for the
  Calico dev box; standard bridge on CI), plus in-memory `WebApplicationFactory` for the
  HTTP contract.
- **CI/CD** — the `test` job gates the `publish` job; on success the image is pushed as
  `:latest` and `:sha-<commit>`. Secrets: `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`.
- **Image** — multi-stage Dockerfile (`sdk:10.0` build -> `aspnet:10.0` runtime), `cinemas.yml`
  copied in; `DATABASE_URL` / `GEMINI_API_KEY` supplied at runtime.
```
