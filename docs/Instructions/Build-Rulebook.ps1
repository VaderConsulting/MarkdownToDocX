<#
.SYNOPSIS
  Renders LAYOUT-RULEBOOK.md from rules.yaml and validates rules.yaml.
.DESCRIPTION
  rules.yaml is the single source of the layout and routing rules. It is written in a strict YAML subset
  (see its header comments), parsed here without any YAML module, so the script runs on Windows PowerShell 5.1
  and PowerShell 7 offline.
  Default: write to a temp file and compare with the current rulebook (never overwrites it).
  -OutFile <path>: write the rendered rulebook there (pass the real LAYOUT-RULEBOOK.md path to publish).
.EXAMPLE
  .\Build-Rulebook.ps1                      # render to temp, validate, diff against LAYOUT-RULEBOOK.md
  .\Build-Rulebook.ps1 -OutFile .\LAYOUT-RULEBOOK.md
#>
[CmdletBinding()]
param(
  [string]$RulesFile,
  [string]$OutFile,
  [string]$CompareTo,
  [switch]$JsonTwin
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $RulesFile) { $RulesFile = Join-Path $here 'rules.yaml' }
else { $RulesFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RulesFile) }
if (-not $PSBoundParameters.ContainsKey('CompareTo')) { $CompareTo = Join-Path $here 'LAYOUT-RULEBOOK.md' }
$utf8 = New-Object System.Text.UTF8Encoding($false)

function ConvertFrom-RulesYaml([string]$path) {
  $lines = [System.IO.File]::ReadAllText($path, $utf8) -split "`r?`n"
  $doc = [ordered]@{}; $listKey = $null; $item = $null; $i = 0
  while ($i -lt $lines.Count) {
    $l = $lines[$i]
    if ($l -eq '' -or $l.StartsWith('#')) { $i++; continue }
    if ($l -match '^([a-z_]+):\s*(.*)$') {
      if ($matches[2] -eq '') { $listKey = $matches[1]; $doc[$listKey] = New-Object System.Collections.ArrayList; $item = $null }
      else { $doc[$matches[1]] = ConvertFrom-Json $matches[2]; $listKey = $null }
      $i++; continue
    }
    if ($l -match '^  - ([a-z_]+): (.*)$') {
      if (-not $listKey) { throw "Line $($i+1): list item outside a list" }
      $item = [ordered]@{}; [void]$doc[$listKey].Add($item)
      $item[$matches[1]] = ConvertFrom-Json $matches[2]; $i++; continue
    }
    if ($l -match '^    ([a-z_]+): \|-$') {
      $key = $matches[1]; $buf = New-Object System.Collections.Generic.List[string]; $i++
      while ($i -lt $lines.Count) {
        $b = $lines[$i]
        if ($b.StartsWith('      ')) { $buf.Add($b.Substring(6)); $i++; continue }
        if ($b -eq '') {
          $j = $i; while ($j -lt $lines.Count -and $lines[$j] -eq '') { $j++ }
          if ($j -lt $lines.Count -and $lines[$j].StartsWith('      ')) { for ($k = $i; $k -lt $j; $k++) { $buf.Add('') }; $i = $j; continue }
        }
        break
      }
      $item[$key] = $buf.ToArray(); continue
    }
    if ($l -match '^    ([a-z_]+): (.*)$') {
      if ($null -eq $item) { throw "Line $($i+1): field outside an item" }
      $v = $matches[2]
      if ($v.StartsWith('[')) { $item[$matches[1]] = @((ConvertFrom-Json $v) | ForEach-Object { $_ }) }
      else { $item[$matches[1]] = ConvertFrom-Json $v }
      $i++; continue
    }
    throw "Line $($i+1) of $path is outside the supported YAML subset: $l"
  }
  return $doc
}

function Get-RenderedRulebook($doc) {
  $all = New-Object System.Collections.ArrayList
  foreach ($s in $doc.sections) { [void]$all.Add(@{ o = [int]$s.order; b = [int]$s.blank_before; t = @($s.heading) }) }
  foreach ($t in $doc.terms) {
    $q = if ($t.qualifier) { ' ' + $t.qualifier } else { '' }
    $tx = @($t.text); $tx[0] = "- **$($t.name)**${q}: " + $tx[0]
    [void]$all.Add(@{ o = [int]$t.order; b = [int]$t.blank_before; t = $tx })
  }
  foreach ($r in $doc.rules) { $tx = @($r.text); $tx[0] = "$($r.id) " + $tx[0]; [void]$all.Add(@{ o = [int]$r.order; b = [int]$r.blank_before; t = $tx }) }
  foreach ($e in $doc.entries) { [void]$all.Add(@{ o = [int]$e.order; b = [int]$e.blank_before; t = @($e.text) }) }
  $out = New-Object System.Collections.Generic.List[string]
  foreach ($x in ($all | Sort-Object { $_.o })) { for ($k = 0; $k -lt $x.b; $k++) { $out.Add('') }; foreach ($t in $x.t) { $out.Add($t) } }
  $nl = if ($doc.line_ending -eq 'LF') { "`n" } else { "`r`n" }
  return ($out -join $nl) + $nl
}

function Test-Rules($doc, [string]$rulebookPath) {
  $errors = New-Object System.Collections.Generic.List[string]
  $ruleIds = @($doc.rules | ForEach-Object { $_.id })
  $dups = $ruleIds | Group-Object | Where-Object Count -gt 1
  foreach ($d in $dups) { $errors.Add("Rule $($d.Name) appears $($d.Count) times") }
  $known = @{}; foreach ($x in $ruleIds) { $known[$x] = 1 }; foreach ($e in $doc.entries) { $known[$e.id] = 1 }; $known['legality'] = 1
  $paramIds = @{}; foreach ($p in $doc.parameters) { $paramIds[$p.id] = 1 }
  foreach ($p in $doc.parameters) {
    if ($p.id -eq 'repair_chain_lines_fan_hub') {
      if ($p.value -cne 'fan_line_count') { $errors.Add('repair_chain_lines_fan_hub must evaluate to fan_line_count') }
    } elseif ($p.value -is [string]) { $errors.Add("Parameter $($p.id): expected a numeric value") }
  }
  $stageIds = @{}; foreach ($s in $doc.precedence) { $stageIds[$s.id] = 1 }
  $orders = @{}
  foreach ($r in $doc.rules) {
    foreach ($f in 'refs', 'outranks', 'yields') { foreach ($x in @($r.$f)) { if ($x -and -not $known.ContainsKey($x)) { $errors.Add("Rule $($r.id) ${f}: $x does not resolve") } } }
    foreach ($x in @($r.params)) { if ($x -and -not $paramIds.ContainsKey($x)) { $errors.Add("Rule $($r.id): parameter $x does not exist") } }
    if (-not $stageIds.ContainsKey($r.stage)) { $errors.Add("Rule $($r.id): stage $($r.stage) does not exist") }
    if ($r.pointer -and -not $known.ContainsKey($r.pointer)) { $errors.Add("Rule $($r.id): pointer $($r.pointer) does not resolve") }
  }
  foreach ($e in $doc.entries) { foreach ($x in @($e.refs)) { if ($x -and -not $known.ContainsKey($x)) { $errors.Add("Entry $($e.id): ref $x does not resolve") } } }
  foreach ($p in $doc.parameters) { foreach ($x in @($p.used_by)) { if ($x -and -not $known.ContainsKey($x)) { $errors.Add("Parameter $($p.id): used_by $x does not resolve") } } }
  foreach ($s in $doc.precedence) { foreach ($x in @($s.rules)) { if (-not $known.ContainsKey($x)) { $errors.Add("Stage $($s.id): rule $x does not exist") } } }
  foreach ($c in @($doc.sections) + @($doc.terms) + @($doc.rules) + @($doc.entries)) {
    if ($orders.ContainsKey([int]$c.order)) { $errors.Add("Order $($c.order) used twice ($($c.id))") } else { $orders[[int]$c.order] = 1 } }
  $missing = @(); $bookIds = @()
  if ($rulebookPath -and (Test-Path $rulebookPath)) {
    $bookIds = @([System.IO.File]::ReadAllLines($rulebookPath, $utf8) | Where-Object { $_ -match '^(\d+\.\d+) ' } | ForEach-Object { ($_ -split ' ')[0] })
    foreach ($b in $bookIds) { $n = @($ruleIds | Where-Object { $_ -eq $b }).Count; if ($n -ne 1) { $errors.Add("Rulebook rule $b appears $n times in rules.yaml") } }
    foreach ($x in $ruleIds) { if ($bookIds -notcontains $x) { $errors.Add("rules.yaml rule $x is not in the rulebook") } }
  }
  [pscustomobject]@{
    Rules = $ruleIds.Count; Active = @($doc.rules | Where-Object status -eq 'active').Count; Removed = @($doc.rules | Where-Object status -eq 'removed').Count
    Terms = @($doc.terms).Count; Parameters = @($doc.parameters).Count; Stages = @($doc.precedence).Count; Entries = @($doc.entries).Count
    RulebookRuleNumbers = $bookIds.Count; Errors = $errors.ToArray()
  }
}

$doc = ConvertFrom-RulesYaml $RulesFile
$val = Test-Rules $doc $CompareTo
$text = Get-RenderedRulebook $doc
$target = if ($OutFile) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile) } else { Join-Path ([System.IO.Path]::GetTempPath()) 'LAYOUT-RULEBOOK.rendered.md' }
[System.IO.File]::WriteAllText($target, $text, $utf8)
if ($JsonTwin) {
  $jsonPath = [System.IO.Path]::ChangeExtension($RulesFile, '.json')
  $exporter = Join-Path $here 'Export-RulesJson.py'
  if (Test-Path $exporter) {
    $py = if (Get-Command python3 -ErrorAction SilentlyContinue) { 'python3' } else { 'python' }
    & $py $exporter
    if ($LASTEXITCODE -ne 0) { throw "Export-RulesJson.py failed with exit code $LASTEXITCODE" }
  }
  else {
    [System.IO.File]::WriteAllText($jsonPath, ($doc | ConvertTo-Json -Depth 8), $utf8)
  }
}

"Rendered: $target"
"Rules $($val.Rules) (active $($val.Active), removed $($val.Removed)), terms $($val.Terms), parameters $($val.Parameters), stages $($val.Stages), other entries $($val.Entries); rulebook rule numbers $($val.RulebookRuleNumbers)"
if ($val.Errors.Count) { 'VALIDATION ERRORS:'; $val.Errors | ForEach-Object { "  $_" } } else { 'Validation: OK' }
if ($CompareTo -and (Test-Path $CompareTo) -and ((Resolve-Path $CompareTo).Path -ne (Resolve-Path $target).Path)) {
  $cur = [System.IO.File]::ReadAllText($CompareTo, $utf8)
  if ($cur -ceq $text) { 'Diff: byte-identical to ' + $CompareTo }
  else {
    $a = $cur -split "`r?`n" | ForEach-Object { ($_ -replace '\s+', ' ').Trim() } | Where-Object { $_ -ne '' }
    $b = $text -split "`r?`n" | ForEach-Object { ($_ -replace '\s+', ' ').Trim() } | Where-Object { $_ -ne '' }
    $d = Compare-Object $a $b -SyncWindow 1000 -CaseSensitive
    if (-not $d) { 'Diff: identical apart from whitespace' } else { 'Diff: text differences:'; $d | ForEach-Object { "  $($_.SideIndicator) $($_.InputObject)" } }
  }
}
if ($val.Errors.Count) { exit 1 }
