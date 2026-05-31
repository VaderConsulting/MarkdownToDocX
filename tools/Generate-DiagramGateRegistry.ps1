#Requires -Version 5.1
<#
.SYNOPSIS
    Merges gate manifests into spec-gates/registry.generated.md (§11.1.14.2).
#>
param(
    [string] $ManifestsDirectory = (Join-Path $PSScriptRoot "..\new_src\MermaidRecognition.Tests\Gates\manifests"),
    [string] $OutputPath = (Join-Path $PSScriptRoot "..\spec-gates\registry.generated.md")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ManifestsDirectory)) {
    throw "Manifests directory not found: $ManifestsDirectory"
}

$ManifestFiles = Get-ChildItem -LiteralPath $ManifestsDirectory -Filter "*.gate-manifest.json" | Sort-Object Name
$Rows = New-Object System.Collections.Generic.List[string]

foreach ($File in $ManifestFiles) {
    $Document = Get-Content -LiteralPath $File.FullName -Raw | ConvertFrom-Json
    $FixtureCount = @($Document.Fixtures).Count
    $FixtureList = ($Document.Fixtures -join ", ")
    $Rows.Add("| $($Document.DiagramKind) | $($Document.GateLevel) | $($Document.FixtureDirectory) | $FixtureCount | $FixtureList |")
}

$GeneratedUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'")

$Markdown = @"
# Diagram gate registry (generated)

Do not edit by hand. Regenerate with:

``````powershell
.\tools\Generate-DiagramGateRegistry.ps1
``````

Generated: $GeneratedUtc

| Diagram kind | Gate level | Fixture directory | Fixture count | Fixture names |
| --- | --- | --- | ---: | --- |
$($Rows -join "`n")

Total manifests: $($ManifestFiles.Count)
"@

$OutputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $Markdown, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $OutputPath ($($ManifestFiles.Count) manifests)"
