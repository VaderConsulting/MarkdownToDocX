param(
    [int]$BatchTimeoutSeconds = 59
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'MermaidRecognition.RuleValidation.Tests.csproj'
$results = Join-Path $PSScriptRoot 'TestResults\RouteRuleAudit'
New-Item -ItemType Directory -Path $results -Force | Out-Null

$testList = & dotnet test $project --no-build --no-restore --list-tests --filter 'FullyQualifiedName~RouteRuleCatalogueAuditTests' 2>&1
if ($LASTEXITCODE -ne 0) { throw "Could not enumerate route-audit scenarios (dotnet exit $LASTEXITCODE)." }
$expected = @($testList | ForEach-Object {
    if ($_ -match '^\s+RouteRuleAudit_(\S+)\s*$') { $Matches[1] }
} | Sort-Object -Unique)
if ($expected.Count -eq 0) { throw 'The route-audit test list contained no scenario cases.' }

$rows = @{}
$cleanup = @{}
function Get-MapValue($Map, [string]$Key, $Default = 0) {
    if ($Map.ContainsKey($Key)) { return $Map[$Key] }
    return $Default
}
$failedBatches = [System.Collections.Generic.List[string]]::new()
$totalWatch = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($prefix in @('F', 'A', 'EIP')) {
    $stdout = Join-Path $results "$prefix.stdout.log"
    $stderr = Join-Path $results "$prefix.stderr.log"
    $filter = "FullyQualifiedName~RouteRuleAudit_$prefix"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath 'dotnet' -PassThru -WindowStyle Hidden `
        -ArgumentList @('test', $project, '--no-build', '--no-restore', '--filter', $filter,
            '--logger', 'console;verbosity=normal') `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $progressWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $lastSummary = ''
    while (-not $process.WaitForExit(1000)) {
        # The limit detects a stalled scenario, rather than cancelling a busy
        # catalogue just because several legal searches add up to that limit.
        $latest = Get-Content -LiteralPath $stdout -Tail 100 | Where-Object { $_ -match '^\s*ROUTE_AUDIT\|' } | Select-Object -Last 1
        if ($latest -and $latest -ne $lastSummary) {
            $lastSummary = $latest
            $progressWatch.Restart()
        }
        if ($progressWatch.Elapsed.TotalSeconds -ge $BatchTimeoutSeconds) {
            $process.Kill($true)
            $failedBatches.Add("$prefix-timeout")
            Write-Output "BATCH_TIMEOUT|$prefix|noSummaryForSeconds=$BatchTimeoutSeconds"
            $process.WaitForExit(1000) | Out-Null
            break
        }
    }
    $watch.Stop()
    if ($process.ExitCode -ne 0) { $failedBatches.Add($prefix) }
    $output = @(Get-Content -LiteralPath $stdout) + @(Get-Content -LiteralPath $stderr)
    foreach ($line in $output) {
        if ($line -match '^\s*ROUTE_AUDIT\|[^|]+\|(?<id>[^|]+)\|(?<fields>.*)$') {
            $id = $Matches['id']
            $values = @{}
            foreach ($field in ($Matches['fields'] -split '[,|]')) {
                if ($field -match '^(?<name>[^=]+)=(?<value>-?\d+)$') {
                    $values[$Matches['name']] = [int]$Matches['value']
                } elseif ($field -match '^status=(?<status>[^|,]+)$') {
                    $values['status'] = $Matches['status']
                }
            }
            if (-not $rows.ContainsKey($id)) { $rows[$id] = $values }
        }
        if ($line -match '^\s*ORTHOGONAL_CLEANUP\|(?<id>[^|]+)\|count=(?<count>\d+)') {
            $cleanup[$Matches['id']] = [int]$Matches['count']
        }
    }
    Write-Output ("BATCH|{0}|{1:0.00}s|dotnetExit={2}|summaries={3}" -f $prefix, $watch.Elapsed.TotalSeconds, $process.ExitCode, $rows.Count)
}
$totalWatch.Stop()

$missing = @($expected | Where-Object { -not $rows.ContainsKey($_) })
$unexpected = @($rows.Keys | Where-Object { $_ -notin $expected })
if ($missing.Count -or $unexpected.Count) {
    Write-Output "INCOMPLETE|expected=$($expected.Count)|summaries=$($rows.Count)|missing=[$($missing -join ',')]|unexpected=[$($unexpected -join ',')]. Missing scenarios cannot count as zero."
}

$totals = [ordered]@{
    scenarios = $rows.Count
    unmeasured = $missing.Count
    offSlotPorts = 0
    offGridNodes = 0
    offGridFrameEdges = 0
    titleBands = 0
    frameOverlaps = 0
    missingRoutes = 0
    noLegalRouteFailures = 0
    illegalPaths = 0
    sharedTracks = 0
    rule4_2 = 0
    rule5_9 = 0
    rule3_1 = 0
    rule4_1 = 0
    cleanupCorrections = 0
}
foreach ($id in $rows.Keys) {
    $row = $rows[$id]
    if ($row.ContainsKey('status')) {
        $totals.unmeasured++
        continue
    }
    $totals.offSlotPorts += Get-MapValue $row 'offSlotPorts'
    $totals.offGridNodes += Get-MapValue $row 'offGridNodes'
    $totals.offGridFrameEdges += Get-MapValue $row 'offGridFrameEdges'
    $totals.titleBands += Get-MapValue $row 'titleBand'
    $totals.frameOverlaps += Get-MapValue $row 'frameOverlaps'
    $totals.missingRoutes += Get-MapValue $row 'missing'
    $totals.noLegalRouteFailures += Get-MapValue $row 'noLegalRoute'
    $totals.illegalPaths += Get-MapValue $row '10'
    $totals.sharedTracks += Get-MapValue $row '5.1'
    $totals.rule4_2 += Get-MapValue $row '4.2'
    $totals.rule5_9 += Get-MapValue $row '5.9'
    $totals.rule3_1 += Get-MapValue $row '3.1'
    $totals.rule4_1 += Get-MapValue $row '4.1'
    $totals.cleanupCorrections += Get-MapValue $cleanup $id
}
$auditState = if ($missing.Count -or $unexpected.Count) { 'ROUTE_AUDIT_INCOMPLETE' } else { 'ROUTE_AUDIT_COMPLETE' }
Write-Output ("{0}|{1:0.00}s|{2}" -f $auditState, $totalWatch.Elapsed.TotalSeconds,
    (($totals.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '|'))
Write-Output ("FAILED_BATCHES|{0}" -f ($failedBatches -join ','))

if ($missing.Count -or $unexpected.Count -or $totals.unmeasured -or $failedBatches.Count -or $totals.offSlotPorts -or $totals.offGridNodes -or $totals.offGridFrameEdges -or
    $totals.titleBands -or $totals.frameOverlaps -or $totals.missingRoutes -or $totals.noLegalRouteFailures -or $totals.cleanupCorrections -or $totals.illegalPaths -or $totals.sharedTracks) {
    Write-Error 'One or more audited invariants remain nonzero; see per-scenario ROUTE_AUDIT rows.'
    exit 1
}
