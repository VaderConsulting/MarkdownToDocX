# Generates ruleset.json (v2.0.0) from the validation specification and ruleset-org.json catalogue.
$ErrorActionPreference = 'Stop'
$OutputPath = Join-Path $PSScriptRoot 'ruleset.json'
$OrgPath = Join-Path $PSScriptRoot 'ruleset-org.json'

$OrgData = Get-Content -Raw -Path $OrgPath | ConvertFrom-Json
$OrgDescriptions = @{}
foreach ($GenericRule in $OrgData.GenericRules)
{
    $OrgDescriptions[$GenericRule.RuleId] = $GenericRule.Description
}

function New-Rule
{
    param(
        [string]$RuleId,
        [string]$RuleGroup,
        [string]$AppliesTo,
        [string]$Severity,
        [string]$Description,
        [object]$When,
        [hashtable]$Require,
        [string[]]$FailWhen,
        [string]$DiagnosticTemplate,
        [string]$SuggestedCorrectionTemplate,
        [string]$LegacyRuleId = $null
    )

    $Rule = [ordered]@{
        RuleId = $RuleId
        RuleGroup = $RuleGroup
        AppliesTo = $AppliesTo
        Severity = $Severity
        Description = $Description
        When = $When
        Require = $Require
        FailWhen = $FailWhen
        DiagnosticTemplate = $DiagnosticTemplate
        SuggestedCorrectionTemplate = $SuggestedCorrectionTemplate
    }

    if ($LegacyRuleId -and $OrgDescriptions.ContainsKey($LegacyRuleId))
    {
        $Rule.Description = $OrgDescriptions[$LegacyRuleId]
    }

    return $Rule
}

function New-Condition
{
    param(
        [string]$Field,
        [string]$Operator,
        $Value = $null
    )

    $Condition = [ordered]@{
        Field = $Field
        Operator = $Operator
    }

    if ($null -ne $Value)
    {
        $Condition.Value = $Value
    }

    return $Condition
}

function New-All
{
    param([object[]]$Conditions)
    return [ordered]@{ All = $Conditions }
}

function New-Any
{
    param([object[]]$Conditions)
    return [ordered]@{ Any = $Conditions }
}

$VisibleEdgeWhen = New-All @(
    (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
)

$ConnectedNodeWhen = New-All @(
    (New-Condition -Field 'Node.IsRendered' -Operator 'Equals' -Value $true),
    (New-Condition -Field 'Node.IsConnected' -Operator 'Equals' -Value $true)
)

$Rules = @()

# --- 11.1 Recognition ---
$Rules += New-Rule `
    -RuleId 'Recognition.Header.FlowchartFamily' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Diagram' `
    -Severity 'Error' `
    -Description 'graph, flowchart, and flowchart-elk are recognised as flowchart-compatible inputs.' `
    -When (New-All @((New-Condition -Field 'Diagram.Kind' -Operator 'In' -Value @('flowchart', 'graph', 'flowchart-elk')))) `
    -Require @{ 'Recognition.FlowchartFamilyRecognised' = $true } `
    -FailWhen @('UnrecognisedDiagramKind') `
    -DiagnosticTemplate 'Diagram {DiagramId} uses unsupported header kind {Kind}.' `
    -SuggestedCorrectionTemplate 'Recognise graph, flowchart, and flowchart-elk as flowchart-compatible diagram kinds.' `
    -LegacyRuleId 'Header.Flowchart'

$Rules += New-Rule `
    -RuleId 'Recognition.Direction.Canonical' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Diagram' `
    -Severity 'Error' `
    -Description 'TD normalises to TB; symbolic directions map to canonical values.' `
    -When (New-All @((New-Condition -Field 'Diagram.Direction' -Operator 'Exists'))) `
    -Require @{ 'Direction.IsCanonical' = $true } `
    -FailWhen @('NonCanonicalDirection') `
    -DiagnosticTemplate 'Diagram {DiagramId} direction {RawDirection} did not normalise to {ExpectedDirection}.' `
    -SuggestedCorrectionTemplate 'Map TD to TB and symbolic direction tokens to canonical TB, BT, LR, or RL.' `
    -LegacyRuleId 'Direction.Symbolic'

$Rules += New-Rule `
    -RuleId 'Recognition.Node.IdentityMerge' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Repeated declarations merge into one node contract.' `
    -When (New-All @((New-Condition -Field 'Node.DeclarationCount' -Operator 'GreaterThan' -Value 1))) `
    -Require @{ 'Node.IdentityMerged' = $true } `
    -FailWhen @('DuplicateNodeFromRepeatedDeclaration') `
    -DiagnosticTemplate 'Node {NodeId} was declared {DeclarationCount} times without merging into one contract.' `
    -SuggestedCorrectionTemplate 'Merge repeated node declarations into a single node contract.' `
    -LegacyRuleId 'Recognition.Node.RepeatedDeclarationMerge'

$Rules += New-Rule `
    -RuleId 'Recognition.Node.ShapeCanonicalisation' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Mermaid node syntax and metadata shapes map to canonical shape intent.' `
    -When (New-All @((New-Condition -Field 'Node.ShapeSource' -Operator 'Exists'))) `
    -Require @{ 'Node.ShapeCanonicalised' = $true } `
    -FailWhen @('UnmappedNodeShape') `
    -DiagnosticTemplate 'Node {NodeId} shape source {ShapeSource} did not map to canonical shape {ExpectedShape}.' `
    -SuggestedCorrectionTemplate 'Map Mermaid node syntax and metadata shapes to canonical shape intent.'

$Rules += New-Rule `
    -RuleId 'Recognition.Edge.TokenDecomposition' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Edge token decomposes into stroke, arrow start, arrow end, directionality, and min length.' `
    -When (New-All @((New-Condition -Field 'Edge.LinkToken' -Operator 'Exists'))) `
    -Require @{ 'Edge.TokenDecomposed' = $true } `
    -FailWhen @('IncompleteEdgeTokenDecomposition') `
    -DiagnosticTemplate 'Edge {EdgeId} link token {LinkToken} did not decompose into stroke and marker facts.' `
    -SuggestedCorrectionTemplate 'Decompose the edge token into stroke, arrow start, arrow end, directionality, and min length.' `
    -LegacyRuleId 'Recognition.Edge.TokenDecomposition'

$Rules += New-Rule `
    -RuleId 'Recognition.Edge.Expansion' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Chained and grouped expressions expand into individual logical edges.' `
    -When (New-All @((New-Condition -Field 'Edge.RequiresExpansion' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Edge.ExpandedToLogicalEdges' = $true } `
    -FailWhen @('UnexpandedEdgeExpression') `
    -DiagnosticTemplate 'Edge expression for {EdgeId} was not expanded into individual logical edges.' `
    -SuggestedCorrectionTemplate 'Expand chained and grouped edge expressions into separate logical edges.' `
    -LegacyRuleId 'Recognition.Edge.ChainExpansion'

$Rules += New-Rule `
    -RuleId 'Recognition.Edge.MinLengthFromToken' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'MinLength is derived from the link token, not label width.' `
    -When (New-All @((New-Condition -Field 'Edge.LinkToken' -Operator 'Exists'))) `
    -Require @{ 'Edge.MinLengthFromTokenOnly' = $true } `
    -FailWhen @('MinLengthDerivedFromLabelWidth') `
    -DiagnosticTemplate 'Edge {EdgeId} min length was influenced by label width instead of the link token.' `
    -SuggestedCorrectionTemplate 'Derive MinLength only from the link token hyphen count.' `
    -LegacyRuleId 'Recognition.Edge.MinLengthFromToken'

$Rules += New-Rule `
    -RuleId 'Recognition.Subgraph.Membership' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Group' `
    -Severity 'Error' `
    -Description 'Subgraphs create groups with deterministic parent/child membership.' `
    -When (New-All @((New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Group.MembershipDeterministic' = $true } `
    -FailWhen @('AmbiguousSubgraphMembership') `
    -DiagnosticTemplate 'Group {GroupId} has ambiguous or duplicated subgraph membership.' `
    -SuggestedCorrectionTemplate 'Assign each node to one effective parent group with deterministic nested ownership.' `
    -LegacyRuleId 'Recognition.Subgraph.Basic'

$Rules += New-Rule `
    -RuleId 'Recognition.Style.Mutations' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Diagram' `
    -Severity 'Warning' `
    -Description 'style, classDef, class, linkStyle, and inline classes are preserved as mutations.' `
    -When (New-All @((New-Condition -Field 'Diagram.HasStyleMutations' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Recognition.StyleMutationsPreserved' = $true } `
    -FailWhen @('LostStyleMutation') `
    -DiagnosticTemplate 'Diagram {DiagramId} lost style mutation {MutationKind} for target {TargetId}.' `
    -SuggestedCorrectionTemplate 'Preserve style, classDef, class, linkStyle, and inline class mutations in the recognition contract.' `
    -LegacyRuleId 'Recognition.Style.StyleDirective'

$Rules += New-Rule `
    -RuleId 'Recognition.Unsupported.MustDiagnose' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Diagram' `
    -Severity 'Error' `
    -Description 'Recognised but unsupported constructs must emit diagnostics.' `
    -When (New-All @((New-Condition -Field 'Recognition.HasUnsupportedConstruct' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Diagnostics.UnsupportedConstructReported' = $true } `
    -FailWhen @('UnsupportedConstructSilenced') `
    -DiagnosticTemplate 'Diagram {DiagramId} contains unsupported construct {ConstructKind} without a diagnostic.' `
    -SuggestedCorrectionTemplate 'Emit a structured diagnostic for every recognised but unsupported construct.'

# --- 11.2 Layout ---
$Rules += New-Rule `
    -RuleId 'Layout.Node.BoundsPresent' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Every rendered node has non-empty bounds and centre co-ordinates.' `
    -When (New-All @((New-Condition -Field 'Node.IsRendered' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Node.BoundsPresent' = $true; 'Node.CentrePresent' = $true } `
    -FailWhen @('MissingNodeBounds', 'MissingNodeCentre') `
    -DiagnosticTemplate 'Node {NodeId} is rendered without complete bounds or centre co-ordinates.' `
    -SuggestedCorrectionTemplate 'Assign non-empty bounds and centre co-ordinates to every rendered node.'

$Rules += New-Rule `
    -RuleId 'Layout.Node.RankLanePresent' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Every connected node has rank and lane assignment.' `
    -When $ConnectedNodeWhen `
    -Require @{ 'Node.RankPresent' = $true; 'Node.LanePresent' = $true } `
    -FailWhen @('MissingNodeRank', 'MissingNodeLane') `
    -DiagnosticTemplate 'Connected node {NodeId} is missing rank or lane assignment.' `
    -SuggestedCorrectionTemplate 'Assign rank and lane values to every connected node in the layout model.'

$Rules += New-Rule `
    -RuleId 'Layout.Node.PrimaryDirectionOrdering' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'For primary-flow edges, source and target ordering matches diagram direction.' `
    -When (New-All @(
        (New-Condition -Field 'Edge.IsPrimaryFlow' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.PrimaryDirectionOrderingValid' = $true } `
    -FailWhen @('PrimaryFlowOrderingViolation') `
    -DiagnosticTemplate 'Primary-flow edge {EdgeId} ordering conflicts with diagram direction {Direction}.' `
    -SuggestedCorrectionTemplate 'Ensure primary-flow source and target ordering follows the diagram direction.'

$Rules += New-Rule `
    -RuleId 'Layout.DisconnectedComponentsFollowFlowAxis' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Diagram' `
    -Severity 'Error' `
    -Description 'Disconnected single-node components stack on the flow axis in declaration order (F12 LR, F82 TD).' `
    -When (New-All @(
        (New-Condition -Field 'Layout.HasDisconnectedSingleNodeComponents' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.DisconnectedComponentsFollowFlowAxis' = $true } `
    -FailWhen @('DisconnectedComponentOrderingViolation') `
    -DiagnosticTemplate 'Diagram {DiagramId} disconnected nodes are not stacked on the flow axis in declaration order.' `
    -SuggestedCorrectionTemplate 'Pack disconnected single-node components along the diagram flow axis in source declaration order.'

$Rules += New-Rule `
    -RuleId 'Layout.Node.NeighbourRelativePosition' `
    -RuleGroup 'NeighbourRelationships' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Above/below/left/right/aligned neighbour facts match node centres and direction.' `
    -When $ConnectedNodeWhen `
    -Require @{ 'Node.NeighbourFactsConsistent' = $true } `
    -FailWhen @('NeighbourFactMismatch') `
    -DiagnosticTemplate 'Node {NodeId} neighbour fact {NeighbourFact} conflicts with centre positions.' `
    -SuggestedCorrectionTemplate 'Reconcile neighbour facts with node centre positions and diagram direction.'

$Rules += New-Rule `
    -RuleId 'Layout.Node.NoOverlap' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Unrelated node bodies must not overlap beyond tolerance.' `
    -When (New-All @(
        (New-Condition -Field 'Node.IsRendered' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'NodePair.IsUnrelated' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.NodeBodiesDoNotOverlap' = $true } `
    -FailWhen @('NodeBodyOverlap') `
    -DiagnosticTemplate 'Node {NodeId} overlaps unrelated node {OtherNodeId} beyond tolerance.' `
    -SuggestedCorrectionTemplate 'Adjust layout so unrelated node bodies do not overlap beyond tolerance.'

$Rules += New-Rule `
    -RuleId 'Layout.Node.GroupContainment' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'A group member must lie within its group bounds, allowing configured padding.' `
    -When (New-All @(
        (New-Condition -Field 'Node.ParentGroupId' -Operator 'Exists'),
        (New-Condition -Field 'Node.IsRendered' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.GroupContainmentValid' = $true } `
    -FailWhen @('NodeOutsideGroupBounds') `
    -DiagnosticTemplate 'Node {NodeId} lies outside parent group {GroupId} bounds.' `
    -SuggestedCorrectionTemplate 'Contain group members within group bounds using configured padding.'

$Rules += New-Rule `
    -RuleId 'Layout.Subgraph.SiblingMinimumGap' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Group' `
    -Severity 'Error' `
    -Description 'Sibling subgraph cluster boxes must maintain a minimum exterior gap on the primary flow axis (24px, including padding and title band clearance).' `
    -When (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasSiblingSubgraph' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.SiblingSubgraphGapValid' = $true } `
    -FailWhen @('SiblingSubgraphGapViolation') `
    -DiagnosticTemplate 'Subgraph {GroupId} is too close to a sibling subgraph on the primary flow axis.' `
    -SuggestedCorrectionTemplate 'Increase spacing between sibling subgraph cluster boxes so the exterior gap is at least 24px on the flow axis.'

$Rules += New-Rule `
    -RuleId 'Layout.Subgraph.ExternalMinimumGap' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Group' `
    -Severity 'Error' `
    -Description 'External nodes linked across a subgraph boundary must maintain a minimum gap from the cluster box on the primary flow axis (24px, including padding and title band clearance).' `
    -When (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasExternalBoundaryNode' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.ExternalSubgraphGapValid' = $true } `
    -FailWhen @('ExternalSubgraphGapViolation') `
    -DiagnosticTemplate 'Subgraph {GroupId} is too close to an external node on the primary flow axis.' `
    -SuggestedCorrectionTemplate 'Increase spacing between external nodes and the subgraph cluster box so the exterior gap is at least 24px on the flow axis.'

$Rules += New-Rule `
    -RuleId 'Layout.Subgraph.NestedClusterContainment' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Group' `
    -Severity 'Error' `
    -Description 'A nested child subgraph cluster must sit inside its parent cluster box with at least 24px exterior gap on the cross-flow axis and at least rankSpacing (default 50px) on the primary flow axis.' `
    -When (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasNestedChildSubgraph' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.NestedSubgraphContainmentValid' = $true } `
    -FailWhen @('NestedSubgraphContainmentViolation') `
    -DiagnosticTemplate 'Parent subgraph {GroupId} does not fully contain a nested child cluster with the required exterior gap.' `
    -SuggestedCorrectionTemplate 'Expand the parent cluster box so each nested child subgraph border respects the configured cross-flow (24px) and flow-axis (rankSpacing) exterior gaps.'

$Rules += New-Rule `
    -RuleId 'Layout.Subgraph.DirectMemberNestedSeparation' `
    -RuleGroup 'Layout' `
    -AppliesTo 'Group' `
    -Severity 'Error' `
    -Description 'Direct member nodes in a parent subgraph must maintain at least rankSpacing (default 50px) clearance from nested child subgraph cluster boxes on the primary flow axis.' `
    -When (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasDirectMemberWithNestedChildSubgraph' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Layout.DirectMemberNestedGapValid' = $true } `
    -FailWhen @('DirectMemberNestedSubgraphGapViolation') `
    -DiagnosticTemplate 'Parent subgraph {GroupId} places direct member nodes too close to a nested child cluster on the primary flow axis.' `
    -SuggestedCorrectionTemplate 'Shift direct member nodes outside nested child cluster boxes so the flow-axis exterior gap is at least rankSpacing.'

# --- 11.3 Route classification ---
$RouteClassRules = @(
    @{ Id = 'Routing.Class.CentreSpine'; Legacy = 'Routing.CentreSpine'; Class = 'CentreSpine'; Desc = 'Adjacent-rank primary-flow edges align to the primary spine without unnecessary detours.' },
    @{ Id = 'Routing.Class.ShortAdjacent'; Legacy = 'Routing.ShortAdjacent'; Class = 'ShortAdjacent'; Desc = 'Nearby source-target pairs use direct or minimal routes.' },
    @{ Id = 'Routing.Class.LocalDecisionBranch'; Legacy = 'Routing.LocalDecisionBranch'; Class = 'LocalDecisionBranch'; Desc = 'Decision branches leave distinct ports and do not collapse into sibling branches.' },
    @{ Id = 'Routing.Class.FanOut'; Legacy = 'Routing.FanOut'; Class = 'FanOut'; Desc = 'One-to-many edges distribute without ambiguous path collapse.' },
    @{ Id = 'Routing.Class.FanIn'; Legacy = 'Routing.FanIn'; Class = 'FanIn'; Desc = 'Many-to-one edges merge cleanly with separable target entry.' },
    @{ Id = 'Routing.Class.MergeBack'; Legacy = 'Routing.MergeBack'; Class = 'MergeBack'; Desc = 'Branch paths returning to the primary path use a clear merge-back route.' },
    @{ Id = 'Routing.Class.LongBypass'; Legacy = 'Routing.LongExceptionPath'; Class = 'LongBypass'; Desc = 'Rank-spanning or exception edges use an exterior or obstacle-aware corridor.' },
    @{ Id = 'Routing.Class.CycleOrReturn'; Legacy = 'Routing.CycleOrReturn'; Class = 'CycleOrReturn'; Desc = 'Backward edges route outside the primary flow and avoid node bodies.' }
)

foreach ($RouteClassRule in $RouteClassRules)
{
    $AppliesToValue = if ($RouteClassRule.Class -in @('FanOut', 'FanIn')) { 'EdgeSet' } else { 'Edge' }
    $WhenExpression = if ($AppliesToValue -eq 'EdgeSet')
    {
        New-All @(
            (New-Condition -Field 'EdgeSet.RouteClass' -Operator 'Equals' -Value $RouteClassRule.Class),
            (New-Condition -Field 'EdgeSet.HasVisibleEdges' -Operator 'Equals' -Value $true)
        )
    }
    else
    {
        New-All @(
            (New-Condition -Field 'Edge.RouteClass' -Operator 'Equals' -Value $RouteClassRule.Class),
            (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
        )
    }

    $Rules += New-Rule `
        -RuleId $RouteClassRule.Id `
        -RuleGroup 'RoutingClassification' `
        -AppliesTo $AppliesToValue `
        -Severity 'Error' `
        -Description $RouteClassRule.Desc `
        -When $WhenExpression `
        -Require @{ 'RouteClass.BehaviourSatisfied' = $true } `
        -FailWhen @('RouteClassBehaviourViolation', 'LocalBranchMisclassification', 'LongBypassMisclassification') `
        -DiagnosticTemplate "Route class $($RouteClassRule.Class) failed for {ElementId}." `
        -SuggestedCorrectionTemplate 'Satisfy the structural behaviour expected for the inferred route class without fixture-specific special cases.' `
        -LegacyRuleId $RouteClassRule.Legacy
}

# --- 11.4 Port selection ---
$Rules += New-Rule `
    -RuleId 'Routing.Port.PrimaryDirection' `
    -RuleGroup 'PortSelection' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Primary-flow edges leave and enter via direction-appropriate ports.' `
    -When (New-All @(
        (New-Condition -Field 'Edge.IsPrimaryFlow' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Ports.MatchPrimaryDirection' = $true } `
    -FailWhen @('PrimaryDirectionPortMismatch') `
    -DiagnosticTemplate 'Edge {EdgeId} uses ports {SourcePort}->{TargetPort} that conflict with direction {Direction}.' `
    -SuggestedCorrectionTemplate 'Select source and target ports appropriate to the diagram primary direction.'

$Rules += New-Rule `
    -RuleId 'Routing.Port.DecisionBranchDistinct' `
    -RuleGroup 'PortSelection' `
    -AppliesTo 'EdgeSet' `
    -Severity 'Error' `
    -Description 'Multiple branches from a decision node use distinct or non-conflicting source regions.' `
    -When (New-All @(
        (New-Condition -Field 'SourceNode.Shape' -Operator 'Equals' -Value 'Diamond'),
        (New-Condition -Field 'SourceNode.OutDegree' -Operator 'GreaterThanOrEqual' -Value 2)
    )) `
    -Require @{ 'Ports.DecisionBranchesDistinct' = $true } `
    -FailWhen @('DecisionBranchPortCollapse', 'UnintendedEdgeCollapse') `
    -DiagnosticTemplate 'Decision node {SourceNodeId} branches use conflicting source ports.' `
    -SuggestedCorrectionTemplate 'Assign distinct or non-conflicting source boundary regions to decision branches.' `
    -LegacyRuleId 'Routing.MultiWayDecisionBranch'

$Rules += New-Rule `
    -RuleId 'Routing.Port.MergeSeparation' `
    -RuleGroup 'PortSelection' `
    -AppliesTo 'EdgeSet' `
    -Severity 'Error' `
    -Description 'Multiple incoming edges to a merge target use separable entry regions or merge points.' `
    -When (New-All @(
        (New-Condition -Field 'TargetNode.InDegree' -Operator 'GreaterThanOrEqual' -Value 2)
    )) `
    -Require @{ 'Ports.MergeEntrySeparable' = $true } `
    -FailWhen @('AmbiguousMerge', 'HookAtTarget') `
    -DiagnosticTemplate 'Merge target {TargetNodeId} incoming edges do not have separable entry regions.' `
    -SuggestedCorrectionTemplate 'Separate merge entry regions or merge points for multiple incoming edges.' `
    -LegacyRuleId 'Routing.MultiInputMerge'

$Rules += New-Rule `
    -RuleId 'Routing.Port.ShapeBoundary' `
    -RuleGroup 'PortSelection' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Edges terminate at shape boundary intersections, not node centres.' `
    -When $VisibleEdgeWhen `
    -Require @{ 'Ports.TerminateAtShapeBoundary' = $true } `
    -FailWhen @('EdgeTerminatesAtNodeCentre') `
    -DiagnosticTemplate 'Edge {EdgeId} terminates at node centre instead of shape boundary.' `
    -SuggestedCorrectionTemplate 'Terminate edges at rendered shape boundary intersections.' `
    -LegacyRuleId 'Routing.ShapeBoundaryIntersection'

$Rules += New-Rule `
    -RuleId 'Render.Ports.TerminateAtVisibleBoundary' `
    -RuleGroup 'PortSelection' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Visible edge endpoints meet the rendered shape boundary (shape-specific ray exit), not a padded bbox centre.' `
    -When $VisibleEdgeWhen `
    -Require @{ 'Ports.TerminateAtVisibleBoundary' = $true } `
    -FailWhen @('EdgeTerminatesBeforeVisibleBoundary', 'EdgeOvershootsVisibleBoundary') `
    -DiagnosticTemplate 'Edge {EdgeId} does not terminate on the visible shape boundary.' `
    -SuggestedCorrectionTemplate 'Terminate the connector at the shape-specific boundary intersection used by the renderer.'

$Rules += New-Rule `
    -RuleId 'Render.Ports.PerpendicularAttach' `
    -RuleGroup 'PortSelection' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Every visible connector leaves and enters node faces at right angles through perpendicular terminal stubs on both layout guides and rendered strokes.' `
    -When (New-All @(
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Ports.PerpendicularAttach' = $true } `
    -FailWhen @('NonPerpendicularAttach', 'RenderedAttachFailure') `
    -DiagnosticTemplate 'Edge {EdgeId} does not attach perpendicular to the rendered node boundary.' `
    -SuggestedCorrectionTemplate 'Adjust the rendered route so terminal stubs are perpendicular to the joined node face.'

$Rules += New-Rule `
    -RuleId 'Render.Path.NoVisibleKinkInSmoothBody' `
    -RuleGroup 'PathShape' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Rendered corridor, bypass, fan-in/out, and decision connectors must not contain visible kinks, hooks, or hard elbows in the curved body.' `
    -When (New-All @(
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Edge.PathFamily' -Operator 'In' -Value @('Smooth', 'Spline', 'Polyline', 'Orthogonal')),
        (New-Condition -Field 'Path.RenderedAsSvgPath' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Path.NoVisibleKinkInSmoothBody' = $true } `
    -FailWhen @('VisiblePathKink', 'HardElbowWhenSmoothRequired') `
    -DiagnosticTemplate 'Edge {EdgeId} contains a visible kink in the rendered smooth path.' `
    -SuggestedCorrectionTemplate 'Simplify guide points or corridor curves so the rendered stroke eases smoothly into terminal stubs.'

$TrapezoidBracketShapeWhen = New-All @(
    (New-Condition -Field 'Node.Shape' -Operator 'In' -Value @('lean_right', 'lean_left', 'trapezoid', 'inv_trapezoid')),
    (New-Condition -Field 'Node.IsRendered' -Operator 'Equals' -Value $true)
)

$NodeLabelAlignmentWhen = New-All @(
    (New-Condition -Field 'Node.IsRendered' -Operator 'Equals' -Value $true),
    (New-Condition -Field 'Node.Label' -Operator 'Exists')
)

$Rules += New-Rule `
    -RuleId 'Render.Label.NodeCentredHorizontally' `
    -RuleGroup 'Labels' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Rendered node labels are centred horizontally within the node body.' `
    -When $NodeLabelAlignmentWhen `
    -Require @{ 'Label.CentredHorizontally' = $true } `
    -FailWhen @('NodeLabelOffCentreHorizontally') `
    -DiagnosticTemplate 'Node label on {NodeId} failed Render.Label.NodeCentredHorizontally.' `
    -SuggestedCorrectionTemplate 'Centre the node label horizontally within the node bounds in FlowchartSugiyamaSvgRenderer.'

$Rules += New-Rule `
    -RuleId 'Render.Label.NodeCentredVertically' `
    -RuleGroup 'Labels' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Rendered node labels are centred vertically within the node body.' `
    -When $NodeLabelAlignmentWhen `
    -Require @{ 'Label.CentredVertically' = $true } `
    -FailWhen @('NodeLabelOffCentreVertically') `
    -DiagnosticTemplate 'Node label on {NodeId} failed Render.Label.NodeCentredVertically.' `
    -SuggestedCorrectionTemplate 'Centre the node label vertically within the node bounds in FlowchartSugiyamaSvgRenderer.'

$Rules += New-Rule `
    -RuleId 'Render.Shape.TrapezoidBracketGeometry' `
    -RuleGroup 'Recognition' `
    -AppliesTo 'Node' `
    -Severity 'Error' `
    -Description 'Legacy trapezoid bracket delimiters render as slanted bracket paths with correct width asymmetry (F81/F82), not axis-aligned rectangles.' `
    -When $TrapezoidBracketShapeWhen `
    -Require @{ 'Node.RenderedAsTrapezoidBracket' = $true } `
    -FailWhen @('TrapezoidBracketRenderedAsRectangle', 'TrapezoidBracketWidthAsymmetryWrong') `
    -DiagnosticTemplate 'Node {NodeId} legacy bracket shape {Shape} did not render with correct trapezoid bracket geometry.' `
    -SuggestedCorrectionTemplate 'Render [/label/], [/label \], [\label/], and [\label \] using the matching slanted bracket path builder, not a rectangle.'

$Rules += New-Rule `
    -RuleId 'Render.Marker.CrossEndpointStandoff' `
    -RuleGroup 'Markers' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Cross endpoint markers keep a small exterior stand-off from the shape boundary; all other markers terminate on the boundary.' `
    -When $VisibleEdgeWhen `
    -Require @{ 'Ports.CrossEndpointStandoffValid' = $true } `
    -FailWhen @('CrossMarkerMissingStandoff', 'NonCrossMarkerHasStandoff') `
    -DiagnosticTemplate 'Edge {EdgeId} failed Render.Marker.CrossEndpointStandoff.' `
    -SuggestedCorrectionTemplate 'Apply the cross-marker exterior gap only to cross endpoints; other markers must meet the boundary.'

# --- 11.5 Path shape ---
$PathShapeRules = @(
    @{ Id = 'Routing.Path.Straightness'; Desc = 'Straight routes remain within perpendicular drift tolerance.'; RequireKey = 'Path.StraightWithinTolerance'; Fail = 'StraightPathDrift'; Legacy = 'Routing.CentreSpine' },
    @{ Id = 'Routing.Path.OrthogonalSegments'; Desc = 'Orthogonal routes use legal axis-aligned segments.'; RequireKey = 'Path.OrthogonalSegmentsLegal'; Fail = 'IllegalOrthogonalSegment'; Legacy = 'Routing.Orthogonal' },
    @{ Id = 'Routing.Path.NoHardElbowWhenSmoothRequired'; Desc = 'Smooth routes do not contain sharp, unsupported elbows.'; RequireKey = 'Path.NoHardElbowWhenSmoothRequired'; Fail = 'HardElbowWhenSmoothRequired'; Legacy = 'Routing.Polyline' },
    @{ Id = 'Routing.Path.NoOrthogonalPolylineWhenCurveBasisDefault'; Desc = 'Default curveBasis diagrams must not render multi-segment orthogonal edge polylines (F58 terminal collector, bypass, and fan-in routes use smooth corridor curves).'; RequireKey = 'Path.NoOrthogonalPolylineWhenCurveBasisDefault'; Fail = 'OrthogonalPolylineWhenCurveBasisDefault' }
    @{ Id = 'Routing.Path.NoHookAtSource'; Desc = 'Route does not reverse direction immediately after leaving source.'; RequireKey = 'Path.NoHookAtSource'; Fail = 'HookAtSource' },
    @{ Id = 'Routing.Path.NoHookAtTarget'; Desc = 'Route does not overshoot and hook into target.'; RequireKey = 'Path.NoHookAtTarget'; Fail = 'HookAtTarget' },
    @{ Id = 'Routing.Path.NoUnnecessaryDetour'; Desc = 'Direct routes are not replaced by arbitrary long detours.'; RequireKey = 'Path.NoUnnecessaryDetour'; Fail = 'UnnecessaryDetour'; Legacy = 'Routing.ObstacleAvoidance' }
)

foreach ($PathRule in $PathShapeRules)
{
    $WhenExpression = if ($PathRule.Id -eq 'Routing.Path.OrthogonalSegments')
    {
        New-All @(
            (New-Condition -Field 'Edge.PathFamily' -Operator 'Equals' -Value 'Orthogonal'),
            (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
        )
    }
    elseif ($PathRule.Id -eq 'Routing.Path.NoHardElbowWhenSmoothRequired')
    {
        New-All @(
            (New-Condition -Field 'Edge.PathFamily' -Operator 'In' -Value @('Smooth', 'Spline', 'Polyline', 'Orthogonal')),
            (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true),
            (New-Condition -Field 'Path.RenderedAsSvgPath' -Operator 'Equals' -Value $true)
        )
    }
    elseif ($PathRule.Id -eq 'Routing.Path.NoOrthogonalPolylineWhenCurveBasisDefault')
    {
        New-All @(
            (New-Condition -Field 'Config.RequiresCurveBasisPaths' -Operator 'Equals' -Value $true),
            (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
        )
    }
    else
    {
        $VisibleEdgeWhen
    }

    $RequireTable = @{}
    $RequireTable[$PathRule.RequireKey] = $true

    $Rules += New-Rule `
        -RuleId $PathRule.Id `
        -RuleGroup 'PathShape' `
        -AppliesTo 'Edge' `
        -Severity 'Error' `
        -Description $(if ($PathRule.Legacy -and $OrgDescriptions.ContainsKey($PathRule.Legacy)) { $OrgDescriptions[$PathRule.Legacy] } else { $PathRule.Desc }) `
        -When $WhenExpression `
        -Require $RequireTable `
        -FailWhen @($PathRule.Fail) `
        -DiagnosticTemplate "Edge {EdgeId} failed $($PathRule.Id)." `
        -SuggestedCorrectionTemplate 'Adjust the route path to satisfy the required path-shape constraint.' `
        -LegacyRuleId $PathRule.Legacy
}

# --- 11.6 Obstacle avoidance ---
$Rules += New-Rule `
    -RuleId 'Routing.Obstacle.NoNodeBodyIntersection' `
    -RuleGroup 'ObstacleAvoidance' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Visible edge does not pass through unrelated node bodies.' `
    -When $VisibleEdgeWhen `
    -Require @{ 'Path.DoesNotIntersectUnrelatedNodeBodies' = $true } `
    -FailWhen @('NodeBodyIntersection', 'PathIntersectsUnrelatedNodeBody') `
    -DiagnosticTemplate 'Edge {EdgeId} crosses node {IntersectedNodeId}. Route the edge around the node body.' `
    -SuggestedCorrectionTemplate 'Move the route into an available corridor and preserve the source/target ports and marker intent.' `
    -LegacyRuleId 'Routing.ObstacleAvoidance'

$Rules += New-Rule `
    -RuleId 'Routing.Obstacle.NoClusterBodyIntersection' `
    -RuleGroup 'ObstacleAvoidance' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Edge does not pass through unrelated cluster bodies or confusing group interiors.' `
    -When $VisibleEdgeWhen `
    -Require @{ 'Path.DoesNotIntersectUnrelatedClusterBodies' = $true } `
    -FailWhen @('ClusterBodyIntersection') `
    -DiagnosticTemplate 'Edge {EdgeId} crosses unrelated cluster {IntersectedGroupId}.' `
    -SuggestedCorrectionTemplate 'Route around unrelated cluster bodies and avoid confusing group interiors.'

$Rules += New-Rule `
    -RuleId 'Routing.Obstacle.NoSiblingBranchCrossing' `
    -RuleGroup 'ObstacleAvoidance' `
    -AppliesTo 'EdgeSet' `
    -Severity 'Error' `
    -Description 'Sibling decision branches do not cross each other unnecessarily.' `
    -When (New-All @(
        (New-Condition -Field 'EdgeSet.IsSiblingDecisionBranchSet' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Path.SiblingBranchesDoNotCross' = $true } `
    -FailWhen @('CrossesSiblingBranch') `
    -DiagnosticTemplate 'Sibling branches from {SourceNodeId} cross each other unnecessarily.' `
    -SuggestedCorrectionTemplate 'Separate sibling decision branch corridors to avoid unnecessary crossings.' `
    -LegacyRuleId 'Routing.LocalDecisionBranch'

$Rules += New-Rule `
    -RuleId 'Routing.Obstacle.NoSpineCrossingForException' `
    -RuleGroup 'ObstacleAvoidance' `
    -AppliesTo 'Edge' `
    -Severity 'Error' `
    -Description 'Long exception routes avoid cutting through the centre spine corridor unless structurally required.' `
    -When (New-All @(
        (New-Condition -Field 'Edge.RouteClass' -Operator 'In' -Value @('LongBypass', 'CycleOrReturn')),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) `
    -Require @{ 'Path.DoesNotCrossCentreSpineCorridor' = $true } `
    -FailWhen @('RouteThroughCentreSpine', 'DiagonalSpineCrossing') `
    -DiagnosticTemplate 'Exception edge {EdgeId} cuts through the centre spine corridor.' `
    -SuggestedCorrectionTemplate 'Route long exception paths in an exterior corridor and avoid the centre spine unless structurally required.'

# --- 11.7 Labels ---
$LabelRules = @(
    @{ Id = 'Routing.Label.AttachedToOwningEdge'; Legacy = 'Routing.LabelClearance'; RequireKey = 'Label.AttachedToOwningEdge'; Fail = 'DetachedLabel'; When = (New-All @((New-Condition -Field 'Edge.Label' -Operator 'Exists'))) },
    @{ Id = 'Routing.Label.ClearOfNodeBodies'; Legacy = 'Routing.LabelClearance'; RequireKey = 'Label.ClearOfNodeBodies'; Fail = 'LabelOverlapsNodeBody'; When = (New-All @((New-Condition -Field 'Edge.Label' -Operator 'Exists'))) },
    @{ Id = 'Routing.Label.ClearOfMarkers'; RequireKey = 'Label.ClearOfMarkers'; Fail = 'LabelOverlapsMarker'; When = (New-All @((New-Condition -Field 'Edge.Label' -Operator 'Exists'))) },
    @{ Id = 'Routing.Label.LongLabelWrapped'; Legacy = 'Routing.LongLabelClearance'; RequireKey = 'Label.LongLabelWrapped'; Fail = 'LongLabelCausesDetour'; When = (New-All @((New-Condition -Field 'Edge.Label.IsLong' -Operator 'Equals' -Value $true))) },
    @{ Id = 'Routing.Label.CentredOnConnectorSpine'; RequireKey = 'Label.CentredOnConnectorSpine'; Fail = 'LabelOffConnectorSpine'; When = (New-All @(
        (New-Condition -Field 'Edge.Label' -Operator 'Exists'),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Routing.Label.CentredOnConnectorHorizontally'; RequireKey = 'Label.CentredOnConnectorHorizontally'; Fail = 'LabelOffConnectorHorizontally'; When = (New-All @(
        (New-Condition -Field 'Edge.Label' -Operator 'Exists'),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Routing.Label.CentredOnConnectorVertically'; RequireKey = 'Label.CentredOnConnectorVertically'; Fail = 'LabelOffConnectorVertically'; When = (New-All @(
        (New-Condition -Field 'Edge.Label' -Operator 'Exists'),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Routing.Label.NotOnConnectorLine'; RequireKey = 'Label.NotOnConnectorLine'; Fail = 'LabelOnConnectorStroke'; When = (New-All @(
        (New-Condition -Field 'Edge.Label' -Operator 'Exists'),
        (New-Condition -Field 'Edge.PathFamily' -Operator 'In' -Value @('Smooth', 'Spline')),
        (New-Condition -Field 'Edge.RouteClass' -Operator 'NotEquals' -Value 'LocalDecisionBranch')
    )) },
    @{ Id = 'Routing.Label.HalfPolylineMidspan'; RequireKey = 'Label.HalfPolylineMidspan'; Fail = 'LabelNotAtHalfPolylineDistance'; When = (New-All @(
        (New-Condition -Field 'Edge.Label' -Operator 'Exists'),
        (New-Condition -Field 'Edge.PathFamily' -Operator 'In' -Value @('Smooth', 'Spline', 'Polyline'))
    )) },
    @{ Id = 'Routing.Label.BranchLabelNearBranch'; Legacy = 'Routing.LocalDecisionBranch'; RequireKey = 'Label.NearBranchSegment'; Fail = 'LabelCloserToWrongEdge'; When = (New-All @(
        (New-Condition -Field 'Edge.Label' -Operator 'Exists'),
        (New-Condition -Field 'Edge.RouteClass' -Operator 'Equals' -Value 'LocalDecisionBranch')
    )) }
)

foreach ($LabelRule in $LabelRules)
{
    $Rules += New-Rule `
        -RuleId $LabelRule.Id `
        -RuleGroup 'Labels' `
        -AppliesTo 'Edge' `
        -Severity 'Error' `
        -Description $(if ($LabelRule.Legacy) { $OrgDescriptions[$LabelRule.Legacy] } else { "Validate $($LabelRule.Id) for labelled edges." }) `
        -When $LabelRule.When `
        -Require @{ $LabelRule.RequireKey = $true } `
        -FailWhen @($LabelRule.Fail) `
        -DiagnosticTemplate "Edge label on {EdgeId} failed $($LabelRule.Id)." `
        -SuggestedCorrectionTemplate 'Adjust label placement to satisfy label routing constraints.' `
        -LegacyRuleId $LabelRule.Legacy
}

# --- 11.8 Markers ---
$MarkerRules = @(
    @{ Id = 'Render.Marker.StartMatchesToken'; RequireKey = 'Marker.StartMatchesToken'; Fail = 'StartMarkerMismatch' },
    @{ Id = 'Render.Marker.EndMatchesToken'; RequireKey = 'Marker.EndMatchesToken'; Fail = 'EndMarkerMismatch' },
    @{ Id = 'Render.Marker.BidirectionalPreserved'; RequireKey = 'Marker.BidirectionalPreserved'; Fail = 'BidirectionalMarkerLost'; When = (New-All @((New-Condition -Field 'Edge.IsBidirectional' -Operator 'Equals' -Value $true))) },
    @{ Id = 'Render.Marker.NotLost'; RequireKey = 'Marker.NotLost'; Fail = 'LostMarker' },
    @{ Id = 'Render.Marker.NotInvented'; RequireKey = 'Marker.NotInvented'; Fail = 'InventedMarker' },
    @{ Id = 'Render.Marker.EndAlignedWithPathTangent'; RequireKey = 'Marker.EndAlignedWithPathTangent'; Fail = 'EndMarkerTangentDivergence'; When = (New-All @((New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true), (New-Condition -Field 'Edge.ArrowEnd' -Operator 'Equals' -Value 'Point'))) },
    @{ Id = 'Render.Marker.StartAlignedWithPathTangent'; RequireKey = 'Marker.StartAlignedWithPathTangent'; Fail = 'StartMarkerTangentDivergence'; When = (New-All @((New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true), (New-Condition -Field 'Edge.ArrowStart' -Operator 'Equals' -Value 'Point'))) }
)

foreach ($MarkerRule in $MarkerRules)
{
    $WhenExpression = if ($MarkerRule.When) { $MarkerRule.When } else { $VisibleEdgeWhen }

    $Rules += New-Rule `
        -RuleId $MarkerRule.Id `
        -RuleGroup 'Markers' `
        -AppliesTo 'Edge' `
        -Severity 'Error' `
        -Description $OrgDescriptions['Render.MarkerAssignment'] `
        -When $WhenExpression `
        -Require @{ $MarkerRule.RequireKey = $true } `
        -FailWhen @($MarkerRule.Fail) `
        -DiagnosticTemplate "Edge {EdgeId} failed $($MarkerRule.Id)." `
        -SuggestedCorrectionTemplate 'Preserve marker intent from edge token decomposition without loss or invention.' `
        -LegacyRuleId 'Render.MarkerAssignment'
}

# --- 11.9 Subgraphs ---
$SubgraphRules = @(
    @{ Id = 'Subgraph.MemberMinimumInset'; AppliesTo = 'Group'; Legacy = 'Layout.Subgraph.MemberInset'; RequireKey = 'Layout.MemberClusterInsetValid'; Desc = 'Member node bodies maintain minimum insets from the cluster border (18px sides/bottom, 18px below title band at top).'; When = (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasMemberNodes' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Subgraph.MembersVerticallyCentered'; AppliesTo = 'Group'; Legacy = 'Layout.Subgraph.VerticalCentering'; RequireKey = 'Layout.MembersVerticallyCenteredInCluster'; Desc = 'Member content is vertically centred in the cluster content region below the title band.'; When = (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasMemberNodes' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Subgraph.MembersHorizontallyCentered'; AppliesTo = 'Group'; Legacy = 'Layout.Subgraph.HorizontalCentering'; RequireKey = 'Layout.MembersHorizontallyCenteredInCluster'; Desc = 'The primary internal member column (for example Queue/Decision in F59) is horizontally centred in the cluster content region; otherwise the member bounding box is centred with side padding.'; When = (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasMemberNodes' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Subgraph.SiblingCrossBoundarySpineAligned'; AppliesTo = 'Group'; Legacy = 'Layout.Subgraph.SiblingSpineAlignment'; RequireKey = 'Layout.SiblingSubgraphSpineAligned'; Desc = 'Sibling subgraphs with cross-boundary member rows share a common spine coordinate so primary cross-stage connectors can run horizontally (F60).'; When = (New-All @(
        (New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Group.HasCrossBoundarySiblingSubgraph' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Layout.ParallelColumnRowsAligned'; AppliesTo = 'Diagram'; Legacy = 'Layout.ParallelColumn.RowAlignment'; RequireKey = 'Layout.ParallelColumnRowsAligned'; Desc = 'Parallel vertical columns align corresponding rows to the same cross-flow coordinate (F73).'; When = (New-All @(
        (New-Condition -Field 'Layout.HasParallelColumnLayout' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Subgraph.BoundsContainMembers'; AppliesTo = 'Group'; Legacy = 'Routing.NestedSubgraph'; RequireKey = 'Group.BoundsContainMembers' },
    @{ Id = 'Subgraph.InternalEdgesRemainInside'; AppliesTo = 'Edge'; Legacy = 'Routing.SubgraphInternal'; RequireKey = 'Path.RemainsInsideGroupWhenPossible'; When = (New-All @(
        (New-Condition -Field 'Edge.IsSubgraphInternal' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Subgraph.CrossBoundaryEdgesAreClear'; AppliesTo = 'Edge'; Legacy = 'Routing.SubgraphCrossBoundary'; RequireKey = 'Path.CrossBoundaryClear'; When = (New-All @(
        (New-Condition -Field 'Edge.IsSubgraphCrossBoundary' -Operator 'Equals' -Value $true),
        (New-Condition -Field 'Edge.IsVisible' -Operator 'Equals' -Value $true)
    )) },
    @{ Id = 'Subgraph.NestedOwnershipDeterministic'; AppliesTo = 'Group'; Legacy = 'Recognition.Subgraph.MembershipUniqueness'; RequireKey = 'Group.NestedOwnershipDeterministic'; When = (New-All @((New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true))) }
)

foreach ($SubgraphRule in $SubgraphRules)
{
    $WhenExpression = if ($SubgraphRule.When)
    {
        $SubgraphRule.When
    }
    elseif ($SubgraphRule.AppliesTo -eq 'Group')
    {
        New-All @((New-Condition -Field 'Group.IsSubgraph' -Operator 'Equals' -Value $true))
    }
    else
    {
        $VisibleEdgeWhen
    }

    $Rules += New-Rule `
        -RuleId $SubgraphRule.Id `
        -RuleGroup 'Subgraphs' `
        -AppliesTo $SubgraphRule.AppliesTo `
        -Severity 'Error' `
        -Description $(if ($SubgraphRule.Desc) { $SubgraphRule.Desc } else { $OrgDescriptions[$SubgraphRule.Legacy] }) `
        -When $WhenExpression `
        -Require @{ $SubgraphRule.RequireKey = $true } `
        -FailWhen @('SubgraphConstraintViolation') `
        -DiagnosticTemplate "Subgraph {GroupId} failed $($SubgraphRule.Id)." `
        -SuggestedCorrectionTemplate 'Satisfy subgraph containment and routing constraints.' `
        -LegacyRuleId $SubgraphRule.Legacy
}

# --- 11.10 Anti-patterns ---
$Rules += New-Rule `
    -RuleId 'Diagnostics.NoScenarioNameDrivenRouting' `
    -RuleGroup 'AntiPatterns' `
    -AppliesTo 'Implementation' `
    -Severity 'Error' `
    -Description 'Routing must not branch on scenario ID or fixture name.' `
    -When (New-All @((New-Condition -Field 'Implementation.StaticAnalysis.Available' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Implementation.NoScenarioNameSelectors' = $true } `
    -FailWhen @('ScenarioNameDrivenSpecialCase', 'ScenarioNameDrivenSelectorFound') `
    -DiagnosticTemplate 'Implementation contains scenario-driven routing selector {SelectorName}.' `
    -SuggestedCorrectionTemplate 'Replace the selector with structural graph, layout, or routing conditions.'

$Rules += New-Rule `
    -RuleId 'Diagnostics.NoNodeLabelDrivenRouting' `
    -RuleGroup 'AntiPatterns' `
    -AppliesTo 'Implementation' `
    -Severity 'Error' `
    -Description 'Routing must not branch on visible node label text.' `
    -When (New-All @((New-Condition -Field 'Implementation.StaticAnalysis.Available' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Implementation.NoNodeLabelSelectors' = $true } `
    -FailWhen @('NodeLabelDrivenSelectorFound') `
    -DiagnosticTemplate 'Implementation contains node-label-driven routing selector {SelectorName}.' `
    -SuggestedCorrectionTemplate 'Use structural node facts such as shape, degree, rank, and lane instead of label text.'

$Rules += New-Rule `
    -RuleId 'Diagnostics.NoEnglishLabelDrivenBranching' `
    -RuleGroup 'AntiPatterns' `
    -AppliesTo 'Implementation' `
    -Severity 'Error' `
    -Description 'Routing must not use English edge labels such as Yes or No to determine geometry.' `
    -When (New-All @((New-Condition -Field 'Implementation.StaticAnalysis.Available' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Implementation.NoEnglishLabelSelectors' = $true } `
    -FailWhen @('EnglishLabelDrivenSelectorFound') `
    -DiagnosticTemplate 'Implementation contains English-label-driven routing selector {SelectorName}.' `
    -SuggestedCorrectionTemplate 'Use graph topology and route class inference instead of edge label language.'

$Rules += New-Rule `
    -RuleId 'Diagnostics.UnsupportedFeaturesReported' `
    -RuleGroup 'Diagnostics' `
    -AppliesTo 'Diagram' `
    -Severity 'Error' `
    -Description 'Unsupported recognised features produce diagnostics.' `
    -When (New-All @((New-Condition -Field 'Diagram.HasUnsupportedFeature' -Operator 'Equals' -Value $true))) `
    -Require @{ 'Diagnostics.UnsupportedFeatureReported' = $true } `
    -FailWhen @('UnsupportedFeatureSilenced') `
    -DiagnosticTemplate 'Diagram {DiagramId} has unsupported feature {FeatureKind} without a diagnostic.' `
    -SuggestedCorrectionTemplate 'Report unsupported recognised features as structured diagnostics.'

# --- Extended recognition rules from ruleset-org catalogue ---
$ExtendedRecognitionRules = @(
    @{ Legacy = 'Header.GraphAlias'; When = (New-All @((New-Condition -Field 'Recognition.HeaderToken' -Operator 'Equals' -Value 'graph'))) },
    @{ Legacy = 'Header.FlowchartElkAlias'; When = (New-All @((New-Condition -Field 'Recognition.HeaderToken' -Operator 'Equals' -Value 'flowchart-elk'))) },
    @{ Legacy = 'Recognition.Edge.Bidirectional'; When = (New-All @((New-Condition -Field 'Edge.IsBidirectional' -Operator 'Equals' -Value $true))) },
    @{ Legacy = 'Recognition.Edge.Dotted'; When = (New-All @((New-Condition -Field 'Edge.Stroke' -Operator 'Equals' -Value 'Dotted'))) },
    @{ Legacy = 'Recognition.Edge.Thick'; When = (New-All @((New-Condition -Field 'Edge.Stroke' -Operator 'Equals' -Value 'Thick'))) },
    @{ Legacy = 'Recognition.Edge.Invisible'; When = (New-All @((New-Condition -Field 'Edge.Stroke' -Operator 'Equals' -Value 'Invisible'))) },
    @{ Legacy = 'Recognition.Edge.CircleMarker'; When = (New-All @((New-Condition -Field 'Edge.ArrowStart' -Operator 'Equals' -Value 'Circle'))) },
    @{ Legacy = 'Recognition.Edge.CrossMarker'; When = (New-All @((New-Condition -Field 'Edge.ArrowEnd' -Operator 'Equals' -Value 'Cross'))) },
    @{ Legacy = 'Recognition.Edge.GroupExpansion'; When = (New-All @((New-Condition -Field 'Edge.RequiresGroupExpansion' -Operator 'Equals' -Value $true))) },
    @{ Legacy = 'Recognition.Edge.LabelPipeSyntax'; When = (New-All @((New-Condition -Field 'Edge.LabelSyntax' -Operator 'Equals' -Value 'Pipe'))) },
    @{ Legacy = 'Recognition.Edge.LongLabel'; When = (New-All @((New-Condition -Field 'Edge.Label.IsLong' -Operator 'Equals' -Value $true))) },
    @{ Legacy = 'Recognition.Subgraph.Nested'; When = (New-All @((New-Condition -Field 'Group.NestingDepth' -Operator 'GreaterThan' -Value 1))) },
    @{ Legacy = 'Recognition.Subgraph.Direction'; When = (New-All @((New-Condition -Field 'Group.LocalDirection' -Operator 'Exists'))) },
    @{ Legacy = 'Recognition.Config.Init'; When = (New-All @((New-Condition -Field 'Config.HasInitDirective' -Operator 'Equals' -Value $true))) },
    @{ Legacy = 'Recognition.Config.FrontMatter'; When = (New-All @((New-Condition -Field 'Config.HasFrontMatter' -Operator 'Equals' -Value $true))) },
    @{ Legacy = 'Routing.ELK'; When = (New-All @((New-Condition -Field 'Diagram.LayoutEngine' -Operator 'Equals' -Value 'ELK'))) },
    @{ Legacy = 'Routing.InvisibleLayoutConstraint'; When = (New-All @((New-Condition -Field 'Edge.Stroke' -Operator 'Equals' -Value 'Invisible'))) },
    @{ Legacy = 'Routing.MinLenDummySegments'; When = (New-All @((New-Condition -Field 'Edge.MinLength' -Operator 'GreaterThan' -Value 1))) },
    @{ Legacy = 'Routing.ScalingStability'; When = (New-All @((New-Condition -Field 'Diagram.NodeCount' -Operator 'GreaterThanOrEqual' -Value 20))) }
)

$ExistingRuleIds = [System.Collections.Generic.HashSet[string]]::new([string[]]($Rules | ForEach-Object { $_.RuleId }))

foreach ($ExtendedRule in $ExtendedRecognitionRules)
{
    $ExtendedRuleId = $ExtendedRule.Legacy
    if ($ExistingRuleIds.Contains($ExtendedRuleId))
    {
        continue
    }

    $AppliesToValue = switch -Regex ($ExtendedRuleId)
    {
        '^Recognition\.Subgraph\.' { 'Group' }
        '^Recognition\.Config\.' { 'Diagram' }
        '^Routing\.' { if ($ExtendedRuleId -eq 'Routing.ELK') { 'Diagram' } else { 'Edge' } }
        default { if ($ExtendedRuleId -like 'Recognition.Edge.*') { 'Edge' } else { 'Diagram' } }
    }

    $RuleGroupValue = switch -Regex ($ExtendedRuleId)
    {
        '^Routing\.' { if ($ExtendedRuleId -eq 'Routing.ELK') { 'RoutingClassification' } else { 'PathShape' } }
        '^Recognition\.Subgraph\.' { 'Subgraphs' }
        '^Recognition\.Config\.' { 'Recognition' }
        default { 'Recognition' }
    }

    $Rules += New-Rule `
        -RuleId $ExtendedRuleId `
        -RuleGroup $RuleGroupValue `
        -AppliesTo $AppliesToValue `
        -Severity 'Error' `
        -Description $OrgDescriptions[$ExtendedRuleId] `
        -When $ExtendedRule.When `
        -Require @{ 'LegacyCatalogueRuleSatisfied' = $true } `
        -FailWhen @('LegacyCatalogueRuleViolation') `
        -DiagnosticTemplate "{ElementId} failed legacy catalogue rule $ExtendedRuleId." `
        -SuggestedCorrectionTemplate 'Satisfy the recognition or routing intent described by the legacy catalogue rule using structural model facts.'

    [void]$ExistingRuleIds.Add($ExtendedRuleId)
}

# Node shape recognition rules
$NodeShapes = @(
    'Asymmetric', 'Circle', 'Cylinder', 'Diamond', 'DoubleCircle', 'Hexagon',
    'Rect', 'Rounded', 'Stadium', 'Subroutine', 'Trapezoid'
)

foreach ($Shape in $NodeShapes)
{
    $ShapeRuleId = "Recognition.Node.Shape.$Shape"
    if ($ExistingRuleIds.Contains($ShapeRuleId))
    {
        continue
    }

    $ShapeWhen = if ($Shape -eq 'Trapezoid')
    {
        New-All @((New-Condition -Field 'Node.Shape' -Operator 'In' -Value @('lean_right', 'lean_left', 'trapezoid', 'inv_trapezoid')))
    }
    else
    {
        New-All @((New-Condition -Field 'Node.Shape' -Operator 'Equals' -Value $Shape))
    }

    $TrapezoidRequire = if ($Shape -eq 'Trapezoid')
    {
        @{
            'Node.ShapeCanonicalised' = $true
            'Node.TrapezoidBracketShapeRecognised' = $true
        }
    }
    else
    {
        @{ 'Node.ShapeCanonicalised' = $true }
    }

    $Rules += New-Rule `
        -RuleId $ShapeRuleId `
        -RuleGroup 'Recognition' `
        -AppliesTo 'Node' `
        -Severity 'Error' `
        -Description $OrgDescriptions[$ShapeRuleId] `
        -When $ShapeWhen `
        -Require $TrapezoidRequire `
        -FailWhen @('UnmappedNodeShape') `
        -DiagnosticTemplate "Node {NodeId} shape $Shape was not canonicalised correctly." `
        -SuggestedCorrectionTemplate 'Map the Mermaid node syntax to the canonical shape intent.'

    [void]$ExistingRuleIds.Add($ShapeRuleId)
}

$Ruleset = [ordered]@{
    SchemaVersion = '2.0.0'
    Name = 'MermaidFlowchartUniversalRuleset'
    Scope = [ordered]@{
        DiagramKinds = @('flowchart', 'graph', 'flowchart-elk')
        PrimaryKind = 'flowchart'
    }
    DefaultToleranceProfileId = 'DefaultGeometry'
    RuleGroups = @(
        'Recognition',
        'Layout',
        'NeighbourRelationships',
        'RoutingClassification',
        'PortSelection',
        'PathShape',
        'Markers',
        'Labels',
        'ObstacleAvoidance',
        'Subgraphs',
        'Diagnostics',
        'AntiPatterns'
    )
    Rules = $Rules
}

$Ruleset | ConvertTo-Json -Depth 20 | Set-Content -Path $OutputPath -Encoding UTF8

$RuleCount = $Rules.Count
Write-Host "Wrote $OutputPath with $RuleCount rules."
