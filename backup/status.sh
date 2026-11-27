#!/usr/bin/env bash
# Backup health = the durability SLI (docs/BACKUP_RECOVERY.md §Monitoring).
#
#   status.sh [--quiet]
#
# Per database: recovery model, log-chain state, age of the newest FULL/DIFF/LOG file and the current
# exposure ("if the disk died now, how many minutes of data would be lost") compared with the RPO.
# Also checks the key archives, the off-site lag and the last restore drill. Writes reports/status.json.
# Exit 0 = healthy, 1 = at least one objective at risk. Used as the container healthcheck.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
quiet=0; [[ "${1:-}" == --quiet ]] && quiet=1
DRILL_INTERVAL_MIN="${BACKUP_DRILL_INTERVAL_MIN:-10080}"
KEYS_INTERVAL_MIN="${BACKUP_KEYS_INTERVAL_MIN:-1440}"
GRACE_MIN="${BACKUP_STATUS_GRACE_MIN:-2}"

ok_all=1; dbs_json=()
while read -r db full_min diff_min log_min rpo_min rto_min tier; do
  f=$(latest_file "$db" FULL); d=$(latest_file "$db" DIFF); l=$(latest_file "$db" LOG)
  newest=$(max_ts "$(ts_of "$f")" "$(ts_of "$d")" "$(ts_of "$l")")
  exposure=$(age_min_ts "$newest")
  model="missing"; chain=0
  if db_exists "$db"; then model=$(recovery_model "$db"); [[ "$model" == FULL ]] && chain=$(log_chain_started "$db"); fi
  ok=1; reasons=()
  [[ -z "$f" ]] && { ok=0; reasons+=("no full backup"); }
  (( exposure > rpo_min + GRACE_MIN )) && { ok=0; reasons+=("exposure ${exposure}m > RPO ${rpo_min}m"); }
  (( log_min > 0 )) && [[ "$model" != FULL ]] && { ok=0; reasons+=("policy needs log backups but model is $model"); }
  [[ "$model" == FULL && "$chain" != 1 ]] && { ok=0; reasons+=("log chain not started"); }
  (( ok )) || ok_all=0
  reason=$(IFS=';'; echo "${reasons[*]}")
  dbs_json+=("$(printf '{"database":"%s","tier":"%s","recoveryModel":"%s","lastFullAgeMin":%s,"lastDiffAgeMin":%s,"lastLogAgeMin":%s,"exposureMin":%s,"rpoTargetMin":%s,"rtoTargetMin":%s,"ok":%s,"issue":"%s"}' \
    "$db" "$tier" "$model" "$(age_min_file "$f")" "$(age_min_file "$d")" "$(age_min_file "$l")" "$exposure" "$rpo_min" "$rto_min" \
    "$([[ $ok -eq 1 ]] && echo true || echo false)" "$reason")")
done < <(policy_lines)

keys_json=()
for src in "$KEY_SOURCES"/*/; do
  [[ -d "$src" ]] || continue
  name=$(basename "$src")
  latest=$(ls -1 "$BACKUP_ROOT/keys/${name}_"*_KEYS.tar.gz 2>/dev/null | sort | tail -n1)
  age=$(age_min_file "$latest"); kok=true
  (( age > KEYS_INTERVAL_MIN + 60 )) && { kok=false; ok_all=0; }
  keys_json+=("$(printf '{"volume":"%s","lastArchiveAgeMin":%s,"ok":%s}' "$name" "$age" "$kok")")
done

offsite_pending=-1
if [[ -d "$OFFSITE_ROOT" ]]; then
  offsite_pending=0
  while IFS= read -r f; do
    rel="${f#"$BACKUP_ROOT"/}"; dest="$OFFSITE_ROOT/$rel"; [[ "$f" == *.tar.gz ]] || dest="$dest.gz"
    [[ -f "$dest" ]] || offsite_pending=$((offsite_pending + 1))
  done < <(find "$BACKUP_ROOT" -type f \( -name '*.bak' -o -name '*.trn' -o -name '*_KEYS.tar.gz' \) \
             -not -path '*/restore-staging/*' -not -path '*/quarantine/*')
  # Files are copied at the end of every tick; more than a few pending means the off-site tier is failing.
  (( offsite_pending > 10 )) && ok_all=0
fi

drill_passed=null; drill_age=-1
if [[ -f "$REPORT_DIR/drill-latest.json" ]]; then
  drill_passed=$(grep -o '"allPassed":[a-z]*' "$REPORT_DIR/drill-latest.json" | head -n1 | cut -d: -f2)
  drill_age=$(( ($(date -u +%s) - $(stat -c %Y "$REPORT_DIR/drill-latest.json")) / 60 ))
  [[ "$drill_passed" == false ]] && ok_all=0
  (( drill_age > 2 * DRILL_INTERVAL_MIN )) && ok_all=0
fi

used=$(du -sb "$BACKUP_ROOT" 2>/dev/null | cut -f1); offsite_used=$(du -sb "$OFFSITE_ROOT" 2>/dev/null | cut -f1)
json=$(printf '{"checkedAt":"%s","policy":"%s","healthy":%s,"databases":[%s],"keys":[%s],"offsitePendingFiles":%s,"lastDrillPassed":%s,"lastDrillAgeMin":%s,"primaryBytes":%s,"offsiteBytes":%s}' \
  "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$(basename "$POLICY_FILE")" "$([[ $ok_all -eq 1 ]] && echo true || echo false)" \
  "$(IFS=,; echo "${dbs_json[*]}")" "$(IFS=,; echo "${keys_json[*]}")" "$offsite_pending" "$drill_passed" "$drill_age" "${used:-0}" "${offsite_used:-0}")
ensure_dir "$REPORT_DIR"; echo "$json" > "$REPORT_DIR/status.json"
(( quiet )) || echo "$json"
(( ok_all ))
