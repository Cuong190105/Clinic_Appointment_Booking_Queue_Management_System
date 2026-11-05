# PowerShell Script: Seed Phase 9 Workload Data with Strict Run-Isolated Resources
param(
    [string]$SqlContainer = "clinic_sql_server",
    [string]$SqlPassword = $null,
    [string]$TestRunId = $null
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
        $TestRunId = "PH9-" + ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
    }
}

$ErrorActionPreference = "Stop"

function Get-DeterministicGuid([string]$seed) {
    $bytes = [System.Security.Cryptography.MD5]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($seed))
    return [System.Guid]::new($bytes).ToString()
}

$contentionScheduleId  = Get-DeterministicGuid "$TestRunId-contention-sched"
$workloadScheduleId    = Get-DeterministicGuid "$TestRunId-workload-sched"
$degradationScheduleId = Get-DeterministicGuid "$TestRunId-degradation-sched"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   SEEDING PHASE 9 SYNTHETIC WORKLOAD DATA                " -ForegroundColor Cyan
Write-Host "   TestRunId               : $TestRunId                   " -ForegroundColor Cyan
Write-Host "   Contention Schedule ID  : $contentionScheduleId        " -ForegroundColor Cyan
Write-Host "   Workload Schedule ID    : $workloadScheduleId          " -ForegroundColor Cyan
Write-Host "   Degradation Schedule ID : $degradationScheduleId       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

function Run-Sql($database, $query) {
    $cleanQuery = "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; $query"
    $output = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d $database -Q $cleanQuery 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "SQL Error in ${database}: $output"
    }
    return $output
}

# 1. Sync Bcrypt hash for admin1 to match doctor1
Write-Host "[1/4] Ensuring admin1 account password is synchronized..." -ForegroundColor Yellow
$syncAdminSql = @"
UPDATE Users 
SET PasswordHash = (SELECT TOP 1 PasswordHash FROM Users WHERE Username = 'doctor1') 
WHERE Username = 'admin1';
"@
Run-Sql "ClinicIdentityDB" $syncAdminSql | Out-Null
Write-Host "  -> Password synchronized successfully." -ForegroundColor Green

# 2. Insert or Ensure Test WorkSchedules in ClinicSchedulingDB strictly tagged with TestRunId
Write-Host "[2/4] Upserting Test WorkSchedules in ClinicSchedulingDB (Tagged with [$TestRunId])..." -ForegroundColor Yellow
$upsertSchedulesSql = @"
DECLARE @tag NVARCHAR(500) = '[$TestRunId] Test Schedule';

-- Schedule 1: Contention Schedule (RecommendedCapacity = 1)
IF NOT EXISTS (SELECT 1 FROM WorkSchedules WHERE Id = '$contentionScheduleId')
BEGIN
    INSERT INTO WorkSchedules (Id, DoctorId, Date, ShiftStart, ShiftEnd, RecommendedCapacity, Status, IsDeleted, CreatedAt, UpdatedAt, CancellationReason)
    VALUES ('$contentionScheduleId', 'a0000004-0000-0000-0000-000000000002', DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), '08:00:00', '20:00:00', 1, 'Active', 0, GETUTCDATE(), GETUTCDATE(), @tag);
END
ELSE
BEGIN
    UPDATE WorkSchedules 
    SET DoctorId = 'a0000004-0000-0000-0000-000000000002', Date = DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), ShiftStart = '08:00:00', ShiftEnd = '20:00:00', RecommendedCapacity = 1, Status = 'Active', IsDeleted = 0, CancellationReason = @tag 
    WHERE Id = '$contentionScheduleId';
END

-- Schedule 2: General Workload / Baseline Schedule (RecommendedCapacity = 100, 48 available slots)
IF NOT EXISTS (SELECT 1 FROM WorkSchedules WHERE Id = '$workloadScheduleId')
BEGIN
    INSERT INTO WorkSchedules (Id, DoctorId, Date, ShiftStart, ShiftEnd, RecommendedCapacity, Status, IsDeleted, CreatedAt, UpdatedAt, CancellationReason)
    VALUES ('$workloadScheduleId', 'a0000004-0000-0000-0000-000000000001', DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), '08:00:00', '20:00:00', 100, 'Active', 0, GETUTCDATE(), GETUTCDATE(), @tag);
END
ELSE
BEGIN
    UPDATE WorkSchedules 
    SET DoctorId = 'a0000004-0000-0000-0000-000000000001', Date = DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), ShiftStart = '08:00:00', ShiftEnd = '20:00:00', RecommendedCapacity = 100, Status = 'Active', IsDeleted = 0, CancellationReason = @tag 
    WHERE Id = '$workloadScheduleId';
END

-- Schedule 3: Degradation Schedule (RecommendedCapacity = 10)
IF NOT EXISTS (SELECT 1 FROM WorkSchedules WHERE Id = '$degradationScheduleId')
BEGIN
    INSERT INTO WorkSchedules (Id, DoctorId, Date, ShiftStart, ShiftEnd, RecommendedCapacity, Status, IsDeleted, CreatedAt, UpdatedAt, CancellationReason)
    VALUES ('$degradationScheduleId', 'a0000004-0000-0000-0000-000000000003', DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), '08:00:00', '20:00:00', 10, 'Active', 0, GETUTCDATE(), GETUTCDATE(), @tag);
END
ELSE
BEGIN
    UPDATE WorkSchedules 
    SET DoctorId = 'a0000004-0000-0000-0000-000000000003', Date = DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), ShiftStart = '08:00:00', ShiftEnd = '20:00:00', RecommendedCapacity = 10, Status = 'Active', IsDeleted = 0, CancellationReason = @tag 
    WHERE Id = '$degradationScheduleId';
END

-- Clear any pre-existing reservations on these newly seeded schedules
DELETE FROM SlotReservations WHERE WorkScheduleId IN ('$contentionScheduleId', '$workloadScheduleId', '$degradationScheduleId');
"@
Run-Sql "ClinicSchedulingDB" $upsertSchedulesSql | Out-Null
Write-Host "  -> Dynamic WorkSchedules prepared for [$TestRunId]." -ForegroundColor Green

# 3. Create Sample Confirmed Appointments for Check-in Testing in ClinicAppointmentDB
Write-Host "[3/4] Ensuring 9 Confirmed Appointments exist for Check-in Testing (Tagged with [$TestRunId])..." -ForegroundColor Yellow

$appointmentIds = @()
for ($i = 1; $i -le 9; $i++) {
    $appId = Get-DeterministicGuid "$TestRunId-checkin-app-$i"
    $patientNumber = (($i - 1) % 9) + 1
    $patientId = "a0000005-0000-0000-0000-" + ($patientNumber.ToString().PadLeft(12, '0'))
    $appointmentIds += $appId

    $slotHour = 8 + [int]($i * 15 / 60)
    $slotMin = ($i * 15) % 60
    $slotStr = "{0:D2}:{1:D2}:00" -f $slotHour, $slotMin
    $reason = "[$TestRunId] Checkin seed test appointment $i"
    $key = "$TestRunId-seed-checkin-$i"

    $sql = @"
IF NOT EXISTS (SELECT 1 FROM Appointments WHERE Id = '$appId')
BEGIN
    INSERT INTO Appointments (
        Id, PatientId, PatientName, PatientPhone, PatientGender, PatientDateOfBirth,
        WorkScheduleId, DoctorId, DoctorName, SpecialtyName, ScheduleDate, TimeSlot,
        Reason, Status, IsWalkIn, UpdatedByUserId, CreatedAt, UpdatedAt, IsDeleted, IdempotencyKey
    ) VALUES (
        '$appId', '$patientId', 'Patient Seed $i', '0901000001', 'Female', '1995-01-01',
        '$workloadScheduleId', 'a0000004-0000-0000-0000-000000000001', 'GS.TS Nguyen Van An',
        'Cardiology', CAST(GETUTCDATE() AS DATE), '$slotStr',
        '$reason', 'Confirmed', 0, '$patientId',
        GETUTCDATE(), GETUTCDATE(), 0, '$key'
    );
END
ELSE
BEGIN
    UPDATE Appointments 
    SET Status = 'Confirmed', IsDeleted = 0, Reason = '$reason', UpdatedByUserId = '$patientId', IdempotencyKey = '$key', ScheduleDate = CAST(GETUTCDATE() AS DATE)
    WHERE Id = '$appId';
END
"@
    Run-Sql "ClinicAppointmentDB" $sql | Out-Null
}
Write-Host "  -> 9 Check-in appointments seeded on workload schedule $workloadScheduleId." -ForegroundColor Green

# 4. Save metadata to $env:TEMP/clinic_phase9_env.json (OUTSIDE workspace)
$envData = [PSCustomObject]@{
    testRunId = $TestRunId
    contentionScheduleId = $contentionScheduleId
    workloadScheduleId = $workloadScheduleId
    degradationScheduleId = $degradationScheduleId
    checkinAppointmentIds = $appointmentIds
}
$tempConfigPath = Join-Path $env:TEMP "clinic_phase9_env.json"
$envData | ConvertTo-Json -Depth 5 | Set-Content -Path $tempConfigPath -Encoding UTF8
Write-Host "[4/4] Environment configuration saved out-of-tree to: $tempConfigPath" -ForegroundColor Green

# Set environment variables for the current session
$env:TEST_RUN_ID = $TestRunId
$env:CONTENTION_SCHEDULE_ID = $contentionScheduleId
$env:WORKLOAD_SCHEDULE_ID = $workloadScheduleId
$env:DEGRADATION_SCHEDULE_ID = $degradationScheduleId

Write-Host "SUCCESS: Phase 9 workload dataset ready for run [$TestRunId]." -ForegroundColor Green
