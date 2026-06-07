using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds primary spine topology for TB/BT flowcharts with terminal collector sinks.
/// </summary>
public static class FlowchartSpineGraphHelper
{
    private const double SPINE_COLUMN_TOLERANCE_PX = 30.0;

    private const double FAR_MARGIN_COLUMN_FACTOR = 1.75;

    /// <summary>
    /// Attempts to build spine layout metadata when spine-column post-processing would apply.
    /// </summary>
    public static bool TryBuildContext(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options,
        out FlowchartSpineLayoutContext Context)
    {
        Context = null!;

        if (BoundsById.Count <= 1)
        {
            return false;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (HorizontalFlow)
        {
            return false;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in BoundsById)
        {
            MutableById[Entry.Key] = Entry.Value;
        }

        if (!ShouldApplySpineColumnLayout(Contract, MutableById))
        {
            return false;
        }

        List<string> RootNodeIds = FindRootNodeIds(Contract, MutableById.Keys);
        if (RootNodeIds.Count == 0)
        {
            return false;
        }

        string PrimaryRootId = RootNodeIds
            .OrderBy(NodeId => GetEarliestCreationIndex(Contract, NodeId))
            .First();

        if (!MutableById.TryGetValue(PrimaryRootId, out FlowchartLayoutNodeBounds RootBounds))
        {
            return false;
        }

        HashSet<string> SpineNodeIds = BuildPrimarySpineNodeIds(Contract, PrimaryRootId);
        if (SpineNodeIds.Count == 0)
        {
            return false;
        }

        string PrimarySinkId = ResolvePrimarySinkId(Contract);
        double ColumnStep = Math.Max(
            Options.NodeSpacingPx,
            GetTypicalNodeWidth(MutableById.Values) + Options.NodeSpacingPx);

        Context = new FlowchartSpineLayoutContext(
            SpineNodeIds,
            PrimaryRootId,
            PrimarySinkId,
            RootBounds.CenterX,
            ColumnStep);
        return true;
    }

    /// <summary>
    /// Returns true when the node centre sits on the spine column within tolerance.
    /// </summary>
    public static bool IsOnSpineColumn(
        FlowchartLayoutNodeBounds NodeBounds,
        FlowchartSpineLayoutContext Context)
    {
        return Math.Abs(NodeBounds.CenterX - Context.SpineCrossAxis) <= SPINE_COLUMN_TOLERANCE_PX;
    }

    /// <summary>
    /// Returns true when the node centre sits in the far-left or far-right margin lane.
    /// </summary>
    public static bool IsFarMarginColumn(
        FlowchartLayoutNodeBounds NodeBounds,
        FlowchartSpineLayoutContext Context)
    {
        double Delta = Math.Abs(NodeBounds.CenterX - Context.SpineCrossAxis);
        return Delta >= Context.ColumnStep * FAR_MARGIN_COLUMN_FACTOR;
    }

    /// <summary>
    /// Returns true when the node centre sits in an inner side column adjacent to the spine.
    /// </summary>
    public static bool IsInnerMarginColumn(
        FlowchartLayoutNodeBounds NodeBounds,
        FlowchartSpineLayoutContext Context)
    {
        double Delta = Math.Abs(NodeBounds.CenterX - Context.SpineCrossAxis);
        return Delta >= Context.ColumnStep * 0.5
            && Delta < Context.ColumnStep * FAR_MARGIN_COLUMN_FACTOR;
    }

    /// <summary>
    /// Returns the spine continuation edge leaving the source, if any.
    /// </summary>
    public static EdgeContract? SelectSpineOutgoingEdge(
        DiagramContract Contract,
        string SourceNodeId,
        FlowchartSpineLayoutContext Context)
    {
        List<EdgeContract> OutgoingEdges = GetVisibleOutgoingEdges(Contract, SourceNodeId);
        if (OutgoingEdges.Count == 0)
        {
            return null;
        }

        if (OutgoingEdges.Count == 1)
        {
            return OutgoingEdges[0];
        }

        EdgeContract? YesEdge = FindYesLabeledEdge(OutgoingEdges);
        EdgeContract? NoEdge = FindNoLabeledEdge(OutgoingEdges);
        if (YesEdge is not null && NoEdge is not null)
        {
            if (HasDirectedPath(Contract, YesEdge.TargetId, NoEdge.TargetId))
            {
                if (!string.Equals(NoEdge.TargetId, Context.PrimarySinkId, StringComparison.Ordinal))
                {
                    return NoEdge;
                }

                return YesEdge;
            }
        }

        if (YesEdge is not null)
        {
            foreach (EdgeContract OtherEdge in OutgoingEdges)
            {
                if (string.Equals(OtherEdge.TargetId, YesEdge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (HasDirectedPath(Contract, OtherEdge.TargetId, YesEdge.TargetId))
                {
                    return YesEdge;
                }
            }
        }

        foreach (EdgeContract CandidateEdge in OutgoingEdges)
        {
            foreach (EdgeContract OtherEdge in OutgoingEdges)
            {
                if (string.Equals(CandidateEdge.TargetId, OtherEdge.TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (HasDirectedPath(Contract, OtherEdge.TargetId, CandidateEdge.TargetId))
                {
                    return OutgoingEdges.First(
                        Edge => string.Equals(Edge.TargetId, CandidateEdge.TargetId, StringComparison.Ordinal));
                }
            }
        }

        EdgeContract BestEdge = OutgoingEdges[0];
        int BestDistance = ShortestPathLength(Contract, BestEdge.TargetId, Context.PrimarySinkId);
        for (int EdgeIndex = 1; EdgeIndex < OutgoingEdges.Count; EdgeIndex++)
        {
            EdgeContract Candidate = OutgoingEdges[EdgeIndex];
            int CandidateDistance = ShortestPathLength(Contract, Candidate.TargetId, Context.PrimarySinkId);
            if (CandidateDistance > BestDistance)
            {
                BestDistance = CandidateDistance;
                BestEdge = Candidate;
            }
        }

        return BestEdge;
    }

    /// <summary>
    /// Returns true when the edge continues downward on the shared spine column.
    /// </summary>
    public static bool IsSpineContinuationEdge(
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartSpineLayoutContext Context,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        if (!IsOnSpineColumn(SourceBounds, Context)
            || !IsOnSpineColumn(TargetBounds, Context))
        {
            return false;
        }

        if (!FlowchartShapeBoundaryAttachHelper.IsSourceAboveTarget(SourceBounds, TargetBounds))
        {
            return false;
        }

        EdgeContract? SpineEdge = SelectSpineOutgoingEdge(Contract, Edge.SourceId, Context);
        if (SpineEdge is not null
            && string.Equals(SpineEdge.TargetId, Edge.TargetId, StringComparison.Ordinal)
            && IsOnSpineColumn(TargetBounds, Context))
        {
            return true;
        }

        if (IsAffirmativeSpineColumnBranch(Edge))
        {
            return true;
        }

        return CountVisibleOutgoing(Contract, Edge.SourceId) == 1;
    }

    private static bool IsAffirmativeSpineColumnBranch(EdgeContract Edge)
    {
        if (string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        string NormalisedLabel = Edge.Label.Trim();
        return string.Equals(NormalisedLabel, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true when the source shape is a decision diamond with multiple visible outgoing edges.
    /// </summary>
    public static bool IsDecisionFanOutSource(
        DiagramContract Contract,
        string SourceNodeId,
        string? SourceShape)
    {
        return IsDecisionShape(SourceShape)
            && CountVisibleOutgoing(Contract, SourceNodeId) >= 2;
    }

    private static bool ShouldApplySpineColumnLayout(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById)
    {
        if (!HasNonDecisionTerminalCollectorSink(Contract, MutableById))
        {
            return false;
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!IsDecisionShape(Node.Shape))
            {
                continue;
            }

            if (CountVisibleOutgoing(Contract, Node.Id) >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNonDecisionTerminalCollectorSink(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (IsDecisionShape(Node.Shape))
            {
                continue;
            }

            if (CountVisibleOutgoing(Contract, Node.Id) > 0)
            {
                continue;
            }

            if (IsTerminalCollectorTarget(Contract, Node.Id, MutableById))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTerminalCollectorTarget(
        DiagramContract Contract,
        string TargetNodeId,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById)
    {
        if (!MutableById.ContainsKey(TargetNodeId))
        {
            return false;
        }

        int IncomingCount = CountVisibleIncoming(Contract, TargetNodeId);
        bool HasCylinderSource = false;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(Edge.TargetId, TargetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsCylinderShape(GetNodeShape(Contract, Edge.SourceId)))
            {
                HasCylinderSource = true;
                break;
            }
        }

        const int MINIMUM_INCOMING_FOR_TERMINAL_COLLECTOR = 3;
        return IncomingCount >= MINIMUM_INCOMING_FOR_TERMINAL_COLLECTOR
            || (HasCylinderSource && IncomingCount >= 2);
    }

    private static HashSet<string> BuildPrimarySpineNodeIds(DiagramContract Contract, string RootNodeId)
    {
        HashSet<string> SpineNodeIds = new HashSet<string>(StringComparer.Ordinal);
        string? CurrentNodeId = RootNodeId;
        while (CurrentNodeId is not null)
        {
            if (!SpineNodeIds.Add(CurrentNodeId))
            {
                break;
            }

            string PrimarySinkId = ResolvePrimarySinkId(Contract);
            FlowchartSpineLayoutContext TemporaryContext = new FlowchartSpineLayoutContext(
                SpineNodeIds,
                RootNodeId,
                PrimarySinkId,
                0.0,
                0.0);
            EdgeContract? NextEdge = SelectSpineOutgoingEdge(Contract, CurrentNodeId, TemporaryContext);
            CurrentNodeId = NextEdge?.TargetId;
        }

        return SpineNodeIds;
    }

    private static string ResolvePrimarySinkId(DiagramContract Contract)
    {
        List<string> SinkCandidates = new List<string>();
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (CountVisibleOutgoing(Contract, Node.Id) == 0)
            {
                SinkCandidates.Add(Node.Id);
            }
        }

        if (SinkCandidates.Count == 0)
        {
            return Contract.Nodes[Contract.Nodes.Count - 1].Id;
        }

        return SinkCandidates
            .OrderByDescending(NodeId => GetLatestCreationIndex(Contract, NodeId))
            .First();
    }

    private static List<EdgeContract> GetVisibleOutgoingEdges(DiagramContract Contract, string SourceNodeId)
    {
        List<EdgeContract> OutgoingEdges = new List<EdgeContract>();
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, SourceNodeId, StringComparison.Ordinal))
            {
                OutgoingEdges.Add(Edge);
            }
        }

        OutgoingEdges.Sort((Left, Right) => Left.CreationIndex.CompareTo(Right.CreationIndex));
        return OutgoingEdges;
    }

    private static EdgeContract? FindYesLabeledEdge(IReadOnlyList<EdgeContract> OutgoingEdges)
    {
        foreach (EdgeContract Edge in OutgoingEdges)
        {
            if (string.Equals(Edge.Label, "Yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Edge.Label, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return Edge;
            }
        }

        return null;
    }

    private static EdgeContract? FindNoLabeledEdge(IReadOnlyList<EdgeContract> OutgoingEdges)
    {
        foreach (EdgeContract Edge in OutgoingEdges)
        {
            if (string.Equals(Edge.Label, "No", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Edge.Label, "no", StringComparison.OrdinalIgnoreCase))
            {
                return Edge;
            }
        }

        return null;
    }

    private static bool HasDirectedPath(DiagramContract Contract, string StartNodeId, string TargetNodeId)
    {
        if (string.Equals(StartNodeId, TargetNodeId, StringComparison.Ordinal))
        {
            return true;
        }

        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        Queue<string> Pending = new Queue<string>();
        Pending.Enqueue(StartNodeId);
        while (Pending.Count > 0)
        {
            string NodeId = Pending.Dequeue();
            if (!Visited.Add(NodeId))
            {
                continue;
            }

            foreach (EdgeContract Edge in GetVisibleOutgoingEdges(Contract, NodeId))
            {
                if (string.Equals(Edge.TargetId, TargetNodeId, StringComparison.Ordinal))
                {
                    return true;
                }

                Pending.Enqueue(Edge.TargetId);
            }
        }

        return false;
    }

    private static int ShortestPathLength(DiagramContract Contract, string StartNodeId, string TargetNodeId)
    {
        if (string.Equals(StartNodeId, TargetNodeId, StringComparison.Ordinal))
        {
            return 0;
        }

        Dictionary<string, int> DistanceByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        Queue<string> Pending = new Queue<string>();
        Pending.Enqueue(StartNodeId);
        DistanceByNodeId[StartNodeId] = 0;

        while (Pending.Count > 0)
        {
            string NodeId = Pending.Dequeue();
            int CurrentDistance = DistanceByNodeId[NodeId];
            foreach (EdgeContract Edge in GetVisibleOutgoingEdges(Contract, NodeId))
            {
                if (DistanceByNodeId.ContainsKey(Edge.TargetId))
                {
                    continue;
                }

                int NextDistance = CurrentDistance + 1;
                if (string.Equals(Edge.TargetId, TargetNodeId, StringComparison.Ordinal))
                {
                    return NextDistance;
                }

                DistanceByNodeId[Edge.TargetId] = NextDistance;
                Pending.Enqueue(Edge.TargetId);
            }
        }

        return int.MaxValue;
    }

    private static int CountVisibleOutgoing(DiagramContract Contract, string NodeId)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static int CountVisibleIncoming(DiagramContract Contract, string NodeId)
    {
        int Count = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }

    private static List<string> FindRootNodeIds(DiagramContract Contract, IEnumerable<string> NodeIds)
    {
        HashSet<string> TargetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TargetIds.Add(Edge.TargetId);
        }

        List<string> RootNodeIds = new List<string>();
        foreach (string NodeId in NodeIds)
        {
            if (!TargetIds.Contains(NodeId))
            {
                RootNodeIds.Add(NodeId);
            }
        }

        return RootNodeIds;
    }

    private static int GetEarliestCreationIndex(DiagramContract Contract, string NodeId)
    {
        int Earliest = int.MaxValue;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Earliest = Math.Min(Earliest, Edge.CreationIndex);
            }
        }

        return Earliest == int.MaxValue ? 0 : Earliest;
    }

    private static int GetLatestCreationIndex(DiagramContract Contract, string NodeId)
    {
        int Latest = int.MinValue;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                || string.Equals(Edge.TargetId, NodeId, StringComparison.Ordinal))
            {
                Latest = Math.Max(Latest, Edge.CreationIndex);
            }
        }

        return Latest == int.MinValue ? 0 : Latest;
    }

    private static double GetTypicalNodeWidth(IEnumerable<FlowchartLayoutNodeBounds> Bounds)
    {
        List<double> Widths = Bounds.Select(Bound => Bound.Width).OrderBy(Width => Width).ToList();
        if (Widths.Count == 0)
        {
            return 120.0;
        }

        return Widths[Widths.Count / 2];
    }

    private static string? GetNodeShape(DiagramContract Contract, string NodeId)
    {
        NodeContract? Node = Contract.Nodes.FirstOrDefault(Candidate => Candidate.Id == NodeId);
        return Node?.Shape;
    }

    private static bool IsCylinderShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "cylinder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "database", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }
}
