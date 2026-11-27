#!/usr/bin/env bash
# Shared helpers for the db-backup sidecar (docs/BACKUP_RECOVERY.md).
#
# File layout under $BACKUP_ROOT (a volume shared with the SQL Server container at the same path):
#   <Database>/<Database>_<yyyymmddThhmmssZ>_<FULL|DIFF|LOG>.<bak|trn>   restore chains
#   snapshots/<Database>_<ts>_COPYONLY.bak                               on-demand copy-only fulls
#   keys/<volume>_<ts>_KEYS.tar.gz                                        JWT signing / data-protection keys
#   reports/                                                               events, status, drill reports
#   restore-staging/                                                       decompressed off-site files
# The timestamp in the name is the backup start time (UTC); chains are selected from names alone,
# so restores work even when msdb (backup history) is lost together with the server.
set -o pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
SQL_SERVER="${BACKUP_SQL_SERVER:-db,1433}"
SQL_USER="${BACKUP_SQL_USER:-svc_backup}"
# The password is read by sqlcmd from SQLCMDPASSWORD (never passed on the command line).
BACKUP_ROOT="${BACKUP_ROOT:-/var/opt/mssql/backup}"
OFFSITE_ROOT="${BACKUP_OFFSITE_ROOT:-/offsite}"
KEY_SOURCES="${BACKUP_KEY_SOURCES:-/sources}"
REPORT_DIR="$BACKUP_ROOT/reports"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POLICY_FILE="${BACKUP_POLICY_FILE:-$SCRIPT_DIR/${BACKUP_POLICY:-policy}.conf}"
SQL_UID=10001   # mssql user inside the SQL Server container; it writes the .bak/.trn files

log() { printf '%s [%s] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${LOG_TAG:-backup}" "$*"; }
die() { log "ERROR: $*"; exit 1; }

# sql <database> <statement>: rows as '|'-separated text without headers; non-zero exit on SQL error.
sql() {
  "$SQLCMD" -C -S "$SQL_SERVER" -U "$SQL_USER" -d "$1" -b -h -1 -W -s '|' -l 15 -t 0 \
    -Q "SET NOCOUNT ON; $2"
}

# Directory writable by the SQL Server process (it creates the backup files, not this container).
ensure_dir() {
  mkdir -p "$1"
  chown "$SQL_UID:0" "$1" 2>/dev/null || true
  chmod 2775 "$1" 2>/dev/null || true
}

now_ts() { date -u +%Y%m%dT%H%M%SZ; }
ts_of() { basename "$1" | sed -nE 's/^.*_([0-9]{8}T[0-9]{6}Z)_[A-Z]+\..*$/\1/p'; }
ts_epoch() { local t="$1"; date -u -d "${t:0:4}-${t:4:2}-${t:6:2} ${t:9:2}:${t:11:2}:${t:13:2}" +%s; }
ts_iso() { local t="$1"; printf '%s-%s-%sT%s:%s:%s' "${t:0:4}" "${t:4:2}" "${t:6:2}" "${t:9:2}" "${t:11:2}" "${t:13:2}"; }
age_min_ts() { [[ -z "$1" ]] && { echo 999999; return; }; echo $(( ($(date -u +%s) - $(ts_epoch "$1")) / 60 )); }
age_min_file() { [[ -z "$1" ]] && { echo 999999; return; }; age_min_ts "$(ts_of "$1")"; }
max_ts() { printf '%s\n' "$@" | grep -E '^[0-9]{8}T' | sort | tail -n1; }

# Completed chain files of one type for a database, oldest first (works for primary and .gz off-site copies).
chain_files() { # <dir> <database> <FULL|DIFF|LOG>
  ls -1 "$1/$2_"*"_$3".* 2>/dev/null | grep -vE '\.(partial|tmp|sha256)$' | sort
}
latest_file() { chain_files "$BACKUP_ROOT/$1" "$1" "$2" | tail -n1; }

policy_lines() { grep -vE '^[[:space:]]*(#|$)' "$POLICY_FILE"; }
policy_for() { policy_lines | awk -v db="$1" '$1 == db'; }
policy_databases() { policy_lines | awk '{print $1}'; }

db_exists() { [[ "$(sql master "SELECT COUNT(*) FROM sys.databases WHERE name = N'$1'" 2>/dev/null | tr -d '[:space:]')" == "1" ]]; }
recovery_model() { sql master "SELECT recovery_model_desc FROM sys.databases WHERE name = N'$1'" | tr -d '[:space:]'; }
db_state() { sql master "SELECT state_desc FROM sys.databases WHERE name = N'$1'" | tr -d '[:space:]'; }
# 1 when a log chain exists (FULL model + a full backup taken since the switch); 0 means the next backup must be FULL.
log_chain_started() {
  sql master "SELECT CASE WHEN last_log_backup_lsn IS NULL THEN 0 ELSE 1 END FROM sys.database_recovery_status WHERE database_id = DB_ID(N'$1')" | tr -d '[:space:]'
}

# Append one JSON event per backup/restore/drill step (reports/events.jsonl).
event() { # <kind> <database> <type> <ok:true|false> <seconds> <file> <message>
  ensure_dir "$REPORT_DIR"
  local bytes=0
  [[ -f "$6" ]] && bytes=$(stat -c %s "$6")
  printf '{"at":"%s","kind":"%s","database":"%s","type":"%s","ok":%s,"seconds":%s,"bytes":%s,"file":"%s","message":"%s"}\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$1" "$2" "$3" "$4" "$5" "$bytes" "$(basename "${6:-}")" "${7//\"/\'}" \
    >> "$REPORT_DIR/events.jsonl"
}

json_escape() { local s="${1//\\/\\\\}"; s="${s//\"/\\\"}"; printf '%s' "${s//$'\n'/ }"; }
