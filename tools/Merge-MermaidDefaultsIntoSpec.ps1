$SpecPath = Join-Path $PSScriptRoot '..\spec.md'
$CatPath = Join-Path $PSScriptRoot '..\spec-mermaid-defaults-catalogue.generated.md'
$Spec = Get-Content -Path $SpecPath -Raw
$Cat = Get-Content -Path $CatPath -Raw

$Intro = @"
#### 3.17.6 All diagram families (including experimental): required config defaults and overrides

This specification covers **all Mermaid diagram families present in the pinned Mermaid version**, including experimental/beta diagrams (`wardley-beta`). For each family, defaults and overrides are defined in the vendored Mermaid configuration schema and theme implementation. **Every default value in §3.17.7–§3.17.39 includes a source file reference.**

Regenerate the machine-readable tables when the Mermaid pin changes:

```text
powershell -File tools/Generate-MermaidDefaultsMarkdown.ps1
```

Contract requirements (all diagram kinds):

- The recogniser must emit `effectiveConfig` for the diagram kind that includes global defaults, diagram-specific defaults, directive overrides, and resolved `themeVariables`.
- The recogniser must preserve raw directive payload and source spans.
- For experimental/beta families, recognise config using schema defaults and emit diagnostics for unsupported native render/export features (see §7.3).
- The renderer/exporter must treat `effectiveConfig` + `themeVariables` as inputs for fonts, colours, spacing, padding, and label measurement.
- Silent substitution without diagnostics is not acceptable for DOCX export.

"@

$Pattern = '(?s)#### 3\.17\.6 All diagram families.*?Note: this section intentionally defines.*?\r?\n\r?\n'
if ($Spec -notmatch $Pattern) {
    throw 'Could not find 3.17.6 section to replace'
}

$Replacement = $Intro + $Cat.Trim() + "`r`n`r`n"
$New = [regex]::Replace($Spec, $Pattern, $Replacement)
Set-Content -Path $SpecPath -Value $New -Encoding UTF8 -NoNewline
Write-Output "Merged catalogue into $SpecPath"
