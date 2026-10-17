using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Keeps the primary TB/BT spine in one column and assigns side branches to margin lanes (F58).
/// </summary>
public static partial class FlowchartSpineColumnPostProcessor
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Aligns the primary spine to a shared cross-axis and places side branches in margin columns.
    /// </summary>
    public static List<FlowchartLayoutNodeBounds> Apply(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Source.Count <= 1)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (HorizontalFlow)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        if (!ShouldApplySpineColumnLayout(Contract, MutableById))
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        List<string> RootNodeIds = FindRootNodeIds(Contract, MutableById.Keys);
        if (RootNodeIds.Count == 0)
        {
            return MutableById.Values.ToList();
        }

        string PrimaryRootId = RootNodeIds
            .OrderBy(NodeId => GetEarliestCreationIndex(Contract, NodeId))
            .First();
        HashSet<string> SpineNodeIds = BuildPrimarySpineNodeIds(Contract, PrimaryRootId);
        if (SpineNodeIds.Count == 0)
        {
            return MutableById.Values.ToList();
        }

        if (!MutableById.TryGetValue(PrimaryRootId, out FlowchartLayoutNodeBounds RootBounds))
        {
            return MutableById.Values.ToList();
        }

        double SpineCrossAxis = RootBounds.CenterX;
        foreach (string SpineNodeId in SpineNodeIds)
        {
            if (!MutableById.TryGetValue(SpineNodeId, out FlowchartLayoutNodeBounds SpineBounds))
            {
                continue;
            }

            MutableById[SpineNodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = SpineBounds.NodeId,
                CenterX = SpineCrossAxis,
                CenterY = SpineBounds.CenterY,
                Width = SpineBounds.Width,
                Height = SpineBounds.Height
            };
        }

        double ColumnStep = Math.Max(Options.NodeSpacingPx, GetTypicalNodeWidth(MutableById.Values) + Options.NodeSpacingPx);
        AssignSideBranchColumns(Contract, MutableById, SpineNodeIds, SpineCrossAxis, ColumnStep);
        AlignTerminalCollectorRows(Contract, MutableById, SpineNodeIds, SpineCrossAxis, ColumnStep, Options);

        if (CountOverlappingPairs(MutableById.Values.ToList()) > CountOverlappingPairs(Source))
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        return MutableById.Values.ToList();
    }

    /// <summary>
    /// Counts the node pairs whose boxes overlap. The spine column layout is dropped when it would add an overlap,
    /// for example when several unlabelled decision branches would all be sent to the same margin column.
    /// </summary>
    private static int CountOverlappingPairs(IReadOnlyList<FlowchartLayoutNodeBounds> Bounds)
    {
        int Count = 0;
        for (int FirstIndex = 0; FirstIndex < Bounds.Count; FirstIndex++)
        {
            for (int SecondIndex = FirstIndex + 1; SecondIndex < Bounds.Count; SecondIndex++)
            {
                FlowchartLayoutNodeBounds First = Bounds[FirstIndex];
                FlowchartLayoutNodeBounds Second = Bounds[SecondIndex];
                double OverlapX = ((First.Width + Second.Width) / 2.0) - Math.Abs(First.CenterX - Second.CenterX);
                double OverlapY = ((First.Height + Second.Height) / 2.0) - Math.Abs(First.CenterY - Second.CenterY);
                if (OverlapX > COLLINEAR_EPSILON_PX && OverlapY > COLLINEAR_EPSILON_PX)
                {
                    Count++;
                }
            }
        }

        return Count;
    }

    private static void AssignSideBranchColumns(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        HashSet<string> SpineNodeIds,
        double SpineCrossAxis,
        double ColumnStep)
    {
        HashSet<string> SideBranchNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MutableById.ContainsKey(Edge.SourceId)
                || !MutableById.ContainsKey(Edge.TargetId))
            {
                continue;
            }

            if (SpineNodeIds.Contains(Edge.TargetId))
            {
                continue;
            }

            NodeContract? SourceNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.SourceId);
            if (SourceNode is null || !IsDecisionShape(SourceNode.Shape))
            {
                continue;
            }

            SideBranchNodeIds.Add(Edge.TargetId);
        }

        foreach (string SideBranchNodeId in SideBranchNodeIds)
        {
            if (!MutableById.TryGetValue(SideBranchNodeId, out FlowchartLayoutNodeBounds SideBounds))
            {
                continue;
            }

            EdgeContract? DecisionEdge = FindDecisionEdgeToTarget(Contract, SideBranchNodeId);
            double TargetCrossAxis = ResolveSideBranchCrossAxis(
                Contract,
                SideBranchNodeId,
                SideBranchNodeIds,
                SpineCrossAxis,
                ColumnStep,
                DecisionEdge);
            MutableById[SideBranchNodeId] = new FlowchartLayoutNodeBounds
            {
                NodeId = SideBounds.NodeId,
                CenterX = TargetCrossAxis,
                CenterY = SideBounds.CenterY,
                Width = SideBounds.Width,
                Height = SideBounds.Height
            };
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!SideBranchNodeIds.Contains(Edge.SourceId)
                || !SpineNodeIds.Contains(Edge.TargetId))
            {
                continue;
            }

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                continue;
            }

            if (!IsNoLabeledEdge(FindDecisionEdgeToTarget(Contract, Edge.SourceId)))
            {
                continue;
            }

            double MarginCrossAxis = SpineCrossAxis - (ColumnStep * 2.0);
            if (Math.Abs(SourceBounds.CenterX - MarginCrossAxis) > COLLINEAR_EPSILON_PX)
            {
                MutableById[Edge.SourceId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = SourceBounds.NodeId,
                    CenterX = MarginCrossAxis,
                    CenterY = SourceBounds.CenterY,
                    Width = SourceBounds.Width,
                    Height = SourceBounds.Height
                };
            }
        }
    }

    private static bool ShouldApplySpineColumnLayout<T>(
        DiagramContract Contract,
        IReadOnlyDictionary<string, T> MutableById)
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

    private static bool HasNonDecisionTerminalCollectorSink<T>(
        DiagramContract Contract,
        IReadOnlyDictionary<string, T> MutableById)
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

    private static void AlignTerminalCollectorRows(
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        HashSet<string> SpineNodeIds,
        double SpineCrossAxis,
        double ColumnStep,
        FlowchartRenderOptions Options)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!IsTerminalCollectorTarget(Contract, Node.Id, MutableById))
            {
                continue;
            }

            if (!MutableById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            double CollectorRowY = ResolveTerminalCollectorRowY(
                Contract,
                Node.Id,
                TargetBounds,
                MutableById,
                SpineCrossAxis,
                ColumnStep,
                Options);
            if (double.IsNegativeInfinity(CollectorRowY))
            {
                continue;
            }

            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(Edge.TargetId, Node.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
                {
                    continue;
                }

                bool IsFarLeftMargin = SourceBounds.CenterX < SpineCrossAxis - (ColumnStep * 1.25);
                bool IsSpineColumnCollector = IsSpineColumnCollectorSource(Contract, Edge.SourceId);
                if (!IsFarLeftMargin && !IsSpineColumnCollector)
                {
                    continue;
                }

                double TargetCrossAxis = IsFarLeftMargin
                    ? SpineCrossAxis - (ColumnStep * 2.0)
                    : SpineCrossAxis;
                double TargetCenterY = IsFarLeftMargin
                    ? CollectorRowY
                    : SourceBounds.CenterY;
                MutableById[Edge.SourceId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = SourceBounds.NodeId,
                    CenterX = TargetCrossAxis,
                    CenterY = TargetCenterY,
                    Width = SourceBounds.Width,
                    Height = SourceBounds.Height
                };
            }
        }
    }

    private static double ResolveTerminalCollectorRowY(
        DiagramContract Contract,
        string TargetNodeId,
        FlowchartLayoutNodeBounds TargetBounds,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        double SpineCrossAxis,
        double ColumnStep,
        FlowchartRenderOptions Options)
    {
        double CollectorRowY = double.PositiveInfinity;
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

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                continue;
            }

            if (SourceBounds.CenterX < SpineCrossAxis - (ColumnStep * 1.25))
            {
                continue;
            }

            if (IsDecisionShape(GetNodeShape(Contract, Edge.SourceId)))
            {
                continue;
            }

            if (SourceBounds.CenterY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX
                && SourceBounds.CenterY < CollectorRowY)
            {
                CollectorRowY = SourceBounds.CenterY;
            }
        }

        if (double.IsPositiveInfinity(CollectorRowY))
        {
            double TypicalHeight = GetTypicalNodeHeight(MutableById.Values);
            CollectorRowY = TargetBounds.CenterY + Options.RankSpacingPx + (TypicalHeight / 2.0);
        }

        return CollectorRowY;
    }

    private static bool IsTerminalCollectorTarget<T>(
        DiagramContract Contract,
        string TargetNodeId,
        IReadOnlyDictionary<string, T> MutableById)
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

    private static bool IsSpineColumnCollectorSource(DiagramContract Contract, string SourceNodeId) => IsCylinderShape(GetNodeShape(Contract, SourceNodeId));

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

    private static EdgeContract? FindDecisionEdgeToTarget(DiagramContract Contract, string TargetNodeId)
    {
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

            NodeContract? SourceNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.SourceId);
            if (SourceNode is not null && IsDecisionShape(SourceNode.Shape))
            {
                return Edge;
            }
        }

        return null;
    }

    private static bool IsYesLabeledEdge(EdgeContract? Edge)
    {
        if (Edge is null || string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        return string.Equals(Edge.Label.Trim(), "Yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.Label.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoLabeledEdge(EdgeContract? Edge)
    {
        if (Edge is null || string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        return string.Equals(Edge.Label.Trim(), "No", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Edge.Label.Trim(), "no", StringComparison.OrdinalIgnoreCase);
    }

    private static double ResolveSideBranchCrossAxis(
        DiagramContract Contract,
        string SideBranchNodeId,
        HashSet<string> SideBranchNodeIds,
        double SpineCrossAxis,
        double ColumnStep,
        EdgeContract? DecisionEdge)
    {
        bool FeedsSpineMerge = Contract.Edges.Any(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Edge.SourceId, SideBranchNodeId, StringComparison.Ordinal)
            && !SideBranchNodeIds.Contains(Edge.TargetId));

        bool FeedsTerminal = Contract.Edges.Any(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Edge.SourceId, SideBranchNodeId, StringComparison.Ordinal)
            && CountVisibleIncoming(Contract, Edge.TargetId) >= 2);

        if (IsNoLabeledEdge(DecisionEdge) || (FeedsTerminal && !FeedsSpineMerge))
        {
            return SpineCrossAxis - (ColumnStep * 2.0);
        }

        if (IsYesLabeledEdge(DecisionEdge) && FeedsSpineMerge)
        {
            return SpineCrossAxis - ColumnStep;
        }

        if (IsYesLabeledEdge(DecisionEdge))
        {
            return SpineCrossAxis + ColumnStep;
        }

        return SpineCrossAxis - (ColumnStep * 2.0);
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

            EdgeContract? NextEdge = SelectSpineOutgoingEdge(Contract, CurrentNodeId);
            CurrentNodeId = NextEdge?.TargetId;
        }

        return SpineNodeIds;
    }

    private static EdgeContract? SelectSpineOutgoingEdge(DiagramContract Contract, string SourceNodeId)
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

        string PrimarySinkId = ResolvePrimarySinkId(Contract);
        EdgeContract? YesEdge = FindYesLabeledEdge(OutgoingEdges);
        EdgeContract? NoEdge = FindNoLabeledEdge(OutgoingEdges);
        if (YesEdge is not null && NoEdge is not null)
        {
            if (HasDirectedPath(Contract, YesEdge.TargetId, NoEdge.TargetId))
            {
                if (!string.Equals(NoEdge.TargetId, PrimarySinkId, StringComparison.Ordinal))
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
        int BestDistance = ShortestPathLength(Contract, BestEdge.TargetId, PrimarySinkId);
        for (int EdgeIndex = 1; EdgeIndex < OutgoingEdges.Count; EdgeIndex++)
        {
            EdgeContract Candidate = OutgoingEdges[EdgeIndex];
            int CandidateDistance = ShortestPathLength(Contract, Candidate.TargetId, PrimarySinkId);
            if (CandidateDistance > BestDistance)
            {
                BestDistance = CandidateDistance;
                BestEdge = Candidate;
            }
        }

        return BestEdge;
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

    private static string ResolvePrimarySinkId(DiagramContract Contract)
    {
        List<string> SinkCandidates = [];
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

    private static List<EdgeContract> GetVisibleOutgoingEdges(DiagramContract Contract, string SourceNodeId)
    {
        List<EdgeContract> OutgoingEdges = [];
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

        List<string> RootNodeIds = [];
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

    private static double GetTypicalNodeHeight(IEnumerable<FlowchartLayoutNodeBounds> Bounds)
    {
        List<double> Heights = Bounds.Select(Bound => Bound.Height).OrderBy(Height => Height).ToList();
        if (Heights.Count == 0)
        {
            return 40.0;
        }

        return Heights[Heights.Count / 2];
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
