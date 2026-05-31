# Generates MermaidPinnedDefaults.generated.cs from vendored config.schema.yaml (spec §3.17).
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
$SchemaPath = Join-Path $Root 'mermaid\packages\mermaid\src\schemas\config.schema.yaml'
$OutPath = Join-Path $Root 'new_src\MermaidRecognition\Config\MermaidPinnedDefaults.generated.cs'
$Lines = Get-Content -Path $SchemaPath

$Defs = @(
    @{ Name = 'BaseDiagramConfig'; InitKey = $null; Start = 359 },
    @{ Name = 'C4DiagramConfig'; InitKey = 'c4'; Start = 373 },
    @{ Name = 'GitGraphDiagramConfig'; InitKey = 'gitGraph'; Start = 875 },
    @{ Name = 'RequirementDiagramConfig'; InitKey = 'requirement'; Start = 936 },
    @{ Name = 'ArchitectureDiagramConfig'; InitKey = 'architecture'; Start = 973 },
    @{ Name = 'MindmapDiagramConfig'; InitKey = 'mindmap'; Start = 1003 },
    @{ Name = 'IshikawaDiagramConfig'; InitKey = 'ishikawa'; Start = 1026 },
    @{ Name = 'KanbanDiagramConfig'; InitKey = 'kanban'; Start = 1043 },
    @{ Name = 'PieDiagramConfig'; InitKey = 'pie'; Start = 1060 },
    @{ Name = 'QuadrantChartConfig'; InitKey = 'quadrantChart'; Start = 1074 },
    @{ Name = 'XYChartAxisConfig'; InitKey = $null; Start = 1194 },
    @{ Name = 'XYChartConfig'; InitKey = 'xyChart'; Start = 1264 },
    @{ Name = 'ErDiagramConfig'; InitKey = 'er'; Start = 1329 },
    @{ Name = 'StateDiagramConfig'; InitKey = 'state'; Start = 1406 },
    @{ Name = 'ClassDiagramConfig'; InitKey = 'class'; Start = 1498 },
    @{ Name = 'JourneyDiagramConfig'; InitKey = 'journey'; Start = 1553 },
    @{ Name = 'TimelineDiagramConfig'; InitKey = 'timeline'; Start = 1694 },
    @{ Name = 'GanttDiagramConfig'; InitKey = 'gantt'; Start = 1822 },
    @{ Name = 'SequenceDiagramConfig'; InitKey = 'sequence'; Start = 1944 },
    @{ Name = 'FlowchartDiagramConfig'; InitKey = 'flowchart'; Start = 2108 },
    @{ Name = 'SankeyDiagramConfig'; InitKey = 'sankey'; Start = 2267 },
    @{ Name = 'PacketDiagramConfig'; InitKey = 'packet'; Start = 2331 },
    @{ Name = 'BlockDiagramConfig'; InitKey = 'block'; Start = 2368 },
    @{ Name = 'EventModelingDiagramConfig'; InitKey = 'eventmodeling'; Start = 2380 },
    @{ Name = 'TreeViewDiagramConfig'; InitKey = 'treeView'; Start = 2396 },
    @{ Name = 'RadarDiagramConfig'; InitKey = 'radar'; Start = 2424 },
    @{ Name = 'VennDiagramConfig'; InitKey = 'venn'; Start = 2478 },
    @{ Name = 'WardleyDiagramConfig'; InitKey = 'wardley-beta'; Start = 2503 }
)

function Get-DefEnd {
    param([int]$Start)
    for ($Index = $Start + 1; $Index -lt $Lines.Count; $Index++) {
        if ($Lines[$Index] -match '^  [A-Za-z0-9_-]+:' -and $Lines[$Index] -notmatch '^    ') {
            return $Index
        }
    }
    return $Lines.Count
}

function Get-DefaultsInRange {
    param([int]$Start, [int]$End)
    $Results = @()
    $CurrentKey = $null
    for ($Index = $Start; $Index -lt $End; $Index++) {
        $Line = $Lines[$Index]
        if ($Line -match '^\s{4,8}([a-zA-Z0-9_]+):\s*$' -and $Line -notmatch 'properties:|required:|enum:|type:|description:|allOf:|title:|items:|minimum:|maximum:|anyOf:|meta:') {
            $CurrentKey = $Matches[1]
        }
        if ($Line -match '^\s+default:\s*(.*)$' -and $CurrentKey -and $CurrentKey -ne 'default') {
            $Value = $Matches[1].Trim()
            if ($Value -eq '' -or $Value -eq '@' -or $Value -match '\$ref' -or $Value.StartsWith('{')) {
                $CurrentKey = $null
                continue
            }
            $Results += [PSCustomObject]@{ Key = $CurrentKey; Value = $Value }
            $CurrentKey = $null
        }
    }
    return $Results
}

function Convert-SchemaDefaultToEffectiveString {
    param([string]$Value)
    if ($Value -eq 'null') {
        return 'null'
    }
    if ($Value -match "^'(.+)'$") {
        return $Matches[1]
    }
    if ($Value -match '^"(.+)"$') {
        return $Matches[1]
    }
    if ($Value -match '^(.*?)\s+#') {
        return $Matches[1].Trim()
    }
    return $Value
}

function Escape-CSharpString {
    param([string]$Value)
    return ($Value -replace '\\', '\\' -replace '"', '\"')
}

$GlobalDefaults = @(
    @('theme', 'default'),
    @('look', 'classic'),
    @('handDrawnSeed', '0'),
    @('layout', 'dagre'),
    @('maxTextSize', '50000'),
    @('maxEdges', '500'),
    @('darkMode', 'false'),
    @('fontFamily', '"trebuchet ms", verdana, arial, sans-serif;'),
    @('fontSize', '16'),
    @('markdownAutoWrap', 'true'),
    @('securityLevel', 'strict'),
    @('startOnLoad', 'true'),
    @('arrowMarkerAbsolute', 'false'),
    @('htmlLabels', 'true'),
    @('logLevel', '5'),
    @('useMaxWidth', 'true'),
    @('elk.mergeEdges', 'false'),
    @('elk.nodePlacementStrategy', 'BRANDES_KOEPF'),
    @('elk.cycleBreakingStrategy', 'GREEDY_MODEL_ORDER'),
    @('elk.forceNodeModelOrder', 'false'),
    @('elk.considerModelOrder', 'NODES_AND_EDGES')
)

$ThemeDefaults = @(
    @('themeVariables.fontFamily', '"trebuchet ms", verdana, arial, sans-serif'),
    @('themeVariables.fontSize', '16px'),
    @('themeVariables.fontWeight', 'normal'),
    @('themeVariables.background', 'white'),
    @('themeVariables.mainBkg', '#ECECFF'),
    @('themeVariables.secondBkg', '#ffffde'),
    @('themeVariables.lineColor', '#333333'),
    @('themeVariables.textColor', '#333'),
    @('themeVariables.arrowheadColor', '#333333'),
    @('themeVariables.labelBackground', 'rgba(232,232,232, 0.8)'),
    @('themeVariables.radius', '5'),
    @('themeVariables.strokeWidth', '1'),
    @('themeVariables.nodeBkg', '#ECECFF'),
    @('themeVariables.nodeBorder', '#9370DB'),
    @('themeVariables.clusterBkg', '#ffffde'),
    @('themeVariables.clusterBorder', '#aaaa33'),
    @('themeVariables.defaultLinkColor', '#333333'),
    @('themeVariables.edgeLabelBackground', 'rgba(232,232,232, 0.8)'),
    @('themeVariables.actorTextColor', 'black'),
    @('themeVariables.noteBkgColor', '#fff5ad')
)

$KindDefaults = @{}
foreach ($Def in $Defs) {
    if (-not $Def.InitKey) {
        continue
    }
    $End = Get-DefEnd -Start $Def.Start
    $Rows = Get-DefaultsInRange -Start $Def.Start -End $End
    $Entries = @()
    foreach ($Row in $Rows) {
        if ($Row.Value -eq 'null') {
            continue
        }
        $Effective = Convert-SchemaDefaultToEffectiveString -Value $Row.Value
        $Entries += @("$($Def.InitKey).$($Row.Key)", $Effective)
    }
    if ($Def.Name -eq 'FlowchartDiagramConfig') {
        $Entries += @('flowchart.subGraphTitleMargin.top', '0')
        $Entries += @('flowchart.subGraphTitleMargin.bottom', '0')
    }
    if ($Def.Name -eq 'XYChartConfig') {
        $Entries += @('xyChart.showTitle', 'true')
    }
    $KindDefaults[$Def.InitKey] = $Entries
}

$Treemap = @(
    @('treemap.useMaxWidth', 'true'),
    @('treemap.padding', '10'),
    @('treemap.diagramPadding', '8'),
    @('treemap.showValues', 'true'),
    @('treemap.nodeWidth', '100'),
    @('treemap.nodeHeight', '40'),
    @('treemap.borderWidth', '1'),
    @('treemap.valueFontSize', '12'),
    @('treemap.labelFontSize', '14'),
    @('treemap.valueFormat', ',')
)
$KindDefaults['treemap'] = $Treemap

$Builder = New-Object System.Text.StringBuilder
[void]$Builder.AppendLine('// <auto-generated />')
[void]$Builder.AppendLine('// Generated by tools/Generate-MermaidPinnedDefaultsCs.ps1 from vendored Mermaid 11.14.0 config.schema.yaml.')
[void]$Builder.AppendLine('// Normative reference: spec.md section 3.17. Regenerate when the Mermaid pin changes.')
[void]$Builder.AppendLine('namespace MermaidRecognition.Config;')
[void]$Builder.AppendLine('')
[void]$Builder.AppendLine('public static partial class MermaidPinnedDefaults')
[void]$Builder.AppendLine('{')
[void]$Builder.AppendLine('    private static readonly (string Key, string Value)[] GlobalRootDefaults = new (string Key, string Value)[]')
[void]$Builder.AppendLine('    {')
foreach ($Row in $GlobalDefaults) {
    [void]$Builder.AppendLine(('        ("{0}", "{1}"),' -f (Escape-CSharpString $Row[0]), (Escape-CSharpString $Row[1])))
}
[void]$Builder.AppendLine('    };')
[void]$Builder.AppendLine('')
[void]$Builder.AppendLine('    private static readonly (string Key, string Value)[] ThemeVariableDefaults = new (string Key, string Value)[]')
[void]$Builder.AppendLine('    {')
foreach ($Row in $ThemeDefaults) {
    [void]$Builder.AppendLine(('        ("{0}", "{1}"),' -f (Escape-CSharpString $Row[0]), (Escape-CSharpString $Row[1])))
}
[void]$Builder.AppendLine('    };')
[void]$Builder.AppendLine('')
[void]$Builder.AppendLine('    private static readonly Dictionary<string, (string Key, string Value)[]> KindDefaultsByInitKey =')
[void]$Builder.AppendLine('        new Dictionary<string, (string Key, string Value)[]>(StringComparer.OrdinalIgnoreCase)')
[void]$Builder.AppendLine('    {')
foreach ($Key in ($KindDefaults.Keys | Sort-Object)) {
    $InitKey = if ($Key -eq '__base__') { '__base__' } else { $Key }
    $Entries = $KindDefaults[$Key]
    [void]$Builder.AppendLine(('        {{ "{0}", new (string Key, string Value)[]' -f (Escape-CSharpString $InitKey)))
    [void]$Builder.AppendLine('        {')
    for ($Index = 0; $Index -lt $Entries.Count; $Index += 2) {
        $PairKey = $Entries[$Index]
        $PairValue = $Entries[$Index + 1]
        [void]$Builder.AppendLine(('            ("{0}", "{1}"),' -f (Escape-CSharpString $PairKey), (Escape-CSharpString $PairValue)))
    }
    [void]$Builder.AppendLine('        }},')
}
[void]$Builder.AppendLine('    };')
[void]$Builder.AppendLine('}')

$Builder.ToString() | Set-Content -Path $OutPath -Encoding UTF8
Write-Output "Wrote $OutPath ($((Get-Item $OutPath).Length) bytes)"
