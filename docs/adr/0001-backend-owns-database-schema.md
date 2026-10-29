# 0001 — The backend owns the database schema

**Status:** accepted · 2026-10-28

## Context

The Postgres database is shared by this backend (reads, plus lobby writes) and the Node
scrapers (`cinema-scrapers`, raw `pg`, the main writer). Until now the scrapers owned the
schema: `src/lib/db/postgres/schema.ts` re-ran ~370 lines of idempotent DDL on the first
connection of every process, and this repo copied that DDL into idempotent EF migrations
after the fact. Changing the schema meant editing both repos in lockstep, and the two drifted
— columns such as `movies.canonical_title`, `has_matches`, `tmdb_match_attempts` and the
`pg_trgm` index existed in production but not in the EF model.

## Decision

EF Core migrations in this repository are the single source of truth for all DDL.

- Every table, column, index, constraint, extension, trigger and function is in the EF model or a
  migration, including columns only the scrapers use. Drift from production is treated as a bug.
- One final idempotent migration (`AdoptScraperSchema`) absorbs what currently exists only in the
  scrapers' bootstrap. After it, migrations are ordinary, non-idempotent EF migrations.
- Migrations are applied by a dedicated one-shot migrator (EF migration bundle) that runs before
  both the API and the scrapers. The API still never migrates at startup.
- The scrapers stop issuing DDL. They check at startup that `__EFMigrationsHistory` contains the
  migration they were written against, and refuse to run otherwise.

- The existing databases (production and local) have no `__EFMigrationsHistory`: the scrapers
  created their schema, and EF migrations only ever ran in test containers. A one-time baseline
  script marks the five migrations up to `MovieCreditsAndAwards` as applied, once a drift test has
  shown the database already matches them.

## Consequences

- A schema change is one PR here plus, if the scrapers write the new column, a scraper release
  that bumps its expected migration id.
- The backend's domain model carries persistence for columns the API never exposes.
- `pg_trgm` becomes a hard requirement instead of an optional extension.
