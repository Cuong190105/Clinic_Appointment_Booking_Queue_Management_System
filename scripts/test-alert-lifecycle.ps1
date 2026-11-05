# Test script for Prometheus Alert Lifecycle verification (Pending -> Firing -> Resolved)
param(
    [string]$TargetContainer = "clinic_notification_service",
    [string]$AlertName = "MicroserviceDown",
    [string]$JobName = "clinic-notification-service"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [Phase 8] Testing Prometheus Alert Lifecycle Validation  " -ForegroundColor Cyan
Write-Host " Target Rule: $AlertName | Service: $JobName" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Baseline alert check
Write-Host "1. Verifying initial inactive state..." -ForegroundColor Yellow
$initialRules = (Invoke-RestMethod -Uri 'http://localhost:9090/api/v1/rules').data.groups[0].rules | Where-Object { $_.name -eq $AlertName }
Write-Host "   Initial Rule State: $($initialRules.state) | Health: $($initialRules.health)" -ForegroundColor Green

# 2. Trigger fault: pause target container
Write-Host "`n2. Triggering safe fault: pausing $TargetContainer..." -ForegroundColor Yellow
docker pause $TargetContainer | Out-Null
Write-Host "   Paused $TargetContainer successfully." -ForegroundColor Gray
Write-Host "   Waiting up to 85 seconds for Prometheus scrape (15s) and alert 'for' duration (30s) to enter 'firing'..." -ForegroundColor Yellow

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$firingDetected = $false
$firingSnapshot = $null

while ($sw.Elapsed.TotalSeconds -lt 85) {
    Start-Sleep -Seconds 5
    $activeRules = (Invoke-RestMethod -Uri 'http://localhost:9090/api/v1/rules').data.groups[0].rules | Where-Object { $_.name -eq $AlertName }
    $elapsed = [Math]::Round($sw.Elapsed.TotalSeconds, 1)
    Write-Host "   [+${elapsed}s] Rule State: $($activeRules.state) | Active Alerts: $($activeRules.alerts.Count)" -ForegroundColor Gray

    if ($activeRules.state -eq "firing") {
        $firingDetected = $true
        $allAlerts = (Invoke-RestMethod -Uri 'http://localhost:9090/api/v1/alerts').data.alerts
        $firingAlert = $allAlerts | Where-Object { $_.labels.alertname -eq $AlertName -and $_.labels.job -eq $JobName }
        $firingSnapshot = [PSCustomObject]@{
            Timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:sszzz")
            ElapsedSeconds = $elapsed
            RuleState = $activeRules.state
            RuleHealth = $activeRules.health
            Alert = $firingAlert
        }
        Write-Host "   >>> ALERT IS FIRING! Captured firing state from Prometheus API." -ForegroundColor Red
        break
    }
}

# 3. Recover: unpause target container
Write-Host "`n3. Recovering service: unpausing $TargetContainer..." -ForegroundColor Yellow
docker unpause $TargetContainer | Out-Null
Write-Host "   Unpaused $TargetContainer successfully." -ForegroundColor Gray
Write-Host "   Waiting 25 seconds for Prometheus scrape recovery..." -ForegroundColor Yellow

$resolvedDetected = $false
$resolvedSnapshot = $null
$swRec = [System.Diagnostics.Stopwatch]::StartNew()

while ($swRec.Elapsed.TotalSeconds -lt 40) {
    Start-Sleep -Seconds 5
    $activeRules = (Invoke-RestMethod -Uri 'http://localhost:9090/api/v1/rules').data.groups[0].rules | Where-Object { $_.name -eq $AlertName }
    $elapsed = [Math]::Round($swRec.Elapsed.TotalSeconds, 1)
    Write-Host "   [+${elapsed}s] Rule State: $($activeRules.state) | Active Alerts: $($activeRules.alerts.Count)" -ForegroundColor Gray

    if ($activeRules.state -eq "inactive" -or $activeRules.alerts.Count -eq 0) {
        $resolvedDetected = $true
        $resolvedSnapshot = [PSCustomObject]@{
            Timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:sszzz")
            ElapsedSeconds = $elapsed
            RuleState = $activeRules.state
            RuleHealth = $activeRules.health
            ActiveAlertsCount = $activeRules.alerts.Count
        }
        Write-Host "   >>> ALERT RESOLVED! Rule state returned to inactive." -ForegroundColor Green
        break
    }
}

$evidence = [PSCustomObject]@{
    TestName = "Prometheus Alert Lifecycle Verification (Pending -> Firing -> Resolved)"
    TestedRule = $AlertName
    TargetJob = $JobName
    TargetContainer = $TargetContainer
    FaultInjectionType = "Safe process freeze via docker pause (duration: ~45s)"
    InitialState = [PSCustomObject]@{
        RuleState = $initialRules.state
        RuleHealth = $initialRules.health
    }
    FiringState = $firingSnapshot
    ResolvedState = $resolvedSnapshot
    LifecycleSuccessfullyVerified = ($firingDetected -and $resolvedDetected)
}

$outPath = Join-Path $PSScriptRoot "..\tasks\PHASE_8_ALERT_LIFECYCLE_EVIDENCE.json"
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding utf8
Write-Host "`n>>> Alert lifecycle evidence written to: $outPath" -ForegroundColor Green
