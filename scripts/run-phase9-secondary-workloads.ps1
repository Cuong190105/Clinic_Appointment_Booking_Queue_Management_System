param(
    [string]$GatewayBaseUrl = "http://localhost:5000",
    [string]$SqlContainer = "clinic_sql_server",
    [string]$SqlPassword = $null,
    [string]$SeedPassword = $null,
    [string]$TestRunId = $null,
    [string]$OutputJson = "$env:TEMP\clinic_phase9_secondary_workloads.json"
)

$ErrorActionPreference = "Stop"
if (-not $SqlPassword) { $SqlPassword = $env:DB_SA_PASSWORD }
if (-not $SeedPassword) { $SeedPassword = $env:SEED_PASSWORD }
if (-not $SqlPassword) { throw "Missing mandatory environment variable DB_SA_PASSWORD or -SqlPassword." }
if (-not $SeedPassword) { throw "Missing mandatory environment variable SEED_PASSWORD or -SeedPassword." }
if (-not $TestRunId) { $TestRunId = "PH9-secondary-" + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() }
Add-Type -AssemblyName System.Net.Http
$httpClient = New-Object System.Net.Http.HttpClient

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        [object]$Body = $null,
        [hashtable]$ExtraHeaders = @{}
    )
    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::new($Method), "$GatewayBaseUrl$Path")
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $Token) }
    $request.Headers.Add("X-Correlation-ID", [Guid]::NewGuid().ToString())
    foreach ($key in $ExtraHeaders.Keys) { $request.Headers.Add($key, [string]$ExtraHeaders[$key]) }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 8 -Compress
        $request.Content = New-Object System.Net.Http.StringContent($json, [System.Text.Encoding]::UTF8, "application/json")
    }
    $response = $httpClient.SendAsync($request).GetAwaiter().GetResult()
    $parsed = $null
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ($content) {
        try { $parsed = $content | ConvertFrom-Json } catch { $parsed = $content }
    }
    return [PSCustomObject]@{ status = [int]$response.StatusCode; body = $parsed }
}

function Invoke-SqlScalar {
    param([string]$Database, [string]$Query)
    $raw = docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $SqlPassword -C -d $Database -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL query failed for ${Database}: $raw" }
    $value = @($raw | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -Last 1)
    if ($value.Count -eq 0) { throw "SQL scalar result missing for ${Database}: $raw" }
    return [int]$value[0].Trim()
}

Write-Host "[1/6] Seeding isolated secondary-workload dataset: $TestRunId"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\seed-phase9-workload-data.ps1" `
    -SqlContainer $SqlContainer -SqlPassword $SqlPassword -TestRunId $TestRunId | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Phase 9 seed failed." }
$configPath = Join-Path $env:TEMP "clinic_phase9_env.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$appointmentId = [string]$config.checkinAppointmentIds[0]
$doctorId = "a0000004-0000-0000-0000-000000000001"

Write-Host "[2/6] Authenticating admin1 and checking in the seeded appointment"
$login = Invoke-JsonApi -Method Post -Path "/auth/login" -Token "" -Body @{ username = "admin1"; password = $SeedPassword }
if ($login.status -ne 200) { throw "Admin login failed with HTTP $($login.status)." }
$token = if ($login.body.result.accessToken) { $login.body.result.accessToken } else { $login.body.accessToken }
if ([string]::IsNullOrWhiteSpace($token)) { throw "Admin login did not return an access token." }

$checkinHeaders = @{ "Idempotency-Key" = "$TestRunId-checkin-1" }
$checkinStartedAt = [DateTimeOffset]::UtcNow
$checkin1 = Invoke-JsonApi -Method Post -Path "/appointments/$appointmentId/check-in?isUrgent=false" -Token $token -ExtraHeaders $checkinHeaders
$checkin2 = Invoke-JsonApi -Method Post -Path "/appointments/$appointmentId/check-in?isUrgent=false" -Token $token -ExtraHeaders $checkinHeaders
if ($checkin1.status -notin @(200, 204)) { throw "Check-in failed with HTTP $($checkin1.status)." }
if ($checkin2.status -notin @(200, 204, 409)) { throw "Repeated check-in returned unexpected HTTP $($checkin2.status)." }
Start-Sleep -Seconds 3
$ticket = Invoke-JsonApi -Method Get -Path "/queue/appointment/$appointmentId" -Token $token
if ($ticket.status -ne 200 -or $null -eq $ticket.body.id) { throw "Queue ticket was not created after check-in." }
$checkinTicketLagMs = ([DateTimeOffset]::UtcNow - $checkinStartedAt).TotalMilliseconds
$eventLagsMs = @([double]$checkinTicketLagMs)
for ($index = 1; $index -lt @($config.checkinAppointmentIds).Count; $index++) {
    $additionalAppointmentId = [string]$config.checkinAppointmentIds[$index]
    $additionalStartedAt = [DateTimeOffset]::UtcNow
    $additionalHeaders = @{ "Idempotency-Key" = "$TestRunId-checkin-$($index + 1)" }
    $additionalCheckin = Invoke-JsonApi -Method Post -Path "/appointments/$additionalAppointmentId/check-in?isUrgent=false" -Token $token -ExtraHeaders $additionalHeaders
    if ($additionalCheckin.status -notin @(200, 204, 409)) { throw "Additional check-in $($index + 1) failed with HTTP $($additionalCheckin.status)." }
    $additionalTicket = $null
    for ($poll = 1; $poll -le 20; $poll++) {
        $additionalTicket = Invoke-JsonApi -Method Get -Path "/queue/appointment/$additionalAppointmentId" -Token $token
        if ($additionalTicket.status -eq 200 -and $null -ne $additionalTicket.body.id) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($additionalTicket.status -ne 200 -or $null -eq $additionalTicket.body.id) { throw "Queue ticket was not created for appointment $additionalAppointmentId." }
    $eventLagsMs += [double](([DateTimeOffset]::UtcNow - $additionalStartedAt).TotalMilliseconds)
}
$ticketId = [string]$ticket.body.id
$queueList = Invoke-JsonApi -Method Get -Path "/doctors/$doctorId/queue" -Token $token
$callNext = Invoke-JsonApi -Method Post -Path "/queue/call-next/$doctorId" -Token $token
if ($callNext.status -notin @(200, 409)) { throw "Call-next returned unexpected HTTP $($callNext.status)." }
$call = Invoke-JsonApi -Method Post -Path "/queue/$ticketId/call" -Token $token
$startExam = Invoke-JsonApi -Method Post -Path "/queue/$ticketId/start-exam" -Token $token
$completeExam = Invoke-JsonApi -Method Post -Path "/queue/$ticketId/complete-exam" -Token $token
if ($startExam.status -ne 204) { throw "Start-exam failed with HTTP $($startExam.status)." }
if ($completeExam.status -ne 204) { throw "Complete-exam failed with HTTP $($completeExam.status)." }
$finalTicket = Invoke-JsonApi -Method Get -Path "/queue/$ticketId" -Token $token
if ($finalTicket.status -ne 200 -or $finalTicket.body.status -ne "Completed") { throw "Queue ticket did not reach Completed." }

Write-Host "[3/6] Creating and finalizing one Medical Record"
$reportBody = @{
    appointmentId = $appointmentId
    symptoms = "Phase 9 synthetic symptom"
    diagnosis = "Phase 9 synthetic diagnosis"
    prescription = "Phase 9 synthetic prescription"
    notes = "[$TestRunId] runtime acceptance"
    isFinalize = $false
}
$draft = $null
for ($attempt = 1; $attempt -le 6; $attempt++) {
    $draft = Invoke-JsonApi -Method Post -Path "/medical-reports" -Token $token -Body $reportBody
    if ($draft.status -eq 200 -and $null -ne $draft.body.id) { break }
    if ($attempt -lt 6) { Start-Sleep -Seconds 2 }
}
if ($draft.status -ne 200 -or $null -eq $draft.body.id) {
    throw "Medical report draft failed with HTTP $($draft.status): $($draft.body | ConvertTo-Json -Compress)"
}
$reportId = [string]$draft.body.id
$finalize = Invoke-JsonApi -Method Post -Path "/medical-reports/$reportId/finalize" -Token $token
if ($finalize.status -ne 200) { throw "Medical report finalize failed with HTTP $($finalize.status)." }
$finalizeRepeat = Invoke-JsonApi -Method Post -Path "/medical-reports/$reportId/finalize" -Token $token
if ($finalizeRepeat.status -notin @(200, 400, 409)) { throw "Repeated finalize returned unexpected HTTP $($finalizeRepeat.status)." }
$reportByAppointment = Invoke-JsonApi -Method Get -Path "/medical-reports/appointment/$appointmentId" -Token $token
if ($reportByAppointment.status -ne 200 -or $reportByAppointment.body.status -ne "Finalized") { throw "Finalized report could not be read back." }

Write-Host "[4/6] Exercising notification read endpoints"
$notificationsBefore = Invoke-JsonApi -Method Get -Path "/notifications" -Token $token
$unreadBefore = Invoke-JsonApi -Method Get -Path "/notifications/unread-count" -Token $token
$readAll = Invoke-JsonApi -Method Post -Path "/notifications/read-all" -Token $token
if ($notificationsBefore.status -ne 200) { throw "Notification list failed with HTTP $($notificationsBefore.status)." }
if ($unreadBefore.status -ne 200) { throw "Unread-count failed with HTTP $($unreadBefore.status)." }
if ($readAll.status -notin @(200, 204)) { throw "Read-all failed with HTTP $($readAll.status)." }
$unreadAfter = Invoke-JsonApi -Method Get -Path "/notifications/unread-count" -Token $token
if ($unreadAfter.status -ne 200) { throw "Unread-count after read-all failed with HTTP $($unreadAfter.status)." }

Write-Host "[5/6] Reconciling database invariants"
$ticketCount = Invoke-SqlScalar -Database "ClinicQueueDB" -Query "SELECT COUNT(*) FROM QueueTickets WHERE AppointmentId = '$appointmentId';"
$duplicateTicketCount = Invoke-SqlScalar -Database "ClinicQueueDB" -Query "SELECT COUNT(*) FROM (SELECT AppointmentId FROM QueueTickets WHERE AppointmentId = '$appointmentId' GROUP BY AppointmentId HAVING COUNT(*) > 1) d;"
$reportCount = Invoke-SqlScalar -Database "ClinicMedicalRecordsDB" -Query "SELECT COUNT(*) FROM MedicalReports WHERE AppointmentId = '$appointmentId';"
$notificationCount = Invoke-SqlScalar -Database "ClinicNotificationDB" -Query "SELECT COUNT(*) FROM Notifications;"
if ($ticketCount -ne 1 -or $duplicateTicketCount -ne 0 -or $reportCount -ne 1) { throw "Database invariant failed: ticket=$ticketCount duplicateTickets=$duplicateTicketCount reports=$reportCount." }

$evidence = [ordered]@{
    metadata = [ordered]@{
        test = "phase9_secondary_workloads"
        testRunId = $TestRunId
        timestamp = [DateTime]::UtcNow.ToString("o")
        gateway = $GatewayBaseUrl
        appointmentId = $appointmentId
        ticketId = $ticketId
        reportId = $reportId
    }
    checkIn = [ordered]@{ firstStatus = $checkin1.status; repeatedStatus = $checkin2.status; ticketStatus = $finalTicket.body.status }
    queueOperations = [ordered]@{ queueListStatus = $queueList.status; callNextStatus = $callNext.status; callStatus = $call.status; startExamStatus = $startExam.status; completeExamStatus = $completeExam.status }
    medicalRecords = [ordered]@{ draftStatus = $draft.status; finalizeStatus = $finalize.status; repeatedFinalizeStatus = $finalizeRepeat.status; readBackStatus = $reportByAppointment.status; finalState = $reportByAppointment.body.status }
    notifications = [ordered]@{ listStatus = $notificationsBefore.status; unreadBeforeStatus = $unreadBefore.status; readAllStatus = $readAll.status; unreadAfterStatus = $unreadAfter.status; databaseNotificationCount = $notificationCount }
    eventTiming = [ordered]@{
        samples = $eventLagsMs.Count
        checkInToQueueTicketMs = [Math]::Round($checkinTicketLagMs, 2)
        checkInToQueueTicketP95Ms = [Math]::Round((($eventLagsMs | Sort-Object)[[Math]::Min($eventLagsMs.Count - 1, [Math]::Floor(0.95 * $eventLagsMs.Count))]), 2)
        checkInToQueueTicketWithinFiveSeconds = (($eventLagsMs | Measure-Object -Maximum).Maximum -le 5000)
    }
    invariants = [ordered]@{ queueTicketCount = $ticketCount; duplicateTicketCount = $duplicateTicketCount; medicalReportCount = $reportCount; passed = $true }
}
$evidence | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "[6/6] Evidence saved to $OutputJson"

& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\cleanup-phase9-workload-data.ps1" `
    -SqlContainer $SqlContainer -SqlPassword $SqlPassword -TestRunId $TestRunId | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Phase 9 secondary-workload cleanup failed." }
Write-Host "[SUCCESS] Secondary workload acceptance passed and cleanup reconciled."
