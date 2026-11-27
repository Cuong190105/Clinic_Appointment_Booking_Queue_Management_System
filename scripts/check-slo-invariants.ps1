<#
.SYNOPSIS
  Verifies the data-integrity SLOs of SLI_SLO_MATRIX §2.5 after any write/load/fault test.
  Exit code 0 = all invariants hold, 1 = at least one violation, 2 = could not query.

.DESCRIPTION
  Read-only audit queries run as SA through the SQL Server container. The cross-database check is
  an operator audit tool only; services never query each other's databases.

.EXAMPLE
  ./scripts/check-slo-invariants.ps1
  ./scripts/check-slo-invariants.ps1 -OutputFile tests/load/evidence/invariants-latest.json
#>
param(
    [string]$SqlContainer = "clinic_sql_server",
    [string]$RabbitContainer = "",
    [string]$SqlPassword = $null,
    [string]$OutputFile = $null
)

$ErrorActionPreference = "Stop"

if (-not $SqlPassword) { $SqlPassword = $env:DB_SA_PASSWORD }
if (-not $SqlPassword) {
    $envFile = Join-Path (Split-Path $PSScriptRoot -Parent) ".env"
    if (Test-Path $envFile) {
        $line = Get-Content $envFile | Where-Object { $_ -match '^\s*DB_SA_PASSWORD\s*=' } | Select-Object -First 1
        if ($line) { $SqlPassword = ($line -split '=', 2)[1].Trim().Trim('"').Trim("'") }
    }
}
if (-not $SqlPassword) { Write-Error "Set DB_SA_PASSWORD (env or .env) or pass -SqlPassword."; exit 2 }

function Invoke-Scalar([string]$Database, [string]$Query) {
    $out = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d $Database -h -1 -W -b -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Query failed on ${Database}: $out" }
    $value = ($out | Where-Object { "$_".Trim() -ne '' } | Select-Object -Last 1)
    return [int]"$value".Trim()
}

$checks = @(
    @{ Name = "Shifts over RecommendedCapacity (active reservations)"; Db = "ClinicSchedulingDB"; Query = @"
SELECT COUNT(*) FROM (
  SELECT r.WorkScheduleId FROM SlotReservations r JOIN WorkSchedules s ON s.Id = r.WorkScheduleId
  WHERE r.Status = 'Confirmed' OR (r.Status = 'Reserved' AND r.ExpiresAt > SYSDATETIMEOFFSET())
  GROUP BY r.WorkScheduleId, s.RecommendedCapacity HAVING COUNT(*) > s.RecommendedCapacity) t;
"@ },
    @{ Name = "Slots with more than one active reservation"; Db = "ClinicSchedulingDB"; Query = @"
SELECT COUNT(*) FROM (
  SELECT WorkScheduleId, TimeSlot FROM SlotReservations
  WHERE Status = 'Confirmed' OR (Status = 'Reserved' AND ExpiresAt > SYSDATETIMEOFFSET())
  GROUP BY WorkScheduleId, TimeSlot HAVING COUNT(*) > 1) t;
"@ },
    @{ Name = "Orphan reservations (Reserved, expired > 1 min, not swept)"; Db = "ClinicSchedulingDB"; Query = @"
SELECT COUNT(*) FROM SlotReservations WHERE Status = 'Reserved' AND ExpiresAt < DATEADD(minute, -1, SYSDATETIMEOFFSET());
"@ },
    # Only saga bookings (they hold a Scheduling reservation); imported seed rows never went through the saga.
    @{ Name = "Online appointments stuck Pending > 10 min"; Db = "ClinicAppointmentDB"; Query = @"
SELECT COUNT(*) FROM Appointments WHERE Status = 'Pending' AND IsWalkIn = 0 AND IsDeleted = 0 AND SchedulingReservationId IS NOT NULL AND CreatedAt < DATEADD(minute, -10, SYSUTCDATETIME());
"@ },
    @{ Name = "Confirmed/CheckedIn online appointments without a Confirmed reservation"; Db = "ClinicAppointmentDB"; Query = @"
SELECT COUNT(*) FROM ClinicAppointmentDB.dbo.Appointments a
LEFT JOIN ClinicSchedulingDB.dbo.SlotReservations r ON r.Id = a.SchedulingReservationId
WHERE a.Status IN ('Confirmed', 'CheckedIn') AND a.IsWalkIn = 0 AND a.IsDeleted = 0
  AND a.SchedulingReservationId IS NOT NULL AND (r.Id IS NULL OR r.Status <> 'Confirmed');
"@ },
    @{ Name = "Duplicate active appointments per doctor/date/slot"; Db = "ClinicAppointmentDB"; Query = @"
SELECT COUNT(*) FROM (
  SELECT DoctorId, ScheduleDate, TimeSlot FROM Appointments
  WHERE Status <> 'Cancelled' AND IsDeleted = 0 AND IsWalkIn = 0
  GROUP BY DoctorId, ScheduleDate, TimeSlot HAVING COUNT(*) > 1) t;
"@ },
    @{ Name = "Duplicate idempotency keys"; Db = "ClinicAppointmentDB"; Query = @"
SELECT COUNT(*) FROM (SELECT IdempotencyKey FROM Appointments WHERE IdempotencyKey IS NOT NULL AND IsDeleted = 0 GROUP BY IdempotencyKey HAVING COUNT(*) > 1) t;
"@ },
    @{ Name = "Duplicate queue numbers (DoctorId, QueueDate, QueueNumber)"; Db = "ClinicQueueDB"; Query = @"
SELECT COUNT(*) FROM (SELECT DoctorId, QueueDate, QueueNumber FROM QueueTickets GROUP BY DoctorId, QueueDate, QueueNumber HAVING COUNT(*) > 1) t;
"@ },
    @{ Name = "Appointments with more than one queue ticket"; Db = "ClinicQueueDB"; Query = @"
SELECT COUNT(*) FROM (SELECT AppointmentId FROM QueueTickets GROUP BY AppointmentId HAVING COUNT(*) > 1) t;
"@ }
)

$results = @()
$violations = 0
try {
    foreach ($c in $checks) {
        $count = Invoke-Scalar $c.Db $c.Query
        $ok = ($count -eq 0)
        if (-not $ok) { $violations++ }
        $results += [pscustomobject]@{ check = $c.Name; database = $c.Db; violations = $count; ok = $ok }
        $color = if ($ok) { "Green" } else { "Red" }
        Write-Host ("[{0}] {1}: {2}" -f ($(if ($ok) { "PASS" } else { "FAIL" }), $c.Name, $count)) -ForegroundColor $color
    }

    if (-not $RabbitContainer) {
        $RabbitContainer = (docker ps --format "{{.Names}}" | Where-Object { $_ -match 'rabbitmq' } | Select-Object -First 1)
    }
    if ($RabbitContainer) {
        $queues = docker exec $RabbitContainer rabbitmqctl list_queues name messages --quiet 2>$null
        $dlqMessages = 0
        foreach ($line in $queues) {
            $parts = "$line" -split '\s+'
            if ($parts.Count -ge 2 -and $parts[0] -match '([.-]dlq|\.dead)$') { $dlqMessages += [int]$parts[1] }
        }
        $ok = ($dlqMessages -eq 0)
        if (-not $ok) { $violations++ }
        $results += [pscustomobject]@{ check = "Messages in dead-letter queues"; database = "rabbitmq"; violations = $dlqMessages; ok = $ok }
        Write-Host ("[{0}] Messages in dead-letter queues: {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" }), $dlqMessages)) -ForegroundColor $(if ($ok) { "Green" } else { "Red" })
    }

    # Durability SLO (§5.6): every database's backup exposure is within its RPO, chains intact, last drill passed.
    $backupContainer = (docker ps --format "{{.Names}}" | Where-Object { $_ -eq 'clinic_db_backup' } | Select-Object -First 1)
    if ($backupContainer) {
        $statusJson = docker exec $backupContainer bash /opt/clinic-backup-run/status.sh 2>$null | Out-String
        $ok = ($LASTEXITCODE -eq 0)
        $atRisk = 0
        try { $atRisk = @((($statusJson | ConvertFrom-Json).databases) | Where-Object { -not $_.ok }).Count } catch { $atRisk = 1 }
        if (-not $ok) { $violations++ }
        $results += [pscustomobject]@{ check = "Backups within RPO / last drill passed"; database = "db-backup"; violations = [math]::Max($atRisk, [int](-not $ok)); ok = $ok }
        Write-Host ("[{0}] Backups within RPO / last drill passed (scripts/backup.ps1 status)" -f ($(if ($ok) { "PASS" } else { "FAIL" }))) -ForegroundColor $(if ($ok) { "Green" } else { "Red" })
    }
}
catch {
    Write-Error $_
    exit 2
}

$report = [pscustomobject]@{
    checkedAt = (Get-Date).ToUniversalTime().ToString("o")
    allPassed = ($violations -eq 0)
    checks = $results
}
if ($OutputFile) { $report | ConvertTo-Json -Depth 5 | Out-File -FilePath $OutputFile -Encoding utf8 }

if ($violations -gt 0) { Write-Host "$violations invariant check(s) FAILED." -ForegroundColor Red; exit 1 }
Write-Host "All SLO invariants hold." -ForegroundColor Green
exit 0
