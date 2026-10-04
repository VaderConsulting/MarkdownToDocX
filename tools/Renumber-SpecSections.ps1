# Renumber spec.md section 11 (11.0 -> 11.1, shift legacy 11.1-11.14 -> 11.2-11.15)
# and resequence line-type catalogues (11.0.12.x -> 11.1.14.x sequential).

$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent

function Update-SectionReferences {
    param([string]$Text)

    # Phase A: protect legacy main sections 11.1-11.14 (before 11.0 block becomes 11.1)
    $Text = $Text -replace '### 11\.14 ', '### @@LEG11.14@@ '
    $Text = $Text -replace '### 11\.13 ', '### @@LEG11.13@@ '
    $Text = $Text -replace '### 11\.12 ', '### @@LEG11.12@@ '
    $Text = $Text -replace '### 11\.11 ', '### @@LEG11.11@@ '
    $Text = $Text -replace '### 11\.10 ', '### @@LEG11.10@@ '
    $Text = $Text -replace '### 11\.9 ', '### @@LEG11.9@@ '
    $Text = $Text -replace '### 11\.8 ', '### @@LEG11.8@@ '
    $Text = $Text -replace '### 11\.7 ', '### @@LEG11.7@@ '
    $Text = $Text -replace '### 11\.6 ', '### @@LEG11.6@@ '
    $Text = $Text -replace '### 11\.5 ', '### @@LEG11.5@@ '
    $Text = $Text -replace '### 11\.4 ', '### @@LEG11.4@@ '
    $Text = $Text -replace '### 11\.3 ', '### @@LEG11.3@@ '
    $Text = $Text -replace '### 11\.2 ', '### @@LEG11.2@@ '
    $Text = $Text -replace '### 11\.1 Current Problem', '### @@LEG11.1@@ Current Problem'

    # Phase B: line-type catalogues (longest first; skip removed .2)
    $CatalogueMap = [ordered]@{
        '11.0.12.10' = '11.1.14.9'
        '11.0.12.9'  = '11.1.14.8'
        '11.0.12.8'  = '11.1.14.7'
        '11.0.12.7'  = '11.1.14.6'
        '11.0.12.6'  = '11.1.14.5'
        '11.0.12.5'  = '11.1.14.4'
        '11.0.12.4'  = '11.1.14.3'
        '11.0.12.3'  = '11.1.14.2'
        '11.0.12.1'  = '11.1.14.1'
        '11.0.12'    = '11.1.14'
    }
    foreach ($Key in $CatalogueMap.Keys) {
        $Text = $Text -replace [regex]::Escape($Key), $CatalogueMap[$Key]
    }

    # Phase C: fix duplicate 11.0.9 before general 11.0.9 replacement
    $Text = $Text -replace '### 11\.0\.9 ELK', '### 11.1.10 ELK'

    $IntroMap = [ordered]@{
        '11.0.11' = '11.1.12'
        '11.0.10' = '11.1.11'
        '11.0.9'  = '11.1.9'
        '11.0.8.2' = '11.1.8.2'
        '11.0.8.1' = '11.1.8.1'
        '11.0.8'  = '11.1.8'
        '11.0.7'  = '11.1.7'
        '11.0.6'  = '11.1.6'
        '11.0.5'  = '11.1.5'
        '11.0.4'  = '11.1.4'
        '11.0.3'  = '11.1.3'
        '11.0.2'  = '11.1.2'
        '11.0.1'  = '11.1.1'
    }
    foreach ($Key in $IntroMap.Keys) {
        $Text = $Text -replace [regex]::Escape($Key), $IntroMap[$Key]
    }

    $Text = $Text -replace '### 11\.0 Compatibility', '### 11.1 Compatibility'
    $Text = $Text -replace '(?<![\d\.])§11\.0(?![\d\.])', '§11.1'
    $Text = $Text -replace '(?<![\d\.])11\.0(?![\d\.])', '11.1'

    # Phase D: legacy main sections -> 11.2-11.15
    $LegacyMap = [ordered]@{
        '@@LEG11.14@@' = '11.15'
        '@@LEG11.13@@' = '11.14'
        '@@LEG11.12@@' = '11.13'
        '@@LEG11.11@@' = '11.12'
        '@@LEG11.10@@' = '11.11'
        '@@LEG11.9@@'  = '11.10'
        '@@LEG11.8@@'  = '11.9'
        '@@LEG11.7@@'  = '11.8'
        '@@LEG11.6@@'  = '11.7'
        '@@LEG11.5@@'  = '11.6'
        '@@LEG11.4@@'  = '11.5'
        '@@LEG11.3@@'  = '11.4'
        '@@LEG11.2@@'  = '11.3'
        '@@LEG11.1@@'  = '11.2'
    }
    foreach ($Key in $LegacyMap.Keys) {
        $Text = $Text -replace [regex]::Escape($Key), $LegacyMap[$Key]
    }

    # Phase E: shift §11.x references for legacy content sections (high to low)
    $LegacyRefMap = [ordered]@{
        '§11.14' = '§11.15'
        '§11.13' = '§11.14'
        '§11.12' = '§11.13'
        '§11.11' = '§11.12'
        '§11.10' = '§11.11'
        '§11.9'  = '§11.10'
        '§11.8'  = '§11.9'
        '§11.7'  = '§11.8'
        '§11.6'  = '§11.7'
        '§11.5'  = '§11.6'
        '§11.4'  = '§11.5'
        '§11.3'  = '§11.4'
        '§11.2'  = '§11.3'
    }
    foreach ($Key in $LegacyRefMap.Keys) {
        $Text = $Text -replace [regex]::Escape($Key), $LegacyRefMap[$Key]
    }

    return $Text
}

$IncludePaths = @(
    (Join-Path $Root 'spec.md')
    (Join-Path $Root 'status.md')
    (Join-Path $Root 'sequence.md')
    (Join-Path $Root 'class.md')
    (Join-Path $Root 'state.md')
    (Join-Path $Root 'entityrelationship.md')
    (Join-Path $Root 'src\MermaidRecognition\README.md')
    (Join-Path $Root 'spec-gates\draft-11.0.12.3-sequence-produces-section.md')
    (Join-Path $Root 'spec-gates\draft-11.0.12.5-state-section.md')
    (Join-Path $Root 'spec-gates\draft-11.0.12.6-er-section.md')
    (Join-Path $Root 'tools\Generate-DiagramGateRegistry.ps1')
)

Get-ChildItem -Path (Join-Path $Root 'src'), (Join-Path $Root 'tests') -Directory -Filter 'MermaidRecognition*' | Get-ChildItem -Recurse -Include '*.cs' | ForEach-Object {
    $IncludePaths += $_.FullName
}

foreach ($FilePath in ($IncludePaths | Select-Object -Unique)) {
    if (-not (Test-Path $FilePath)) { continue }
    $Original = [System.IO.File]::ReadAllText($FilePath)
    $Updated = Update-SectionReferences -Text $Original
    if ($Updated -ne $Original) {
        [System.IO.File]::WriteAllText($FilePath, $Updated)
        Write-Host "Updated: $FilePath"
    }
}

# spec.md-only structural fixes
$SpecPath = Join-Path $Root 'spec.md'
$Spec = [System.IO.File]::ReadAllText($SpecPath)

# Remove obsolete reserved catalogue stub
$Spec = $Spec -replace '(?ms)\r?\n#### 11\.1\.13\.2 Reserved\r?\n\r?\nSection number retained[^\r\n]*\r?\n\r?\n', "`r`n"

# Fix section 3.18 heading levels (was nested under 3.17)
$Spec = $Spec -replace '#### 3\.18 Entity type catalogue', '### 3.18 Entity type catalogue'
$Spec = $Spec -replace '##### 3\.18\.1 Recognition contract model entities', '#### 3.18.1 Recognition contract model entities'
$Spec = $Spec -replace '##### 3\.18\.2 Mermaid diagram kinds', '#### 3.18.2 Mermaid diagram kinds'

# Reorder 11.1.8.1 before 11.1.8.2 (Open decision before Repository layout)
$Pattern811 = '(?ms)(### 11\.1\.8\.1 Open decision: full replacement of Naiad.*?Record outcomes of this investigation in spec revision history when the decision is resolved\.\r?\n)'
$Pattern812 = '(?ms)(### 11\.1\.8\.2 Repository layout \(`new_src` vs legacy\).*?See also `new_src/README\.md`\.\r?\n)'
if ($Spec -match $Pattern812 -and $Spec -match $Pattern811) {
    $Block812 = [regex]::Match($Spec, $Pattern812).Value
    $Block811 = [regex]::Match($Spec, $Pattern811).Value
    if ($Spec.IndexOf($Block812) -lt $Spec.IndexOf($Block811)) {
        $Spec = $Spec -replace $Pattern812, '@@REPO812@@'
        $Spec = $Spec -replace $Pattern811, '@@OPEN811@@'
        $Spec = $Spec -replace '@@REPO812@@\r?\n\r?\n@@OPEN811@@', ($Block811 + "`r`n" + $Block812)
        $Spec = $Spec -replace '@@REPO812@@', $Block812
        $Spec = $Spec -replace '@@OPEN811@@', $Block811
        Write-Host 'Reordered 11.1.8.1 before 11.1.8.2 in spec.md'
    }
}

# ELK cross-ref should point to 11.1.10 not 11.1.9 (code map)
$Spec = $Spec -replace '\(see §11\.1\.9, \[`status\.md`\]\(status\.md\)\)', '(see §11.1.10, [`status.md`](status.md))'
$Spec = $Spec -replace 'not Naiad''s current engine \(§11\.1\.9\)', 'not Naiad''s current engine (§11.1.10)'

[System.IO.File]::WriteAllText($SpecPath, $Spec)
Write-Host "Finished spec.md structural fixes"
