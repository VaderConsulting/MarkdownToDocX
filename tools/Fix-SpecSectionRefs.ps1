$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent

function Fix-LegacySectionRefs {
    param([string]$Text)

    for ($N = 14; $N -ge 2; $N--) {
        $Next = $N + 1
        $Pattern = '§11\.' + $N + '(?!\.\d)'
        $Replacement = '§11.' + $Next
        $Text = [regex]::Replace($Text, $Pattern, $Replacement)
    }

    $Text = $Text -replace '§11\.0(?!\.\d)', '§11.1'
    $Text = $Text -replace '\(see §11\.1\.9,', '(see §11.1.10,'
    $Text = $Text -replace 'engine \(§11\.1\.9\)', 'engine (§11.1.10)'
    return $Text
}

$Paths = @(
    (Join-Path $Root 'spec.md')
    (Join-Path $Root 'status.md')
    (Join-Path $Root 'sequence.md')
    (Join-Path $Root 'class.md')
    (Join-Path $Root 'state.md')
    (Join-Path $Root 'entityrelationship.md')
    (Join-Path $Root 'src\MermaidRecognition\README.md')
)
Get-ChildItem -Path (Join-Path $Root 'src'), (Join-Path $Root 'tests') -Directory -Filter 'MermaidRecognition*' | Get-ChildItem -Recurse -Include '*.cs' | ForEach-Object {
    $Paths += $_.FullName
}

foreach ($Path in ($Paths | Select-Object -Unique)) {
    if (-not (Test-Path $Path)) { continue }
    $Original = [System.IO.File]::ReadAllText($Path)
    $Updated = Fix-LegacySectionRefs -Text $Original
    if ($Updated -ne $Original) {
        [System.IO.File]::WriteAllText($Path, $Updated)
        Write-Host "Fixed refs: $Path"
    }
}
