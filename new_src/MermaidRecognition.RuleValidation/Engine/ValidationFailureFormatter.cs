using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Engine;

/// <summary>
/// Builds human-readable validation failure descriptions from diagram labels.
/// </summary>
public static class ValidationFailureFormatter
{
    /// <summary>
    /// Describes the failing element using visible node and edge label text.
    /// </summary>
    public static string DescribeElement(EvaluationContext Context)
    {
        if (Context.Edge is not null)
        {
            return DescribeEdge(Context.Diagram, Context.Edge);
        }

        if (Context.Node is not null)
        {
            return DescribeNode(Context.Node);
        }

        if (Context.Group is not null)
        {
            return DescribeGroup(Context.Group);
        }

        return Context.Diagram.DiagramId;
    }

    /// <summary>
    /// Describes one edge as "Source label → Target label" with an optional edge label suffix.
    /// </summary>
    public static string DescribeEdge(DiagramActualResult Diagram, EdgeActualResult Edge)
    {
        string SourceLabel = ResolveNodeLabel(Diagram, Edge.SourceNodeId);
        string TargetLabel = ResolveNodeLabel(Diagram, Edge.TargetNodeId);

        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            return SourceLabel + " → " + TargetLabel + " (" + Edge.Label + ")";
        }

        return SourceLabel + " → " + TargetLabel;
    }

    /// <summary>
    /// Describes one node using its visible label text.
    /// </summary>
    public static string DescribeNode(NodeActualResult Node)
    {
        if (!string.IsNullOrWhiteSpace(Node.Label))
        {
            return "\"" + Node.Label + "\"";
        }

        return Node.NodeId;
    }

    /// <summary>
    /// Describes one group using its visible label text when available.
    /// </summary>
    public static string DescribeGroup(GroupActualResult Group)
    {
        if (Group.Facts.TryGetValue("Label", out object? LabelValue)
            && LabelValue is string Label
            && !string.IsNullOrWhiteSpace(Label))
        {
            return "subgraph \"" + Label + "\"";
        }

        return Group.GroupId;
    }

    /// <summary>
    /// Returns the scenario title without markdown heading decoration.
    /// </summary>
    public static string DescribeScenarioTitle(DiagramActualResult Diagram)
    {
        if (string.IsNullOrWhiteSpace(Diagram.ScenarioTitle))
        {
            return string.Empty;
        }

        string Title = Diagram.ScenarioTitle.Trim();
        if (Title.StartsWith("###", StringComparison.Ordinal))
        {
            Title = Title.Substring(3).Trim();
        }

        while (Title.StartsWith("*", StringComparison.Ordinal) && Title.EndsWith("*", StringComparison.Ordinal) && Title.Length >= 2)
        {
            Title = Title.Substring(1, Title.Length - 2).Trim();
        }

        return Title;
    }

    /// <summary>
    /// Converts a required fact key into plain language for report messages.
    /// </summary>
    public static string DescribeRequireKey(string RequireKey)
    {
        switch (RequireKey)
        {
            case "Path.NoUnnecessaryDetour":
                return "the arrow takes an unnecessary detour or routing complexity";
            case "Path.DoesNotIntersectUnrelatedNodeBodies":
                return "the arrow passes through an unrelated node body";
            case "Path.DoesNotIntersectUnrelatedClusterBodies":
                return "the arrow passes through an unrelated cluster body";
            case "Path.NoHookAtSource":
                return "the arrow hooks or overshoots near its source";
            case "Path.NoHookAtTarget":
                return "the arrow hooks or overshoots near its target";
            case "Path.NoHardElbowWhenSmoothRequired":
                return "the arrow uses hard elbows where a smooth branch is expected";
            case "Path.StraightWithinTolerance":
                return "the arrow is not straight within tolerance";
            case "Path.OrthogonalSegmentsLegal":
                return "the arrow contains illegal orthogonal segments";
            case "Path.DoesNotCrossCentreSpineCorridor":
                return "the arrow crosses the centre spine corridor";
            case "RouteClass.BehaviourSatisfied":
                return "the route does not satisfy its expected route-class behaviour";
            case "Ports.MatchPrimaryDirection":
                return "the arrow ports conflict with diagram direction";
            case "Ports.TerminateAtShapeBoundary":
                return "the arrow does not terminate at the shape boundary";
            case "Ports.MergeEntrySeparable":
                return "incoming arrows do not have separable merge entry regions";
            case "Label.AttachedToOwningEdge":
                return "the edge label is not attached to its owning arrow";
            case "Label.ClearOfNodeBodies":
                return "the edge label overlaps a node body";
            case "Label.ClearOfMarkers":
                return "the edge label overlaps a marker";
            case "Layout.NodeBodiesDoNotOverlap":
                return "the node body overlaps an unrelated node beyond tolerance";
            default:
                return "required check '" + RequireKey + "' was not satisfied";
        }
    }

    /// <summary>
    /// Builds a validation message using visible labels and a plain-language fact description.
    /// </summary>
    public static string BuildMessage(
        string MessageTemplate,
        string RuleId,
        EvaluationContext Context,
        string RequireKey,
        string HumanElementDescription)
    {
        string ScenarioTitle = DescribeScenarioTitle(Context.Diagram);
        string FactDescription = DescribeRequireKey(RequireKey);
        string SourceLabel = Context.Edge is not null
            ? ResolveNodeLabel(Context.Diagram, Context.Edge.SourceNodeId)
            : string.Empty;
        string TargetLabel = Context.Edge is not null
            ? ResolveNodeLabel(Context.Diagram, Context.Edge.TargetNodeId)
            : string.Empty;
        string EdgeLabel = Context.Edge?.Label ?? string.Empty;
        string NodeLabel = Context.Node?.Label ?? string.Empty;
        string OtherNodeId = Context.FieldValues.TryGetValue("OtherNodeId", out object? OtherNodeIdValue)
            ? Convert.ToString(OtherNodeIdValue) ?? string.Empty
            : string.Empty;
        string OtherNodeLabel = Context.FieldValues.TryGetValue("OtherNodeLabel", out object? OtherNodeLabelValue)
            ? Convert.ToString(OtherNodeLabelValue) ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(OtherNodeLabel) && !string.IsNullOrWhiteSpace(OtherNodeId))
        {
            NodeActualResult? OtherNode = Context.Diagram.Nodes.FirstOrDefault(
                Candidate => Candidate.NodeId == OtherNodeId);
            if (OtherNode is not null && !string.IsNullOrWhiteSpace(OtherNode.Label))
            {
                OtherNodeLabel = "\"" + OtherNode.Label + "\"";
            }
            else
            {
                OtherNodeLabel = OtherNodeId;
            }
        }

        string Message = MessageTemplate
            .Replace("{DiagramId}", Context.Diagram.DiagramId, StringComparison.Ordinal)
            .Replace("{ScenarioTitle}", ScenarioTitle, StringComparison.Ordinal)
            .Replace("{HumanElement}", HumanElementDescription, StringComparison.Ordinal)
            .Replace("{HumanEdge}", HumanElementDescription, StringComparison.Ordinal)
            .Replace("{SourceLabel}", SourceLabel, StringComparison.Ordinal)
            .Replace("{TargetLabel}", TargetLabel, StringComparison.Ordinal)
            .Replace("{EdgeLabel}", EdgeLabel, StringComparison.Ordinal)
            .Replace("{NodeLabel}", NodeLabel, StringComparison.Ordinal)
            .Replace("{OtherNodeId}", OtherNodeLabel, StringComparison.Ordinal)
            .Replace("{EdgeId}", HumanElementDescription, StringComparison.Ordinal)
            .Replace("{NodeId}", HumanElementDescription, StringComparison.Ordinal)
            .Replace("{ElementId}", HumanElementDescription, StringComparison.Ordinal)
            .Replace("{RequireKey}", RequireKey, StringComparison.Ordinal)
            .Replace("{FactDescription}", FactDescription, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(Message)
            || Message.Contains("{", StringComparison.Ordinal)
            || LooksLikeInternalIdentifier(Message))
        {
            Message = BuildDefaultMessage(Context.Diagram.DiagramId, ScenarioTitle, HumanElementDescription, FactDescription, Context.ElementType);
        }

        if (!Message.Contains(RuleId, StringComparison.Ordinal))
        {
            Message = Message + " [" + RuleId + "]";
        }

        return Message;
    }

    /// <summary>
    /// Builds a specific, actionable correction for the failing element and rule.
    /// </summary>
    public static string BuildSuggestedCorrection(
        string RuleId,
        string RequireKey,
        EvaluationContext Context,
        string HumanElementDescription,
        string TemplateFallback)
    {
        string ScenarioTitle = DescribeScenarioTitle(Context.Diagram);
        string ScenarioPrefix = Context.Diagram.DiagramId;
        if (!string.IsNullOrWhiteSpace(ScenarioTitle)
            && !ScenarioTitle.StartsWith(Context.Diagram.DiagramId, StringComparison.Ordinal))
        {
            ScenarioPrefix = Context.Diagram.DiagramId + " (" + ScenarioTitle + ")";
        }
        else if (!string.IsNullOrWhiteSpace(ScenarioTitle))
        {
            ScenarioPrefix = ScenarioTitle;
        }

        string DiagnosticReason = ResolveDiagnosticReason(Context);
        string DirectionPhrase = DescribePrimaryFlowDirection(Context.Diagram.Direction);

        if (string.Equals(RuleId, "Layout.Node.NoOverlap", StringComparison.Ordinal)
            && Context.Node is not null)
        {
            return BuildNodeOverlapCorrection(Context, ScenarioPrefix);
        }

        if (Context.Edge is not null)
        {
            string Arrow = HumanElementDescription;
            int PathPointCount = Context.Edge.Path.Points.Count;
            string PathSummary = DescribePathSummary(Context.Edge);

            if (string.Equals(RuleId, "Routing.Obstacle.NoNodeBodyIntersection", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.DoesNotIntersectUnrelatedNodeBodies", StringComparison.Ordinal))
            {
                string CollisionDetail = DescribeCollisionFacts(Context.Edge);
                return ScenarioPrefix
                    + ": Reroute arrow "
                    + Arrow
                    + " so it no longer crosses any unrelated node rectangle. "
                    + CollisionDetail
                    + "Use an exterior corridor or additional orthogonal/curved guide points in FlowchartEdgeRouteResolver so the polyline "
                    + PathSummary
                    + " stays outside obstructing node bodies while preserving the same source/target ports and marker intent.";
            }

            if (string.Equals(RuleId, "Routing.Path.NoHookAtSource", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.NoHookAtSource", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Fix the source-side geometry of arrow "
                    + Arrow
                    + ". The first segment must continue outward from "
                    + ResolveNodeLabel(Context.Diagram, Context.Edge.SourceNodeId)
                    + " in the primary "
                    + DirectionPhrase
                    + " direction without reversing back toward the attach point. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Rebuild the guide polyline ("
                    + PathSummary
                    + ") in the decision-branch or fan-out builder so the stub tangent leaves the shape boundary cleanly.";
            }

            if (string.Equals(RuleId, "Routing.Path.NoHookAtTarget", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.NoHookAtTarget", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Fix the target-side geometry of arrow "
                    + Arrow
                    + ". The final segment must approach "
                    + ResolveNodeLabel(Context.Diagram, Context.Edge.TargetNodeId)
                    + " smoothly in the primary "
                    + DirectionPhrase
                    + " direction without folding back or overshooting the inbound port. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Adjust the terminal stub or curve terminal in the route builder for this edge ("
                    + PathSummary
                    + ").";
            }

            if (string.Equals(RuleId, "Routing.Path.NoUnnecessaryDetour", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.NoUnnecessaryDetour", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Simplify arrow "
                    + Arrow
                    + " to the shortest valid connector between the resolved source and target attach points. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "When the nodes are spine-column aligned and unobstructed, emit a straight 2-point centre-spine route via FlowchartCentreSpineRouteBuilder instead of a longer curved or orthogonal detour ("
                    + PathSummary
                    + ").";
            }

            if (string.Equals(RuleId, "Routing.Label.AttachedToOwningEdge", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Label.AttachedToOwningEdge", StringComparison.Ordinal))
            {
                string LabelText = string.IsNullOrWhiteSpace(Context.Edge.Label)
                    ? "the edge label"
                    : "label \"" + Context.Edge.Label + "\"";
                return ScenarioPrefix
                    + ": Reposition "
                    + LabelText
                    + " on arrow "
                    + Arrow
                    + " so it is measurably closer to this connector than to any other visible edge. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Place the label on the owning path midsection in FlowchartEdgeLabelPlacementHelper, not near "
                    + ResolveNodeLabel(Context.Diagram, Context.Edge.TargetNodeId)
                    + " or a neighbouring branch.";
            }

            if (string.Equals(RuleId, "Routing.Label.ClearOfNodeBodies", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Label.ClearOfNodeBodies", StringComparison.Ordinal))
            {
                string LabelText = string.IsNullOrWhiteSpace(Context.Edge.Label)
                    ? "The edge label"
                    : "Label \"" + Context.Edge.Label + "\"";
                return ScenarioPrefix
                    + ": Move "
                    + LabelText
                    + " on arrow "
                    + Arrow
                    + " away from all node rectangles, especially "
                    + ResolveNodeLabel(Context.Diagram, Context.Edge.TargetNodeId)
                    + ". "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Increase label offset along the connector and verify padded label bounds clear both source and target node bodies before rendering.";
            }

            if (string.Equals(RuleId, "Routing.Label.ClearOfMarkers", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Label.ClearOfMarkers", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Move the label on arrow "
                    + Arrow
                    + " so its text box does not overlap the arrowhead or tail marker. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Shift label placement along the path in FlowchartEdgeLabelPlacementHelper.";
            }

            if (string.Equals(RuleId, "Routing.Path.NoHardElbowWhenSmoothRequired", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.NoHardElbowWhenSmoothRequired", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Replace hard orthogonal elbows on arrow "
                    + Arrow
                    + " with a smooth decision-branch curve where the semantic route class expects a local branch. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Rebuild "
                    + PathSummary
                    + " using curveBasis guide points rather than kinked polyline segments.";
            }

            if (string.Equals(RuleId, "Routing.Path.Straightness", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.StraightWithinTolerance", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Make arrow "
                    + Arrow
                    + " straight within tolerance by aligning attach points on one axis in the primary "
                    + DirectionPhrase
                    + " direction. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Current path has "
                    + PathPointCount.ToString(CultureInfo.InvariantCulture)
                    + " guide points ("
                    + PathSummary
                    + ").";
            }

            if (string.Equals(RuleId, "Routing.Obstacle.NoClusterBodyIntersection", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.DoesNotIntersectUnrelatedClusterBodies", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Reroute arrow "
                    + Arrow
                    + " around unrelated subgraph/cluster bounds. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Use a cross-group or exterior corridor route ("
                    + PathSummary
                    + ").";
            }

            if (string.Equals(RuleId, "Routing.Path.LongDiagonalCrossing", StringComparison.Ordinal)
                || string.Equals(RequireKey, "Path.DoesNotCrossCentreSpineCorridor", StringComparison.Ordinal))
            {
                return ScenarioPrefix
                    + ": Reroute arrow "
                    + Arrow
                    + " outside the centre spine corridor instead of cutting diagonally across it. "
                    + FormatDiagnosticClause(DiagnosticReason)
                    + "Use a merge-back or long-bypass route builder for "
                    + PathSummary
                    + ".";
            }
        }

        if (!string.IsNullOrWhiteSpace(TemplateFallback)
            && !string.Equals(TemplateFallback, "Adjust the route path to satisfy the required path-shape constraint.", StringComparison.Ordinal)
            && !string.Equals(TemplateFallback, "Adjust label placement to satisfy label routing constraints.", StringComparison.Ordinal)
            && !string.Equals(TemplateFallback, "Adjust layout so unrelated node bodies do not overlap beyond tolerance.", StringComparison.Ordinal))
        {
            return ScenarioPrefix + ": " + TemplateFallback;
        }

        return ScenarioPrefix
            + ": Fix "
            + HumanElementDescription
            + " so "
            + DescribeRequireKey(RequireKey)
            + ". Rule "
            + RuleId
            + " requires "
            + RequireKey
            + " = true."
            + FormatDiagnosticClause(DiagnosticReason);
    }

    /// <summary>
    /// Populates human-readable fields on a validation failure record.
    /// </summary>
    public static void PopulateHumanReadableFields(
        ValidationFailureRecord Failure,
        EvaluationContext Context,
        string HumanElementDescription)
    {
        Failure.ScenarioTitle = DescribeScenarioTitle(Context.Diagram);
        Failure.HumanElementDescription = HumanElementDescription;

        if (Context.Edge is not null)
        {
            Failure.SourceLabel = ResolveNodeLabel(Context.Diagram, Context.Edge.SourceNodeId);
            Failure.TargetLabel = ResolveNodeLabel(Context.Diagram, Context.Edge.TargetNodeId);
            Failure.EdgeLabel = Context.Edge.Label;
        }
        else if (Context.Node is not null)
        {
            Failure.NodeLabel = Context.Node.Label;
        }
        else if (Context.Group is not null
            && Context.Group.Facts.TryGetValue("Label", out object? GroupLabelValue)
            && GroupLabelValue is string GroupLabel)
        {
            Failure.GroupLabel = GroupLabel;
        }
    }

    private static string ResolveNodeLabel(DiagramActualResult Diagram, string NodeId)
    {
        NodeActualResult? Node = Diagram.Nodes.FirstOrDefault(Candidate => Candidate.NodeId == NodeId);
        if (Node is not null && !string.IsNullOrWhiteSpace(Node.Label))
        {
            return "\"" + Node.Label + "\"";
        }

        return NodeId;
    }

    private static string BuildDefaultMessage(
        string DiagramId,
        string ScenarioTitle,
        string HumanElementDescription,
        string FactDescription,
        string ElementType)
    {
        string Prefix = DiagramId;
        if (!string.IsNullOrWhiteSpace(ScenarioTitle))
        {
            bool ScenarioStartsWithDiagramId = ScenarioTitle.StartsWith(DiagramId, StringComparison.Ordinal);
            Prefix = ScenarioStartsWithDiagramId
                ? ScenarioTitle
                : DiagramId + " — " + ScenarioTitle;
        }

        string ElementKind = ElementType switch
        {
            "Edge" => "Arrow ",
            "Node" => "Node ",
            "Group" => "Group ",
            _ => string.Empty
        };

        return Prefix + ": " + ElementKind + HumanElementDescription + " — " + FactDescription + ".";
    }

    private static bool LooksLikeInternalIdentifier(string Message)
    {
        return Message.Contains("L-", StringComparison.Ordinal)
            || Message.Contains(" failed Routing.", StringComparison.Ordinal)
            || Message.Contains(" failed required fact ", StringComparison.Ordinal);
    }

    private static string BuildNodeOverlapCorrection(EvaluationContext Context, string ScenarioPrefix)
    {
        if (Context.Node is null)
        {
            return ScenarioPrefix + ": Separate overlapping node rectangles in the Sugiyama layout.";
        }

        string OtherNodeId = Context.FieldValues.TryGetValue("OtherNodeId", out object? OtherNodeIdValue)
            ? Convert.ToString(OtherNodeIdValue) ?? string.Empty
            : string.Empty;
        NodeActualResult? OtherNode = Context.Diagram.Nodes.FirstOrDefault(
            Candidate => Candidate.NodeId == OtherNodeId);
        if (OtherNode is null)
        {
            return ScenarioPrefix
                + ": Reposition "
                + DescribeNode(Context.Node)
                + " so its rectangle no longer intersects an unrelated node body.";
        }

        string PrimaryAxis = DescribeSeparationAxis(Context.Diagram.Direction);
        string NodeCentre = FormatNodeCentre(Context.Node);
        string OtherCentre = FormatNodeCentre(OtherNode);
        string OverlapAxes = DescribeOverlapAxes(Context.Node.Bounds, OtherNode.Bounds);

        return ScenarioPrefix
            + ": Reposition layout nodes "
            + DescribeNode(Context.Node)
            + " and "
            + DescribeNode(OtherNode)
            + " so their rendered rectangles are fully disjoint. "
            + "Observed overlap on "
            + OverlapAxes
            + ". Centres are "
            + DescribeNode(Context.Node)
            + " at "
            + NodeCentre
            + " (rank "
            + Context.Node.Rank.ToString(CultureInfo.InvariantCulture)
            + ", lane "
            + Context.Node.Lane.ToString(CultureInfo.InvariantCulture)
            + ") and "
            + DescribeNode(OtherNode)
            + " at "
            + OtherCentre
            + " (rank "
            + OtherNode.Rank.ToString(CultureInfo.InvariantCulture)
            + ", lane "
            + OtherNode.Lane.ToString(CultureInfo.InvariantCulture)
            + "). "
            + "Increase "
            + PrimaryAxis
            + " separation in FlowchartSugiyamaLayoutEngine rank/lane assignment or node spacing so both shapes remain readable and edges have room to route.";
    }

    private static string ResolveDiagnosticReason(EvaluationContext Context)
    {
        if (Context.Edge is not null)
        {
            if (Context.Edge.Facts.TryGetValue("Diagnostic.Reason", out object? EdgeReason)
                && EdgeReason is string EdgeReasonText
                && !string.IsNullOrWhiteSpace(EdgeReasonText))
            {
                return EdgeReasonText;
            }

            if (Context.Edge.CollisionFacts.Count > 0
                && Context.Edge.CollisionFacts[0].TryGetValue("Reason", out object? CollisionReason)
                && CollisionReason is string CollisionReasonText)
            {
                return CollisionReasonText;
            }
        }

        foreach (Dictionary<string, object> Diagnostic in Context.Diagram.Diagnostics)
        {
            if (!Diagnostic.TryGetValue("Reason", out object? ReasonValue)
                || ReasonValue is not string ReasonText)
            {
                continue;
            }

            if (Context.Edge is not null
                && Diagnostic.TryGetValue("HumanEdge", out object? HumanEdgeValue)
                && HumanEdgeValue is string HumanEdgeText
                && string.Equals(
                    HumanEdgeText,
                    DescribeEdge(Context.Diagram, Context.Edge),
                    StringComparison.Ordinal))
            {
                return ReasonText;
            }
        }

        return string.Empty;
    }

    private static string FormatDiagnosticClause(string DiagnosticReason)
    {
        if (string.IsNullOrWhiteSpace(DiagnosticReason))
        {
            return string.Empty;
        }

        return "Diagnostic analyser reported: " + DiagnosticReason + " ";
    }

    private static string DescribePrimaryFlowDirection(string Direction)
    {
        return Direction switch
        {
            "TB" => "top-to-bottom",
            "BT" => "bottom-to-top",
            "LR" => "left-to-right",
            "RL" => "right-to-left",
            _ => "diagram"
        };
    }

    private static string DescribeSeparationAxis(string Direction)
    {
        return Direction switch
        {
            "TB" => "vertical rank spacing",
            "BT" => "vertical rank spacing",
            "LR" => "horizontal lane spacing",
            "RL" => "horizontal lane spacing",
            _ => "primary-axis spacing"
        };
    }

    private static string FormatNodeCentre(NodeActualResult Node)
    {
        return "("
            + Node.Bounds.CentreX.ToString("F1", CultureInfo.InvariantCulture)
            + ", "
            + Node.Bounds.CentreY.ToString("F1", CultureInfo.InvariantCulture)
            + ")";
    }

    private static string DescribePathSummary(EdgeActualResult Edge)
    {
        if (Edge.Path.Points.Count == 0)
        {
            return "no guide points";
        }

        PointActualResult First = Edge.Path.Points[0];
        PointActualResult Last = Edge.Path.Points[Edge.Path.Points.Count - 1];
        return Edge.Path.Points.Count.ToString(CultureInfo.InvariantCulture)
            + " guide points from ("
            + First.X.ToString("F1", CultureInfo.InvariantCulture)
            + ", "
            + First.Y.ToString("F1", CultureInfo.InvariantCulture)
            + ") to ("
            + Last.X.ToString("F1", CultureInfo.InvariantCulture)
            + ", "
            + Last.Y.ToString("F1", CultureInfo.InvariantCulture)
            + ")";
    }

    private static string DescribeCollisionFacts(EdgeActualResult Edge)
    {
        if (Edge.CollisionFacts.Count == 0)
        {
            return string.Empty;
        }

        List<string> Parts = new List<string>();
        foreach (Dictionary<string, object> CollisionFact in Edge.CollisionFacts)
        {
            if (CollisionFact.TryGetValue("Reason", out object? ReasonValue)
                && ReasonValue is string ReasonText
                && !string.IsNullOrWhiteSpace(ReasonText))
            {
                Parts.Add(ReasonText);
            }
        }

        if (Parts.Count == 0)
        {
            return string.Empty;
        }

        return "Collision detail: " + string.Join(" ", Parts) + " ";
    }

    private static string DescribeOverlapAxes(BoundsActualResult Left, BoundsActualResult Right)
    {
        List<string> Axes = new List<string>();
        double HorizontalOverlap = Math.Min(Left.X + Left.Width, Right.X + Right.Width) - Math.Max(Left.X, Right.X);
        double VerticalOverlap = Math.Min(Left.Y + Left.Height, Right.Y + Right.Height) - Math.Max(Left.Y, Right.Y);

        if (HorizontalOverlap > 0.5)
        {
            Axes.Add("horizontal axis (" + HorizontalOverlap.ToString("F1", CultureInfo.InvariantCulture) + "px)");
        }

        if (VerticalOverlap > 0.5)
        {
            Axes.Add("vertical axis (" + VerticalOverlap.ToString("F1", CultureInfo.InvariantCulture) + "px)");
        }

        if (Axes.Count == 0)
        {
            return "both axes";
        }

        return string.Join(" and ", Axes);
    }
}
