<#
.SYNOPSIS
Renders every flow.md fixture from the committed code (HEAD) and from the working tree, then lists the fixtures whose SVG changed.

.PARAMETER RefreshBaseline
Rebuilds the HEAD worktree and re-renders the baseline SVGs.

.PARAMETER Png
Also writes before and after PNGs for each changed fixture.
#>
param(
    [switch]$RefreshBaseline,
    [switch]$Png
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$Worktree = Join-Path $env:TEMP 'md2docx_baseline'
$BaselineDir = Join-Path $RepoRoot 'out\Regression\baseline'
$CurrentDir = Join-Path $RepoRoot 'out\Regression\current'

Push-Location $RepoRoot
try {
    if ($RefreshBaseline -or -not (Test-Path $Worktree)) {
        if (Test-Path $Worktree) {
            git worktree remove --force $Worktree
        }

        git worktree add --detach $Worktree HEAD | Out-Null
        New-Item -ItemType Directory -Force (Join-Path $Worktree 'out\FixtureProbe') | Out-Null
        if (Test-Path $BaselineDir) {
            Remove-Item -Recurse -Force $BaselineDir
        }
    }

    Copy-Item "$PSScriptRoot\Program.cs", "$PSScriptRoot\FixtureProbe.csproj" (Join-Path $Worktree 'out\FixtureProbe') -Force
    Copy-Item (Join-Path $RepoRoot 'flow.md') (Join-Path $Worktree 'flow.md') -Force

    if (-not (Test-Path $BaselineDir) -or (Get-ChildItem $BaselineDir -Filter *.svg).Count -eq 0) {
        dotnet run --project (Join-Path $Worktree 'out\FixtureProbe\FixtureProbe.csproj') -c Release -- --all --svgonly --png --out $BaselineDir | Out-Null
    }

    if (Test-Path $CurrentDir) {
        Remove-Item -Recurse -Force $CurrentDir
    }

    dotnet run --project "$PSScriptRoot\FixtureProbe.csproj" -c Release -- --all --svgonly --png --out $CurrentDir | Out-Null

    $Changed = @()
    foreach ($File in Get-ChildItem $CurrentDir -Filter *.svg) {
        $BaselineFile = Join-Path $BaselineDir $File.Name
        if (-not (Test-Path $BaselineFile) -or (Get-FileHash $BaselineFile).Hash -ne (Get-FileHash $File.FullName).Hash) {
            $Changed += $File.BaseName
        }
    }

    $ApprovedDir = Join-Path $RepoRoot 'out\Regression\approved'
    $ApprovedChanged = @()
    if (Test-Path $ApprovedDir) {
        foreach ($File in Get-ChildItem $ApprovedDir -Filter *.svg) {
            $CurrentFile = Join-Path $CurrentDir $File.Name
            if (-not (Test-Path $CurrentFile) -or (Get-FileHash $CurrentFile).Hash -ne (Get-FileHash $File.FullName).Hash) {
                $ApprovedChanged += $File.BaseName
            }
        }

        if ($ApprovedChanged.Count -gt 0) {
            Write-Output "REGRESSION - approved fixtures changed: $($ApprovedChanged -join ' ')"
        }
        else {
            Write-Output "Approved fixtures unchanged: $((Get-ChildItem $ApprovedDir -Filter *.svg).Count)"
        }
    }

    $Total = (Get-ChildItem $CurrentDir -Filter *.svg).Count
    Write-Output "Fixtures: $Total  Unchanged: $($Total - $Changed.Count)  Changed: $($Changed.Count)"
    Write-Output ($Changed -join ' ')

    if ($Png -and $Changed.Count -gt 0) {
        dotnet run --project (Join-Path $Worktree 'out\FixtureProbe\FixtureProbe.csproj') -c Release --no-build -- @Changed --png --out $BaselineDir | Out-Null
        dotnet run --project "$PSScriptRoot\FixtureProbe.csproj" -c Release --no-build -- @Changed --png --out $CurrentDir | Out-Null
    }
}
finally {
    Pop-Location
}
