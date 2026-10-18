# Cinema Backend — Specification

Reference document for reimplementing this backend in any language/framework while maintaining full frontend compatibility.

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `DATABASE_URL` | Yes | Postgres connection string |
| `GEMINI_API_KEY` | Yes (Felix) | Google Gemini API key |
| `TMDB_READ_ACCESS_TOKEN` | Scrapers only | Not needed by the HTTP server |
| `PORT` | No | HTTP port, default `3001` |

---

## REST API

All responses are JSON. CORS is enabled for all origins (`*`).

### `GET /health`

```json
{ "ok": true }
```

---

### `GET /movies`

Returns all movies, ordered by recency.

**Query params**

| Param | Type | Default | Max |
|---|---|---|---|
| `limit` | integer | 500 | 2000 |
| `offset` | integer | 0 | — |

**Response** — array of `Movie` objects (see [Entities](#entities)).

---

### `GET /movies/:id`

Returns a single movie by numeric id.

**Response** — `Movie` object, or `404 { "error": "Not found" }`.  
Returns `400 { "error": "Invalid id" }` if `:id` is not a number.

---

### `GET /movies/slug/:slug`

Returns a single movie by its URL slug.

**Response** — `Movie` object, or `404 { "error": "Not found" }`.

> **Important:** this route must be registered **before** `/:id` or routers that match `:id` greedily will treat `"slug"` as an id.

---

### `GET /screenings`

Returns screenings with optional filtering and pagination.

**Query params**

| Param | Type | Description |
|---|---|---|
| `cinemaId` | string | Filter by cinema slug |
| `movieId` | integer | Filter by movie id |
| `city` | string | Filter by city |
| `from` | ISO 8601 datetime | Lower bound on `datetime` (inclusive) |
| `to` | ISO 8601 datetime | Upper bound on `datetime` (inclusive) |
| `withRelations` | `"true"` | Include nested `movie` and `cinema` objects on each result |
| `limit` | integer | Default 50 000, max 100 000 |
| `offset` | integer | Default 0 |

**Response** — array of `Screening` objects (see [Entities](#entities)).

---

### `GET /cinemas`

Returns all cinemas from the database.

**Response** — array of `Cinema` objects.

---

### `GET /cinemas/:id`

Returns a single cinema by its string id (slug).

**Response** — `Cinema` object, or `404 { "error": "Not found" }`.

---

### `POST /felix/chat`

Sends a chat message to Felix (Gemini-backed cinema assistant).

**Request body**

```json
{
  "messages": [
    { "role": "user", "content": "Cosa c'è stasera?" }
  ],
  "filters": {
    "cinemas": ["cinema-farnese"],
    "dates": ["2026-06-22"],
    "times": ["sera"],
    "genres": ["Horror"],
    "ov": false,
    "q": "vampiri"
  }
}
```

`messages` — required, non-empty array of `{ role: "user" | "assistant", content: string }`.  
`filters` — optional; active filters from the UI, appended as context to the last user message.

**Response**

```json
{ "reply": "Stasera al Farnese trovi…" }
```

**Error responses**

| Status | Body | Condition |
|---|---|---|
| 400 | `{ "error": "Richiesta non valida." }` | Missing/malformed messages |
| 503 | `{ "error": "Felix non è configurato al momento." }` | `GEMINI_API_KEY` not set |
| 500 | `{ "error": "Qualcosa è andato storto. Riprova tra qualche secondo." }` | Gemini call failed |

---

### Lobbies

Shareable group outings to a screening (`/e/<id>` on the frontend). Every request must carry `X-Device-Token`, which the Next.js proxy sets from its `felinoir_device` cookie. Only `sha256(token)` is stored, and it decides `isMe` / `meId`. Every change bumps `version` through database triggers and returns the full lobby (`200`). Lobbies expire 48 h after their screening starts and are then `404`. Editing stays open until they expire.

| Method & path | Body | Does |
|---|---|---|
| `POST /lobbies` | `{ screeningId, name, note? }` | Snapshot of the screening from the schedule; creator joins first. `201` + `Location`. |
| `GET /lobbies/:id?ifVersion=N` | | `304` (no body) if still at version N, else `200`. |
| `PATCH /lobbies/:id` | `{ note }` | Edit the note (anyone). |
| `POST /lobbies/:id/participants` | `{ name }` | Join with this device. |
| `PATCH /lobbies/:id/participants/:pid` | `{ name }` | Rename anyone. |
| `DELETE /lobbies/:id/participants/:pid` | | Remove anyone (also how you leave). |
| `POST /lobbies/:id/participants/:pid/claim` | | Move this device onto that entry, off any other entry in the lobby. |

**Response**

```ts
{
  id: string                // uuid
  version: number
  screening: {
    screeningId: number | null   // null once it left the schedule
    available: boolean
    movieId: number | null
    movieSlug: string | null
    movieTitle: string           // tmdbTitle ?? title at creation
    movieTitleEn: string | null  // tmdbTitleEn
    posterUrl: string | null     // tmdbPosterUrl ?? posterUrl
    cinemaId: string
    cinemaName: string
    datetime: string             // Rome wall clock, e.g. "2026-10-17T21:00:00"
    isOV: boolean
    is3D: boolean
    bookingUrl: string | null
  }
  note: string
  participants: { id: string, name: string, joinedAt: string /* ISO UTC */, isMe: boolean }[]  // by joinedAt
  meId: string | null
  expiresAt: string         // ISO UTC, starts_at + 48 h
}
```

Unlike the rest of the API, nullable fields here are sent as `null`, never omitted.

**Validation and errors** (`{ "error": "<code>" }`): names are trimmed, 1–40 characters, with no control characters. Notes are at most 500 characters.

| Status | Code | When |
|---|---|---|
| 400 | `missing_device` | No `X-Device-Token` header |
| 400 | `invalid_name` / `invalid_note` | Fails validation |
| 404 | `not_found` | Unknown, malformed or expired lobby; unknown participant |
| 409 | `name_taken` | Name already in the lobby (case-insensitive), on join or rename |
| 409 | `already_joined` | This device already has an entry |
| 422 | `screening_not_found` | `POST /lobbies` with an unknown screening |
| 422 | `screening_past` | `POST /lobbies` for a screening that has started |

---

## Entities

All fields use **camelCase** in JSON responses, mapped from snake_case columns.

### Cinema

```ts
{
  id: string              // slug, e.g. "cinema-farnese"
  name: string
  chain?: string
  address?: string
  city: string
  country: string         // default "IT"
  website: string
  scheduleUrl: string
  createdAt: string       // ISO 8601 UTC
  updatedAt: string
}
```

### Movie

```ts
{
  id: number              // SERIAL
  title: string           // scraped Italian title
  originalTitle?: string
  director?: string
  durationMinutes?: number
  genres?: string         // comma-separated
  synopsis?: string
  posterUrl?: string
  trailerUrl?: string
  externalId?: string
  rating?: string
  slug?: string           // URL-safe identifier, unique

  // TMDB enrichment
  tmdbId?: number
  imdbId?: string
  tmdbRating?: string
  tmdbVotes?: string
  tmdbTitle?: string      // Italian title from TMDB
  tmdbTitleEn?: string    // English title from TMDB
  tmdbOriginalTitle?: string
  tmdbPlot?: string       // Italian synopsis
  tmdbPlotEn?: string
  tmdbGenre?: string      // Italian genre string
  tmdbGenreEn?: string
  tmdbLanguage?: string   // original language code
  tmdbCountry?: string
  tmdbPosterUrl?: string
  tmdbTrailerUrlIt?: string
  tmdbTrailerUrlEn?: string
  tmdbPopularity?: string
  tmdbStatus?: string
  tmdbTagline?: string
  tmdbBudget?: number
  tmdbRevenue?: number
  tmdbHomepage?: string
  tmdbCollection?: string           // JSON: { id, name, poster_path }
  tmdbProductionCompanies?: string  // JSON: { id, name, ... }[]
  tmdbProductionCountries?: string  // JSON: { iso_3166_1, name }[]
  tmdbSpokenLanguages?: string      // JSON: { iso_639_1, name }[]
  tmdbBackdrops?: string            // JSON array
  tmdbVideos?: string               // JSON array
  tmdbKeywords?: string             // JSON: { name }[]
  tmdbRecommendations?: string      // JSON array
  tmdbWatchProviders?: string       // JSON object
  tmdbCredits?: string              // JSON: TmdbCredits, see below
  tmdbEnrichedAt?: string           // ISO 8601 UTC

  contentRating?: string  // Italian certification from TMDB, e.g. "T", "VM14"
  writer?: string
  actors?: string
  released?: string       // "YYYY-MM-DD"

  // OMDb enrichment
  imdbRating?: string
  imdbVotes?: string
  rtRating?: string       // Rotten Tomatoes %
  metacriticScore?: string
  awards?: string         // e.g. "Won 2 Oscars. 126 wins & 375 nominations total"
  omdbEnrichedAt?: string

  createdAt: string
  updatedAt: string
}
```

`tmdbCredits` is a raw JSON string (like the other `tmdb*` blobs), written by the scraper's TMDB enrichment:

```ts
type Person = { id: number, name: string, profile_path: string | null }   // TMDB person id
type TmdbCredits = {
  directors: Person[]
  writers: (Person & { jobs: string[] })[]   // "Screenplay" | "Writer" | "Novel" | "Story", one entry per person
  cast: (Person & { character: string })[]   // top 12 by billing order
  composers: Person[]                        // "Original Music Composer" / "Music"
  cinematographers: Person[]                 // "Director of Photography"
}
```

Photo URL: `https://image.tmdb.org/t/p/w185{profile_path}` (other sizes: `w45`, `h632`, `original`).

### Screening

```ts
{
  id: number
  movieId: number
  cinemaId: string
  datetime: string        // ISO 8601 UTC, e.g. "2026-06-22T20:30:00Z"
  hall?: string
  is3D: boolean
  isOV: boolean           // original version (no dubbing)
  subtitleLanguage?: string
  bookingUrl?: string
  meta?: string           // arbitrary JSON blob from scraper

  // only present when withRelations=true
  movie?: Movie
  cinema?: Cinema

  createdAt: string
  updatedAt: string
}
```

---

## Database Schema

Postgres 16. All timestamps stored as `TEXT` in ISO 8601 UTC (`YYYY-MM-DDThh:mm:ssZ`), except in `lobbies` / `lobby_participants`, which use `TIMESTAMPTZ`.

### `cinemas`

| Column | Type | Notes |
|---|---|---|
| `id` | TEXT PK | slug |
| `name` | TEXT NOT NULL | |
| `chain` | TEXT | |
| `address` | TEXT | |
| `city` | TEXT NOT NULL | |
| `country` | TEXT NOT NULL | default `'IT'` |
| `website` | TEXT NOT NULL | |
| `schedule_url` | TEXT NOT NULL | |
| `created_at` | TEXT NOT NULL | |
| `updated_at` | TEXT NOT NULL | |

Index: `idx_cinemas_city` on `city`.

### `movies`

| Column | Type | Notes |
|---|---|---|
| `id` | SERIAL PK | |
| `title` | TEXT NOT NULL | |
| `original_title` | TEXT | |
| `director` | TEXT | |
| `duration_minutes` | INTEGER | |
| `genres` | TEXT | |
| `synopsis` | TEXT | |
| `poster_url` | TEXT | |
| `trailer_url` | TEXT | |
| `external_id` | TEXT | |
| `rating` | TEXT | |
| `slug` | TEXT | unique where not null |
| `tmdb_id` | INTEGER | unique where not null |
| `imdb_id` | TEXT | unique where not null |
| `tmdb_rating` | TEXT | |
| `tmdb_votes` | TEXT | |
| `tmdb_title` | TEXT | |
| `tmdb_title_en` | TEXT | |
| `tmdb_original_title` | TEXT | |
| `tmdb_plot` | TEXT | |
| `tmdb_plot_en` | TEXT | |
| `tmdb_genre` | TEXT | |
| `tmdb_genre_en` | TEXT | |
| `tmdb_language` | TEXT | |
| `tmdb_country` | TEXT | |
| `tmdb_poster_url` | TEXT | |
| `tmdb_trailer_url_it` | TEXT | |
| `tmdb_trailer_url_en` | TEXT | |
| `tmdb_popularity` | TEXT | |
| `tmdb_status` | TEXT | |
| `tmdb_tagline` | TEXT | |
| `tmdb_budget` | BIGINT | |
| `tmdb_revenue` | BIGINT | |
 | `tmdb_homepage` | TEXT | |
| `tmdb_collection` | TEXT | JSON blob |
| `tmdb_production_companies` | TEXT | JSON blob |
| `tmdb_production_countries` | TEXT | JSON blob |
| `tmdb_spoken_languages` | TEXT | JSON blob |
| `tmdb_backdrops` | TEXT | JSON blob |
| `tmdb_videos` | TEXT | JSON blob |
| `tmdb_keywords` | TEXT | JSON blob |
| `tmdb_recommendations` | TEXT | JSON blob |
| `tmdb_watch_providers` | TEXT | JSON blob |
| `tmdb_credits` | TEXT | JSON blob (`TmdbCredits`) |
| `tmdb_enriched_at` | TEXT | |
| `content_rating` | TEXT | |
| `writer` | TEXT | |
| `actors` | TEXT | |
| `released` | TEXT | `YYYY-MM-DD` |
| `imdb_rating` | TEXT | |
| `imdb_votes` | TEXT | |
| `rt_rating` | TEXT | |
| `metacritic_score` | TEXT | |
| `awards` | TEXT | OMDb awards line |
| `omdb_enriched_at` | TEXT | |
| `created_at` | TEXT NOT NULL | |
| `updated_at` | TEXT NOT NULL | |

Unique index: `(title, COALESCE(original_title, ''))` — deduplication key used by scrapers.

### `screenings`

| Column | Type | Notes |
|---|---|---|
| `id` | SERIAL PK | |
| `movie_id` | INTEGER NOT NULL | FK → movies(id) ON DELETE CASCADE |
| `cinema_id` | TEXT NOT NULL | FK → cinemas(id) ON DELETE CASCADE |
| `datetime` | TEXT NOT NULL | ISO 8601 UTC |
| `hall` | TEXT | |
| `is_3d` | BOOLEAN NOT NULL | default false |
| `is_ov` | BOOLEAN NOT NULL | default false |
| `subtitle_language` | TEXT | |
| `booking_url` | TEXT | |
| `meta` | TEXT | arbitrary JSON from scraper |
| `created_at` | TEXT NOT NULL | |
| `updated_at` | TEXT NOT NULL | |

Unique constraint: `(cinema_id, movie_id, datetime)` — prevents duplicate showings.

### `tmdb_lookup_cache`

Caches title → TMDB id resolutions to avoid redundant API calls during enrichment.

| Column | Type | Notes |
|---|---|---|
| `scraped_title` | TEXT NOT NULL | |
| `scraped_original_title` | TEXT NOT NULL | default `''` |
| `tmdb_id` | INTEGER NOT NULL | |
| `looked_up_at` | TEXT NOT NULL | |
| `scraped_poster_url` | TEXT | poster of the scraped row the entry was written for |

Unique index on `(scraped_title, scraped_original_title)`.

### `ai_match_verdicts`

What the scraper's AI verification pass (`npm run enrich:ai`) concluded about a scraped title. Keyed like `tmdb_lookup_cache`, not by movie id, so a verdict outlives the row it was reached on. Written by the scraper only; the API never reads it.

| Column | Type | Notes |
|---|---|---|
| `scraped_title` | TEXT NOT NULL | |
| `scraped_original_title` | TEXT NOT NULL | default `''` |
| `verdict` | TEXT NOT NULL | `match` \| `not_a_film` \| `unknown` (CHECK) |
| `tmdb_id` | INTEGER | |
| `confidence` | TEXT NOT NULL | `high` \| `low` (CHECK) |
| `checked_tmdb_id` | INTEGER | |
| `rejected_tmdb_ids` | INTEGER[] NOT NULL | default `'{}'` |
| `reason` | TEXT | |
| `model` | TEXT | |
| `verified_at` | TEXT NOT NULL | ISO 8601 UTC |

Unique index on `(scraped_title, scraped_original_title)`.

### `felix_cache`

Single-row table (id = 1) that persists the active Gemini context cache reference across server restarts.

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PK | always 1 |
| `cache_name` | TEXT NOT NULL | Gemini resource name, e.g. `cachedContents/abc123` |
| `expires_at` | TEXT NOT NULL | ISO 8601 UTC |
| `created_at` | TEXT NOT NULL | |

### `lobbies`

A group outing to one screening. The screening's details are copied in once at creation; there is intentionally no FK to `screenings`, since screenings are re-scraped.

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | default `gen_random_uuid()` |
| `version` | BIGINT NOT NULL | default `1`; bumped by triggers, polled by clients |
| `screening_id` | INTEGER | no FK; check `EXISTS` in `screenings` for the `available` flag |
| `movie_id` | INTEGER | |
| `movie_slug` | TEXT | |
| `movie_title` | TEXT NOT NULL | |
| `movie_title_en` | TEXT | |
| `poster_url` | TEXT | |
| `cinema_id` | TEXT NOT NULL | |
| `cinema_name` | TEXT NOT NULL | |
| `starts_at` | TIMESTAMPTZ NOT NULL | Rome wall-clock converted with `AT TIME ZONE 'Europe/Rome'`, both ways |
| `is_ov` | BOOLEAN NOT NULL | default false |
| `is_3d` | BOOLEAN NOT NULL | default false |
| `booking_url` | TEXT | |
| `note` | TEXT NOT NULL | default `''`; CHECK `lobbies_note_length` (≤ 500 chars) |
| `created_at` | TIMESTAMPTZ NOT NULL | default `now()` |

Index `lobbies_starts_at_idx` on `starts_at`. `expiresAt` is `starts_at + interval '48 hours'`, computed in queries; expired lobbies must be treated as not found.

### `lobby_participants`

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | default `gen_random_uuid()` |
| `lobby_id` | UUID NOT NULL | FK → lobbies(id) ON DELETE CASCADE |
| `name` | TEXT NOT NULL | CHECK `lobby_participants_name_length` (1–40 chars) |
| `device_hash` | BYTEA | SHA-256 of the device token; null = unclaimed |
| `joined_at` | TIMESTAMPTZ NOT NULL | default `now()` |

- Unique constraint `lobby_participants_one_per_device` on `(lobby_id, device_hash)`. A 23505 on it maps to `already_joined`.
- Unique index `lobby_participants_name_uniq` on `(lobby_id, lower(name))`. A 23505 on it maps to `name_taken`.
- Claiming a name, in one transaction: first null this device's other entry in the lobby, then set it on the claimed row. The reverse order hits the unique constraint.

### Lobby triggers & functions

- `lobby_participants_bump_version` (AFTER INSERT/UPDATE/DELETE on `lobby_participants`): `lobbies.version + 1`.
- `lobbies_bump_version_on_note` (BEFORE UPDATE OF `note`, when it actually changes): `version + 1`.
- `delete_expired_lobbies()` returns the number of deleted lobbies (those with `starts_at` more than 48 h ago). The API calls it at startup and hourly (`ExpiredLobbyCleanupService`), since the server has no pg_cron.

---

## Felix AI Subsystem

Felix is a Gemini-backed chat assistant that answers questions about films showing in Rome.

### Corpus

Built on demand from the database. Contains every movie that has at least one future screening, formatted as Markdown with:
- Title (Italian, original, English)
- Tagline, genres, year, runtime, language, content rating, country
- Director, writer, cast
- Saga (collection)
- Ratings: IMDb → RT → Metacritic
- Keywords (up to 12)
- Plot summary
- Screenings grouped by cinema, with date/time and OV/3D tags

### Caching strategy

Two layers, tried in order:

1. **Gemini context cache** — the corpus is uploaded once via `POST /v1beta/cachedContents` with a 24 h TTL. The resulting `cache_name` is persisted to `felix_cache`. On every request `ensureReady()` checks memory → DB → creates new cache. Falls back to layer 2 if cache creation fails or returns 404.

2. **Inline context** — corpus is prepended directly to the conversation contents on every Gemini call. Valid for 24 h in memory, then rebuilt.

### Model

`gemini-2.5-flash-lite` via the REST API (`https://generativelanguage.googleapis.com/v1beta`).

Generation config: `maxOutputTokens: 700`, `temperature: 0.4`.

### Filter context

When the frontend passes `filters`, a plain-text block is appended to the last user message before sending to Gemini:

```
[Note: I currently have these filters active on felinoir.it]
- Cinemas: cinema-farnese
- Dates: today (2026-06-22)
- Genres: Horror
Please only include films and screenings that match ALL of these filters.
```

### System prompt (summary)

- Felix is the cinema assistant for felinoir.it, covering films showing in Rome
- Always assumes the user is in Rome
- Replies in the same language the user writes in (IT or EN)
- Prefers IMDb → RT → MC for ratings; only uses TMDB if none available
- Never invents showtimes

---

## Static Cinema Config

`cinemas.yml` at the project root is the source of truth for cinema metadata not stored in the DB (coordinates, `openAir` flag). The corpus builder reads this to tag open-air venues with `[Open Air]` in the screening list.

```yaml
- id: cinema-farnese
  name: Cinema Farnese
  address: Campo de' Fiori 56, Roma
  website: https://...
  openAir: false
  coordinates: { lat: 41.894, lng: 12.472 }
```

The `id` must match the `cinema.id` in the database.

---

## Behavioural Notes

- **No authentication** — all endpoints are public
- **CORS** — all origins allowed (`*`)
- **Timestamps** — stored and returned as `TEXT` in ISO 8601 UTC; the DB never uses `TIMESTAMPTZ` columns
- **Genres on Movie** — the scraped `genres` field is a comma-separated string; TMDB enrichment adds `tmdb_genre` (Italian) and `tmdb_genre_en` (English) as separate columns
- **JSON blobs** — TMDB fields like `tmdb_keywords`, `tmdb_collection`, `tmdb_production_companies` etc. are stored as raw JSON strings in TEXT columns; the backend returns them as strings, not parsed objects
- **Slug uniqueness** — slugs are generated from the Italian title; the unique index is partial (`WHERE slug IS NOT NULL`)
- **Screening deduplication** — the unique constraint `(cinema_id, movie_id, datetime)` is the upsert key; scraper uses `ON CONFLICT DO UPDATE`
