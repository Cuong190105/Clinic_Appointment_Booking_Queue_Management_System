# PowerShell Script: Execute Soak Test with Telemetry Snapshot Progression (T0, T30s, T60s, T_cooldown)
param(
    [string]$SqlContainer = "clinic_sql_server",
    [string]$SqlPassword = $null,
    [string]$SeedPassword = $null,
    [string]$RabbitContainer = "clinic_appointment_booking_queue_management_system-rabbitmq-1",
    [string]$OutputFile = "tests/load/evidence/raw_soak_telemetry.json"
)

if (-not $SqlPassword) {
    if ($env:DB_SA_PASSWORD) {
        $SqlPassword = $env:DB_SA_PASSWORD
    } else {
        throw "Missing mandatory environment variable DB_SA_PASSWORD or -SqlPassword parameter."
    }
}
if (-not $SeedPassword) {
    if ($env:SEED_PASSWORD) {
        $SeedPassword = $env:SEED_PASSWORD
    } else {
        throw "Missing mandatory environment variable SEED_PASSWORD or -SeedPassword parameter."
    }
}

$ErrorActionPreference = "Stop"

function Get-SqlConnections {
    $raw = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.dm_exec_connections;" 2>&1
    $matching = @($raw) -match '\d+'
    if ($matching.Count -gt 0) {
        return [int]([string]$matching[0]).Trim()
    }
    return 0
}

function Get-RabbitConsumers {
    $raw = docker exec $RabbitContainer rabbitmqctl list_consumers 2>&1
    $lines = @($raw) -match 'amq\.ctag'
    return $lines.Count
}

function Parse-MemMb([string]$memStr) {
    $used = ($memStr -split '/')[0].Trim()
    if ($used -match '([\d\.]+)\s*(GiB|GB)') {
        return [double]$matches[1] * 1024
    } elseif ($used -match '([\d\.]+)\s*(MiB|MB)') {
        return [double]$matches[1]
    } elseif ($used -match '([\d\.]+)\s*(KiB|KB)') {
        return [double]$matches[1] / 1024
    }
    return 0
}

function Get-ContainerStats {
    $raw = docker stats --no-stream --format "{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}|{{.MemPerc}}"
    $stats = @()
    foreach ($line in ($raw -split "`n")) {
        $trimmed = $line.Trim()
        if ($trimmed) {
            $parts = $trimmed -split '\|'
            if ($parts.Count -ge 4) {
                $memMb = Parse-MemMb $parts[2]
                $stats += [PSCustomObject]@{
                    name = $parts[0]
                    cpuPerc = $parts[1]
                    memUsage = $parts[2]
                    memUsageMb = [Math]::Round($memMb, 2)
                    memPerc = $parts[3]
                }
            }
        }
    }
    return $stats
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   SOAK TEST TELEMETRY PROFILER (Phase 9)                 " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Snapshot T0 (Baseline)
Write-Host "[1/5] Capturing T0 (Baseline) Telemetry..." -ForegroundColor Yellow
$t0Sql = Get-SqlConnections
$t0Consumers = Get-RabbitConsumers
$t0Containers = Get-ContainerStats
$t0 = [PSCustomObject]@{
    stage = "T0_Baseline"
    timestamp = [DateTime]::UtcNow.ToString("o")
    sqlConnections = $t0Sql
    activeRabbitConsumers = $t0Consumers
    containers = $t0Containers
}
Write-Host "  -> T0 SQL Connections: $t0Sql, Rabbit Consumers: $t0Consumers, Profiled Containers: $($t0Containers.Count)" -ForegroundColor Green

# 2. Start Soak Test directly via Docker process
Write-Host "[2/5] Launching k6 Soak Test (60s steady endurance)..." -ForegroundColor Yellow
docker rm -f clinic-soak-runner 2>&1 | Out-Null
$k6Args = @(
    "run", "-d", "--name", "clinic-soak-runner",
    "--network", "clinic_appointment_booking_queue_management_system_default",
    "-v", "$($PWD.Path)/tests/load:/tests/load",
    "-e", "BASE_URL=http://clinic_backend:8080",
    "-e", "SEED_PASSWORD=$SeedPassword",
    "grafana/k6", "run",
    "--summary-export=/tests/load/evidence/raw_soak.json",
    "/tests/load/soak.js"
)
$containerId = (& docker $k6Args).Trim()
Write-Host "  -> Started soak container: $containerId" -ForegroundColor Green

# 3. Wait 30s and Capture T30s
Start-Sleep -Seconds 30
Write-Host "[3/5] Capturing T30s (Mid-run Sustained Load) Telemetry..." -ForegroundColor Yellow
$t30Sql = Get-SqlConnections
$t30Containers = Get-ContainerStats
$t30 = [PSCustomObject]@{
    stage = "T30s_MidRun"
    timestamp = [DateTime]::UtcNow.ToString("o")
    sqlConnections = $t30Sql
    containers = $t30Containers
}
Write-Host "  -> T30s SQL Connections: $t30Sql" -ForegroundColor Green

# 4. Wait another 30s (Total 60s) and Capture T60s
Start-Sleep -Seconds 30
Write-Host "[4/5] Capturing T60s (Peak Endurance) Telemetry..." -ForegroundColor Yellow
$t60Sql = Get-SqlConnections
$t60Containers = Get-ContainerStats
$t60 = [PSCustomObject]@{
    stage = "T60s_Peak"
    timestamp = [DateTime]::UtcNow.ToString("o")
    sqlConnections = $t60Sql
    containers = $t60Containers
}
Write-Host "  -> T60s SQL Connections: $t60Sql" -ForegroundColor Green

# Wait for k6 container to exit cleanly via docker wait
Write-Host "Waiting for k6 soak container to conclude..." -ForegroundColor Yellow
$k6ExitRaw = (docker wait clinic-soak-runner 2>&1)
$k6ExitCode = [int]($k6ExitRaw.ToString().Trim())
Write-Host "  -> k6 soak container exited with code: $k6ExitCode" -ForegroundColor Green
docker rm clinic-soak-runner 2>&1 | Out-Null

# 5. Cooldown 15s and Capture T_cooldown
Write-Host "[5/5] Waiting 15s Cooldown and Capturing T_cooldown Telemetry..." -ForegroundColor Yellow
Start-Sleep -Seconds 15
$tCoolSql = Get-SqlConnections
$tCoolConsumers = Get-RabbitConsumers
$tCoolContainers = Get-ContainerStats
$tCool = [PSCustomObject]@{
    stage = "T_cooldown"
    timestamp = [DateTime]::UtcNow.ToString("o")
    sqlConnections = $tCoolSql
    activeRabbitConsumers = $tCoolConsumers
    containers = $tCoolContainers
}
Write-Host "  -> T_cooldown SQL Connections: $tCoolSql (Baseline T0 was: $t0Sql), Rabbit Consumers: $tCoolConsumers" -ForegroundColor Green

# Analyze per-container memory growth and return-to-baseline
$containerAnalysis = @()
$leakingContainers = @()

foreach ($c0 in $t0Containers) {
    $cCool = $tCoolContainers | Where-Object { $_.name -eq $c0.name } | Select-Object -First 1
    if ($cCool) {
        $m0 = $c0.memUsageMb
        $mCool = $cCool.memUsageMb
        $growthMb = [Math]::Round(($mCool - $m0), 2)
        $growthPct = if ($m0 -gt 0) { [Math]::Round(($growthMb / $m0 * 100), 2) } else { 0 }
        
        # Leaking threshold: growth > 15% AND absolute growth > 30MB
        $isLeaking = ($growthPct -gt 15.0 -and $growthMb -gt 30.0)
        if ($isLeaking) {
            $leakingContainers += $c0.name
        }

        $containerAnalysis += [PSCustomObject]@{
            container = $c0.name
            baselineT0Mb = $m0
            cooldownMb = $mCool
            growthMb = $growthMb
            growthPercentage = $growthPct
            isLeakFree = (-not $isLeaking)
        }
    }
}

$isMemoryLeakFree = ($leakingContainers.Count -eq 0)
$isConnectionLeakFree = ($tCoolSql -le ($t0Sql + 2))
$isWorkerDegradationFree = ($tCoolConsumers -ge $t0Consumers)
$overallPassed = ($k6ExitCode -eq 0) -and $isMemoryLeakFree -and $isConnectionLeakFree -and $isWorkerDegradationFree

$telemetryReport = [PSCustomObject]@{
    metadata = [PSCustomObject]@{
        test = "k6_soak_telemetry"
        timestamp = [DateTime]::UtcNow.ToString("o")
        durationSeconds = 60
        cooldownSeconds = 15
        k6ExitCode = $k6ExitCode
        overallPassed = $overallPassed
    }
    telemetry = [PSCustomObject]@{
        T0 = $t0
        T30s = $t30
        T60s = $t60
        T_cooldown = $tCool
    }
    reconciliation = [PSCustomObject]@{
        sqlConnectionBaseline = $t0Sql
        sqlConnectionPeak = [Math]::Max($t30Sql, $t60Sql)
        sqlConnectionCooldown = $tCoolSql
        sqlConnectionReturnedToBaseline = $isConnectionLeakFree
        noConnectionLeak = $isConnectionLeakFree
        rabbitConsumersBaseline = $t0Consumers
        rabbitConsumersCooldown = $tCoolConsumers
        noWorkerDegradation = $isWorkerDegradationFree
        memoryThresholds = "Growth <= 15% or <= 30MB buffer"
        isMemoryLeakFree = $isMemoryLeakFree
        leakingContainers = $leakingContainers
        containerAnalysis = $containerAnalysis
    }
}

$outputDir = Split-Path -Path $OutputFile -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$telemetryReport | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "Soak telemetry progression report saved to: $OutputFile" -ForegroundColor Green

if (-not $overallPassed) {
    Write-Error "[FAIL] Soak test violated SLO or showed resource leakage (k6ExitCode: $k6ExitCode, isMemoryLeakFree: $isMemoryLeakFree, isConnectionLeakFree: $isConnectionLeakFree)"
    exit 1
}

Write-Host "[SUCCESS] Soak endurance test completed with 0 leaks and verified return-to-baseline." -ForegroundColor Green
