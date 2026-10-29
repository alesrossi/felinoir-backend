# Alignment — scrapers, frontend and server

What the other projects and the server need to change or check before the reworked backend
(`refactoring` branch) goes live. Kept up to date at every rework step; the detailed scraper
strategy is in `docs/SCRAPERS-SCHEMA-MIGRATION.md`, the plan in `docs/REWORK-PLAN.md`.

**Last updated:** 2026-10-29, after rework step 7 (the rework is complete).

Legend: **Required** = the release breaks without it · **Check** = verify while testing locally ·
**Optional** = an improvement, nothing breaks without it.

---

## 1. Backend changes that affect other projects

| Step | Change | Affects |
|---|---|---|
| 1 | EF migrations own the schema; a one-shot **migrator** image runs before the API and scrapers | scrapers, server |
| 1 | The migrator **adopts** the existing database (records `Baseline` as applied); history table columns are snake_case: `migration_id`, `product_version` | scrapers, server |
| 1 | `NormaliseKeys`: `tmdb_lookup_cache` / `ai_match_verdicts` get primary keys (the unique indexes `idx_tmdb_lookup_cache_key` / `idx_ai_match_verdicts_key` become `…_pkey`); `lobby_participants_one_per_device` becomes a unique index | scrapers |
| 2 | `cities` table; `cinemas.city` (free text) **dropped**, replaced by `cinemas.city_id` (FK → `cities`, today always `rome`) | scrapers |
| 2 | `cinemas.latitude`, `cinemas.longitude`, `cinemas.open_air` added (filled once from `cinemas.yml`) | scrapers, frontend |
| 2 | `felix_cache` keyed by `city_id` | — |
| 2 | Cinema JSON `city` is now the city's name: `"Roma"` for **every** cinema (it used to be the town, e.g. `"Velletri"`) | frontend |
| 2 | `?city=` on `/movies`, `/cinemas`, `/screenings` accepts a city id (`rome`) or name (`Roma`), any case; `?city=Velletri` now returns `[]` | frontend |
| 2 | `?from=` / `?to=` with `Z` or an offset are converted to Italian time; values without one are compared as before | frontend |
| 2 | Felix's corpus compares with the Italian "now" (it offered screenings that started up to 2 h earlier) | — |
| 2 | The backend no longer ships or reads `cinemas.yml`; open-air comes from the database | scrapers, server |
| 3 | Error responses are RFC 7807 `application/problem+json` keeping the `error` member, plus `traceId`; empty framework errors (malformed JSON, unknown route, bad query parameter) now have a body | frontend |
| 3 | Every response has `X-Trace-Id` (exposed by CORS); an incoming W3C `traceparent` is continued | frontend |
| 4 | `/movies/{id}`, `/movies/slug/{slug}`, `/cinemas/{id}`: 404 and 400 are problem responses; `error` keeps its old values (`"Not found"`, `"Invalid id"`); success bodies unchanged | frontend |
| 5 | `/lobbies/*` errors are problem responses with the same codes and statuses (`missing_device`, `invalid_name`, `invalid_note`, `not_found`, `name_taken`, `already_joined`, `screening_not_found`, `screening_past`), plus a `detail` text on validation errors | frontend |
| 5 | Lobby `version`: a new lobby starts at **1** (was 2) and every change adds **1** (a claim added 2); still strictly increasing | frontend |
| 5 | New 409 `conflict` when a lobby change loses a race twice in a row (extremely rare) | frontend |
| 5 | Version triggers and their functions dropped; `lobby_participants_one_per_device` is a deferrable constraint | scrapers |
| 6 | `/felix/chat` errors are problem responses keeping the Italian `error` texts the UI shows (`Richiesta non valida.`, `Felix non è configurato al momento.`, `Qualcosa è andato storto…`), plus a `detail` on 400s | frontend |
| 6 | `/felix/chat?city=` (optional, id or name; Rome by default; unknown city → 400) | frontend |
| 6 | Felix rebuilds its corpus on the first question after 05:00 Italian time (`Felix__DailyRebuildAt`), so answers follow the night's scrape | server |
| 6 | Gemini calls retry transient failures: a Felix answer can now take up to ~75 s in the worst case (30 s per attempt) | frontend, server |
| 3 | Logs: Elastic Common Schema JSON on stdout (Production), configurable through `Serilog__…` variables; tracing exported over OTLP when configured | server |

---

## 2. Scrapers (`~/programming/typescript/cinema/scrapers`)

Full strategy and code sketches: `docs/SCRAPERS-SCHEMA-MIGRATION.md`.

### Required

- [ ] **Stop issuing DDL**: delete `src/lib/db/postgres/schema.ts` (`SCHEMA`, `OPTIONAL_SCHEMA`), the
      statement splitter and the bootstrap call in `client.ts`; delete `npm run db:migrate` and
      `src/scripts/migrate-db.ts`. Otherwise every connection re-creates the old unique indexes next to
      the new primary keys, `CREATE INDEX … ON cinemas (city)` **fails** now that the column is gone,
      and the bootstrap would re-create the lobby version triggers the backend dropped (versions would
      then jump by two per change).
- [ ] **Schema version guard** at pool creation:
      `SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = $1` (snake_case column), with
      `REQUIRED_MIGRATION` set to the backend's latest migration (today
      `20261029110416_LobbyVersionInDomain`).
- [ ] **`cinemas.yml`**: add `city: rome` to every cinema (the Velletri, Tivoli, Frascati… cinemas
      included). It becomes the only copy the backend relies on.
- [ ] **`sync-cinemas.ts`**: write `city_id`, `latitude`, `longitude`, `open_air` from the yml; stop
      writing `city`; fail clearly if a `city` id isn't in `cities`.
- [ ] **Scrapers that set `city`** (`ScraperCinema.city`, e.g. `multisala-jolly`, `cinema-troisi`,
      `multiplex-cinema-augustus`): stop; `CinemaRecord` / the cinemas repository use `cityId`;
      `findByCity` filters on `city_id`.
- [ ] Remove the runtime "is the trigram index usable" fallback in the movies repository: `pg_trgm`
      and `idx_movies_canonical_trgm` are now guaranteed.

### Check

- [ ] Every upsert still targets column lists (`ON CONFLICT (scraped_title, scraped_original_title)`,
      `(cinema_id, movie_id, datetime)`, `(title, COALESCE(original_title, ''))`, `(id)`), never
      constraint names (verified on 2026-10-29; re-check after scraper changes).
- [ ] `db:reset` still works (it truncates; update its comment about "the next connection re-runs the
      migration").
- [ ] A full `scrape → enrich → enrich:ai → enrich:omdb` run against the migrated database.

### Optional (but do the two together)

- [ ] **Troisi writes times without seconds** (`2026-10-18T16:15`, 73 rows on the dev data). Fix the
      scraper to write `HH:mm:ss` **and**, in the same release, add a backend migration normalising
      those rows. Either alone makes the upsert on `(cinema_id, movie_id, datetime)` duplicate them.
- [ ] Type generation from the migrated schema in CI (`kanel` / `pg-to-ts`).

---

## 3. Frontend (`~/programming/typescript/felinoir-frontend`)

### Required

Nothing so far: the HTTP contract is unchanged apart from additions, and the frontend's error
handling (`res.json()` then `data.error`; the lobby proxy passes `Content-Type` through) already
works with problem responses.

### Check

- [ ] Cinema `city` is `"Roma"` for every cinema. `ApiCinema.city` is only typed, not displayed
      (2026-10-29); re-check if a page starts showing it.
- [ ] Error paths still render the right message: Felix (`data.error` is still the Italian text), lobby
      errors (`name_taken`, `already_joined`, `not_found`, …), 404 pages. Movie and cinema lookups
      (`getMovieById`, `getMovieBySlug`, cinema pages) only look at `res.ok`, so their 404 becoming a
      problem response changes nothing (checked 2026-10-29 in `src/lib/api/client.ts`).
- [ ] Lobby polling (`lobby-view.tsx`): only needs `version` to increase, which it does; check a lobby
      created before the cutover keeps updating after it. (An unknown error code such as `conflict`
      already falls back to the generic message in `lobbyErrorText`, checked 2026-10-29.)
- [ ] `?from=` uses `romeWallClockKey` (`YYYY-MM-DDTHH:MM`, Rome time, no offset): unchanged
      behaviour. Only a Troisi screening starting in exactly the current minute is now excluded
      (its stored time has no seconds).
- [ ] Felix's proxy route (`src/app/api/felix/route.ts`) has no timeout of its own; with Gemini retries
      an answer can take longer than before. Check the chat's loading state copes (optional: a
      client-side timeout).
- [ ] Movie, cinema, home, lobby and Felix pages against the new image (section 5).

### Optional

- [ ] **Observability**: send `traceparent` from the Next.js server to the backend (`instrumentation.ts`
      with `@vercel/otel`, or forward the incoming header in `apiFetch` and the proxies) and log or show
      `traceId` / `X-Trace-Id` on errors. This is the only frontend change the rework plans for.
- [ ] **A third copy of `cinemas.yml`** lives in the frontend (`src/lib/cinemas-config.ts`, used by
      10 pages for coordinates, open-air, address, website; identical to the scrapers' copy on
      2026-10-29). The database now has coordinates and `open_air`; once the API exposes them on
      cinemas (planned, additive), the frontend can drop its copy. Until then keep it in sync with
      the scrapers' file.
- [ ] Send `?city=rome` once there is more than one city (not needed while Rome is the only one).

---

## 4. Server (`server/docker-compose.yml`, `server/daily-report.sh`)

### Services

| Service | Image | Change |
|---|---|---|
| `cinema-migrator` | `alesrossi/cinema-backend-migrator:latest` | **new**: one-shot, `restart: "no"`, after `db` is healthy |
| `cinema-backend` | `alesrossi/cinema-backend:latest` | now `depends_on: cinema-migrator: service_completed_successfully` (was `db`) |
| `cinema-scraper` | `alesrossi/cinema-scrapers:latest` | same `depends_on` change; `flaresolverr` unchanged |
| others | — | unchanged |

`docker compose run --rm cinema-scraper` (the daily cron) starts its dependencies, so the migrator
runs before every scrape; with nothing pending it exits 0 in about a second.

### Environment variables

| Variable | Service | Status |
|---|---|---|
| `DATABASE_URL` | migrator, backend, scraper | required; URI form `postgres://user:pass@host:port/db` |
| `PORT` | backend | optional, default 3001 |
| `GEMINI_API_KEY` | backend (Felix), scraper (`enrich:ai`) | required for Felix; without it `/felix/chat` answers 503 |
| `TMDB_READ_ACCESS_TOKEN` | scraper | still needed there; **the backend never reads it** (can be removed from `cinema-backend`) |
| `ASPNETCORE_ENVIRONMENT` | backend | leave unset (= Production: ECS JSON logs, no developer pages) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | backend | **new, optional**: e.g. Elastic APM's OTLP endpoint; traces and metrics are exported only when set (`OTEL_EXPORTER_OTLP_HEADERS` for auth) |
| `Serilog__MinimumLevel__Default` and `Serilog__MinimumLevel__Override__<Namespace>` | backend | **new, optional**: log levels, e.g. `Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore=Information` |
| `Felix__DailyRebuildAt` | backend | **new, optional**: Italian time (`HH:mm`, default `05:00`) after which Felix rebuilds its corpus; keep it after the nightly scrape's end |
| `CINEMAS_CONFIG_PATH` | backend | **removed**: the backend no longer reads `cinemas.yml` |

### Other

- [ ] Docker Hub: the repository `alesrossi/cinema-backend-migrator` must exist (or be creatable by the
      CI token) before the first push from `main`.
- [ ] CI publishes both images with the same tags (`latest` and the commit sha) only from `main`; the
      `refactoring` branch isn't built by CI.
- [ ] `daily-report.sh` reads `movies.has_matches` (still there) and no dropped column; re-check if
      the report's queries change.
- [ ] Logs are JSON lines now: the report attaches `scraper.log` only (the scraper's, unchanged); any
      grep on backend container logs needs to match JSON.
- [ ] Reverse proxy (`VIRTUAL_HOST`, nginx) unchanged; the backend is still only reached through the
      frontend on `cinema_internal`.

---

## 5. Testing the final images locally

Before the production cutover (`docs/SCRAPERS-SCHEMA-MIGRATION.md`, "Production cutover", which has
the server runbook and the rollback).

1. **Images**: `docker build --network host -t cinema-backend:test .` and
   `docker build --network host --target migrator -t cinema-backend-migrator:test .`; scrapers and
   frontend images from their repos with the changes above. `--network host` is needed on the dev
   machine (Calico blocks Docker's bridge network, so restores can't reach NuGet / npm); CI and the
   server don't need it.
2. **Data**: restore a `pg_dump` of production into a local Postgres (never point at production).
3. **Migrate**: run the migrator against it. Expect *"Adopting a database built by the scrapers"*, then
   the pending migrations, then exit 0; a second run reports *"up to date"*.
4. **Scrape**: one full scraper run (`scrape`, `enrich`, `enrich:ai`, `enrich:omdb`) against it; no DDL,
   no errors, cinemas keep `city_id = 'rome'` and gain coordinates.
5. **API**: `/health`, `/movies`, `/movies/slug/…`, `/cinemas` (every `city` is `"Roma"`),
   `/screenings?city=rome&withRelations=true`, a Felix chat (`GEMINI_API_KEY` set), create, join,
   claim and poll a lobby; check `X-Trace-Id` and the JSON log lines.
6. **Frontend**: home, a movie page, a cinema page (map, open-air badge), Felix, a lobby (`/e/<id>`),
   error paths.
7. **Rollback rehearsal**: restore the dump again and start the old images.
