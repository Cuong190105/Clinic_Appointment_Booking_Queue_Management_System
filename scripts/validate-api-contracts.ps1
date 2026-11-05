<#
.SYNOPSIS
    Automated API Contract and Drift Validation Runner (Phase 7 - Member A).
.DESCRIPTION
    Executes ApiContractDriftTests against the API Gateway and microservice OpenAPI specifications,
    ensuring no breaking route changes, schema regressions, or downstream spec unavailability occur.
    Returns non-zero exit code if contract validation or drift detection fails.
    By default, baseline snapshots in tasks/contracts/ are strictly read-only.
    Pass -UpdateSnapshots only when intentionally regenerating reviewed contract snapshots.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$Detailed = $false,
    [switch]$UpdateSnapshots = $false,
    [string]$Filter = "FullyQualifiedName~ApiContractDriftTests"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [Phase 7] Running API Contract & Drift Validation Tests   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$verbosity = if ($Detailed) { "detailed" } else { "normal" }
$projectPath = "backend/Clinic.IntegrationTests/Clinic.IntegrationTests.csproj"

$previousEnv = $env:UPDATE_CONTRACT_SNAPSHOTS
try {
    $env:UPDATE_CONTRACT_SNAPSHOTS = if ($UpdateSnapshots) { "true" } else { "false" }
    if ($UpdateSnapshots) {
        Write-Host ">>> [MODE] Intentional Snapshot Update enabled (UPDATE_CONTRACT_SNAPSHOTS=true)" -ForegroundColor Magenta
    }

    $testArgs = @(
        "test",
        $projectPath,
        "-c", $Configuration,
        "--disable-build-servers",
        "-p:UseSharedCompilation=false",
        "-p:UseRazorBuildServer=false",
        "--filter", $Filter,
        "--logger", "console;verbosity=$verbosity"
    )

    Write-Host "Executing: dotnet $($testArgs -join ' ')" -ForegroundColor Yellow
    & dotnet @testArgs
    $exitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } else { 1 }

    if ($exitCode -eq 0) {
        Write-Host "`n>>> [SUCCESS] All API contract and drift validation checks passed." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n>>> [FAILURE] API contract drift or specification validation failed with exit code $exitCode." -ForegroundColor Red
        exit $exitCode
    }
}
finally {
    $env:UPDATE_CONTRACT_SNAPSHOTS = $previousEnv
}
