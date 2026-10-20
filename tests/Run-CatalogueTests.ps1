param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Filter,
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot 'TestResults\CatalogueWatchdog'),
    [string]$Name = 'Catalogue',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogueWatchdog.ps1')
$Project = (Resolve-Path -LiteralPath $Project).Path
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
if (-not $NoBuild) {
    & dotnet build $Project --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the requested catalogue fixtures.' }
}
$result = Invoke-CatalogueWatchdog -Project $Project -Filter $Filter -Configuration $Configuration -Results $ResultsDirectory -Stem $Name
Get-Content -LiteralPath $result.Stdout
Get-Content -LiteralPath $result.Stderr
Write-Output ('CATALOGUE_RUN|' + ($result | ConvertTo-Json -Compress))
if ($result.TimeoutScenario) { exit 124 }
exit $result.ExitCode
