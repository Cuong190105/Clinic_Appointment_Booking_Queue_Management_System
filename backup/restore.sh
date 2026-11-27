#!/usr/bin/env bash
# Point-in-time restore from the backup chain (docs/BACKUP_RECOVERY.md §Restore).
#
#   restore.sh <db> [--at "YYYY-MM-DD HH:MM:SS"] [--as <new-name>] [--replace] [--source primary|offsite]
#                   [--file <snapshot.bak>] [--dry-run] [--no-tail-log]
#
#   --at        UTC point in time (default: latest possible). FULL-model databases stop exactly there via
#               STOPAT; SIMPLE-model databases restore to the newest full/diff taken at or before it.
#   --as        restore into another database (side-by-side, e.g. to recover deleted rows or for drills).
#   --replace   overwrite an existing database. In place (no --as) a tail-log backup is taken first
#               so no committed transaction is lost (RPO = 0 when the database itself survived).
#   --source    primary volume (default) or the gzip off-site copy (when the primary volume is lost).
#   --file      restore one stand-alone file (e.g. a COPY_ONLY snapshot) instead of a chain.
#
# Prints the plan, executes it, and ends with one line "RESULT {json}" (consumed by drill.sh).
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=restore

db=""; at=""; target=""; replace=0; source_tier=primary; single_file=""; dry_run=0; tail_log=1
while [[ $# -gt 0 ]]; do
  case "$1" in
    --at) at="$2"; shift 2 ;;
    --as) target="$2"; shift 2 ;;
    --replace) replace=1; shift ;;
    --source) source_tier="$2"; shift 2 ;;
    --file) single_file="$2"; shift 2 ;;
    --dry-run) dry_run=1; shift ;;
    --no-tail-log) tail_log=0; shift ;;
    -*) die "unknown option $1" ;;
    *) db="$1"; shift ;;
  esac
done
[[ -n "$db" ]] || die "usage: restore.sh <db> [--at ...] [--as ...] [--replace] [--source primary|offsite] [--file ...]"
[[ -n "$target" ]] || target="$db"
[[ "$target" =~ ^[A-Za-z0-9_]+$ ]] || die "invalid target database name: $target"
in_place=0; [[ "$target" == "$db" ]] && in_place=1

target_ts=""
if [[ -n "$at" ]]; then
  target_ts=$(date -u -d "$at" +%Y%m%dT%H%M%SZ) || die "cannot parse --at '$at' (use 'YYYY-MM-DD HH:MM:SS', UTC)"
fi
stopat_iso=""; [[ -n "$target_ts" ]] && stopat_iso=$(ts_iso "$target_ts")

if db_exists "$target" && (( ! replace )); then
  die "database $target exists; pass --replace to overwrite it or --as <new-name> to restore side by side"
fi

started=$(date +%s)
staging=""
cleanup() { [[ -n "$staging" && -d "$staging" ]] && rm -rf "$staging"; }
trap cleanup EXIT

case "$source_tier" in
  primary) src_dir="$BACKUP_ROOT/$db" ;;
  offsite) src_dir="$OFFSITE_ROOT/$db" ;;
  *) die "--source must be primary or offsite" ;;
esac

# Off-site files are gzip copies: verify the sha256, then decompress into a staging directory on the
# shared volume (the SQL Server process must be able to read them).
# Runs in $(...) subshells: report failures through the exit status, the caller dies.
stage() { # <file> -> path readable by SQL Server
  local f="$1" out
  if [[ "$f" != *.gz ]]; then echo "$f"; return 0; fi
  if [[ -f "$f.sha256" ]]; then
    (cd "$(dirname "$f")" && sha256sum -c --status "$(basename "$f").sha256") || { log "checksum mismatch for $(basename "$f")" >&2; return 1; }
  fi
  out="$staging/$(basename "${f%.gz}")"
  gzip -dc "$f" > "$out" || { log "cannot decompress $(basename "$f")" >&2; return 1; }
  chmod 644 "$out"
  echo "$out"
}

# --- build the chain -------------------------------------------------------------------------------
chain=()
if [[ -n "$single_file" ]]; then
  [[ -f "$single_file" ]] || die "file not found: $single_file"
  chain=("$single_file")
else
  # Tail-log backup for an in-place restore of a surviving FULL-model database: captures everything
  # committed since the last log backup and leaves the database in RESTORING (no new writes).
  if (( in_place && replace && tail_log )) && [[ "$source_tier" == primary ]] && db_exists "$db" &&
     [[ "$(recovery_model "$db")" == FULL && "$(db_state "$db")" == ONLINE ]]; then
    tail_file="$src_dir/${db}_$(now_ts)_LOG.trn"
    log "taking tail-log backup -> $(basename "$tail_file")"
    if (( ! dry_run )); then
      sql master "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" >/dev/null || die "cannot get exclusive access to $db"
      if ! sql master "BACKUP LOG [$db] TO DISK = N'$tail_file' WITH NORECOVERY, CHECKSUM, INIT, FORMAT, NAME = N'$db TAIL'" >/dev/null; then
        sql master "ALTER DATABASE [$db] SET MULTI_USER" >/dev/null
        die "tail-log backup failed; database left online. Re-run with --no-tail-log to accept losing changes since the last log backup."
      fi
      event backup "$db" TAIL true 0 "$tail_file" "tail-log before in-place restore"
    fi
  fi

  full=""
  for f in $(chain_files "$src_dir" "$db" FULL); do
    [[ -z "$target_ts" || ! "$(ts_of "$f")" > "$target_ts" ]] && full="$f"
  done
  [[ -n "$full" ]] || die "no FULL backup of $db at or before ${at:-now} in $src_dir"
  full_ts=$(ts_of "$full")
  diff=""
  for f in $(chain_files "$src_dir" "$db" DIFF); do
    t=$(ts_of "$f")
    [[ "$t" > "$full_ts" ]] && [[ -z "$target_ts" || ! "$t" > "$target_ts" ]] && diff="$f"
  done
  chain=("$full"); base_ts="$full_ts"
  [[ -n "$diff" ]] && { chain+=("$diff"); base_ts=$(ts_of "$diff"); }
  # Logs after the base, up to and including the first one that reaches the target time.
  for f in $(chain_files "$src_dir" "$db" LOG); do
    t=$(ts_of "$f")
    [[ "$t" > "$base_ts" ]] || continue
    chain+=("$f")
    [[ -n "$target_ts" && ! "$t" < "$target_ts" ]] && break
  done
fi

log "restore $db -> $target from $source_tier, target time: ${stopat_iso:-latest}"
for f in "${chain[@]}"; do log "  $(basename "$f")"; done
restore_point_ts=$(ts_of "${chain[-1]}")
[[ -n "$target_ts" && "$restore_point_ts" > "$target_ts" ]] && restore_point_ts="$target_ts"
if (( dry_run )); then
  printf 'RESULT {"database":"%s","restoredAs":"%s","source":"%s","dryRun":true,"files":%s}\n' "$db" "$target" "$source_tier" "${#chain[@]}"
  exit 0
fi

# --- execute ---------------------------------------------------------------------------------------
if [[ "$source_tier" == offsite || "${chain[0]}" == *.gz ]]; then
  ensure_dir "$BACKUP_ROOT/restore-staging"
  staging="$BACKUP_ROOT/restore-staging/$(now_ts)-$$"; ensure_dir "$staging"
fi
# RESTORE needs exclusive access to an existing target (e.g. an in-place restore without tail-log).
if db_exists "$target" && [[ "$(db_state "$target")" == ONLINE ]]; then
  sql master "ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" >/dev/null || die "cannot get exclusive access to $target"
fi
first=$(stage "${chain[0]}") || die "cannot stage $(basename "${chain[0]}")"
move=""
if (( ! in_place )); then
  # Side-by-side restore: relocate every data/log file so the original database is untouched.
  while IFS='|' read -r logical physical type _rest; do
    [[ -n "$logical" ]] || continue
    ext=mdf; [[ "$type" == L ]] && ext=ldf
    move+=", MOVE N'$logical' TO N'/var/opt/mssql/data/${target}_${logical}.${ext}'"
  done < <(sql master "RESTORE FILELISTONLY FROM DISK = N'$first'")
fi
with_replace=""; (( replace )) && with_replace=", REPLACE"
count=${#chain[@]}
for i in "${!chain[@]}"; do
  f="${chain[$i]}"
  if [[ $i -eq 0 ]]; then file="$first"; else file=$(stage "$f") || die "cannot stage $(basename "$f")"; fi
  case "$f" in
    *_LOG.trn*)
      stmt="RESTORE LOG [$target] FROM DISK = N'$file' WITH NORECOVERY, CHECKSUM"
      [[ -n "$stopat_iso" ]] && stmt+=", STOPAT = N'$stopat_iso'" ;;
    *)
      if [[ $i -eq 0 ]]; then stmt="RESTORE DATABASE [$target] FROM DISK = N'$file' WITH NORECOVERY, CHECKSUM${move}${with_replace}"
      else stmt="RESTORE DATABASE [$target] FROM DISK = N'$file' WITH NORECOVERY, CHECKSUM"; fi ;;
  esac
  log "step $((i + 1))/$count: $(basename "$f")"
  if ! out=$(sql master "$stmt" 2>&1); then
    event restore "$db" CHAIN false $(( $(date +%s) - started )) "" "$(json_escape "$out" | tail -c 300)"
    die "restore step failed for $(basename "$f"): $(json_escape "$out" | tail -c 500)"
  fi
done
sql master "RESTORE DATABASE [$target] WITH RECOVERY" >/dev/null || die "final RECOVERY failed for $target"
# A tail-log taken in SINGLE_USER restores that mode too; always reopen to all users.
sql master "ALTER DATABASE [$target] SET MULTI_USER" >/dev/null || true
seconds=$(( $(date +%s) - started ))
log "restored $target in ${seconds}s (restore point $(ts_iso "$restore_point_ts")Z)"
event restore "$db" CHAIN true "$seconds" "" "as $target, point $(ts_iso "$restore_point_ts")Z"

files_json=$(printf '"%s",' "${chain[@]##*/}"); files_json="[${files_json%,}]"
printf 'RESULT {"database":"%s","restoredAs":"%s","source":"%s","targetTime":"%s","restorePoint":"%sZ","restorePointEpoch":%s,"files":%s,"restoreSeconds":%s}\n' \
  "$db" "$target" "$source_tier" "${stopat_iso:-latest}" "$(ts_iso "$restore_point_ts")" "$(ts_epoch "$restore_point_ts")" "$files_json" "$seconds"
