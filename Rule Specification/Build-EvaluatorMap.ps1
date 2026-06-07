# Generates evaluator-map.json for every rule in ruleset.json.
$ErrorActionPreference = 'Stop'
$SpecDirectory = $PSScriptRoot
$RulesetPath = Join-Path $SpecDirectory 'ruleset.json'
$OutputPath = Join-Path $SpecDirectory 'evaluator-map.json'

$Ruleset = Get-Content -Raw -Path $RulesetPath | ConvertFrom-Json
$Bindings = @()

foreach ($Rule in $Ruleset.Rules)
{
    $EvaluatorType = switch -Regex ($Rule.RuleGroup)
    {
        'AntiPatterns' { 'StaticAnalysis' }
        'Recognition' { 'Recognition' }
        'Layout|NeighbourRelationships' { 'Layout' }
        default { 'Geometry' }
    }

    $ToleranceProfileId = switch -Regex ($Rule.RuleId)
    {
        'Routing\.Obstacle\.NoNodeBodyIntersection' { 'StrictIntersection' }
        'Routing\.Label\.' { 'LabelAttachment' }
        'Routing\.Path\.OrthogonalSegments' { 'OrthogonalPath' }
        'Routing\.Path\.Straightness' { 'StraightPath' }
        'Render\.Marker\.' { 'MarkerPlacement' }
        'Diagnostics\.' { 'None' }
        default { 'DefaultGeometry' }
    }

    [string[]]$RequiredFields = switch ($Rule.AppliesTo)
    {
        'Node' { ,@('Nodes[].NodeId', 'Nodes[].Bounds', 'Nodes[].Rank', 'Nodes[].Lane') }
        'Edge' { ,@('Edges[].EdgeId', 'Edges[].Path.Points', 'Edges[].SourceNodeId', 'Edges[].TargetNodeId', 'Edges[].RouteClass') }
        'EdgeSet' { ,@('Edges[].EdgeId', 'Edges[].RouteClass', 'Nodes[].NodeId') }
        'Group' { ,@('Groups[].GroupId', 'Groups[].Bounds', 'Nodes[].NodeId') }
        'Implementation' { ,@('Implementation.StaticAnalysis.Selectors') }
        default { ,@('DiagramId', 'Kind', 'Direction', 'Recognition', 'Nodes', 'Edges') }
    }

    $Bindings += [ordered]@{
        RuleId = $Rule.RuleId
        EvaluatorId = 'RequireFactEvaluator'
        EvaluatorType = $EvaluatorType
        InputModel = 'ActualResults'
        AppliesTo = $Rule.AppliesTo
        RequiredFields = $RequiredFields
        ToleranceProfileId = $ToleranceProfileId
        FailureCode = ($Rule.RuleId -replace '\.', '_').ToUpperInvariant()
    }
}

$EvaluatorMap = [ordered]@{
    SchemaVersion = '2.0.0'
    Name = 'MermaidFlowchartEvaluatorMap'
    EvaluatorBindings = $Bindings
}

$EvaluatorMap | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath with $($Bindings.Count) bindings."
