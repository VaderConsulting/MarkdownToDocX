$RepoRoot = Join-Path $PSScriptRoot '..'
$ShapesPath = Join-Path $RepoRoot 'mermaid\packages\mermaid\src\rendering-util\rendering-elements\shapes.ts'
$OutPath = Join-Path $RepoRoot 'spec-entity-types.generated.md'
$ShapesRel = 'mermaid/packages/mermaid/src/rendering-util/rendering-elements/shapes.ts'

$ShapesText = Get-Content -Path $ShapesPath -Raw

$ShapeRows = @()
$ShapeBlockMatches = [regex]::Matches($ShapesText, 'semanticName:\s*''([^'']+)''[\s\S]*?shortName:\s*''([^'']+)''[\s\S]*?handler:\s*(\w+)')
foreach ($Match in $ShapeBlockMatches) {
    $Line = ($ShapesText.Substring(0, $Match.Index) -split "`n").Count
    $Aliases = ''
    $AliasMatch = [regex]::Match($Match.Value, "aliases:\s*\[([^\]]*)\]")
    if ($AliasMatch.Success) {
        $Aliases = $AliasMatch.Groups[1].Value
    }
    $ShapeRows += [PSCustomObject]@{
        Semantic = $Match.Groups[1].Value
        ShortName = $Match.Groups[2].Value
        Handler = $Match.Groups[3].Value
        Line = $Line
        Aliases = $Aliases
    }
}

$LegacyBracketByShortName = @{
    'rect' = 'A[text]'
    'rounded' = 'A(text)'
    'stadium' = 'A([text])'
    'fr-rect' = 'A[[text]]'
    'subroutine' = 'A[[text]]'
    'cyl' = 'A[(text)]'
    'circle' = 'A((text))'
    'dbl-circ' = 'A(((text)))'
    'diam' = 'A{text}'
    'hex' = 'A{{text}}'
    'odd' = 'A>text]'
}

function Format-MermaidRef {
    param([string]$RelativePath, [string]$LineRange = '')
    $Full = "mermaid/packages/mermaid/src/$RelativePath"
    if ($LineRange) {
        return '`' + $Full + ':' + $LineRange + '`'
    }
    return '`' + $Full + '`'
}

$DiagramKinds = @(
    @('Flowchart (legacy `graph`)', 'flowchart', '`graph`', 'flowchart', 'No', 'diagrams/flowchart/flowDetector.ts:7-18'),
    @('Flowchart (`flowchart`)', 'flowchart-v2', '`flowchart`', 'flowchart', 'No', 'diagrams/flowchart/flowDetector-v2.ts:7-22'),
    @('Flowchart ELK', 'flowchart-elk', '`flowchart-elk`', 'flowchart', 'Lazy-load feature', 'diagrams/flowchart/elk/detector.ts:7'),
    @('Sequence', 'sequence', '`sequenceDiagram`', 'sequence', 'No', 'diagrams/sequence/sequenceDetector.ts:7'),
    @('Class (legacy)', 'class', '`classDiagram` (v1)', 'class', 'No', 'diagrams/class/classDetector.ts:7'),
    @('Class (v2)', 'classDiagram', '`classDiagram`', 'class', 'No', 'diagrams/class/classDetector-V2.ts:7'),
    @('State (legacy)', 'state', '`stateDiagram` (v1)', 'state', 'No', 'diagrams/state/stateDetector.ts:7'),
    @('State (v2)', 'stateDiagram', '`stateDiagram`', 'state', 'No', 'diagrams/state/stateDetector-V2.ts:7'),
    @('Entity Relationship', 'er', '`erDiagram`', 'er', 'No', 'diagrams/er/erDetector.ts:7'),
    @('Gantt', 'gantt', '`gantt`', 'gantt', 'No', 'diagrams/gantt/ganttDetector.ts:7'),
    @('User Journey', 'journey', '`journey`', 'journey', 'No', 'diagrams/user-journey/journeyDetector.ts:7'),
    @('Timeline', 'timeline', '`timeline`', 'timeline', 'No', 'diagrams/timeline/detector.ts:7'),
    @('Git Graph', 'gitGraph', '`gitGraph`', 'gitGraph', 'No', 'diagrams/git/gitGraphDetector.ts:4'),
    @('Pie', 'pie', '`pie`', 'pie', 'No', 'diagrams/pie/pieDetector.ts:7'),
    @('Quadrant Chart', 'quadrantChart', '`quadrantChart`', 'quadrantChart', 'No', 'diagrams/quadrant-chart/quadrantDetector.ts:7'),
    @('XY Chart', 'xychart', '`xychart-beta` / xychart', 'xyChart', 'No', 'diagrams/xychart/xychartDetector.ts:7'),
    @('Requirement', 'requirement', '`requirementDiagram`', 'requirement', 'No', 'diagrams/requirement/requirementDetector.ts:7'),
    @('Architecture', 'architecture', '`architecture-beta`', 'architecture', 'Lazy-load feature', 'diagrams/architecture/architectureDetector.ts:7'),
    @('Mindmap', 'mindmap', '`mindmap`', 'mindmap', 'Lazy-load feature', 'diagrams/mindmap/detector.ts:6-9'),
    @('Kanban', 'kanban', '`kanban`', 'kanban', 'No', 'diagrams/kanban/detector.ts:6'),
    @('Sankey', 'sankey', '`sankey-beta` / sankey', 'sankey', 'No', 'diagrams/sankey/sankeyDetector.ts:3'),
    @('Packet', 'packet', '`packet-beta` / packet', 'packet', 'No', 'diagrams/packet/detector.ts:7'),
    @('Block', 'block', '`block-beta` / block', 'block', 'No', 'diagrams/block/blockDetector.ts:3'),
    @('Event Modeling', 'eventmodeling', '`eventmodeling`', 'eventmodeling', 'No', 'diagrams/eventmodeling/detector.ts:4'),
    @('Tree View', 'treeView', '`treeview` / treeView', 'treeView', 'No', 'diagrams/treeView/detector.ts:4'),
    @('Radar', 'radar', '`radar-beta` / radar', 'radar', 'No', 'diagrams/radar/detector.ts:7'),
    @('Ishikawa', 'ishikawa', '`ishikawa`', 'ishikawa', 'No', 'diagrams/ishikawa/ishikawaDetector.ts:7'),
    @('Treemap', 'treemap', '`treemap`', '(treemap in defaultConfig.ts only)', 'No', 'diagrams/treemap/detector.ts:7'),
    @('Venn', 'venn', '`venn`', 'venn', 'No', 'diagrams/venn/vennDetector.ts:7'),
    @('Wardley', 'wardley-beta', '`wardley-beta`', 'wardley-beta', 'Yes (beta id)', 'diagrams/wardley/wardleyDetector.ts:7'),
    @('C4', 'c4', '`C4Context` / `C4Container` / etc.', 'c4', 'No', 'diagrams/c4/c4Detector.ts:7'),
    @('Info', 'info', '`info`', '(none)', 'No', 'diagrams/info/infoDetector.ts:7'),
    @('YAML front-matter trap', '`---`', 'starts with `---`', '(none)', 'No', 'diagram-api/diagram-orchestration.ts:49-77')
)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('This section documents **all entity types** the recogniser and renderer must be able to represent, classify, or diagnose. Every row includes a vendored Mermaid source reference. Diagram registration order is defined in `mermaid/packages/mermaid/src/diagram-api/diagram-orchestration.ts:84-115`.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('##### 3.18.1 Recognition contract model entities')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Contract entity | Role | Required fields (summary) | Spec reference |')
[void]$sb.AppendLine('| --- | --- | --- | --- |')
[void]$sb.AppendLine('| `DiagramContract` | Root per Mermaid block | Kind, Header, Direction, Config, collections, Diagnostics | spec.md section 4 |')
[void]$sb.AppendLine('| `NodeContract` | Vertex / node | Id, Declarations[], Label, LabelType, Shape, ParentGroupId?, Classes, Styles, Metadata | spec.md section 4.1 |')
[void]$sb.AppendLine('| `EdgeContract` | Directed link | Id, endpoints, markers, stroke, minlen, label, CreationIndex, ExpansionTrace | spec.md sections 4.2, 3.8, 7.1.1 |')
[void]$sb.AppendLine('| `GroupContract` | Subgraph / cluster | Id, Title, parent/children, Direction?, Classes | spec.md section 4.3 |')
[void]$sb.AppendLine('| `ClassContract` | classDef | Class id, style list | spec.md section 3.12 |')
[void]$sb.AppendLine('| `StyleMutation` | style / linkStyle / class | Target ids, merged styles | spec.md section 3.12 |')
[void]$sb.AppendLine('| `InteractionContract` | click / href / tooltip | Target id, callback or url, target | spec.md section 3.14 |')
[void]$sb.AppendLine('| `AccessibilityContract` | accTitle / accDescr | Title and description text | spec.md section 3.15 |')
[void]$sb.AppendLine('| `Diagnostic` | unknown vs unsupported | Severity, Code, Message, SourceSpan | spec.md section 4.4 |')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('##### 3.18.2 Mermaid diagram kinds (detector registration)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Diagram kind | Detector `id` | Typical source header | `%%{init}%%` key | Notes | Reference |')
[void]$sb.AppendLine('| --- | --- | --- | --- | --- | --- |')
foreach ($Row in $DiagramKinds) {
    [void]$sb.AppendLine("| $($Row[0]) | $($Row[1]) | $($Row[2]) | $($Row[3]) | $($Row[4]) | $(Format-MermaidRef -RelativePath $Row[5]) |")
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('##### 3.18.3 Flowchart vertex shape entities (`@{ shape: ... }` and legacy brackets)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Canonical shape ids are defined in `shapesDefs` and exposed through `shapes` / `isValidShape` (`shapes.ts:92-542`).')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Semantic name | Canonical `shortName` | Legacy bracket (if any) | Public aliases (subset) | Reference |')
[void]$sb.AppendLine('| --- | --- | --- | --- | --- |')
foreach ($Row in $ShapeRows) {
    $AliasText = if ($Row.Aliases) { $Row.Aliases } else { '(none listed)' }
    $AliasCell = '`' + $AliasText + '`'
    $LegacyText = if ($LegacyBracketByShortName.ContainsKey($Row.ShortName)) {
        $LegacyBracketByShortName[$Row.ShortName]
    }
    else {
        '(metadata only; or A@{ shape: ... })'
    }
    [void]$sb.AppendLine('| ' + $Row.Semantic + ' | `' + $Row.ShortName + '` | ' + $LegacyText + ' | ' + $AliasCell + ' | `' + $ShapesRel + ':' + $Row.Line + '` |')
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Additional renderer-only shape handlers (not in `shapesDefs` but registered in `undocumentedShapes`):')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Shape id | Used by diagram | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
$Undocumented = @(
    @('state, choice, note', 'state diagram', '487-491'),
    @('icon, iconSquare, iconCircle, iconRounded, imageSquare, anchor', 'flowchart metadata', '497-503'),
    @('kanbanItem', 'kanban', '505-506'),
    @('mindmapCircle, defaultMindmapNode', 'mindmap', '508-510'),
    @('classBox', 'class', '511-512'),
    @('erBox', 'er', '514-515'),
    @('requirementBox', 'requirement', '517-518')
)
foreach ($U in $Undocumented) {
    [void]$sb.AppendLine('| ' + $U[0] + ' | ' + $U[1] + ' | `' + $ShapesRel + ':' + $U[2] + '` |')
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('##### 3.18.4 Flowchart edge / line entity types')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('See sections 3.8, 3.17.38, and 7.1.1. Normalised edge fields:')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Field | Allowed values | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
[void]$sb.AppendLine('| `startArrow` / `endArrow` | none, point, circle, cross | spec.md 3.8; ' + (Format-MermaidRef -RelativePath 'diagrams/flowchart/flowDb.ts' -LineRange '998-1011') + ' |')
[void]$sb.AppendLine('| `stroke` | normal, dotted, thick, invisible | spec.md 3.8; ' + (Format-MermaidRef -RelativePath 'diagrams/flowchart/flowDb.ts' -LineRange '832-901') + ' |')
[void]$sb.AppendLine('| `minlen` | integer 1..10 (from token length) | ' + (Format-MermaidRef -RelativePath 'diagrams/flowchart/flowDb.ts' -LineRange '304') + ' |')
[void]$sb.AppendLine('| `labelType` | text, string, markdown | spec.md 3.4 |')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('##### 3.18.5 Per-diagram domain entity types')
[void]$sb.AppendLine('')

# C4
$C4Ref = (Format-MermaidRef -RelativePath 'diagrams/c4/parser/c4Diagram.jison' -LineRange '277-309')
[void]$sb.AppendLine("**C4** ($C4Ref, $(Format-MermaidRef -RelativePath 'diagrams/c4/c4Db.js'))")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Category | `typeC4Shape` / rel tokens (complete set) | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
[void]$sb.AppendLine("| C4 element shapes | person, external_person, system, system_db, system_queue, external_system, external_system_db, external_system_queue, container, container_db, container_queue, external_container, external_container_db, external_container_queue, component, component_db, component_queue, external_component, external_component_db, external_component_queue | $C4Ref |")
[void]$sb.AppendLine("| C4 boundaries | ENTERPRISE boundary, SYSTEM boundary, BOUNDARY, CONTAINER boundary | $C4Ref |")
[void]$sb.AppendLine("| C4 relationships | rel, birel, rel_u, rel_d, rel_l, rel_r, rel_b | $C4Ref |")
[void]$sb.AppendLine('')

# Block
$BlockRef = (Format-MermaidRef -RelativePath 'diagrams/block/blockTypes.ts' -LineRange '3-30')
[void]$sb.AppendLine("**Block** ($BlockRef)")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| `BlockType` union members | Reference |')
[void]$sb.AppendLine('| --- | --- |')
[void]$sb.AppendLine("| na, column-setting, edge, round, block_arrow, space, square, diamond, hexagon, odd, lean_right, lean_left, trapezoid, inv_trapezoid, rect_left_inv_arrow, odd_right, circle, ellipse, stadium, subroutine, cylinder, group, doublecircle, classDef, applyClass, applyStyles, composite | $BlockRef |")
[void]$sb.AppendLine('')

# Architecture
$ArchRef = (Format-MermaidRef -RelativePath 'diagrams/architecture/architectureTypes.ts')
[void]$sb.AppendLine("**Architecture** ($ArchRef)")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Entity | `type` field | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
[void]$sb.AppendLine('| Service node | `service` | ' + (Format-MermaidRef -RelativePath 'diagrams/architecture/architectureTypes.ts' -LineRange '193-203') + ' |')
[void]$sb.AppendLine('| Junction node | `junction` | ' + (Format-MermaidRef -RelativePath 'diagrams/architecture/architectureTypes.ts' -LineRange '205-212') + ' |')
[void]$sb.AppendLine("| Group | (group record, no type enum) | $(Format-MermaidRef -RelativePath 'diagrams/architecture/architectureTypes.ts' -LineRange '226-231') |")
[void]$sb.AppendLine("| Edge | direction pair L/R/T/B | $(Format-MermaidRef -RelativePath 'diagrams/architecture/architectureTypes.ts' -LineRange '233-243') |")
[void]$sb.AppendLine('')

# Requirement
$ReqTypesRef = (Format-MermaidRef -RelativePath 'diagrams/requirement/types.ts' -LineRange '1-45')
[void]$sb.AppendLine("**Requirement** ($ReqTypesRef)")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Entity | Values | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
[void]$sb.AppendLine('| `Requirement` | RequirementType: Requirement, Functional Requirement, Interface Requirement, Performance Requirement, Physical Requirement, Design Constraint | ' + (Format-MermaidRef -RelativePath 'diagrams/requirement/types.ts' -LineRange '1-22') + ' |')
[void]$sb.AppendLine('| `Element` | name, type (free text), docRef | ' + (Format-MermaidRef -RelativePath 'diagrams/requirement/types.ts' -LineRange '39-45') + ' |')
[void]$sb.AppendLine('| `Relation` | contains, copies, derives, satisfies, verifies, refines, traces | ' + (Format-MermaidRef -RelativePath 'diagrams/requirement/types.ts' -LineRange '24-37') + ' |')
[void]$sb.AppendLine('| `RiskLevel` | Low, Medium, High | ' + (Format-MermaidRef -RelativePath 'diagrams/requirement/types.ts' -LineRange '9') + ' |')
[void]$sb.AppendLine('| `VerifyType` | Analysis, Demonstration, Inspection, Test | ' + (Format-MermaidRef -RelativePath 'diagrams/requirement/types.ts' -LineRange '11') + ' |')
[void]$sb.AppendLine('')

# Sequence (common)
$SeqRef = (Format-MermaidRef -RelativePath 'diagrams/sequence/sequenceDb.ts')
[void]$sb.AppendLine("**Sequence** ($SeqRef)")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Entity | Reference |')
[void]$sb.AppendLine('| --- | --- |')
[void]$sb.AppendLine('| Actor / participant | ' + $SeqRef + ' (`addActor`) |')
[void]$sb.AppendLine("| Message | $SeqRef message list |")
[void]$sb.AppendLine("| Note, loop, alt, opt, par, rect, activate/deactivate | $(Format-MermaidRef -RelativePath 'diagrams/sequence/parser/sequenceDiagram.jison') |")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('**Other diagram kinds (compact index):**')
[void]$sb.AppendLine('')

# ER, State, Class, Gantt, Venn, Sankey, Git, Event modeling, Kanban, Mindmap, Pie, Packet, Radar, TreeView, Quadrant, XY, Timeline, Journey, Ishikawa, Wardley - compact tables
$PerDiagram = @(
    @('Sequence', 'Actor / participant, Message, Note, loop, alt, opt, par, rect, activate/deactivate', 'diagrams/sequence/sequenceDb.ts', 'diagrams/sequence/parser/sequenceDiagram.jison'),
    @('ER', 'Entity, Attribute, Relationship', 'diagrams/er/erDb.ts', 'rendering-util/rendering-elements/shapes.ts:514-515'),
    @('State', 'State, composite, choice, fork, join, note', 'diagrams/state/stateDb.ts', 'rendering-util/rendering-elements/shapes.ts:487-491'),
    @('Class', 'Class, Interface, Namespace, relationship', 'diagrams/class/classDb.ts', 'rendering-util/rendering-elements/shapes.ts:511-512'),
    @('Gantt', 'Section, Task, Milestone, excludes', 'diagrams/gantt/ganttDb.js', ''),
    @('Venn', 'Set region, intersection item (id, sets[], label)', 'diagrams/venn/parser', 'diagrams/venn/venn.spec.ts'),
    @('Sankey', 'Node, Link (gradient/source/target colour)', 'diagrams/sankey/sankeyDb.ts', ''),
    @('GitGraph', 'Commit, Branch, Tag, CherryPick, Merge', 'diagrams/git/gitGraphAst.ts', ''),
    @('Event modeling', 'Frame kinds: cmd/command, evt/event, pcr/processor, rmo/readmodel; swimlanes Command/Read Model and Events', 'diagrams/eventmodeling/db.ts:96-120', 'themes/theme-default.js:418-425'),
    @('Kanban', 'Section, Ticket', 'diagrams/kanban/kanbanDb.ts', 'rendering-util/rendering-elements/shapes.ts:505-506'),
    @('Mindmap', 'Node (hierarchy, mindmapCircle shape)', 'diagrams/mindmap/mindmapDb.ts:239-269', 'rendering-util/rendering-elements/shapes.ts:508-510'),
    @('Pie', 'Slice (label, value)', 'diagrams/pie/pieDb.ts', ''),
    @('Packet', 'Row, Bit field', 'diagrams/packet/packetDb.ts', ''),
    @('Radar', 'Axis, Curve, Legend entry', 'diagrams/radar/radarDb.ts', ''),
    @('TreeView', 'Tree node', 'diagrams/treeView/treeViewDb.ts', ''),
    @('QuadrantChart', 'Quadrant, Point, Axis labels', 'diagrams/quadrant-chart/quadrantDb.ts', ''),
    @('XYChart', 'Series, Axis, Bar/Line plot', 'diagrams/xychart/chartBuilder/', ''),
    @('Timeline', 'Section, Event', 'diagrams/timeline/timelineDb.ts', ''),
    @('Journey', 'Section, Task', 'diagrams/user-journey/journeyDb.ts', ''),
    @('Ishikawa', 'Category, Cause', 'diagrams/ishikawa/ishikawaDb.ts', ''),
    @('Wardley (beta)', 'Node, Axis, Anchor', 'diagrams/wardley/wardleyDb.ts', ''),
    @('Treemap', 'Hierarchy node (width/height/value)', 'diagrams/treemap/', 'defaultConfig.ts:283-294'),
    @('Info', 'Info / showInfo box', 'diagrams/info/', '')
)
[void]$sb.AppendLine('| Diagram | Domain entities (recogniser must classify) | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
foreach ($D in $PerDiagram) {
    $RefParts = @((Format-MermaidRef -RelativePath $D[2]))
    if ($D[3]) {
        if ($D[3] -match '^\d') {
            $RefParts += (Format-MermaidRef -RelativePath $D[3])
        }
        elseif ($D[3] -match ':') {
            $Split = $D[3].Split(':', 2)
            $RefParts += (Format-MermaidRef -RelativePath $Split[0] -LineRange $Split[1])
        }
        else {
            $RefParts += (Format-MermaidRef -RelativePath $D[3])
        }
    }
    [void]$sb.AppendLine("| $($D[0]) | $($D[1]) | $($RefParts -join '; ') |")
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('##### 3.18.6 Naiad `DiagramType` enum (native renderer subset)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Naiad currently exposes a smaller native enum than full Mermaid (`extern/Naiad/src/Naiad/DiagramType.cs`). Unlisted Mermaid kinds must be recognised in the contract and handled via diagnostics or official fallback until native support exists.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| `DiagramType` | Mermaid detector id | Reference |')
[void]$sb.AppendLine('| --- | --- | --- |')
$NaiadTypes = @(
    @('Flowchart', 'flowchart / flowchart-v2'),
    @('Sequence', 'sequence'),
    @('Class', 'class / classDiagram'),
    @('State', 'state / stateDiagram'),
    @('EntityRelationship', 'er'),
    @('Gantt', 'gantt'),
    @('Pie', 'pie'),
    @('GitGraph', 'gitGraph'),
    @('Mindmap', 'mindmap'),
    @('Timeline', 'timeline'),
    @('C4Context', 'C4Context'),
    @('C4Container', 'C4Container'),
    @('C4Component', 'C4Component'),
    @('C4Deployment', 'C4Deployment'),
    @('Block', 'block'),
    @('Kanban', 'kanban'),
    @('Quadrant', 'quadrantChart'),
    @('Requirement', 'requirement'),
    @('Sankey', 'sankey'),
    @('UserJourney', 'journey'),
    @('XYChart', 'xychart'),
    @('Architecture', 'architecture'),
    @('Packet', 'packet'),
    @('Radar', 'radar'),
    @('Treemap', 'treemap'),
    @('ZenUML', '(not in diagram-orchestration.ts 11.14)')
)
foreach ($N in $NaiadTypes) {
    [void]$sb.AppendLine("| $($N[0]) | $($N[1]) | ``extern/Naiad/src/Naiad/DiagramType.cs`` |")
}

$sb.ToString() | Set-Content -Path $OutPath -Encoding UTF8
Write-Output "Wrote $OutPath ($($ShapeRows.Count) flowchart shapes)"
