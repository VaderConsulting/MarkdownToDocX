param(
    [string]$Baseline = "docs/bench/catalogue-baseline.jsonl",
    [string]$Candidate = "docs/bench/catalogue-verify.jsonl"
)

$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Read-BenchMap([string]$Path) {
    $Map = @{}
    Get-Content (Join-Path $Root $Path) | ForEach-Object {
        $Row = $_ | ConvertFrom-Json
        $Map[$Row.Id] = $Row
    }
    return $Map
}

$Base = Read-BenchMap $Baseline
$Cand = Read-BenchMap $Candidate
$Mismatch = @()
$Missing = @()
foreach ($Id in $Base.Keys) {
    if (-not $Cand.ContainsKey($Id)) {
        $Missing += $Id
        continue
    }
    if ($Base[$Id].SvgSha256 -ne $Cand[$Id].SvgSha256) {
        $Mismatch += [pscustomobject]@{ Id = $Id; Baseline = $Base[$Id].SvgSha256; Candidate = $Cand[$Id].SvgSha256 }
    }
}
$Extra = $Cand.Keys | Where-Object { -not $Base.ContainsKey($_) }
Write-Host "Baseline diagrams: $($Base.Count)"
Write-Host "Candidate diagrams: $($Cand.Count)"
if ($Missing.Count -gt 0) { Write-Host "Missing in candidate: $($Missing -join ', ')" }
if ($Extra.Count -gt 0) { Write-Host "Extra in candidate: $($Extra -join ', ')" }
if ($Mismatch.Count -eq 0 -and $Missing.Count -eq 0) {
    Write-Host "All SVG hashes match."
    exit 0
}
$Mismatch | Format-Table -AutoSize
exit 1
