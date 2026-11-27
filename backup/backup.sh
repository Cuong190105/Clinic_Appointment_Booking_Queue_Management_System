#!/usr/bin/env bash
# Backup engine for the db-backup sidecar (docs/BACKUP_RECOVERY.md).
#
#   backup.sh tick                          scheduled run: per-policy full/diff/log, keys, off-site copy, pruning
#   backup.sh run <db|all> <FULL|DIFF|LOG>  one backup now (part of the normal chain)
#   backup.sh snapshot <db|all> [label]     COPY_ONLY full (e.g. before a migration); does not reset the diff base
#   backup.sh offsite                       copy new files to the off-site tier now
#   backup.sh prune                         apply retention now
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=backup

RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-8}"
OFFSITE_RETENTION_DAYS="${BACKUP_OFFSITE_RETENTION_DAYS:-35}"
SNAPSHOT_RETENTION_DAYS="${BACKUP_SNAPSHOT_RETENTION_DAYS:-30}"
KEYS_INTERVAL_MIN="${BACKUP_KEYS_INTERVAL_MIN:-1440}"

# take_backup <db> <FULL|DIFF|LOG>: writes <name>.partial, verifies, then renames, so a crashed or
# failed backup never appears as a usable chain file.
take_backup() {
  local db="$1" type="$2" dir="$BACKUP_ROOT/$1" ts ext final part stmt started out
  ensure_dir "$dir"
  ts=$(now_ts); ext=bak; [[ "$type" == LOG ]] && ext=trn
  final="$dir/${db}_${ts}_${type}.${ext}"; part="$final.partial"
  case "$type" in
    FULL) stmt="BACKUP DATABASE [$db] TO DISK = N'$part' WITH CHECKSUM, INIT, FORMAT, NAME = N'$db FULL $ts'" ;;
    DIFF) stmt="BACKUP DATABASE [$db] TO DISK = N'$part' WITH DIFFERENTIAL, CHECKSUM, INIT, FORMAT, NAME = N'$db DIFF $ts'" ;;
    LOG)  stmt="BACKUP LOG [$db] TO DISK = N'$part' WITH CHECKSUM, INIT, FORMAT, NAME = N'$db LOG $ts'" ;;
    *) die "unknown backup type $type" ;;
  esac
  started=$(date +%s)
  if ! out=$(sql master "$stmt" 2>&1); then
    rm -f "$part"
    log "FAILED $type $db: $(json_escape "$out" | tail -c 400)"
    event backup "$db" "$type" false $(( $(date +%s) - started )) "" "$(json_escape "$out" | tail -c 300)"
    return 1
  fi
  # Full and differential files are the base of every restore: re-read them and validate page checksums.
  if [[ "$type" != LOG ]] && ! out=$(sql master "RESTORE VERIFYONLY FROM DISK = N'$part' WITH CHECKSUM" 2>&1); then
    mkdir -p "$BACKUP_ROOT/quarantine"; mv "$part" "$BACKUP_ROOT/quarantine/" 2>/dev/null
    log "VERIFY FAILED $type $db: $(json_escape "$out" | tail -c 400)"
    event verify "$db" "$type" false $(( $(date +%s) - started )) "" "$(json_escape "$out" | tail -c 300)"
    return 1
  fi
  mv "$part" "$final"
  log "OK $type $db -> $(basename "$final") ($(stat -c %s "$final") bytes, $(( $(date +%s) - started ))s)"
  event backup "$db" "$type" true $(( $(date +%s) - started )) "$final" ""
}

# Decide what each database needs now. State comes from the chain files themselves, so the schedule
# survives sidecar restarts and a lost msdb, and a wiped backup volume triggers a fresh FULL at once.
tick_database() {
  local db="$1" full_min="$2" diff_min="$3" log_min="$4" model last_full last_diff last_log base_ts log_ref
  if ! db_exists "$db"; then log "skip $db (database not created yet)"; return 0; fi
  [[ "$(db_state "$db")" == ONLINE ]] || { log "skip $db (state $(db_state "$db"))"; return 0; }
  model=$(recovery_model "$db")
  last_full=$(latest_file "$db" FULL)

  if [[ -z "$last_full" ]] || (( $(age_min_file "$last_full") >= full_min )) ||
     [[ "$model" == FULL && "$(log_chain_started "$db")" != 1 ]]; then
    take_backup "$db" FULL
    return
  fi

  last_diff=$(latest_file "$db" DIFF)
  base_ts=$(max_ts "$(ts_of "$last_full")" "$(ts_of "$last_diff")")
  if (( diff_min > 0 )) && (( $(age_min_ts "$base_ts") >= diff_min )); then
    take_backup "$db" DIFF
  fi

  if [[ "$model" == FULL ]] && (( log_min > 0 )); then
    last_log=$(latest_file "$db" LOG)
    log_ref=$(max_ts "$(ts_of "$last_full")" "$(ts_of "$last_log")")
    if (( $(age_min_ts "$log_ref") >= log_min )); then
      take_backup "$db" LOG
    fi
  elif [[ "$model" != FULL ]] && (( log_min > 0 )); then
    log "WARN $db has log_min=$log_min but recovery model $model; log backups skipped (see init-service-logins.sql)"
  fi
}

# Keys are not in SQL Server: without the JWT signing keys, a restored Identity DB cannot validate the
# tokens it issued; without the data-protection keys, Notification cannot read its protected payloads.
backup_keys() {
  local src name hash last latest dir="$BACKUP_ROOT/keys" ts
  [[ -d "$KEY_SOURCES" ]] || return 0
  ensure_dir "$dir"
  for src in "$KEY_SOURCES"/*/; do
    [[ -d "$src" ]] || continue
    name=$(basename "$src")
    hash=$(cd "$src" && find . -type f -print0 | sort -z | xargs -0 -r sha256sum | sha256sum | cut -c1-64)
    last=$(cat "$dir/$name.sha256" 2>/dev/null)
    latest=$(ls -1 "$dir/${name}_"*_KEYS.tar.gz 2>/dev/null | sort | tail -n1)
    if [[ "$hash" != "$last" || -z "$latest" ]] || (( $(age_min_file "$latest") >= KEYS_INTERVAL_MIN )); then
      ts=$(now_ts)
      if tar -czf "$dir/${name}_${ts}_KEYS.tar.gz" -C "$src" .; then
        echo "$hash" > "$dir/$name.sha256"
        log "OK KEYS $name -> ${name}_${ts}_KEYS.tar.gz"
        event keys "$name" KEYS true 0 "$dir/${name}_${ts}_KEYS.tar.gz" ""
      else
        event keys "$name" KEYS false 0 "" "tar failed"
      fi
    fi
  done
}

# Second copy on different storage (host directory / another disk / a synced cloud folder), gzip-
# compressed (SQL Server Express cannot compress backups itself) with a sha256 per file.
sync_offsite() {
  [[ -d "$OFFSITE_ROOT" ]] || return 0
  local f rel dest copied=0
  while IFS= read -r f; do
    rel="${f#"$BACKUP_ROOT"/}"
    dest="$OFFSITE_ROOT/$rel"; [[ "$f" == *.tar.gz ]] || dest="$dest.gz"
    [[ -f "$dest" ]] && continue
    mkdir -p "$(dirname "$dest")"
    if [[ "$f" == *.tar.gz ]]; then cp "$f" "$dest.tmp"; else gzip -c "$f" > "$dest.tmp"; fi || { rm -f "$dest.tmp"; log "WARN off-site copy failed for $rel"; continue; }
    mv "$dest.tmp" "$dest"
    (cd "$(dirname "$dest")" && sha256sum "$(basename "$dest")" > "$(basename "$dest").sha256")
    copied=$((copied + 1))
  done < <(find "$BACKUP_ROOT" -type f \( -name '*.bak' -o -name '*.trn' -o -name '*_KEYS.tar.gz' \) \
             -not -path '*/restore-staging/*' -not -path '*/quarantine/*' | sort)
  (( copied > 0 )) && log "off-site: copied $copied file(s)"
  return 0
}

# Chain-aware retention: keep every file needed to restore to any point inside the window. The anchor
# is the newest FULL older than the cutoff; everything older than the anchor is no longer needed.
prune_chains() { # <root> <days>
  local root="$1" days="$2" cutoff dir db anchor anchor_ts f t removed=0
  [[ -d "$root" ]] || return 0
  cutoff=$(date -u -d "-${days} days" +%Y%m%dT%H%M%SZ)
  for dir in "$root"/Clinic*/; do
    [[ -d "$dir" ]] || continue
    db=$(basename "$dir")
    anchor=""
    for f in $(chain_files "$dir" "$db" FULL); do [[ "$(ts_of "$f")" < "$cutoff" || "$(ts_of "$f")" == "$cutoff" ]] && anchor="$f"; done
    [[ -n "$anchor" ]] || continue
    anchor_ts=$(ts_of "$anchor")
    for f in "$dir"*; do
      t=$(ts_of "$f"); [[ -n "$t" && "$t" < "$anchor_ts" ]] || continue
      rm -f "$f"; removed=$((removed + 1))
    done
  done
  # Keys: drop old archives but always keep the newest per volume. Snapshots: plain age limit.
  for f in "$root"/keys/*_KEYS.tar.gz*; do
    [[ -f "$f" ]] || continue
    local name; name=$(basename "$f" | sed -E 's/_[0-9]{8}T[0-9]{6}Z_KEYS.*$//')
    [[ "$(ls -1 "$root/keys/${name}_"*_KEYS.tar.gz 2>/dev/null | sort | tail -n1)" == "${f%.sha256}" ]] && continue
    [[ "$(ts_of "$f")" < "$cutoff" ]] && { rm -f "$f"; removed=$((removed + 1)); }
  done
  local snap_cutoff; snap_cutoff=$(date -u -d "-${SNAPSHOT_RETENTION_DAYS} days" +%Y%m%dT%H%M%SZ)
  for f in "$root"/snapshots/*; do
    [[ -f "$f" && "$(ts_of "$f")" < "$snap_cutoff" ]] && { rm -f "$f"; removed=$((removed + 1)); }
  done
  (( removed > 0 )) && log "retention: removed $removed file(s) from $root (window ${days}d)"
  return 0
}

snapshot() { # <db> [label]
  local db="$1" label="${2:-manual}" dir="$BACKUP_ROOT/snapshots" ts file
  ensure_dir "$dir"
  ts=$(now_ts); file="$dir/${db}_${ts}_COPYONLY.bak"
  sql master "BACKUP DATABASE [$db] TO DISK = N'$file' WITH COPY_ONLY, CHECKSUM, INIT, FORMAT, NAME = N'$db snapshot $label', DESCRIPTION = N'$label'" >/dev/null &&
    sql master "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM" >/dev/null &&
    { log "OK SNAPSHOT $db ($label) -> $(basename "$file")"; event snapshot "$db" COPYONLY true 0 "$file" "$label"; } ||
    { log "FAILED SNAPSHOT $db"; event snapshot "$db" COPYONLY false 0 "" "$label"; return 1; }
}

targets() { if [[ "$1" == all ]]; then policy_databases; else echo "$1"; fi; }

cmd="${1:-tick}"; shift || true
case "$cmd" in
  tick)
    ensure_dir "$BACKUP_ROOT"; ensure_dir "$REPORT_DIR"
    rc=0
    while read -r db full_min diff_min log_min _rpo _rto _tier; do
      tick_database "$db" "$full_min" "$diff_min" "$log_min" || rc=1
    done < <(policy_lines)
    backup_keys || rc=1
    sync_offsite
    prune_chains "$BACKUP_ROOT" "$RETENTION_DAYS"
    prune_chains "$OFFSITE_ROOT" "$OFFSITE_RETENTION_DAYS"
    exit $rc ;;
  run)
    [[ $# -ge 2 ]] || die "usage: backup.sh run <db|all> <FULL|DIFF|LOG>"
    rc=0
    for db in $(targets "$1"); do
      # SIMPLE-recovery databases have no log chain (policy: full + diff only).
      if [[ "${2^^}" == LOG && "$(recovery_model "$db")" != FULL ]]; then log "skip LOG $db (recovery model $(recovery_model "$db"))"; continue; fi
      take_backup "$db" "${2^^}" || rc=1
    done
    sync_offsite; exit $rc ;;
  snapshot)
    [[ $# -ge 1 ]] || die "usage: backup.sh snapshot <db|all> [label]"
    rc=0; for db in $(targets "$1"); do snapshot "$db" "${2:-manual}" || rc=1; done
    sync_offsite; exit $rc ;;
  offsite) sync_offsite ;;
  prune) prune_chains "$BACKUP_ROOT" "$RETENTION_DAYS"; prune_chains "$OFFSITE_ROOT" "$OFFSITE_RETENTION_DAYS" ;;
  *) die "unknown command $cmd" ;;
esac
