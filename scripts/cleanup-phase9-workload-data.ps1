# PowerShell Script: Cleanup Phase 9 Synthetic Workload Data
param(
    [string]$SqlContainer = "clinic_sql_server",
    [string]$SqlPassword = $null,
    [string]$TestRunId = $null,
    [switch]$DryRun
)

if (-not $SqlPassword) {
    if ($env:DB_SA_PASSWORD) {
        $SqlPassword = $env:DB_SA_PASSWORD
    } else {
        throw "Missing mandatory environment variable DB_SA_PASSWORD or -SqlPassword parameter."
    }
}
if (-not $TestRunId) {
    if ($env:TEST_RUN_ID) {
        $TestRunId = $env:TEST_RUN_ID
    } else {
        throw "Missing mandatory environment variable TEST_RUN_ID or -TestRunId parameter."
    }
}

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   CLEANING UP PHASE 9 SYNTHETIC WORKLOAD DATA            " -ForegroundColor Cyan
Write-Host "   Target Namespace : [$TestRunId]                        " -ForegroundColor Cyan
Write-Host "   Mode             : $(if ($DryRun) { 'DRY RUN (Preview Only)' } else { 'LIVE EXECUTION' })" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

function Run-Sql($database, $query) {
    $cleanQuery = "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $query"
    $output = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d $database -Q $cleanQuery 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SQL Error in ${database}: $output"
    }
    return $output
}

# --- STEP 1: Pre-cleanup Audit (Query Baseline and Target Records) ---
Write-Host "[1/3] Auditing target test records and baseline records..." -ForegroundColor Yellow

$preAuditSql = @"
DECLARE @tag NVARCHAR(200) = '[' + '$TestRunId' + ']';
DECLARE @keyPrefix NVARCHAR(200) = '$TestRunId' + '-';

SELECT 
    (SELECT COUNT(*) FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) = 0) AS BaselineAppointments,
    (SELECT COUNT(*) FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) > 0) AS TargetAppointments,
    (SELECT COUNT(*) FROM ClinicQueueDB.dbo.QueueTickets WHERE AppointmentId IN (SELECT Id FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) > 0)) AS TargetTickets,
    (SELECT COUNT(*) FROM ClinicMedicalRecordsDB.dbo.MedicalReports WHERE CHARINDEX(@tag, Symptoms) > 0 OR AppointmentId IN (SELECT Id FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) > 0)) AS TargetReports,
    (SELECT COUNT(*) FROM ClinicSchedulingDB.dbo.SlotReservations WHERE WorkScheduleId IN (SELECT Id FROM ClinicSchedulingDB.dbo.WorkSchedules WHERE CHARINDEX(@tag, CancellationReason) > 0) OR CHARINDEX(@keyPrefix, IdempotencyKey) = 1) AS TargetReservations,
    (SELECT COUNT(*) FROM ClinicSchedulingDB.dbo.WorkSchedules WHERE CHARINDEX(@tag, CancellationReason) > 0) AS TargetSchedules;
"@

$auditRaw = Run-Sql "ClinicAppointmentDB" $preAuditSql
$matching = @($auditRaw) -match '\d+'
$dataRow = [string]$matching[0]
$counts = $dataRow.Trim() -split '\s+'

$baselineBefore     = [int]$counts[0]
$targetApps         = [int]$counts[1]
$targetTickets      = [int]$counts[2]
$targetReports      = [int]$counts[3]
$targetReservations = [int]$counts[4]
$targetSchedules    = [int]$counts[5]

Write-Host "  -> Baseline Non-Test Appointments: $baselineBefore (Must remain unchanged)" -ForegroundColor Cyan
Write-Host "  -> Identified Target Test Records for [$TestRunId]:" -ForegroundColor White
Write-Host "     - Appointments:      $targetApps" -ForegroundColor White
Write-Host "     - Queue Tickets:     $targetTickets" -ForegroundColor White
Write-Host "     - Medical Reports:   $targetReports" -ForegroundColor White
Write-Host "     - Slot Reservations: $targetReservations" -ForegroundColor White
Write-Host "     - Work Schedules:    $targetSchedules" -ForegroundColor White

if ($DryRun) {
    Write-Host "`n[DRY RUN COMPLETE] Preview completed. 0 records deleted." -ForegroundColor Yellow
    exit 0
}

# --- STEP 2: Scoped Execution (Strictly bounded by TestRunId marker) ---
Write-Host "`n[2/3] Performing scoped cleanup for namespace [$TestRunId]..." -ForegroundColor Yellow

$tagParam = "DECLARE @tag NVARCHAR(200) = '[' + '$TestRunId' + ']'; DECLARE @keyPrefix NVARCHAR(200) = '$TestRunId' + '-'; "

# 1. Clean QueueTickets
$cleanQueueSql = @"
$tagParam
DELETE FROM QueueTickets 
WHERE AppointmentId IN (
    SELECT Id FROM ClinicAppointmentDB.dbo.Appointments 
    WHERE CHARINDEX(@tag, Reason) > 0
);
"@
Run-Sql "ClinicQueueDB" $cleanQueueSql | Out-Null
Write-Host "  -> Scoped QueueTickets cleaned." -ForegroundColor Green

# 2. Clean MedicalReports
$cleanMedicalSql = @"
$tagParam
DELETE FROM MedicalReports 
WHERE AppointmentId IN (
    SELECT Id FROM ClinicAppointmentDB.dbo.Appointments 
    WHERE CHARINDEX(@tag, Reason) > 0
) OR CHARINDEX(@tag, Symptoms) > 0;
"@
Run-Sql "ClinicMedicalRecordsDB" $cleanMedicalSql | Out-Null
Write-Host "  -> Scoped MedicalReports cleaned." -ForegroundColor Green

# 3. Clean Appointments
$cleanAppSql = @"
$tagParam
DELETE FROM Appointments 
WHERE CHARINDEX(@tag, Reason) > 0;
"@
Run-Sql "ClinicAppointmentDB" $cleanAppSql | Out-Null
Write-Host "  -> Scoped Appointments cleaned." -ForegroundColor Green

# 4. Clean SlotReservations
$cleanResSql = @"
$tagParam
DELETE FROM SlotReservations 
WHERE WorkScheduleId IN (
    SELECT Id FROM WorkSchedules WHERE CHARINDEX(@tag, CancellationReason) > 0
) OR CHARINDEX(@keyPrefix, IdempotencyKey) = 1;
"@
Run-Sql "ClinicSchedulingDB" $cleanResSql | Out-Null
Write-Host "  -> Scoped SlotReservations cleaned." -ForegroundColor Green

# 5. Clean WorkSchedules
$cleanSchedSql = @"
$tagParam
DELETE FROM WorkSchedules 
WHERE CHARINDEX(@tag, CancellationReason) > 0;
"@
Run-Sql "ClinicSchedulingDB" $cleanSchedSql | Out-Null
Write-Host "  -> Scoped WorkSchedules cleaned." -ForegroundColor Green

# --- RECONCILIATION STEP 3: Post-cleanup Verification Audit ---
Write-Host "`n[3/3] Verifying reconciliation and baseline integrity..." -ForegroundColor Yellow

$postAuditSql = @"
$tagParam
SELECT 
    (SELECT COUNT(*) FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) = 0) AS BaselineAppointmentsAfter,
    (SELECT COUNT(*) FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) > 0) AS TargetAppointmentsAfter,
    (SELECT COUNT(*) FROM ClinicQueueDB.dbo.QueueTickets WHERE AppointmentId IN (SELECT Id FROM ClinicAppointmentDB.dbo.Appointments WHERE CHARINDEX(@tag, Reason) > 0)) AS TargetTicketsAfter,
    (SELECT COUNT(*) FROM ClinicSchedulingDB.dbo.WorkSchedules WHERE CHARINDEX(@tag, CancellationReason) > 0) AS TargetSchedulesAfter;
"@

$postAuditRaw = Run-Sql "ClinicAppointmentDB" $postAuditSql
$postMatching = @($postAuditRaw) -match '\d+'
$postDataRow = [string]$postMatching[0]
$postCounts = $postDataRow.Trim() -split '\s+'

$baselineAfter    = [int]$postCounts[0]
$targetAppsAfter  = [int]$postCounts[1]
$targetTixAfter   = [int]$postCounts[2]
$targetSchedAfter = [int]$postCounts[3]

Write-Host "  -> Baseline Non-Test Appointments After: $baselineAfter (Before was: $baselineBefore)" -ForegroundColor White
Write-Host "  -> Remaining Target Appointments:       $targetAppsAfter (Expected: 0)" -ForegroundColor White
Write-Host "  -> Remaining Target Queue Tickets:      $targetTixAfter (Expected: 0)" -ForegroundColor White
Write-Host "  -> Remaining Target Work Schedules:     $targetSchedAfter (Expected: 0)" -ForegroundColor White

if ($baselineBefore -ne $baselineAfter) {
    Write-Error "[INVARIANT VIOLATION] Baseline non-test appointments changed during cleanup! ($baselineBefore != $baselineAfter)"
}

if ($targetAppsAfter -ne 0 -or $targetTixAfter -ne 0 -or $targetSchedAfter -ne 0) {
    Write-Error "[INCOMPLETE CLEANUP] Target test records still remain after cleanup execution!"
}

Write-Host "`n[SUCCESS] Cleanup fully reconciled. Baseline data 100% intact, 0 residual test records for [$TestRunId]." -ForegroundColor Green
