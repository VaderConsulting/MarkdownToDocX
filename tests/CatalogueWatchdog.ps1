# Shared process supervision for catalogue audits and named fixture invocations.
# Modern .NET cannot safely abort a synchronous engine thread; enforce the
# deadline at the process boundary rather than pretending cooperative cancellation
# stops a search that does not accept a cancellation token.
function Invoke-CatalogueWatchdog {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Filter,
        [Parameter(Mandatory)][string]$Configuration,
        [Parameter(Mandatory)][string]$Results,
        [Parameter(Mandatory)][string]$Stem,
        [int]$NormalTimeoutSeconds = 0
    )
    $policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CatalogueScenarioPolicy.json') -Raw | ConvertFrom-Json
    if ($NormalTimeoutSeconds -eq 0) { $NormalTimeoutSeconds = [int]$policy.normal_watchdog_seconds }
    $stress = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($id in $policy.stress_scenarios) { $stress.Add($id) | Out-Null }
    New-Item -ItemType Directory -Path $Results -Force | Out-Null
    $stdout = Join-Path $Results "$Stem.stdout.log"
    $stderr = Join-Path $Results "$Stem.stderr.log"
    $progress = Join-Path $Results "$Stem.watchdog.json"
    $trx = Join-Path $Results "$Stem.trx"
    # A killed test host may not write a TRX. Never let a previous invocation's
    # result masquerade as the current run's completed measurement.
    if (Test-Path -LiteralPath $trx) { Remove-Item -LiteralPath $trx -Force }
    [IO.File]::WriteAllText($progress, '')
    $previousProgress = $env:CATALOGUE_WATCHDOG_PROGRESS
    $env:CATALOGUE_WATCHDOG_PROGRESS = $progress
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $betweenCases = [Diagnostics.Stopwatch]::StartNew()
    $lastRecord = ''
    $active = $null
    $timeoutScenario = $null
    $timeoutSeconds = 0
    try {
        $process = Start-Process -FilePath 'dotnet' -PassThru -WindowStyle Hidden `
            -ArgumentList @('test', $Project, '--configuration', $Configuration, '--no-build', '--no-restore', '--filter', $Filter,
                '--logger', 'console;verbosity=normal', '--logger', "trx;LogFileName=$Stem.trx", '--results-directory', $Results) `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        while (-not $process.WaitForExit(1000)) {
            # Let the test action publish the next deadline while this reader
            # observes it; a read lock must not fail the test's progress write.
            $stream = [IO.FileStream]::new($progress, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            $reader = [IO.StreamReader]::new($stream)
            try { $record = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
            if ($record -and $record -cne $lastRecord) {
                try {
                    $parsed = $record | ConvertFrom-Json -ErrorAction Stop
                    $lastRecord = $record
                    if ($parsed.Completed) { $active = $null; $betweenCases.Restart() }
                    else { $active = $parsed }
                } catch {
                    # The writer may be replacing this small record. A partial
                    # read neither resets a deadline nor extends an allowance.
                }
            }
            $limit = $NormalTimeoutSeconds
            $elapsed = $betweenCases.Elapsed.TotalSeconds
            if ($null -ne $active) {
                if ($stress.Contains([string]$active.Scenario)) { $limit = [int]$policy.stress_watchdog_seconds }
                $elapsed = ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() - [long]$active.UnixMilliseconds) / 1000.0
            }
            if ($elapsed -ge $limit) {
                $timeoutScenario = if ($null -ne $active) { [string]$active.Scenario } else { 'startup-or-between-cases' }
                $timeoutSeconds = $limit
                $process.Kill($true)
                $process.WaitForExit(1000) | Out-Null
                break
            }
        }
        $watch.Stop()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Seconds = $watch.Elapsed.TotalSeconds
            TimeoutScenario = $timeoutScenario; TimeoutSeconds = $timeoutSeconds; Stdout = $stdout; Stderr = $stderr }
    } finally {
        $env:CATALOGUE_WATCHDOG_PROGRESS = $previousProgress
        if ($null -ne $process -and -not $process.HasExited) { $process.Kill($true) }
    }
}
