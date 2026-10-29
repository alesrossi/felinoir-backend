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
  backend now parses it strictly, so every scraper must produce exactly that format (no
  milliseconds, no `Z`, no offset). Consider one shared formatter used by every scraper.
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
created by the scrapers' bootstrap, and EF has never recorded a migration there. The schema does
already contain everything the backend's `Baseline` migration creates; the backend's drift test
proves this against `tests/schema/production.sql`. The migrator adopts it on its first run.

1. **Freeze:** pause the daily scraper cron (`server/daily-report.sh`).
2. **Back up:** take a `pg_dump` (data and schema) of production and keep it off the server.
3. **Rehearse:** restore the dump into a local Postgres, run the new migrator against it (it
   adopts the database by recording `Baseline` as applied, then applies the later migrations),
   then
   run the new API and one scraper pass against the result. Check the characterisation
   snapshots and the drift test. Fix and repeat until it's clean.
4. **Release images:** the backend API and the migrator are tagged with the same version. The
   scraper release has `REQUIRED_MIGRATION` set to the backend's latest migration.
5. **Deploy, in order:** the migrator (one-shot; adopts production on its first run), then the
   API, then the scraper image (with the
   updated `cinemas.yml`). In `server/docker-compose.yml`, `api` and `cinema-scraper` depend on
   `migrator` with `condition: service_completed_successfully`.
6. **Verify:**
   - `/health`, `/movies`, `/cinemas` (every cinema has `city: "Roma"`) and `/screenings?city=rome`.
   - A Felix chat.
   - Create, join and poll a lobby.
   - Run the scraper manually once (`docker compose run --rm cinema-scraper`).
7. **Resume** the cron.

**Rollback:** restore the backup and redeploy the previous image tags (`main` for the API, the
previous scraper tag). The cutover is all at once, so rolling back means restoring the
database, not running `Down()` migrations.

## Afterwards: changing the schema

1. Add an EF migration in `felinoir-backend` and merge it.
2. If the scrapers write or read the new object: update the scraper code and bump
   `REQUIRED_MIGRATION`.
3. Deploy the migrator and API first, then the scraper release.
