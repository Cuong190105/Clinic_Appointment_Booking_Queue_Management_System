#!/usr/bin/env bash
set -euo pipefail

echo "=========================================================="
echo " [Phase 7] Running API Contract & Drift Validation Tests   "
echo "=========================================================="

PROJECT_PATH="backend/Clinic.IntegrationTests/Clinic.IntegrationTests.csproj"
CONFIG="Debug"
UPDATE_SNAPSHOTS="false"
FILTER="FullyQualifiedName~ApiContractDriftTests"

for arg in "$@"; do
    case "$arg" in
        --update-snapshots)
            UPDATE_SNAPSHOTS="true"
            ;;
        --filter=*)
            FILTER="${arg#*=}"
            ;;
        *)
            CONFIG="$arg"
            ;;
    esac
done

export UPDATE_CONTRACT_SNAPSHOTS="$UPDATE_SNAPSHOTS"

if [ "$UPDATE_SNAPSHOTS" = "true" ]; then
    echo ">>> [MODE] Intentional Snapshot Update enabled (UPDATE_CONTRACT_SNAPSHOTS=true)"
fi

echo "Executing: dotnet test $PROJECT_PATH -c $CONFIG --disable-build-servers -p:UseSharedCompilation=false -p:UseRazorBuildServer=false --filter $FILTER --logger console;verbosity=normal"
if dotnet test "$PROJECT_PATH" -c "$CONFIG" --disable-build-servers -p:UseSharedCompilation=false -p:UseRazorBuildServer=false --filter "$FILTER" --logger "console;verbosity=normal"; then
    echo ">>> [SUCCESS] All API contract and drift validation checks passed."
    exit 0
else
    EXIT_CODE=$?
    echo ">>> [FAILURE] API contract drift or specification validation failed with exit code $EXIT_CODE."
    exit $EXIT_CODE
fi
