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
$hintAccounting = @{}
$failedScenarios = [System.Collections.Generic.HashSet[string]]::new()
$caseTimings = [System.Collections.Generic.List[object]]::new()
function Get-MapValue($Map, [string]$Key, $Default = 0) {
    if ($Map.ContainsKey($Key)) { return $Map[$Key] }
    return $Default
}
$failedBatches = [System.Collections.Generic.List[string]]::new()
$totalWatch = [System.Diagnostics.Stopwatch]::StartNew()
# The progress watchdog already detects a stalled scenario. Run the complete
# catalogue in one host, as the pinned comparison did, instead of paying for
# three process starts, recognizer warm-ups and JIT compilations.
foreach ($prefix in @('Catalogue')) {
    $stdout = Join-Path $results "$prefix.stdout.log"
    $stderr = Join-Path $results "$prefix.stderr.log"
    $filter = 'FullyQualifiedName~RouteRuleCatalogueAuditTests'
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath 'dotnet' -PassThru -WindowStyle Hidden `
        -ArgumentList @('test', $project, '--no-build', '--no-restore', '--filter', $filter,
            '--logger', 'console;verbosity=normal', '--logger', "trx;LogFileName=$prefix.trx",
            '--results-directory', $results) `
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
    $trxPath = Join-Path $results "$prefix.trx"
    if (Test-Path -LiteralPath $trxPath) {
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
        foreach ($result in $trx.TestRun.Results.UnitTestResult) {
            if ($result.testName -notmatch '^RouteRuleAudit_(?<id>\S+)$') { continue }
            $scenarioId = $Matches['id']
            if ($result.outcome -ne 'Passed') { $failedScenarios.Add($scenarioId) | Out-Null }
            $caseTimings.Add([pscustomobject]@{ Scenario = $scenarioId
                Seconds = [TimeSpan]::Parse($result.duration).TotalSeconds; Outcome = $result.outcome })
        }
    }
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
        if ($line -match '^\s*HINT_AUDIT\|(?<id>[^|]+)\|.*\|unreported=(?<count>-?\d+)') {
            $hintAccounting[$Matches['id']] = [Math]::Abs([int]$Matches['count'])
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
    offSlotGroups = 0
    portFindings = 0
    gridFindings = 0
    alignedPortJogs = 0
    nonalignedPortJogs = 0
    hintAccountingFailures = 0
    failedScenarios = $failedScenarios.Count
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
    rule3_2_4_7 = 0
    rule3_3_5_2 = 0
    rule4_9 = 0
    rule4_11 = 0
    rule5_8 = 0
    cleanupCorrections = 0
}
foreach ($id in $rows.Keys) {
    $row = $rows[$id]
    if ($row.ContainsKey('status')) {
        $totals.unmeasured++
        continue
    }
    $totals.offSlotPorts += Get-MapValue $row 'offSlotPorts'
    $totals.offSlotGroups += Get-MapValue $row 'offSlotGroups'
    $totals.portFindings += Get-MapValue $row 'port'
    $totals.gridFindings += Get-MapValue $row 'grid'
    $totals.alignedPortJogs += Get-MapValue $row 'alignedPortJogs'
    $totals.nonalignedPortJogs += Get-MapValue $row 'nonalignedPortJogs'
    $totals.hintAccountingFailures += Get-MapValue $hintAccounting $id
    $totals.offGridNodes += Get-MapValue $row 'offGridNodes'
    $totals.offGridFrameEdges += Get-MapValue $row 'offGridFrameEdges'
    $totals.titleBands += Get-MapValue $row 'titleBand'
    $totals.frameOverlaps += Get-MapValue $row 'frameOverlaps'
    $totals.missingRoutes += Get-MapValue $row 'missing'
    $totals.noLegalRouteFailures += Get-MapValue $row 'noLegalRoute'
    $totals.illegalPaths += Get-MapValue $row '10'
    $totals.sharedTracks += Get-MapValue $row '5.1'
    # Surface every checker rule, including future additions. A summary must
    # never silently omit a rule just because it lacks a hand-written counter.
    foreach ($rule in $row.Keys | Where-Object { $_ -match '^\d+(?:[./]\d+)*$' }) {
        $field = 'rule' + ($rule -replace '[./]', '_')
        if (-not $totals.Contains($field)) { $totals[$field] = 0 }
        $totals[$field] += $row[$rule]
    }
    $totals.cleanupCorrections += Get-MapValue $cleanup $id
}
$auditState = if ($missing.Count -or $unexpected.Count) { 'ROUTE_AUDIT_INCOMPLETE' } else { 'ROUTE_AUDIT_COMPLETE' }
Write-Output ("{0}|{1:0.00}s|{2}" -f $auditState, $totalWatch.Elapsed.TotalSeconds,
    (($totals.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '|'))
Write-Output ("FAILED_BATCHES|{0}" -f ($failedBatches -join ','))
Write-Output ("FAILED_SCENARIOS|{0}" -f (($failedScenarios | Sort-Object) -join ','))
$caseTimings | Sort-Object Scenario | Export-Csv -LiteralPath (Join-Path $results 'Catalogue.timings.csv') -NoTypeInformation
foreach ($timing in $caseTimings | Where-Object Seconds -gt 10 | Sort-Object Seconds -Descending) {
    Write-Output ("SLOW_CASE|{0}|{1:0.0000}s|{2}" -f $timing.Scenario, $timing.Seconds, $timing.Outcome)
}

if ($missing.Count -or $unexpected.Count -or $totals.unmeasured -or $failedBatches.Count -or $totals.offSlotPorts -or $totals.offSlotGroups -or $totals.gridFindings -or $totals.hintAccountingFailures -or $totals.offGridNodes -or $totals.offGridFrameEdges -or
    $totals.titleBands -or $totals.frameOverlaps -or $totals.missingRoutes -or $totals.noLegalRouteFailures -or $totals.cleanupCorrections -or $totals.illegalPaths -or $totals.sharedTracks) {
    Write-Error 'One or more audited invariants remain nonzero; see per-scenario ROUTE_AUDIT rows.'
    exit 1
}
