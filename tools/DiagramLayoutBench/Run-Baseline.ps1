#requires -Version 7
<#
Reproducible all-flags-off baseline for the incremental layout plan (BASE-1/3/4).

Each case runs in a fresh process: once with operation counters (diagnostic record) and -Repeat times
without them (timing). Timing runs must reproduce the diagnostic SVG, geometry and findings hashes, which also
checks that the observers do not change output. The Landgate import is optional: it needs in/Landgate.csv, which
is not committed. Only measurements and hashes are written to the summary; the SVGs and Landgate source stay in -WorkDir.

  pwsh tools/DiagramLayoutBench/Run-Baseline.ps1 [-Repeat 3] [-WorkDir out/baseline] [-Summary docs/bench/layout-baseline.json]
#>
param(
    [int] $Repeat = 3,
    [string] $WorkDir = 'out/baseline',
    [string] $Summary = 'docs/bench/layout-baseline.json',
    [switch] $SkipLandgate,
    [switch] $SkipBuild
)
$ErrorActionPreference = 'Stop'
$Root = (git rev-parse --show-toplevel).Trim()
Set-Location $Root
New-Item -ItemType Directory -Force $WorkDir | Out-Null
if (-not $SkipBuild) {
    dotnet build tools/DiagramLayoutBench/DiagramLayoutBench.csproj -c Release -m:1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'DiagramLayoutBench build failed' }
}
$Bench = Join-Path $Root 'tools/DiagramLayoutBench/bin/Release/net10.0/DiagramLayoutBench.dll'

$Cases = @(
    'single-layer-free', 'single-layer-fan', 'cross-layer-fan', 'nested-and-parallel', 'horizontal-chain',
    'single-node', 'small-chain', 'parallel-relationships', 'self-loops', 'nested-groups-cross-links',
    'hinted-cardinal', 'hinted-quadrant-and-pin', 'disconnected-components', 'row-constrained-lr-with-fan',
    'enterprise-complete') | ForEach-Object { @{ Id = $_; Selector = "@case:$_" } }

$Landgate = Join-Path $Root 'in/Landgate.csv'
if (-not $SkipLandgate -and (Test-Path $Landgate)) {
    $Export = Join-Path $WorkDir 'landgate-enterprise.mmd'
    $Catalogue = Get-ChildItem tools/LayoutCatalogueBench/bin/Release -Recurse -Filter LayoutCatalogueBench.dll -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $Catalogue) {
        dotnet build tools/LayoutCatalogueBench/LayoutCatalogueBench.csproj -c Release -m:1 | Out-Null
        $Catalogue = Get-ChildItem tools/LayoutCatalogueBench/bin/Release -Recurse -Filter LayoutCatalogueBench.dll | Select-Object -First 1
    }
    dotnet $Catalogue.FullName '@landgate-enterprise-source' $Export | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Landgate export failed' }
    $Cases += @{ Id = 'landgate-enterprise'; Selector = $Export }
}

function Invoke-Case($Selector, $Output, $Operations) {
    $env:DIAGRAM_BENCH_OPERATIONS = if ($Operations) { '1' } else { '0' }
    try { dotnet $Bench $Output $Selector | Out-Null } finally { Remove-Item Env:DIAGRAM_BENCH_OPERATIONS }
    if (-not (Test-Path $Output)) { throw "No record for $Selector" }
    Get-Content $Output | ForEach-Object { $_ | ConvertFrom-Json } | Select-Object -First 1
}

$Results = foreach ($Case in $Cases) {
    Write-Host "== $($Case.Id)"
    $Diagnostic = Invoke-Case $Case.Selector (Join-Path $WorkDir "$($Case.Id).diagnostic.jsonl") $true
    $Times = @()
    $Reproduced = $true
    for ($I = 1; $I -le $Repeat; $I++) {
        $Timing = Invoke-Case $Case.Selector (Join-Path $WorkDir "$($Case.Id).timing$I.jsonl") $false
        $Times += [math]::Round($Timing.Milliseconds, 1)
        if ($Timing.SvgHash -ne $Diagnostic.SvgHash -or $Timing.Model.GeometryHash -ne $Diagnostic.Model.GeometryHash `
            -or $Timing.Model.FindingsHash -ne $Diagnostic.Model.FindingsHash) { $Reproduced = $false }
    }
    $Sorted = $Times | Sort-Object
    [ordered]@{
        Id = $Case.Id
        SourceHash = $Diagnostic.SourceHash
        Failure = $Diagnostic.Failure
        RenderedWidthPx = $Diagnostic.RenderedWidthPx
        RenderedHeightPx = $Diagnostic.RenderedHeightPx
        Reference12PxTextAt1200PxFitWidth = [math]::Round($Diagnostic.Reference12PxTextAt1200PxFitWidth, 3)
        Nodes = $Diagnostic.Model.Nodes
        Routes = $Diagnostic.Model.Routes
        ExpectedRoutes = $Diagnostic.Model.ExpectedRoutes
        Bends = $Diagnostic.Model.Bends
        Crossings = $Diagnostic.Model.Crossings
        Overlaps = $Diagnostic.Model.Overlaps
        Disconnections = $Diagnostic.Model.Disconnections
        HardFindings = $Diagnostic.Model.HardFindings
        Findings = $Diagnostic.Model.Findings
        RouteRebuildInstances = $Diagnostic.RouteRebuildInstances
        WrapAlternatives = @($Diagnostic.Wraps).Count
        GeometryHash = $Diagnostic.Model.GeometryHash
        FindingsHash = $Diagnostic.Model.FindingsHash
        SvgHash = $Diagnostic.SvgHash
        SceneHash = $Diagnostic.Scene.SceneHash
        ColdProcessMilliseconds = $Times
        MedianMilliseconds = $Sorted[[int][math]::Floor(($Sorted.Count - 1) / 2)]
        HashesReproducedAcrossProcesses = $Reproduced
        Stages = $Diagnostic.Stages
    }
}

$Commit = (git rev-parse --short HEAD).Trim()
$Dirty = [bool](git status --porcelain -- src tools)
$Document = [ordered]@{
    Captured = (Get-Date -Format 'yyyy-MM-dd')
    Commit = $Commit
    SourceTreeDirty = $Dirty
    Options = 'DiagramLayoutOptions.Default (AF, AG and AH off); production cells on'
    Machine = "$([Environment]::ProcessorCount) logical processors, $([Runtime.InteropServices.RuntimeInformation]::OSDescription), .NET $([Environment]::Version)"
    Method = "Fresh process per run; one diagnostic run with operation counters plus $Repeat timing runs without them. Timings are observations, not claims."
    Cases = @($Results)
}
$Document | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $Summary
Write-Host "Wrote $Summary"
