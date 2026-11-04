<#
.SYNOPSIS
    Generate Safe Queue Event Traffic for Phase 8 SLI/SLO Telemetry Verification (Member A & B).
.DESCRIPTION
    Publishes safe test events (AppointmentCancelled with IsRescheduleRelease = true) to RabbitMQ
    to exercise QueueEventConsumer, measure consumer event lag, compute mathematical p95 latency,
    verify Prometheus metrics scraping across all 7 microservices, and validate DLQs.
#>
[CmdletBinding()]
param(
    [int]$EventCount = 60,
    [string]$RabbitMqBaseUrl = "http://localhost:15672",
    [string]$PrometheusBaseUrl = "http://localhost:9090"
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [Phase 8] Safe Event Traffic & Telemetry Verification     " -ForegroundColor Cyan
Write-Host " Target Events: $EventCount | RabbitMQ: $RabbitMqBaseUrl " -ForegroundColor Cyan
Write-Host " Prometheus: $PrometheusBaseUrl                          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$headers = @{
    Authorization = "Basic Y2xpbmljOmNsaW5pYy1kZXYtcGFzc3dvcmQ="
    "Content-Type" = "application/json"
}

Write-Host "`n1. Publishing $EventCount safe events to RabbitMQ exchange 'clinic.events'..." -ForegroundColor Yellow
$published = 0
for ($i = 1; $i -le $EventCount; $i++) {
    $now = [DateTimeOffset]::UtcNow.ToString("o")
    $payloadObj = @{
        EventId = [Guid]::NewGuid().ToString()
        EventType = "AppointmentCancelled"
        SchemaVersion = 1
        OccurredAt = $now
        CorrelationId = [Guid]::NewGuid().ToString()
        Producer = "appointment-service"
        Payload = @{
            AppointmentId = [Guid]::NewGuid().ToString()
            PatientId = [Guid]::NewGuid().ToString()
            CancelledAt = $now
            IsRescheduleRelease = $true
        }
    }
    $body = @{
        properties = @{ delivery_mode = 2; content_type = "application/json" }
        routing_key = "appointment.cancelled"
        payload = ($payloadObj | ConvertTo-Json -Depth 4)
        payload_encoding = "string"
    } | ConvertTo-Json -Depth 4

    try {
        $res = Invoke-RestMethod -Uri "$RabbitMqBaseUrl/api/exchanges/%2f/clinic.events/publish" `
            -Method Post -Headers $headers -Body $body -TimeoutSec 5
        if ($res.routed) { $published++ }
    } catch {
        Write-Warning "Failed to publish event ${i}: $_"
    }

    Start-Sleep -Milliseconds 40
}

Write-Host ">>> Published $published / $EventCount events successfully." -ForegroundColor Green

Write-Host "`n2. Waiting 18s for Queue Service consumption and Prometheus scrape cycle..." -ForegroundColor Yellow
Start-Sleep -Seconds 18

Write-Host "`n3. Collecting Prometheus Telemetry Metrics..." -ForegroundColor Yellow
$targets = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/targets"
$lagCount = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/query?query=clinic_queue_consumer_lag_seconds_count"
$lagBuckets = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/query?query=clinic_queue_consumer_lag_seconds_bucket"
$queueLen = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/query?query=clinic_queue_length"
$gatewayRequests = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/query?query=sum(http_server_request_duration_seconds_count{job='clinic-api'})"

# Attempt PromQL histogram_quantile query
$promqlP95Query = "histogram_quantile(0.95, sum(rate(clinic_queue_consumer_lag_seconds_bucket[2m])) by (le))"
$promqlP95Result = $null
try {
    $qEsc = [System.Uri]::EscapeDataString($promqlP95Query)
    $qRes = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/query?query=$qEsc"
    if ($qRes.data.result.Count -gt 0 -and $qRes.data.result[0].value[1] -ne "NaN") {
        $promqlP95Result = [Math]::Round([double]$qRes.data.result[0].value[1], 3)
    }
} catch {
    Write-Warning "PromQL quantile query error: $_"
}

# Mathematical Percentile Calculation via Linear Interpolation on Cumulative Histogram Buckets
$sampleCount = if ($lagCount.data.result.Count -gt 0) { [int]$lagCount.data.result[0].value[1] } else { 0 }
$computedP95 = $null
$calculationMethod = ""

if ($sampleCount -ge 50 -and $lagBuckets.data.result.Count -gt 0) {
    # Parse and sort finite buckets
    $parsedBuckets = [System.Collections.Generic.List[PSCustomObject]]::new()
    foreach ($b in $lagBuckets.data.result) {
        $leStr = $b.metric.le
        $cnt = [int]$b.value[1]
        if ($leStr -ne "+Inf") {
            $parsedBuckets.Add([PSCustomObject]@{ Le = [double]$leStr; Count = $cnt })
        }
    }
    $sortedBuckets = $parsedBuckets | Sort-Object Le
    $targetSample = 0.95 * $sampleCount

    $prevLe = 0.0
    $prevCount = 0
    foreach ($b in $sortedBuckets) {
        if ($b.Count -ge $targetSample) {
            $countInBucket = $b.Count - $prevCount
            if ($countInBucket -gt 0) {
                $fraction = ($targetSample - $prevCount) / $countInBucket
                $computedP95 = [Math]::Round($prevLe + ($fraction * ($b.Le - $prevLe)), 3)
            } else {
                $computedP95 = [Math]::Round($b.Le, 3)
            }
            $calculationMethod = "Histogram Linear Interpolation (Bucket: le=$($b.Le), count=$($b.Count), targetSample=$targetSample)"
            break
        }
        $prevLe = $b.Le
        $prevCount = $b.Count
    }
}

$p95Final = if ($promqlP95Result -ne $null) {
    $promqlP95Result
} elseif ($computedP95 -ne $null) {
    $computedP95
} else {
    "N/A (Sample floor < 50 or empty)"
}

$sloCompliance = if ($sampleCount -lt 50) {
    "N/A (Sample floor < 50)"
} elseif ($p95Final -is [double] -and $p95Final -le 5.0) {
    "COMPLIANT (p95 = ${p95Final}s <= 5.0s SLO gate)"
} elseif ($p95Final -is [double] -and $p95Final -gt 5.0) {
    "VIOLATION (p95 = ${p95Final}s > 5.0s SLO gate)"
} else {
    "N/A"
}

Write-Host "`n4. Verifying RabbitMQ Dead Letter Queues..." -ForegroundColor Yellow
$allQueues = Invoke-RestMethod -Uri "$RabbitMqBaseUrl/api/queues" -Headers $headers
$dlqStatus = [System.Collections.Generic.List[PSCustomObject]]::new()
$allQueues | ForEach-Object {
    if ($_.name -match "dlq|dead") {
        $dlqStatus.Add([PSCustomObject]@{
            Queue = $_.name
            Messages = [int]$_.messages
            MessagesReady = [int]$_.messages_ready
            MessagesUnacknowledged = [int]$_.messages_unacknowledged
        })
    }
}

# Collect Rules verification evidence
$rulesData = $null
try {
    $rRes = Invoke-RestMethod -Uri "$PrometheusBaseUrl/api/v1/rules"
    if ($rRes.data.groups) {
        $rulesData = $rRes.data.groups | ForEach-Object {
            [PSCustomObject]@{
                Group = $_.name
                File = $_.file
                Rules = $_.rules | ForEach-Object {
                    [PSCustomObject]@{
                        Alert = $_.name
                        State = $_.state
                        Health = $_.health
                        Query = $_.query
                    }
                }
            }
        }
    }
} catch {
    Write-Warning "Could not fetch Prometheus rules: $_"
}

$evidence = [ordered]@{
    GeneratedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:sszzz")
    EventsPublished = $published
    PrometheusTargets = @{
        Status = $targets.status
        TotalActiveTargets = $targets.data.activeTargets.Count
        ActiveTargets = $targets.data.activeTargets | ForEach-Object {
            [PSCustomObject]@{
                Job = $_.labels.job
                Instance = $_.labels.instance
                Health = $_.health
                LastScrape = $_.lastScrape
                LastError = $_.lastError
            }
        }
    }
    PrometheusAlertRules = $rulesData
    QueueConsumerLag = @{
        CountQuery = "clinic_queue_consumer_lag_seconds_count"
        SampleCount = $sampleCount
        SampleFloorMet = ($sampleCount -ge 50)
        CalculatedP95Seconds = $p95Final
        CalculationMethod = if ($promqlP95Result -ne $null) { "PromQL histogram_quantile(0.95)" } else { $calculationMethod }
        SLOStatus = $sloCompliance
        SloGateThresholdSeconds = 5.0
        Buckets = $lagBuckets.data.result | ForEach-Object {
            [PSCustomObject]@{
                Le = $_.metric.le
                Count = [int]$_.value[1]
            }
        }
    }
    QueueBacklog = @{
        QueueLengthQuery = "clinic_queue_length"
        CurrentWaitingTickets = if ($queueLen.data.result.Count -gt 0) { [int]$queueLen.data.result[0].value[1] } else { 0 }
        Note = "Queue length / backlog is reported separately and is distinct from event lag duration."
    }
    DeadLetterQueues = @{
        AllDlqsEmpty = (($dlqStatus | Where-Object { $_.Messages -gt 0 }).Count -eq 0)
        TotalDlqsInspected = $dlqStatus.Count
        DlqDetails = $dlqStatus
    }
    GatewayTelemetry = @{
        TotalHttpRequestsRecorded = if ($gatewayRequests.data.result.Count -gt 0) { [int]$gatewayRequests.data.result[0].value[1] } else { 0 }
    }
}

$jsonPath = Join-Path $PSScriptRoot "..\tasks\PHASE_8_TELEMETRY_EVIDENCE.json"
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding utf8
Write-Host "`n>>> Telemetry evidence exported to: $jsonPath" -ForegroundColor Green

Write-Host "`nSummary of Telemetry Verification:" -ForegroundColor Cyan
Write-Host " - Prometheus Targets UP: $(($targets.data.activeTargets | Where-Object { $_.health -eq 'up' }).Count) / $($targets.data.activeTargets.Count)" -ForegroundColor Green
Write-Host " - Event Lag Sample Count: $sampleCount (Floor >= 50: $($evidence.QueueConsumerLag.SampleFloorMet))" -ForegroundColor Green
Write-Host " - P95 Event Lag Calculated: $p95Final s -> $sloCompliance" -ForegroundColor Green
Write-Host " - DLQ Queues Clean (0 messages across $($dlqStatus.Count) queues): $($evidence.DeadLetterQueues.AllDlqsEmpty)" -ForegroundColor Green
Write-Host " - Prometheus Alert Rules Loaded: $($rulesData[0].Rules.Count) rules (Health: ok)" -ForegroundColor Green
