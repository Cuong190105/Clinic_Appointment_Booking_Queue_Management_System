<#
.SYNOPSIS
  Operator front-end for the db-backup sidecar (docs/BACKUP_RECOVERY.md).

.DESCRIPTION
  status    Backup health per database: exposure vs RPO, chain state, key archives, off-site lag, last drill.
  list      Chain files of one database (primary and off-site).
  now       Take a FULL / DIFF / LOG backup immediately (part of the normal chain).
  snapshot  COPY_ONLY full of one or all databases (e.g. before a migration); does not break the chain.
  drill     Restore drill: side-by-side restore + DBCC CHECKDB, measures RPO/RTO. Report copied to tests/load/evidence.
  restore   Point-in-time restore, side by side (-As) or in place (-Replace; the owning service is stopped
            and started again, and a tail-log backup is taken first so committed data is not lost).
  events    Last backup/restore/drill events.

.EXAMPLE
  ./scripts/backup.ps1 status
  ./scripts/backup.ps1 now -Database all -Type FULL
  ./scripts/backup.ps1 snapshot -Database ClinicAppointmentDB -Label before-migration
  ./scripts/backup.ps1 drill
  ./scripts/backup.ps1 drill -IntegrityCheck        # adds DBCC CHECKDB on each restored copy (uses the SQL admin login once)
  ./scripts/backup.ps1 drill -Database ClinicMedicalRecordsDB -Source offsite
  ./scripts/backup.ps1 restore -Database ClinicAppointmentDB -As ClinicAppointmentDB_pitr -At "2026-11-27 08:15:00"
  ./scripts/backup.ps1 restore -Database ClinicQueueDB -Replace
  ./scripts/backup.ps1 restore -Database ClinicQueueDB -Replace -Source offsite -NoTailLog
#>
param(
    [Parameter(Position = 0)][ValidateSet("status", "list", "now", "snapshot", "drill", "restore", "events")]
    [string]$Action = "status",
    [string]$Database = "",
    [ValidateSet("FULL", "DIFF", "LOG")][string]$Type = "FULL",
    [string]$Label = "manual",
    [string]$At = "",
    [string]$As = "",
    [switch]$Replace,
    [ValidateSet("", "primary", "offsite")][string]$Source = "",
    [switch]$NoTailLog,
    [switch]$IntegrityCheck,
    [switch]$DryRun,
    [int]$Tail = 20,
    [string]$Container = "clinic_db_backup"
)

# docker writes progress to stderr; results are judged by exit codes.
$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$compose = Join-Path $root "docker-compose.yml"
$run = "/opt/clinic-backup-run"
$env:MSYS_NO_PATHCONV = "1"

$serviceFor = @{
    ClinicIdentityDB       = "identity-service"
    ClinicSchedulingDB     = "scheduling-service"
    ClinicAppointmentDB    = "appointment-service"
    ClinicQueueDB          = "queue-service"
    ClinicMedicalRecordsDB = "medical-records-service"
    ClinicNotificationDB   = "notification-service"
}

function Invoke-Sidecar([string[]]$Arguments) {
    # Send output to the host, not the pipeline, so the function returns only the exit code.
    docker exec $Container @Arguments 2>&1 | Out-Host
    return $LASTEXITCODE
}

function Assert-Sidecar {
    $state = docker inspect -f "{{.State.Running}}" $Container 2>$null
    if ($state -ne "true") {
        Write-Host "Container $Container is not running. Start it with: docker compose up -d db-backup" -ForegroundColor Red
        exit 2
    }
}

function Require-Database {
    if (-not $Database) { Write-Host "-Database is required for '$Action'." -ForegroundColor Red; exit 2 }
}

Assert-Sidecar
switch ($Action) {
    "status" {
        $json = docker exec $Container bash "$run/status.sh" 2>$null | Out-String
        $code = $LASTEXITCODE
        try { $status = $json | ConvertFrom-Json } catch { Write-Host $json; exit 2 }
        $age = { param($m) if ($m -ge 999999) { "-" } else { "$m" } }
        $status.databases | ForEach-Object {
            [pscustomobject]@{
                Database = $_.database; Tier = $_.tier; Model = $_.recoveryModel
                "Full(min)" = & $age $_.lastFullAgeMin; "Diff(min)" = & $age $_.lastDiffAgeMin; "Log(min)" = & $age $_.lastLogAgeMin
                "Exposure(min)" = & $age $_.exposureMin; "RPO(min)" = $_.rpoTargetMin; OK = $_.ok; Issue = $_.issue
            }
        } | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
        if ($status.keys) { $status.keys | Format-Table volume, lastArchiveAgeMin, ok -AutoSize | Out-String -Width 200 | Write-Host }
        $drill = if ($status.lastDrillAgeMin -lt 0) { "never" } else { "{0} ({1} min ago)" -f $status.lastDrillPassed, $status.lastDrillAgeMin }
        Write-Host ("Off-site pending files: {0}   Last drill passed: {1}   Primary: {2:N1} MB   Off-site: {3:N1} MB" -f `
            $status.offsitePendingFiles, $drill, ($status.primaryBytes / 1MB), ($status.offsiteBytes / 1MB))
        $color = if ($status.healthy) { "Green" } else { "Red" }
        Write-Host ("Backup health: {0}" -f $(if ($status.healthy) { "HEALTHY" } else { "AT RISK" })) -ForegroundColor $color
        exit $code
    }
    "list" {
        Require-Database
        Write-Host "Primary (/var/opt/mssql/backup/$Database):" -ForegroundColor Cyan
        docker exec $Container bash -c "ls -lh --time-style=+%F_%T /var/opt/mssql/backup/$Database 2>/dev/null | tail -n +2"
        Write-Host "Off-site (/offsite/$Database):" -ForegroundColor Cyan
        docker exec $Container bash -c "ls -lh --time-style=+%F_%T /offsite/$Database 2>/dev/null | grep -v sha256 | tail -n +2"
    }
    "now" {
        if (-not $Database) { $Database = "all" }
        exit (Invoke-Sidecar @("flock", "-w", "600", "/tmp/clinic-backup.lock", "bash", "$run/backup.sh", "run", $Database, $Type))
    }
    "snapshot" {
        if (-not $Database) { $Database = "all" }
        exit (Invoke-Sidecar @("flock", "-w", "600", "/tmp/clinic-backup.lock", "bash", "$run/backup.sh", "snapshot", $Database, $Label))
    }
    "drill" {
        $arguments = @("flock", "-w", "1800", "/tmp/clinic-drill.lock", "bash", "$run/drill.sh")
        if ($Database) { $arguments += $Database }
        if ($Source) { $arguments += @("--source", $Source) }
        if ($IntegrityCheck) {
            # DBCC CHECKDB needs sysadmin/db_owner; the admin password is passed only to this one exec,
            # never stored in the long-running sidecar (which runs as svc_backup).
            $sa = $env:DB_SA_PASSWORD
            if (-not $sa) {
                $line = Get-Content (Join-Path $root ".env") | Where-Object { $_ -match '^\s*DB_SA_PASSWORD\s*=' } | Select-Object -First 1
                if ($line) { $sa = ($line -split '=', 2)[1].Trim().Trim('"').Trim("'") }
            }
            if (-not $sa) { Write-Host "-IntegrityCheck needs DB_SA_PASSWORD (env or .env)." -ForegroundColor Red; exit 2 }
            docker exec -e "BACKUP_CHECKDB_PASSWORD=$sa" $Container @arguments 2>&1 | Out-Host
            $code = $LASTEXITCODE
        }
        else { $code = Invoke-Sidecar $arguments }
        $evidence = Join-Path $root "tests/load/evidence/backup-drill.json"
        docker cp "${Container}:/var/opt/mssql/backup/reports/drill-latest.json" $evidence 2>$null | Out-Null
        if (Test-Path $evidence) { Write-Host "Drill report: $evidence" -ForegroundColor Cyan }
        exit $code
    }
    "restore" {
        Require-Database
        $arguments = @("flock", "-w", "600", "/tmp/clinic-backup.lock", "bash", "$run/restore.sh", $Database)
        if ($At) { $arguments += @("--at", $At) }
        if ($As) { $arguments += @("--as", $As) }
        if ($Replace) { $arguments += "--replace" }
        if ($Source) { $arguments += @("--source", $Source) }
        if ($NoTailLog) { $arguments += "--no-tail-log" }
        if ($DryRun) { $arguments += "--dry-run" }

        $inPlace = $Replace -and (-not $As -or $As -eq $Database) -and -not $DryRun
        $service = $serviceFor[$Database]
        if ($inPlace -and $service) {
            # Stop the owner first so it does not write during the restore or hold connections.
            Write-Host "[RESTORE] stopping $service" -ForegroundColor Yellow
            docker compose -f $compose stop $service 2>&1 | Out-Null
        }
        $started = Get-Date
        $code = Invoke-Sidecar $arguments
        if ($inPlace -and $service) {
            Write-Host "[RESTORE] starting $service" -ForegroundColor Yellow
            docker compose -f $compose start $service 2>&1 | Out-Null
        }
        Write-Host ("[RESTORE] finished in {0:N0}s with exit code {1}" -f ((Get-Date) - $started).TotalSeconds, $code) `
            -ForegroundColor $(if ($code -eq 0) { "Green" } else { "Red" })
        exit $code
    }
    "events" {
        docker exec $Container bash -c "tail -n $Tail /var/opt/mssql/backup/reports/events.jsonl 2>/dev/null" |
            ForEach-Object { try { $_ | ConvertFrom-Json } catch { } } |
            Format-Table at, kind, database, type, ok, seconds, bytes, file, message -AutoSize
    }
}
