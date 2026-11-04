# PowerShell Script: Dependency Degradation Pass (RabbitMQ Partition Simulation)
param(
    [string]$SqlContainer = "clinic_sql_server",
    [string]$SqlPassword = $null,
    [string]$RabbitContainer = "clinic_appointment_booking_queue_management_system-rabbitmq-1",
    [string]$OutputFile = "tests/load/evidence/raw_degradation.log",
    [string]$OutputJson = "tests/load/evidence/raw_degradation.json",
    [string]$TestRunId = $null,
    [switch]$SimulateFailure
)

$ErrorActionPreference = "Stop"

if (-not $SqlPassword) {
    if ($env:DB_SA_PASSWORD) {
        $SqlPassword = $env:DB_SA_PASSWORD
    } else {
        throw "Missing mandatory environment variable DB_SA_PASSWORD."
    }
}

if (-not $env:SEED_PASSWORD) {
    throw "Missing mandatory environment variable SEED_PASSWORD."
}

if (-not $TestRunId) {
    $TestRunId = if ($env:TEST_RUN_ID) { $env:TEST_RUN_ID } else { "PH9-DEG" }
}

function Get-DeterministicGuid([string]$seed) {
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $hash = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($seed))
    return [System.Guid]::new($hash).ToString()
}

$logLines = @()
$steps = @()

function Log($msg) {
    $timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffZ")
    $entry = "[$timestamp] $msg"
    Write-Host $entry
    $script:logLines += $entry
}

function Record-Step([int]$num, [string]$desc, [string]$status, $details = $null) {
    $script:steps += [PSCustomObject]@{
        step = $num
        description = $desc
        status = $status
        details = $details
        timestamp = [DateTime]::UtcNow.ToString("o")
    }
}

Log "=========================================================="
Log "   PHASE 9: DEPENDENCY DEGRADATION PASS (RabbitMQ Outage) "
Log "=========================================================="
Log "TestRunId: $TestRunId"

$overallPassed = $false
$exitCode = 0

try {
    # 1. Verify RabbitMQ is running initially
    $rbState = (docker inspect $RabbitContainer --format '{{.State.Status}}').Trim()
    Log "[Step 1] Initial RabbitMQ status: $rbState"
    if ($rbState -ne 'running') {
        Record-Step 1 "Verify initial RabbitMQ running state" "FAILED" @{ status = $rbState }
        throw "RabbitMQ container is not in 'running' state: $rbState"
    }
    Record-Step 1 "Verify initial RabbitMQ running state" "PASSED" @{ status = $rbState }

    # Resolve degradation schedule ID dynamically
    $tempEnvPath = Join-Path $env:TEMP "clinic_phase9_env.json"
    $scheduleId = $null
    if ($env:DEGRADATION_SCHEDULE_ID) {
        $scheduleId = $env:DEGRADATION_SCHEDULE_ID
    } elseif (Test-Path $tempEnvPath) {
        try {
            $tempEnv = Get-Content $tempEnvPath -Raw | ConvertFrom-Json
            if ($tempEnv.testRunId -eq $TestRunId -and $tempEnv.degradationScheduleId) {
                $scheduleId = $tempEnv.degradationScheduleId
            }
        } catch {}
    }
    if (-not $scheduleId) {
        $scheduleId = Get-DeterministicGuid "$TestRunId-degradation-sched"
    }
    Log "Degradation WorkSchedule ID: $scheduleId"

    # Ensure dedicated schedule exists in ClinicSchedulingDb
    $prepSql = @"
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM WorkSchedules WHERE Id = '$scheduleId')
BEGIN
    INSERT INTO WorkSchedules (Id, DoctorId, Date, ShiftStart, ShiftEnd, RecommendedCapacity, Status, IsDeleted, CreatedAt, UpdatedAt, CancellationReason)
    VALUES ('$scheduleId', 'a0000004-0000-0000-0000-000000000001', DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), '08:00:00', '20:00:00', 10, 'Active', 0, GETUTCDATE(), GETUTCDATE(), '[$TestRunId] Degradation test schedule');
END
ELSE
BEGIN
    UPDATE WorkSchedules
    SET Date = DATEADD(DAY, 1, CAST(GETUTCDATE() AS DATE)), ShiftStart = '08:00:00', ShiftEnd = '20:00:00', RecommendedCapacity = 10, Status = 'Active', IsDeleted = 0, CancellationReason = '[$TestRunId] Degradation test schedule'
    WHERE Id = '$scheduleId';
END
DELETE FROM SlotReservations WHERE WorkScheduleId = '$scheduleId';
"@
    docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicSchedulingDb -Q $prepSql | Out-Null
    $cleanAppSql = "SET QUOTED_IDENTIFIER ON; DELETE FROM Appointments WHERE WorkScheduleId = '$scheduleId';"
    docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicAppointmentDb -Q $cleanAppSql | Out-Null
    Log "  -> Dedicated WorkSchedule $scheduleId prepared and cleared."

    # 2. Pause RabbitMQ
    Log "[Step 2] Pausing RabbitMQ container ($RabbitContainer)..."
    docker pause $RabbitContainer | Out-Null
    $rbPausedState = (docker inspect $RabbitContainer --format '{{.State.Status}}').Trim()
    Log "  -> RabbitMQ status is now: $rbPausedState"
    if ($rbPausedState -ne 'paused') {
        Record-Step 2 "Pause RabbitMQ broker" "FAILED" @{ status = $rbPausedState }
        throw "Failed to pause RabbitMQ. Status: $rbPausedState"
    }
    Record-Step 2 "Pause RabbitMQ broker" "PASSED" @{ status = $rbPausedState }

    try {
        if ($SimulateFailure) {
            Log "[Step 3] [NEGATIVE TEST INJECTION] Simulating failure during broker outage..."
            Record-Step 3 "Simulated failure during broker outage" "FAILED" @{
                reason = "Intentional negative test injected failure to verify error handling and finally block unpause guarantee"
                simulated = $true
            }
            throw "Intentional negative test error: simulated booking/assertion failure during broker outage."
        }

        # 3. Authenticate patient in memory or from temp cache
        $patientToken = $null
        $tempTokensPath = Join-Path $env:TEMP "clinic_phase9_tokens.json"
        if (Test-Path $tempTokensPath) {
            try {
                $tempTokens = Get-Content $tempTokensPath -Raw | ConvertFrom-Json
                if ($tempTokens.patients -and $tempTokens.patients.Count -gt 0) {
                    $patientToken = $tempTokens.patients[0].token
                }
            } catch {}
        }
        if (-not $patientToken) {
            Log "Authenticating patient in memory..."
            $loginBody = @{
                username = "patient1"
                password = $env:SEED_PASSWORD
            } | ConvertTo-Json
            try {
                $loginRes = Invoke-RestMethod -Uri "http://localhost:5001/v1/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
                $patientToken = $loginRes.accessToken
            } catch {
                $loginRes = Invoke-RestMethod -Uri "http://localhost:5000/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
                $patientToken = if ($loginRes.result -and $loginRes.result.accessToken) { $loginRes.result.accessToken } else { $loginRes.token }
            }
        }

        # Submit online booking through API Gateway while broker is paused
        Log "[Step 3] Submitting POST /appointments while broker is paused..."
        $uniqueKey = "$TestRunId-deg-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
        $body = @{
            workScheduleId = $scheduleId
            timeSlot = "09:00:00"
            reason = "[$TestRunId] Degradation test during RabbitMQ outage"
            isWalkIn = $false
        } | ConvertTo-Json

        $responseStatus = 0
        $responseBody = $null

        try {
            $response = Invoke-RestMethod -Uri "http://localhost:5000/appointments" -Method Post -Body $body -ContentType "application/json" -Headers @{
                Authorization = "Bearer $patientToken"
                "Idempotency-Key" = $uniqueKey
                "X-Correlation-ID" = "degradation-test-req"
            }
            $responseStatus = 201
            $responseBody = ($response | ConvertTo-Json -Compress)
        } catch {
            if ($_.Exception.Response) {
                $responseStatus = [int]$_.Exception.Response.StatusCode
            } else {
                $responseStatus = 500
            }
            if ($_.ErrorDetails) {
                $responseBody = $_.ErrorDetails.Message
            } else {
                $responseBody = $_.Exception.Message
            }
        }

        Log "  -> API Gateway Response Status: $responseStatus"
        Log "  -> Response Body: $responseBody"

        if ($responseStatus -ne 201 -and $responseStatus -ne 200) {
            Record-Step 3 "POST /appointments during broker outage" "FAILED" @{ httpStatus = $responseStatus; response = $responseBody }
            throw "Booking failed during broker outage. Status: $responseStatus"
        }
        Record-Step 3 "POST /appointments during broker outage" "PASSED" @{ httpStatus = $responseStatus; idempotencyKey = $uniqueKey }

        # 4. Check Outbox in SQL Server - Must be 'Pending'
        Log "[Step 4] Inspecting Transactional Outbox in ClinicAppointmentDB..."
        $queryPending = "SET NOCOUNT ON; SELECT TOP 1 Id, Status, EventType, CreatedAt, PublishedAt FROM OutboxMessages WHERE Status = 'Pending' ORDER BY CreatedAt DESC;"
        $pendingOutput = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicAppointmentDb -Q $queryPending 2>&1
        $pendingText = ($pendingOutput | Out-String).Trim()
        Log "  -> Outbox Pending Record query result:"
        Log $pendingText

        $isPendingFound = ($pendingText -match 'Pending')
        if (-not $isPendingFound) {
            Record-Step 4 "Verify Outbox Status is Pending during outage" "FAILED" @{ output = $pendingText }
            throw "Expected Outbox message with status 'Pending', but none found!"
        }
        Record-Step 4 "Verify Outbox Status is Pending during outage" "PASSED" @{ outboxStatus = "Pending" }
    } finally {
        # 5. Always Unpause RabbitMQ
        Log "[Step 5] Unpausing RabbitMQ container ($RabbitContainer)..."
        docker unpause $RabbitContainer 2>&1 | Out-Null
        $rbResumedState = (docker inspect $RabbitContainer --format '{{.State.Status}}').Trim()
        Log "  -> RabbitMQ status is now: $rbResumedState"
        if ($rbResumedState -ne 'running') {
            Record-Step 5 "Unpause RabbitMQ container" "FAILED" @{ status = $rbResumedState }
        } else {
            Record-Step 5 "Unpause RabbitMQ container" "PASSED" @{ status = $rbResumedState }
        }
    }

    # 6. Wait for Outbox Publisher worker to publish pending messages
    Log "[Step 6] Waiting 12 seconds for Outbox Publisher to sweep and deliver backlog..."
    Start-Sleep -Seconds 12

    # 7. Check Outbox again - Status must be 'Sent'
    Log "[Step 7] Inspecting Outbox status post-recovery..."
    $querySent = "SET NOCOUNT ON; SELECT TOP 1 Id, Status, EventType, CreatedAt, PublishedAt FROM OutboxMessages WHERE EventType = 'AppointmentCreated' ORDER BY CreatedAt DESC;"
    $sentOutput = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d ClinicAppointmentDb -Q $querySent 2>&1
    $sentText = ($sentOutput | Out-String).Trim()
    Log "  -> Outbox Post-Recovery query result:"
    Log $sentText

    $isSentFound = ($sentText -match 'Sent')
    if (-not $isSentFound) {
        Record-Step 7 "Verify Outbox Status updated to Sent post-recovery" "FAILED" @{ output = $sentText }
        throw "Expected Outbox message status 'Sent' after recovery, but found: $sentText"
    }
    Record-Step 7 "Verify Outbox Status updated to Sent post-recovery" "PASSED" @{ outboxStatus = "Sent" }

    $overallPassed = $true
    Log "[SUCCESS] Dependency Degradation Pass completed successfully."
} catch {
    $exitCode = 1
    Log "ERROR in Degradation Pass: $_"
} finally {
    # Generate machine-readable JSON
    $failedChecks = @($steps | Where-Object { $_.status -eq "FAILED" }).Count
    $jsonReport = [PSCustomObject]@{
        metadata = [PSCustomObject]@{
            testRunId = $TestRunId
            timestamp = [DateTime]::UtcNow.ToString("o")
            sqlContainer = $SqlContainer
            rabbitContainer = $RabbitContainer
            isNegativeTest = [bool]$SimulateFailure
        }
        passed = $overallPassed
        exitCode = $exitCode
        failedChecks = $failedChecks
        steps = $steps
    }

    $jsonDir = Split-Path -Path $OutputJson -Parent
    if ($jsonDir -and -not (Test-Path $jsonDir)) {
        New-Item -ItemType Directory -Path $jsonDir -Force | Out-Null
    }
    $jsonReport | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputJson -Encoding UTF8
    Log "Machine-readable degradation evidence saved to: $OutputJson"

    $outputDir = Split-Path -Path $OutputFile -Parent
    if ($outputDir -and -not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    $script:logLines | Set-Content -Path $OutputFile -Encoding UTF8
    Log "Log saved to: $OutputFile"

    if ($exitCode -ne 0) {
        exit $exitCode
    }
}

