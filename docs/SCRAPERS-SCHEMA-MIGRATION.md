# Scrapers — Schema Migration Strategy

What has to change in `cinema-scrapers` (`~/programming/typescript/cinema/scrapers`) once the
backend owns the database schema (ADR 0001), and how production is cut over. **Nothing in this
document has been done yet.** It is a strategy for you to carry out, after the backend's
`refactoring` branch is finished.

## Where the scrapers are today

- `src/lib/db/postgres/schema.ts` holds `SCHEMA` (~370 lines of `CREATE … IF NOT EXISTS` /
  `ALTER … ADD COLUMN IF NOT EXISTS`, one-off backfill `UPDATE`s, lobby triggers and
  functions) and `OPTIONAL_SCHEMA` (`pg_trgm` plus the trigram index, allowed to fail).
- `src/lib/db/postgres/client.ts` applies both on the **first connection of every process**,
  with a hand-written statement splitter.
- `npm run db:migrate` (`src/scripts/migrate-db.ts`) only opens a connection so that the
  bootstrap above runs.
- `sync-cinemas.ts` upserts cinemas from `cinemas.yml` and hard-codes `city: "Roma"`.
  Individual scrapers also set `city` (`"Roma"`, `"Velletri"`).
- Data access is raw `pg` with hand-written repositories (`src/lib/db/postgres/repositories`)
  and hand-written row types (`src/lib/db/types.ts`).

## Target

The scrapers are a **schema consumer**. They read and write data, never issue DDL, and refuse
to run against a database whose schema they weren't written for.

## Changes in the scrapers repo

### 1. Remove schema management

- Delete `schema.ts` (`SCHEMA` and `OPTIONAL_SCHEMA`), the statement splitter, and the
  bootstrap call in `client.ts`.
- Delete `npm run db:migrate` and `src/scripts/migrate-db.ts`. Migrations are run by the
  backend's migrator image.
- Keep `db:reset` (it only truncates data). Update its comment, which says "the next connection
  re-runs the migration".
- Remove the runtime "is the trigram index usable" detection in the movies repository. With
  ADR 0001, `pg_trgm` and `idx_movies_canonical_trgm` are guaranteed by the migrations.
- Update `src/lib/db/CLAUDE.md` to say the schema is owned by `felinoir-backend`.
- Names changed by the backend's `NormaliseKeys` migration: `idx_tmdb_lookup_cache_key` and
  `idx_ai_match_verdicts_key` are now the primary keys `tmdb_lookup_cache_pkey` and
  `ai_match_verdicts_pkey`, and `lobby_participants_one_per_device` is a unique index rather than
  a constraint. The scrapers' `ON CONFLICT (scraped_title, scraped_original_title)` targets still
  match. Until `schema.ts` is gone, its bootstrap re-adds the two old unique indexes next to the
  primary keys, which is another reason to remove it.

### 2. Add a schema version guard

At pool creation, before anything else:

```ts
// The migration this scraper release was written against. Bump it together with any code
// that depends on a newer schema.
export const REQUIRED_MIGRATION = "20261xxxxxxxxx_<Name>";

const { rows } = await pool.query(
  // snake_case columns: the backend's naming convention applies to EF's history table too.
  `SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = $1`,
  [REQUIRED_MIGRATION],
);
if (rows.length === 0) {
  throw new Error(
    `Database schema is older than this scraper expects (${REQUIRED_MIGRATION}). ` +
    `Run the felinoir migrator first.`,
  );
}
```

It checks that the migration is **present**, not that it is the latest. A newer backend that
only adds columns doesn't stop an older scraper, so scraper releases only need to bump the
constant when they start using something new.

### 3. Adapt to the new schema (backend step 2)

- **`cinemas.yml`** becomes the only copy (the backend deletes its own). Each cinema gains:
  ```yaml
  city: rome          # id of a row in the cities table (created by backend migrations)
  ```
  It already has `coordinates` and `open-air`. Every current cinema, including the Velletri
  multiplex, gets `city: rome`.
- **`sync-cinemas.ts`** writes `city_id`, `latitude`, `longitude` and `open_air` from the yml,
  and stops writing `city` (the column is gone). If a `city` id doesn't exist in `cities`, it
  fails clearly: new Cities are added by backend migrations first.
- **Scrapers that set `city`** (`ScraperCinema.city` in `src/lib/scraper/types.ts`) set
  `cityId`, or rely on the yml. Remove the free-text city everywhere.
- **`CinemaRecord` and the cinemas repository**: replace `city` with `cityId`.
  `findByCity` filters on `city_id`.
- **Screening `datetime`** stays Italian local time `yyyy-MM-ddTHH:mm:ss` with no offset. The
  backend parses it strictly (`ScreeningTime`): no milliseconds, no `Z`, no offset. It also
  accepts `yyyy-MM-ddTHH:mm` for now, because the **Troisi scraper writes minutes only**
  (`2026-10-18T16:15`, 73 rows on the dev data). Fix that scraper to write seconds and, in the
  same release, normalise its stored rows (`UPDATE screenings SET datetime = datetime || ':00'
  WHERE datetime ~ '^\d{4}-\d\d-\d\dT\d\d:\d\d$'`, as a backend migration); doing either alone
  makes the upsert on `(cinema_id, movie_id, datetime)` create duplicates. Consider one shared
  formatter used by every scraper.
- **Lobbies:** the version-bump triggers are dropped by the backend. Scrapers never wrote
  lobbies, so nothing changes, as long as the scrapers don't recreate the triggers (they won't
  once `schema.ts` is gone).

### 4. Optional: catch schema drift at build time

Raw `pg` has no compile-time link to the schema. If that becomes a problem, add a CI job that
starts Postgres, runs the backend migrator image of the pinned version, and generates row
types (for example with `kanel` or `pg-to-ts`) into `src/lib/db/generated.ts`. Not required,
because the version guard already stops a scraper from running on the wrong schema.

### 5. Local development

- To get a database: run Postgres from the backend's `docker-compose.yml`, then the migrator
  (or `dotnet ef database update` from the backend repo).
- `npm run db:reset`, then `npm run sync-cinemas` and `npm run scrape`, work as before.

## Production cutover (once the backend refactor is finished)

Production has **no `__EFMigrationsHistory` table** (checked 2026-10-29). Its whole schema was
created by the scrapers' bootstrap, and EF has never recorded a migration there. It does contain
everything the backend's `Baseline` migration creates (the drift test proves this against
`tests/schema/production.sql`), so the migrator adopts it on its first run and then applies
`NormaliseKeys`, `Cities` and `LobbyVersionInDomain`.

### Before the day

1. **Scrapers ready**: the changes above are released as an image tag, with
   `REQUIRED_MIGRATION = "20261029110416_LobbyVersionInDomain"` (the backend's latest migration) and
   `city: rome` on every cinema in `cinemas.yml`.
2. **Backend released**: `refactoring` merged into `main`; CI has published
   `alesrossi/cinema-backend` and `alesrossi/cinema-backend-migrator` with the same tags (the Docker
   Hub repository for the migrator must exist or be creatable by the CI token).
3. **Rehearsed locally** on a restored copy of production (`alignment.md`, section 5), clean.
4. **Logs ready**: a DNS A record for `logs.felinoir.it` pointing at the server, and
   `GRAFANA_ADMIN_PASSWORD` added to the server's `.env` (`server/.env.example`). Without it every
   compose command fails, the migrator's included.

### On the server (`cd` to the directory with `server/docker-compose.yml`)

1. **Freeze**: disable the daily `daily-report.sh` cron entry.
2. **Note the running images** for a rollback, since `latest` is about to move:
   `docker image inspect --format '{{index .RepoDigests 0}}' alesrossi/cinema-backend:latest alesrossi/cinema-scrapers:latest`
3. **Back up**, and copy the file off the server:
   `docker compose exec -T db pg_dump -U cinema -d cinema -Fc > felinoir-$(date +%F).dump`
4. **Update** `docker-compose.yml` and the `logging/` folder from the repo (they add
   `cinema-migrator`, the `depends_on` changes and the log services), then
   `docker compose pull cinema-migrator cinema-backend cinema-scraper loki alloy grafana log-retention`.
5. **Migrate**, watching the output: `docker compose run --rm cinema-migrator`. Expect
   *"Adopting a database built by the scrapers: recording 20261029083355_Baseline as applied"*,
   then *"Applying 3 migration(s): ["20261029083600_NormaliseKeys","20261029090606_Cities","20261029110416_LobbyVersionInDomain"]"*, then
   *"Database schema is up to date"* and exit code 0. A second run only prints *"up to date"*.
6. **Start the logs, then the API**: `docker compose up -d loki alloy grafana log-retention`, then
   `docker compose up -d cinema-backend` (the frontends follow its health check).
7. **Verify**:
   - `/health`; `/cinemas` (every `city` is `"Roma"`); `/screenings?city=rome&withRelations=true`;
     `/movies/slug/<a real slug>`; every response has `X-Trace-Id`.
   - On felinoir.it: the home page, a movie page, a cinema page, a Felix question, and a lobby
     (create, join from a second browser, claim, poll).
   - `docker compose logs cinema-backend` shows one readable line per request, and
     `logs/backend/felinoir-api-<date>.json` the same events as ECS JSON.
   - `https://logs.felinoir.it` asks for the Grafana login; Explore → Loki →
     `{service_name="felinoir-api"}` shows the requests just made.
8. **Scrape once by hand**: `docker compose run --rm cinema-scraper`. It must start (the version
   guard passes), issue no DDL, and leave cinemas with `city_id = 'rome'` and coordinates:
   `docker compose exec db psql -U cinema -d cinema -c "select city_id, count(*), count(latitude), sum(open_air::int) from cinemas group by 1"`
9. **Resume** the cron.

### Rollback

Stop `cinema-backend`, restore the backup (`pg_restore --clean --if-exists -U cinema -d cinema`
from the dump), pin the image digests noted in step 2 in `docker-compose.yml`, remove the
`cinema-migrator` service and its `depends_on`, and start again. The log services can stay: the
old backend ignores `LOG_FILE_PATH`, so they simply receive only the scraper's log. Rolling back means restoring the
database, not running `Down()` migrations: the cutover is all at once.

## Afterwards: changing the schema

1. Add an EF migration in `felinoir-backend` and merge it.
2. If the scrapers write or read the new object: update the scraper code and bump
   `REQUIRED_MIGRATION`.
3. Deploy the migrator and API first, then the scraper release.
