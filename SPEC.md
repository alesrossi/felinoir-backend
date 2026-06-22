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
  tmdbEnrichedAt?: string           // ISO 8601 UTC

  contentRating?: string  // e.g. "R", "PG-13"
  writer?: string
  actors?: string
  released?: string       // "YYYY-MM-DD"

  // OMDb enrichment
  imdbRating?: string
  imdbVotes?: string
  rtRating?: string       // Rotten Tomatoes %
  metacriticScore?: string
  omdbEnrichedAt?: string

  createdAt: string
  updatedAt: string
}
```

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

Postgres 16. All timestamps stored as `TEXT` in ISO 8601 UTC (`YYYY-MM-DDThh:mm:ssZ`).

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
| `tmdb_budget` | INTEGER | |
| `tmdb_revenue` | INTEGER | |
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
| `tmdb_enriched_at` | TEXT | |
| `content_rating` | TEXT | |
| `writer` | TEXT | |
| `actors` | TEXT | |
| `released` | TEXT | `YYYY-MM-DD` |
| `imdb_rating` | TEXT | |
| `imdb_votes` | TEXT | |
| `rt_rating` | TEXT | |
| `metacritic_score` | TEXT | |
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

Unique index on `(scraped_title, scraped_original_title)`.

### `felix_cache`

Single-row table (id = 1) that persists the active Gemini context cache reference across server restarts.

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PK | always 1 |
| `cache_name` | TEXT NOT NULL | Gemini resource name, e.g. `cachedContents/abc123` |
| `expires_at` | TEXT NOT NULL | ISO 8601 UTC |
| `created_at` | TEXT NOT NULL | |

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
