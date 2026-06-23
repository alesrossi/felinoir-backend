#!/bin/bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"
LOGS_DIR="$SCRIPT_DIR/logs"
LOG_FILE="$LOGS_DIR/scraper.log"
DATE_STAMP=$(date '+%Y-%m-%d')

SENDMAIL="/usr/bin/msmtp"

mkdir -p "$LOGS_DIR"

echo "[$(date '+%H:%M:%S')] Running docker compose run --rm cinema-scraper…"
echo "[$(date '+%H:%M:%S')] Log file: $LOG_FILE"

SCRAPE_EXIT=0
docker compose -f "$COMPOSE_FILE" run --rm cinema-scraper || SCRAPE_EXIT=$?
# Felix corpus is rebuilt lazily by the backend on the next chat request.

echo "[$(date '+%H:%M:%S')] Scraper finished (exit $SCRAPE_EXIT). Querying DB…"

UNENRICHED=$(docker compose -f "$COMPOSE_FILE" exec -T db \
  psql -U cinema -d cinema -tAc \
  "SELECT title || COALESCE(' (' || original_title || ')', '')
   FROM movies
   WHERE tmdb_enriched_at IS NULL
   ORDER BY title;" 2>/dev/null || echo "(query failed)")

UNENRICHED_OMDB=$(docker compose -f "$COMPOSE_FILE" exec -T db \
  psql -U cinema -d cinema -tAc \
  "SELECT COUNT(*) FROM movies WHERE imdb_id IS NOT NULL AND omdb_enriched_at IS NULL;" 2>/dev/null || echo "?")

UNENRICHED_COUNT=$(docker compose -f "$COMPOSE_FILE" exec -T db \
  psql -U cinema -d cinema -tAc \
  "SELECT COUNT(*) FROM movies WHERE tmdb_enriched_at IS NULL;" 2>/dev/null || echo "?")

TOTAL_MOVIES=$(docker compose -f "$COMPOSE_FILE" exec -T db \
  psql -U cinema -d cinema -tAc \
  "SELECT COUNT(*) FROM movies;" 2>/dev/null || echo "?")

TOTAL_SCREENINGS=$(docker compose -f "$COMPOSE_FILE" exec -T db \
  psql -U cinema -d cinema -tAc \
  "SELECT COUNT(*) FROM screenings WHERE datetime >= NOW();" 2>/dev/null || echo "?")

UNENRICHED_LIST="$UNENRICHED"
if [ -z "$UNENRICHED_LIST" ]; then
  UNENRICHED_LIST="(none — all movies enriched)"
fi

STATUS_LINE=$([ "$SCRAPE_EXIT" -eq 0 ] && echo "success" || echo "FAILED (exit code $SCRAPE_EXIT)")

# Extract the pipeline summary block written at the end of the log by the scraper container.
SUMMARY_START=$(grep -n "PIPELINE SUMMARY" "$LOG_FILE" 2>/dev/null | tail -1 | cut -d: -f1)
if [ -n "$SUMMARY_START" ]; then
  PIPELINE_SUMMARY=$(tail -n +"$SUMMARY_START" "$LOG_FILE")
else
  PIPELINE_SUMMARY="(pipeline summary not found in log)"
fi

"$SENDMAIL" -t << EMAIL
From: alessandro0024@gmail.com
To: alessandro0024@gmail.com
Subject: Cinema Scrape Report — $DATE_STAMP
MIME-Version: 1.0
Content-Type: multipart/mixed; boundary="CINEMA_BOUNDARY"

--CINEMA_BOUNDARY
Content-Type: text/plain; charset=utf-8

Cinema Scrape Report — $DATE_STAMP
===================================

Run completed at: $(date '+%Y-%m-%d %H:%M:%S')
Overall status:    $STATUS_LINE

Database stats:
  Total movies:              $TOTAL_MOVIES
  Upcoming screenings:       $TOTAL_SCREENINGS
  Unenriched (TMDB):         $UNENRICHED_COUNT / $TOTAL_MOVIES
  Unenriched (OMDB, w/ IMDb): $UNENRICHED_OMDB

$PIPELINE_SUMMARY

Movies missing TMDB enrichment:
$UNENRICHED_LIST

See attached log for full details.

--CINEMA_BOUNDARY
Content-Type: text/plain; charset=utf-8
Content-Disposition: attachment; filename="scraper-$DATE_STAMP.log"

$(cat "$LOG_FILE" 2>/dev/null || echo "(log file not found)")

--CINEMA_BOUNDARY--
EMAIL

echo "[$(date '+%H:%M:%S')] Report sent."
