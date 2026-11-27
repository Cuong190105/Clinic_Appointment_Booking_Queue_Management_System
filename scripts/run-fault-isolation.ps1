<#
.SYNOPSIS
  Fault-isolation test for the degraded modes in SLI_SLO_MATRIX §5.4.
  Starts tests/load/fault-isolation.js, stops the target service mid-run, starts it again, then
  verifies data invariants. Exit code != 0 if k6 gates or invariants fail.

.EXAMPLE
  ./scripts/run-fault-isolation.ps1 -Target notification-service
  ./scripts/run-fault-isolation.ps1 -Target scheduling-service
  ./scripts/run-fault-isolation.ps1 -Target identity-service -Users 40
#>
param(
    [ValidateSet("notification-service", "medical-records-service", "scheduling-service", "identity-service")]
    [string]$Target = "notification-service",
    [int]$DownAtSeconds = 60,
    [int]$UpAtSeconds = 150,
    [int]$TotalSeconds = 240,
    [int]$Users = 30,
    [string]$BaseUrl = "http://localhost:5000",
    [hashtable]$Env = @{}
)

# Docker and k6 write progress to stderr; success is judged by exit codes below, not by error records.
$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$compose = Join-Path $root "docker-compose.yml"
$runner = Join-Path $root "tests/load/run-k6.ps1"

$vars = @{
    FAULT_TARGET = $Target; FAULT_DOWN_AT_S = "$DownAtSeconds"; FAULT_UP_AT_S = "$UpAtSeconds"
    FAULT_TOTAL_S = "$TotalSeconds"; FAULT_USERS = "$Users"; TARGET_SYSTEM = "microservices"
    # Tokens must outlive the run when Identity is the fault target (no refresh during the outage).
    TOKEN_REFRESH_AFTER_S = "840"
} + $Env

Write-Host "[FAULT] target=$Target down@${DownAtSeconds}s up@${UpAtSeconds}s total=${TotalSeconds}s users=$Users" -ForegroundColor Cyan
$job = Start-Job -ScriptBlock {
    param($runner, $baseUrl, $vars)
    & $runner fault-isolation.js -BaseUrl $baseUrl -Env $vars
    "K6_EXIT=$LASTEXITCODE"
} -ArgumentList $runner, $BaseUrl, $vars

# The k6 setup phase (login + fixtures) takes a few seconds; the script measures elapsed time from
# the end of setup, so wait for the first scenario output before starting the fault clock.
$deadline = (Get-Date).AddMinutes(3)
while ((Get-Date) -lt $deadline) {
    $out = Receive-Job $job -Keep 2>&1 | Out-String
    if ($out -match "running \(|default \[|users \[") { break }
    if ($job.State -ne "Running") { break }
    Start-Sleep -Seconds 1
}
$clockStart = Get-Date

# docker writes progress to stderr; with ErrorActionPreference=Stop PowerShell would treat that as fatal
# and skip the restart, so compose calls run with Continue and are checked via exit code.
function Invoke-Compose([string]$Action) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { docker compose -f $compose $Action $Target 2>&1 | Out-Null }
    finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { Write-Host "[FAULT] docker compose $Action $Target exited $LASTEXITCODE" -ForegroundColor Red }
}

function Wait-Until([int]$seconds) {
    $remaining = $seconds - ((Get-Date) - $clockStart).TotalSeconds
    if ($remaining -gt 0) { Start-Sleep -Seconds ([math]::Ceiling($remaining)) }
}

try {
    Wait-Until $DownAtSeconds
    Write-Host "[FAULT] stopping $Target" -ForegroundColor Yellow
    Invoke-Compose "stop"
    Wait-Until $UpAtSeconds
}
finally {
    Write-Host "[FAULT] starting $Target" -ForegroundColor Yellow
    Invoke-Compose "start"
}

# k6 progress/log lines arrive as error records from the job; collect them as text instead of throwing.
$ErrorActionPreference = "Continue"
$output = Receive-Job $job -Wait -AutoRemoveJob 2>&1 | Out-String
Write-Host $output
$k6Exit = if ($output -match "K6_EXIT=(\d+)") { [int]$Matches[1] } else { 1 }

Write-Host "[FAULT] waiting 60s for outboxes/consumers to drain before invariant check" -ForegroundColor Cyan
Start-Sleep -Seconds 60
& (Join-Path $PSScriptRoot "check-slo-invariants.ps1") -OutputFile (Join-Path $root "tests/load/evidence/invariants-fault-$Target.json")
$invariantExit = $LASTEXITCODE

if ($k6Exit -ne 0 -or $invariantExit -ne 0) {
    Write-Host "[FAULT] FAILED (k6=$k6Exit, invariants=$invariantExit)" -ForegroundColor Red
    exit 1
}
Write-Host "[FAULT] degraded mode for $Target verified." -ForegroundColor Green
exit 0
