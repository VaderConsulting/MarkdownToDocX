$SpecPath = Join-Path $PSScriptRoot '..\spec.md'
$EntityPath = Join-Path $PSScriptRoot '..\spec-entity-types.generated.md'
$Spec = Get-Content -Path $SpecPath -Raw
$Entity = Get-Content -Path $EntityPath -Raw

$Marker = "---`r`n`r`n## 4. Recognition Contract"
if ($Spec -notmatch [regex]::Escape($Marker)) {
    $Marker = "---`n`n## 4. Recognition Contract"
}

$Intro = @'
Regenerate entity-type tables when the Mermaid pin changes:

```text
powershell -File tools/Generate-EntityTypesMarkdown.ps1
powershell -File tools/Merge-EntityTypesIntoSpec.ps1
```

### 3.18 Entity type catalogue (normative)

'@

$SectionPattern = '(?s)(?:Regenerate entity-type tables when the Mermaid pin changes:[\s\S]*?)?### 3\.18 Entity type catalogue[\s\S]*?(?=\r?\n---\r?\n\r?\n## 4\. Recognition Contract)'

if ($Spec -match $SectionPattern) {
    $Replacement = $Intro.TrimEnd() + "`r`n`r`n" + $Entity.Trim() + "`r`n`r`n"
    $New = [regex]::Replace($Spec, $SectionPattern, $Replacement, 1)
}
else {
    $Insertion = $Intro + $Entity.Trim() + "`r`n`r`n"
    $New = $Spec.Replace($Marker, $Insertion + $Marker)
}
Set-Content -Path $SpecPath -Value $New -Encoding UTF8 -NoNewline
Write-Output 'Inserted section 3.18 into spec.md'
