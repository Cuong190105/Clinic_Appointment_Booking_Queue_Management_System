# PowerShell script to pre-fetch test JWT tokens for k6 load runner via Identity Service
param(
    [string]$IdentityUrl = $null,
    [string]$OutputFile = $null,
    [string]$Password = $null
)

if (-not $IdentityUrl) {
    if ($env:IDENTITY_URL) { $IdentityUrl = $env:IDENTITY_URL } else { $IdentityUrl = "http://localhost:5001" }
}
if (-not $OutputFile) {
    if ($env:TOKEN_OUTPUT_PATH) { $OutputFile = $env:TOKEN_OUTPUT_PATH } else { $OutputFile = Join-Path $env:TEMP "clinic_phase9_tokens.json" }
}
if (-not $Password) {
    if ($env:SEED_PASSWORD) {
        $Password = $env:SEED_PASSWORD
    } else {
        throw "Missing mandatory environment variable SEED_PASSWORD or -Password parameter."
    }
}

Write-Host "Fetching Phase 9 JWT tokens directly from Identity Service ($IdentityUrl/v1/auth/login)..." -ForegroundColor Cyan

$data = @{
    patients = @()
    doctors = @()
    receptionists = @()
    admins = @()
}

function Get-Token($username) {
    try {
        $body = @{ username = $username; password = $Password } | ConvertTo-Json -Compress
        $res = Invoke-RestMethod -Uri "$IdentityUrl/v1/auth/login" -Method Post -ContentType "application/json" -Body $body
        return $res.accessToken
    } catch {
        Write-Warning "Failed to log in ${username}: $($_.Exception.Message)"
        return $null
    }
}

# 1. Patients (1..15)
1..15 | ForEach-Object {
    $u = "patient$_"
    $t = Get-Token $u
    if ($t) { $data.patients += @{ username = $u; token = $t } }
}

# 2. Doctors (1..2)
1..2 | ForEach-Object {
    $u = "doctor$_"
    $t = Get-Token $u
    if ($t) { $data.doctors += @{ username = $u; token = $t } }
}

# 3. Receptionists (1..2)
1..2 | ForEach-Object {
    $u = "receptionist$_"
    $t = Get-Token $u
    if ($t) { $data.receptionists += @{ username = $u; token = $t } }
}

# 4. Admin (admin1)
$t = Get-Token "admin1"
if ($t) { $data.admins += @{ username = "admin1"; token = $t } }

$targetDir = [System.IO.Path]::GetDirectoryName($OutputFile)
if ($targetDir -and !(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$json = $data | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($OutputFile, $json)

Write-Host "Successfully generated tokens file: $($data.patients.Count) patients, $($data.doctors.Count) doctors, $($data.receptionists.Count) receptionists, $($data.admins.Count) admins." -ForegroundColor Green
