# Copies MSAGL sources into MermaidRecognition.Layout/Sugiyama/ with namespace adoption.
param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot "..\extern\Microsoft.AGL\src\Microsoft.AGL"),
    [string]$TargetRoot = (Join-Path $PSScriptRoot "..\new_src\MermaidRecognition.Layout\Sugiyama")
)

$ErrorActionPreference = "Stop"

$SourceRoot = (Resolve-Path $SourceRoot).Path
$TargetRoot = [System.IO.Path]::GetFullPath($TargetRoot)

if (-not (Test-Path $SourceRoot)) {
    throw "Source not found: $SourceRoot"
}

if (Test-Path $TargetRoot) {
    Remove-Item -Recurse -Force $TargetRoot
}

New-Item -ItemType Directory -Path $TargetRoot -Force | Out-Null

$ExcludeDirectoryNames = @("obj", "bin", ".vs", "Properties")
$ExcludeFileNames = @("AssemblyInfo.cs", "app.config", "GlobalSuppressions.cs")

$NamespaceFrom = "Microsoft.AGL"
$NamespaceTo = "MermaidRecognition.Layout.Sugiyama"

function Copy-MsaglDirectory {
    param(
        [string]$SourceDirectory,
        [string]$DestinationDirectory
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    foreach ($Entry in Get-ChildItem -Path $SourceDirectory -Force) {
        if ($Entry.PSIsContainer) {
            if ($ExcludeDirectoryNames -contains $Entry.Name) {
                continue
            }

            $ChildDestination = Join-Path $DestinationDirectory $Entry.Name
            Copy-MsaglDirectory -SourceDirectory $Entry.FullName -DestinationDirectory $ChildDestination
            continue
        }

        if ($ExcludeFileNames -contains $Entry.Name) {
            continue
        }

        if ($Entry.Extension -ne ".cs" -and $Entry.Extension -ne ".resx") {
            continue
        }

        $DestinationPath = Join-Path $DestinationDirectory $Entry.Name
        if ($Entry.Extension -eq ".cs") {
            $Content = [System.IO.File]::ReadAllText($Entry.FullName)
            $Content = $Content.Replace($NamespaceFrom, $NamespaceTo)
            [System.IO.File]::WriteAllText($DestinationPath, $Content)
        }
        else {
            Copy-Item -Path $Entry.FullName -Destination $DestinationPath -Force
        }
    }
}

Copy-MsaglDirectory -SourceDirectory $SourceRoot -DestinationDirectory $TargetRoot

$FileCount = (Get-ChildItem -Path $TargetRoot -Recurse -File).Count
Write-Host "Copied $FileCount files to $TargetRoot"
