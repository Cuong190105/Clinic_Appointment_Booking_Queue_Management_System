#!/usr/bin/env bash
# Restore drill: proves the backups are restorable and measures RPO/RTO against the policy
# (docs/BACKUP_RECOVERY.md §Drill). For each database: restore the latest chain side by side into
# <db>_drill, run DBCC CHECKDB, drop it. The most critical database (first policy line) is restored a
# second time from the off-site copy, so both tiers are exercised.
#
#   drill.sh [db ...] [--source primary|offsite] [--scheduled]
#
#   Measured RPO = drill time - restore point (data that would be lost if the server died now)
#   Measured RTO = restore duration (chain restore + recovery; excludes CHECKDB, reported separately)
# Writes reports/drill-<ts>.json and reports/drill-latest.json; exit 1 if any database misses its targets.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
LOG_TAG=drill

dbs=(); source_tier=""; scheduled=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) source_tier="$2"; shift 2 ;;
    --scheduled) scheduled=1; shift ;;
    *) dbs+=("$1"); shift ;;
  esac
done
(( ${#dbs[@]} )) || mapfile -t dbs < <(policy_databases)

runs=()   # "db|source"
if [[ -n "$source_tier" ]]; then
  for db in "${dbs[@]}"; do runs+=("$db|$source_tier"); done
else
  for db in "${dbs[@]}"; do runs+=("$db|primary"); done
  [[ -d "$OFFSITE_ROOT/${dbs[0]}" ]] && runs+=("${dbs[0]}|offsite")
fi

# DBCC CHECKDB requires sysadmin or db_owner. A restored copy keeps the source database's svc_backup
# user (db_backupoperator only), so svc_backup cannot check it, and cannot read its data either.
# Only this integrity check on the scratch copy uses the optional admin login.
checkdb() { # <database>
  [[ -n "${BACKUP_CHECKDB_PASSWORD:-}" ]] || return 2
  SQLCMDPASSWORD="$BACKUP_CHECKDB_PASSWORD" "$SQLCMD" -C -S "$SQL_SERVER" -U "${BACKUP_CHECKDB_USER:-sa}" -d master \
    -b -h -1 -W -l 15 -t 0 -Q "SET NOCOUNT ON; DBCC CHECKDB (N'$1') WITH NO_INFOMSGS, ALL_ERRORMSGS"
}

ensure_dir "$REPORT_DIR"
ts=$(now_ts); results=(); failed=0
for run in "${runs[@]}"; do
  db="${run%|*}"; src="${run#*|}"; scratch="${db}_drill"
  read -r _db _full _diff _log rpo_min rto_min tier < <(policy_for "$db")
  log "drill $db from $src (RPO target ${rpo_min:-?} min, RTO target ${rto_min:-?} min)"
  if db_exists "$scratch"; then
    sql master "ALTER DATABASE [$scratch] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$scratch]" >/dev/null || true
  fi

  drill_at=$(date -u +%s)
  output=$(bash "$SCRIPT_DIR/restore.sh" "$db" --as "$scratch" --source "$src" 2>&1)
  rc=$?
  echo "$output" | grep -v '^RESULT ' | sed 's/^/    /'
  result=$(echo "$output" | sed -n 's/^RESULT //p' | tail -n1)

  checkdb="skipped"; checkdb_s=0; restore_s=0; rpo_s=0; passed=false; error=""
  if [[ $rc -eq 0 && -n "$result" ]]; then
    restore_s=$(echo "$result" | sed -nE 's/.*"restoreSeconds":([0-9]+).*/\1/p')
    point=$(echo "$result" | sed -nE 's/.*"restorePointEpoch":([0-9]+).*/\1/p')
    rpo_s=$(( drill_at - point ))
    c0=$(date +%s)
    out=$(checkdb "$scratch" 2>&1); crc=$?
    if [[ $crc -eq 0 ]]; then checkdb="ok"
    elif [[ $crc -eq 2 ]]; then checkdb="skipped"   # no admin login configured: restore + CHECKSUM verification only
    else checkdb="failed"; error="$(json_escape "$out" | tail -c 300)"; fi
    checkdb_s=$(( $(date +%s) - c0 ))
    sql master "ALTER DATABASE [$scratch] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$scratch]" >/dev/null || log "WARN could not drop $scratch"
    if [[ "$checkdb" != failed ]] && (( rpo_s <= ${rpo_min:-0} * 60 )) && (( restore_s <= ${rto_min:-0} * 60 )); then passed=true; fi
  else
    error="$(json_escape "$(echo "$output" | grep -i error | tail -n2)" | tail -c 300)"
  fi
  [[ "$passed" == true ]] || failed=$((failed + 1))
  log "$db/$src: restore ${restore_s}s, RPO ${rpo_s}s, CHECKDB $checkdb -> $([[ $passed == true ]] && echo PASS || echo FAIL)"
  event drill "$db" "$src" "$passed" "$restore_s" "" "rpo=${rpo_s}s checkdb=$checkdb"
  results+=("$(printf '{"database":"%s","tier":"%s","source":"%s","passed":%s,"restoreSeconds":%s,"rtoTargetSeconds":%s,"measuredRpoSeconds":%s,"rpoTargetSeconds":%s,"checkdb":"%s","checkdbSeconds":%s,"restore":%s,"error":"%s"}' \
    "$db" "${tier:-}" "$src" "$passed" "${restore_s:-0}" "$(( ${rto_min:-0} * 60 ))" "$rpo_s" "$(( ${rpo_min:-0} * 60 ))" "$checkdb" "$checkdb_s" "${result:-null}" "$error")")
done

joined=$(IFS=,; echo "${results[*]}")
report=$(printf '{"drillAt":"%s","scheduled":%s,"policy":"%s","allPassed":%s,"results":[%s]}' \
  "$(ts_iso "$ts")Z" "$([[ $scheduled -eq 1 ]] && echo true || echo false)" "$(basename "$POLICY_FILE")" \
  "$([[ $failed -eq 0 ]] && echo true || echo false)" "$joined")
echo "$report" > "$REPORT_DIR/drill-$ts.json"
cp "$REPORT_DIR/drill-$ts.json" "$REPORT_DIR/drill-latest.json"
date -u +%s > "$REPORT_DIR/last-drill"
log "drill finished: $(( ${#runs[@]} - failed ))/${#runs[@]} passed -> reports/drill-$ts.json"
(( failed == 0 ))
