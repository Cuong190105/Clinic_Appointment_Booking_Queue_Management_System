#!/usr/bin/env bash
# Entry point of the db-backup sidecar. SQL Server Express has no SQL Agent, so this loop is the job
# scheduler: every BACKUP_TICK_SECONDS it runs backup.sh tick (each database decides from its policy
# whether a FULL, DIFF or LOG backup is due) and starts a restore drill when one is due.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=scheduler
TICK_SECONDS="${BACKUP_TICK_SECONDS:-60}"
DRILL_INTERVAL_MIN="${BACKUP_DRILL_INTERVAL_MIN:-10080}"
DRILL_FIRST_AFTER_MIN="${BACKUP_DRILL_FIRST_AFTER_MIN:-30}"

[[ -n "${SQLCMDPASSWORD:-}" ]] || die "SQLCMDPASSWORD (svc_backup password) is not set"
[[ -f "$POLICY_FILE" ]] || die "policy file $POLICY_FILE not found"
ensure_dir "$BACKUP_ROOT"; ensure_dir "$REPORT_DIR"
mkdir -p "$OFFSITE_ROOT" 2>/dev/null || true

log "policy $(basename "$POLICY_FILE"), tick ${TICK_SECONDS}s, drill every ${DRILL_INTERVAL_MIN} min, off-site: $OFFSITE_ROOT"
policy_lines | while read -r line; do log "  $line"; done

until sql master "SELECT 1" >/dev/null 2>&1; do log "waiting for SQL Server ($SQL_SERVER) as $SQL_USER"; sleep 5; done

# First drill a little after start (once the first chains exist), then on the interval.
[[ -f "$REPORT_DIR/last-drill" ]] ||
  echo $(( $(date -u +%s) - DRILL_INTERVAL_MIN * 60 + DRILL_FIRST_AFTER_MIN * 60 )) > "$REPORT_DIR/last-drill"

while true; do
  # The lock serialises scheduled ticks with manual runs (scripts/backup.ps1 now/snapshot/restore).
  flock -w 600 /tmp/clinic-backup.lock bash "$SCRIPT_DIR/backup.sh" tick || log "tick finished with errors (see events.jsonl)"
  bash "$SCRIPT_DIR/status.sh" --quiet || log "status: at risk (reports/status.json)"

  last=$(cat "$REPORT_DIR/last-drill" 2>/dev/null || echo 0)
  if (( $(date -u +%s) - last >= DRILL_INTERVAL_MIN * 60 )); then
    # Drills restore into scratch databases, so they run beside the backup ticks, not under their lock.
    # Mark the start now so a crashing drill is retried on the next interval, not every tick.
    date -u +%s > "$REPORT_DIR/last-drill"
    ( flock -n /tmp/clinic-drill.lock bash "$SCRIPT_DIR/drill.sh" --scheduled || log "drill reported failures" ) &
  fi
  sleep "$TICK_SECONDS"
done
