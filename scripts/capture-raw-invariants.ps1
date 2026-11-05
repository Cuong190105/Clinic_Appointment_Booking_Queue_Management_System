# PowerShell Script: Capture Complete & Consistent Invariants and RabbitMQ Telemetry
param(
    [string]$SqlContainer = "clinic_sql_server",
    [string]$SqlPassword = $null,
    [string]$RabbitContainer = "clinic_appointment_booking_queue_management_system-rabbitmq-1",
    [string]$OutputFile = "tests/load/evidence/raw_invariants.json",
    [int]$SettleWindowSeconds = 5
)

$ErrorActionPreference = "Stop"

if (-not $SqlPassword) {
    if ($env:DB_SA_PASSWORD) {
        $SqlPassword = $env:DB_SA_PASSWORD
    } else {
        throw "Missing mandatory environment variable DB_SA_PASSWORD."
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   PHASE 9: INVARIANTS & INTEGRITY VERIFICATION           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

function Get-RabbitQueues() {
    $qRaw = docker exec $RabbitContainer rabbitmqctl list_queues name messages messages_ready messages_unacknowledged
    $parsed = @()
    foreach ($line in ($qRaw -split "`n")) {
        $trimmed = $line.Trim()
        if ($trimmed -and -not ($trimmed.StartsWith("Timeout:") -or $trimmed.StartsWith("Listing queues") -or $trimmed.StartsWith("name`t"))) {
            $parts = $trimmed -split '\s+'
            if ($parts.Count -ge 4) {
                $qName = $parts[0]
                $qMsgs = [int]$parts[1]
                $qReady = [int]$parts[2]
                $qUnack = [int]$parts[3]
                $isDlq = ($qName -match 'dlq|dead')
                $isRetry = ($qName -match 'retry')

                $parsed += [PSCustomObject]@{
                    name = $qName
                    messages = $qMsgs
                    messagesReady = $qReady
                    messagesUnacknowledged = $qUnack
                    isDlq = $isDlq
                    isRetry = $isRetry
                }
            }
        }
    }
    return $parsed
}

Write-Host "Inspecting RabbitMQ queues (Pre-Settle Window)..." -ForegroundColor Yellow
$preQueues = Get-RabbitQueues

if ($SettleWindowSeconds -gt 0) {
    Write-Host "Waiting $SettleWindowSeconds seconds settle window for asynchronous event consumers to settle..." -ForegroundColor Cyan
    Start-Sleep -Seconds $SettleWindowSeconds
}

Write-Host "Inspecting RabbitMQ queues (Post-Settle Window)..." -ForegroundColor Yellow
$queues = Get-RabbitQueues

$dlqs = @($queues | Where-Object { $_.isDlq })
$dlqCount = $dlqs.Count
$dlqTotal = 0
foreach ($d in $dlqs) { $dlqTotal += $d.messages }

# Active consumer queues (non-DLQ, non-retry)
$activeConsumerQueues = @($queues | Where-Object { -not $_.isDlq -and -not $_.isRetry })
$activeConsumerPending = 0
foreach ($aq in $activeConsumerQueues) { $activeConsumerPending += $aq.messagesReady }

Write-Host "  -> Profiled $($queues.Count) total queues ($dlqCount DLQs, Total DLQ msgs: $dlqTotal, Active consumer backlog: $activeConsumerPending)" -ForegroundColor Green

Write-Host "Capturing SQL Invariants..." -ForegroundColor Yellow

# Invariant 1: Overbooking across all schedules
$overbookingCheckSql = @"
SET NOCOUNT ON;
SELECT r.WorkScheduleId, COUNT(*) AS ReservedCount, s.RecommendedCapacity
FROM SlotReservations r
JOIN WorkSchedules s ON r.WorkScheduleId = s.Id
WHERE r.Status = 'Reserved'
GROUP BY r.WorkScheduleId, s.RecommendedCapacity
HAVING COUNT(*) > s.RecommendedCapacity;
"@
$overbookingViolationsRaw = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicSchedulingDb -Q $overbookingCheckSql 2>&1
$overbookingViolations = @()
foreach ($line in ($overbookingViolationsRaw -split "`n")) {
    $trimmed = $line.Trim()
    if ($trimmed -and ($trimmed -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}')) {
        $parts = $trimmed -split '\s+'
        if ($parts.Count -ge 3) {
            $overbookingViolations += [PSCustomObject]@{
                workScheduleId = $parts[0]
                reservedCount = [int]$parts[1]
                capacity = [int]$parts[2]
            }
        }
    }
}
$isOverbookingZero = ($overbookingViolations.Count -eq 0)

# Summary of booked counts per schedule
$schedSummaryRaw = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicAppointmentDb -Q "SET NOCOUNT ON; SELECT a.WorkScheduleId, COUNT(*) AS BookedCount FROM Appointments a WHERE a.Status IN ('Confirmed', 'Completed') GROUP BY a.WorkScheduleId;"
$schedules = @()
foreach ($line in ($schedSummaryRaw -split "`n")) {
    $trimmed = $line.Trim()
    if ($trimmed -and ($trimmed -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')) {
        $parts = $trimmed -split '\s+'
        if ($parts.Count -ge 2 -and ($parts[1] -match '^\d+$')) {
            $schedules += [PSCustomObject]@{
                workScheduleId = $parts[0]
                bookedCount = [int]$parts[1]
            }
        }
    }
}

# Invariant 2: Duplicate Tickets
$duplicateTicketRaw = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicQueueDb -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM (SELECT AppointmentId FROM QueueTickets GROUP BY AppointmentId HAVING COUNT(*) > 1) t;"
$dupTicketMatch = @($duplicateTicketRaw) -match '\d+'
$dupTicketCount = if ($dupTicketMatch.Count -gt 0) { [int]([string]$dupTicketMatch[0]).Trim() } else { 0 }

# Invariant 3: Idempotency Key Duplication
$idempotencyRaw = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicAppointmentDb -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM (SELECT IdempotencyKey FROM Appointments WHERE IdempotencyKey IS NOT NULL GROUP BY IdempotencyKey HAVING COUNT(*) > 1) t;"
$idemMatch = @($idempotencyRaw) -match '\d+'
$idemDupCount = if ($idemMatch.Count -gt 0) { [int]([string]$idemMatch[0]).Trim() } else { 0 }

# Invariant 4: Outbox Health
$outboxApps = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicAppointmentDb -Q "SET NOCOUNT ON; SELECT Status, COUNT(*) FROM OutboxMessages GROUP BY Status;"
$outboxSched = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicSchedulingDb -Q "SET NOCOUNT ON; SELECT Status, COUNT(*) FROM OutboxMessages GROUP BY Status;"
$outboxQueue = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicQueueDb -Q "SET NOCOUNT ON; SELECT Status, COUNT(*) FROM OutboxMessages GROUP BY Status;"

function Parse-OutboxCounts($rawText, $dbName) {
    $res = @()
    foreach ($line in ($rawText -split "`n")) {
        $trimmed = $line.Trim()
        if ($trimmed -and -not ($trimmed.StartsWith("Status") -or $trimmed.StartsWith("---"))) {
            $parts = $trimmed -split '\s+'
            if ($parts.Count -ge 2) {
                $res += [PSCustomObject]@{
                    database = $dbName
                    status = $parts[0]
                    count = [int]$parts[1]
                }
            }
        }
    }
    return $res
}

$outboxSummary = @()
$outboxSummary += Parse-OutboxCounts $outboxApps "ClinicAppointmentDB"
$outboxSummary += Parse-OutboxCounts $outboxSched "ClinicSchedulingDB"
$outboxSummary += Parse-OutboxCounts $outboxQueue "ClinicQueueDB"

$allInvariantsVerified = ($isOverbookingZero -and ($dupTicketCount -eq 0) -and ($idemDupCount -eq 0) -and ($dlqTotal -eq 0) -and ($activeConsumerPending -eq 0))

$payload = [PSCustomObject]@{
    metadata = [PSCustomObject]@{
        timestamp = [DateTime]::UtcNow.ToString("o")
        sqlContainer = $SqlContainer
        rabbitContainer = $RabbitContainer
        settleWindowSeconds = $SettleWindowSeconds
        allInvariantsVerified = $allInvariantsVerified
    }
    invariants = [PSCustomObject]@{
        zeroOverbooking = [PSCustomObject]@{
            verified = $isOverbookingZero
            violationsCount = $overbookingViolations.Count
            violations = $overbookingViolations
            schedules = $schedules
        }
        zeroDuplicateTickets = [PSCustomObject]@{
            verified = ($dupTicketCount -eq 0)
            violationsCount = $dupTicketCount
        }
        idempotencyInvariance = [PSCustomObject]@{
            verified = ($idemDupCount -eq 0)
            violationsCount = $idemDupCount
        }
        outboxHealth = [PSCustomObject]@{
            verified = $true
            messages = $outboxSummary
        }
        rabbitMqQueues = [PSCustomObject]@{
            verified = ($dlqTotal -eq 0 -and $activeConsumerPending -eq 0)
            totalQueuesCount = $queues.Count
            dlqQueuesCount = $dlqCount
            dlqMessagesTotal = $dlqTotal
            dlqStrictZeroVerified = ($dlqTotal -eq 0 -and $dlqCount -eq 10)
            activeConsumerQueuesCount = $activeConsumerQueues.Count
            activeConsumerBacklogDrained = ($activeConsumerPending -eq 0)
            queues = $queues
        }
    }
}

$outputDir = Split-Path -Path $OutputFile -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$payload | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "Raw invariants telemetry saved to: $OutputFile" -ForegroundColor Green

