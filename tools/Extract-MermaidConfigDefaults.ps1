$Path = Join-Path $PSScriptRoot '..\mermaid\packages\mermaid\src\schemas\config.schema.yaml'
$Lines = Get-Content -Path $Path

$Defs = @(
    'BaseDiagramConfig', 'C4DiagramConfig', 'GitGraphDiagramConfig', 'RequirementDiagramConfig',
    'ArchitectureDiagramConfig', 'MindmapDiagramConfig', 'IshikawaDiagramConfig', 'KanbanDiagramConfig',
    'PieDiagramConfig', 'QuadrantChartConfig', 'XYChartConfig', 'ErDiagramConfig', 'StateDiagramConfig',
    'ClassDiagramConfig', 'JourneyDiagramConfig', 'TimelineDiagramConfig', 'GanttDiagramConfig',
    'SequenceDiagramConfig', 'FlowchartDiagramConfig', 'SankeyDiagramConfig', 'PacketDiagramConfig',
    'BlockDiagramConfig', 'EventModelingDiagramConfig', 'TreeViewDiagramConfig', 'RadarDiagramConfig',
    'VennDiagramConfig', 'WardleyDiagramConfig'
)

$TopLevelKeys = @(
    'theme', 'look', 'handDrawnSeed', 'layout', 'maxTextSize', 'maxEdges', 'darkMode', 'htmlLabels',
    'fontFamily', 'fontSize', 'markdownAutoWrap', 'securityLevel', 'startOnLoad', 'arrowMarkerAbsolute'
)

function Get-DefaultsInRange {
    param([int]$Start, [int]$End)
    $results = @()
    $currentKey = $null
    for ($i = $Start; $i -lt $End; $i++) {
        $line = $Lines[$i]
        if ($line -match '^\s{4,8}([a-zA-Z0-9_]+):\s*$' -and $line -notmatch 'properties:|required:|enum:|type:|description:|allOf:|title:|items:|minimum:|maximum:') {
            $currentKey = $Matches[1]
        }
        if ($line -match '^\s+default:\s*(.*)$' -and $currentKey) {
            $val = $Matches[1].Trim()
            $results += [PSCustomObject]@{ Key = $currentKey; Value = $val; Line = ($i + 1) }
            $currentKey = $null
        }
    }
    return $results
}

Write-Output '=== GLOBAL ==='
for ($i = 0; $i -lt $Lines.Count; $i++) {
    if ($Lines[$i] -match '^\`$defs:') { break }
    foreach ($k in $TopLevelKeys) {
        if ($Lines[$i] -eq "  ${k}:") {
            for ($j = $i; $j -lt [Math]::Min($i + 40, $Lines.Count); $j++) {
                if ($Lines[$j] -match '^\s+default:\s*(.*)$') {
                    Write-Output "${k} = $($Matches[1].Trim()) @ $($j + 1)"
                    break
                }
            }
        }
    }
}

# elk
$inElk = $false
$ek = $null
for ($i = 0; $i -lt $Lines.Count; $i++) {
    if ($Lines[$i] -eq '  elk:') { $inElk = $true; continue }
    if ($inElk -and $Lines[$i] -match '^  [a-zA-Z]' -and $Lines[$i] -notmatch '^    ') { $inElk = $false }
    if ($inElk -and $Lines[$i] -match '^\s{6}([a-zA-Z]+):') { $ek = $Matches[1] }
    if ($inElk -and $Lines[$i] -match '^\s{8}default:\s*(.*)$' -and $ek) {
        Write-Output "elk.${ek} = $($Matches[1].Trim()) @ $($i + 1)"
        $ek = $null
    }
}

Write-Output ''
foreach ($def in $Defs) {
    $start = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -eq "  ${def}:") { $start = $i; break }
    }
    if ($start -lt 0) { continue }
    $end = $Lines.Count
    for ($i = $start + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^  [A-Za-z0-9_-]+:' -and $Lines[$i] -notmatch '^    ') {
            $end = $i
            break
        }
    }
    Write-Output "=== $def ($($start + 1)-$end) ==="
    foreach ($d in (Get-DefaultsInRange -Start $start -End $end)) {
        Write-Output "  $($d.Key) = $($d.Value) @ $($d.Line)"
    }
    Write-Output ''
}
