#!/bin/sh
# Long-running (the log-retention service): keeps Loki's data under LOG_RETENTION_MAX_MB
# (default 5120, 5 GB). Loki retains by age (31 days, loki.yaml) but has no size limit, so whenever the
# loki_data volume is over the limit this asks Loki to delete the oldest day of logs. Loki applies
# a deletion at its next compaction (within about half an hour) and frees the disk 2 h later, so
# the volume converges back under the limit a day at a time. Checks every
# LOG_RETENTION_INTERVAL_SECONDS (default 1 h).
set -u

LOKI_URL=${LOKI_URL:-http://loki:3100}
DATA_DIR=${LOKI_DATA_DIR:-/loki}
MAX_KB=$(( ${LOG_RETENTION_MAX_MB:-5120} * 1024 ))
INTERVAL=${LOG_RETENTION_INTERVAL_SECONDS:-3600}
ALL='{service_name=~".+"}'
DAY=86400

enforce() {
  used_kb=$(du -sk "$DATA_DIR" 2>/dev/null | cut -f1)
  case "$used_kb" in
    ''|*[!0-9]*) echo "Could not measure $DATA_DIR; retrying later" >&2; return ;;
  esac
  [ "$used_kb" -gt "$MAX_KB" ] || return

  now=$(date +%s)
  # The oldest line still stored, searching the whole retention period (31 days and a margin).
  oldest_ns=$(curl -sfG "$LOKI_URL/loki/api/v1/query_range" \
      --data-urlencode "query=$ALL" \
      --data-urlencode "start=$(( now - 31 * DAY ))000000000" \
      --data-urlencode "end=${now}000000000" \
      --data-urlencode "direction=forward" \
      --data-urlencode "limit=1" \
    | grep -o '"values":\[\["[0-9]*"' | head -1 | grep -o '[0-9][0-9]*') || {
    echo "Over the limit (${used_kb} KB) but could not find the oldest logs; retrying later" >&2
    return
  }

  start=$(( ${oldest_ns%?????????} ))
  end=$(( start + DAY ))
  [ "$end" -le "$now" ] || end=$now # Loki refuses deletions that reach into the future
  if curl -sf -o /dev/null -X POST -G "$LOKI_URL/loki/api/v1/delete" \
      --data-urlencode "query=$ALL" --data-urlencode "start=$start" --data-urlencode "end=$end"; then
    echo "Loki uses ${used_kb} KB, over the ${MAX_KB} KB limit: deleting logs from $(date -u -d "@$start" '+%Y-%m-%d %H:%M') UTC for one day"
  else
    echo "Over the limit (${used_kb} KB) but the delete request failed; retrying later" >&2
  fi
}

echo "Keeping Loki's data under ${MAX_KB} KB, checking every ${INTERVAL} s"
until curl -sf -o /dev/null "$LOKI_URL/ready"; do sleep 5; done
while :; do
  enforce
  sleep "$INTERVAL"
done
