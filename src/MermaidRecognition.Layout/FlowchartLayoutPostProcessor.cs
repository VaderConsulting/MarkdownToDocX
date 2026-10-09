using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Adjusts Sugiyama layout for Mermaid parity (branch centring, edge-label clearance).
/// </summary>
public static class FlowchartLayoutPostProcessor
{
    private const double EDGE_LABEL_FLOW_AXIS_MARGIN_PX = 16.0;

    /// <summary>
    /// Applies fan-out centring and edge-label spacing adjustments.
    /// </summary>
    public static List<FlowchartLayoutNodeBounds> Apply(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        List<FlowchartLayoutNodeBounds> Adjusted = OrderDisconnectedComponents(Contract, Source, Direction, Options);
        Adjusted = CenterSingleSuccessorSources(Contract, Adjusted, Direction);
        Adjusted = SpreadFanOutTargetsAroundHub(Contract, Adjusted, Direction, Options);
        Adjusted = SpreadFanInSourcesByCreationOrder(Contract, Adjusted, Direction, Options);
        Adjusted = CenterFanInTargets(Contract, Adjusted, Direction);
        Adjusted = CenterFanOutSources(Contract, Adjusted, Direction);
        Adjusted = OrderDecisionFanOutTargets(Contract, Adjusted, Direction, Options);
        Adjusted = CenterSingleSuccessorSources(Contract, Adjusted, Direction);
        Adjusted = MirrorFanInSourcesThroughHub(Contract, Adjusted, Direction, Options);
        Adjusted = AlignRootNodesToCommonRank(Contract, Adjusted, Direction);
        Adjusted = ExpandFlowAxisGapForMinLength(Contract, Adjusted, Direction, Options);
        Adjusted = ExpandFlowAxisGapForEdgeLabels(Contract, Adjusted, Direction, Options);
        Adjusted = ApplyInvisibleEdgeStackPlacement(Contract, Adjusted, Direction, Options);
        Adjusted = FlowchartSubgraphLayoutPostProcessor.Apply(Contract, Adjusted, Direction, Options);
        Adjusted = EnforceMinimumCrossAxisSeparation(Adjusted, Direction, Options);
        Adjusted = ApplyLrRetryCycleNodeLayout(Contract, Adjusted, Direction, Options);
        Adjusted = FlowchartReciprocalEdgeHelper.NormalizeTbFeedbackLoopNodeOrder(Contract, Adjusted, Direction);
        Adjusted = FlowchartSpineColumnPostProcessor.Apply(Contract, Adjusted, Direction, Options);
        Adjusted = FlowchartDecisionShoulderLayoutPostProcessor.Apply(Contract, Adjusted, Direction, Options);
        return Adjusted;
    }

    private static List<FlowchartLayoutNodeBounds> OrderDisconnectedComponents(
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

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        Dictionary<string, string> ComponentRootByNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            ComponentRootByNodeId[Bounds.NodeId] = Bounds.NodeId;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ComponentRootByNodeId.ContainsKey(Edge.SourceId)
                || !ComponentRootByNodeId.ContainsKey(Edge.TargetId))
            {
                continue;
            }

            UnionComponentRoots(ComponentRootByNodeId, Edge.SourceId, Edge.TargetId);
        }

        Dictionary<string, List<string>> NodeIdsByComponentRoot =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> Entry in ComponentRootByNodeId)
        {
            if (!NodeIdsByComponentRoot.TryGetValue(Entry.Value, out List<string>? NodeIds))
            {
                NodeIds = [];
                NodeIdsByComponentRoot[Entry.Value] = NodeIds;
            }

            NodeIds.Add(Entry.Key);
        }

        if (NodeIdsByComponentRoot.Count <= 1)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        Dictionary<string, int> CreationIndexByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int NodeIndex = 0; NodeIndex < Contract.Nodes.Count; NodeIndex++)
        {
            CreationIndexByNodeId[Contract.Nodes[NodeIndex].Id] = NodeIndex;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (CreationIndexByNodeId.TryGetValue(Edge.SourceId, out int SourceIndex))
            {
                CreationIndexByNodeId[Edge.SourceId] = Math.Min(SourceIndex, Edge.CreationIndex);
            }

            if (CreationIndexByNodeId.TryGetValue(Edge.TargetId, out int TargetIndex))
            {
                CreationIndexByNodeId[Edge.TargetId] = Math.Min(TargetIndex, Edge.CreationIndex);
            }
        }

        List<ComponentLayoutGroup> ComponentGroups = [];
        foreach (KeyValuePair<string, List<string>> Entry in NodeIdsByComponentRoot)
        {
            List<string> NodeIds = Entry.Value;
            int MinCreationIndex = int.MaxValue;

            foreach (string NodeId in NodeIds)
            {
                if (CreationIndexByNodeId.TryGetValue(NodeId, out int CreationIndex)
                    && CreationIndex < MinCreationIndex)
                {
                    MinCreationIndex = CreationIndex;
                }
            }

            ComponentGroups.Add(new ComponentLayoutGroup(NodeIds, MinCreationIndex));
        }

        ComponentGroups.Sort((Left, Right) => Left.MinCreationIndex.CompareTo(Right.MinCreationIndex));

        bool AllSingleNodeComponents = ComponentGroups.All(Group => Group.NodeIds.Count == 1);
        bool PackOnFlowAxis = AllSingleNodeComponents;
        bool PackFromTrailingEdge = HorizontalFlow != PackOnFlowAxis;

        double CursorPackPosition = PackFromTrailingEdge ? double.MinValue : double.MaxValue;
        foreach (ComponentLayoutGroup Group in ComponentGroups)
        {
            (double GroupLeadingEdge, double GroupExtent) = GetComponentPackBounds(
                Group.NodeIds,
                BoundsById,
                PackOnFlowAxis,
                HorizontalFlow);
            if (PackFromTrailingEdge)
            {
                double GroupTrailingEdge = GroupLeadingEdge + GroupExtent;
                if (GroupTrailingEdge > CursorPackPosition)
                {
                    CursorPackPosition = GroupTrailingEdge;
                }
            }
            else if (GroupLeadingEdge < CursorPackPosition)
            {
                CursorPackPosition = GroupLeadingEdge;
            }
        }

        if (PackFromTrailingEdge && CursorPackPosition == double.MinValue)
        {
            CursorPackPosition = 0.0;
        }
        else if (!PackFromTrailingEdge && (double.IsPositiveInfinity(CursorPackPosition) || CursorPackPosition == double.MaxValue))
        {
            CursorPackPosition = 0.0;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        foreach (ComponentLayoutGroup Group in ComponentGroups)
        {
            (double GroupLeadingEdge, double GroupExtent) = GetComponentPackBounds(
                Group.NodeIds,
                MutableById,
                PackOnFlowAxis,
                HorizontalFlow);
            double GroupTrailingEdge = GroupLeadingEdge + GroupExtent;
            double Shift = PackFromTrailingEdge
                ? CursorPackPosition - GroupTrailingEdge
                : CursorPackPosition - GroupLeadingEdge;
            if (Math.Abs(Shift) > 0.01)
            {
                foreach (string NodeId in Group.NodeIds)
                {
                    FlowchartLayoutNodeBounds Bounds = MutableById[NodeId];
                    MutableById[NodeId] = ShiftBoundsOnPackAxis(Bounds, Shift, PackOnFlowAxis, HorizontalFlow);
                }
            }

            if (PackFromTrailingEdge)
            {
                CursorPackPosition -= GroupExtent + Options.NodeSpacingPx;
            }
            else
            {
                CursorPackPosition += GroupExtent + Options.NodeSpacingPx;
            }
        }

        if (PackOnFlowAxis && HorizontalFlow)
        {
            double AverageCenterY = 0.0;
            int NodeCount = 0;
            foreach (FlowchartLayoutNodeBounds Bounds in MutableById.Values)
            {
                AverageCenterY += Bounds.CenterY;
                NodeCount++;
            }

            if (NodeCount > 0)
            {
                AverageCenterY /= NodeCount;
                List<string> NodeIds = MutableById.Keys.ToList();
                foreach (string NodeId in NodeIds)
                {
                    FlowchartLayoutNodeBounds Bounds = MutableById[NodeId];
                    MutableById[NodeId] = new FlowchartLayoutNodeBounds
                    {
                        NodeId = Bounds.NodeId,
                        CenterX = Bounds.CenterX,
                        CenterY = AverageCenterY,
                        Width = Bounds.Width,
                        Height = Bounds.Height
                    };
                }
            }
        }
        else if (PackOnFlowAxis && !HorizontalFlow)
        {
            double AverageCenterX = 0.0;
            int NodeCount = 0;
            foreach (FlowchartLayoutNodeBounds Bounds in MutableById.Values)
            {
                AverageCenterX += Bounds.CenterX;
                NodeCount++;
            }

            if (NodeCount > 0)
            {
                AverageCenterX /= NodeCount;
                List<string> NodeIds = MutableById.Keys.ToList();
                foreach (string NodeId in NodeIds)
                {
                    FlowchartLayoutNodeBounds Bounds = MutableById[NodeId];
                    MutableById[NodeId] = new FlowchartLayoutNodeBounds
                    {
                        NodeId = Bounds.NodeId,
                        CenterX = AverageCenterX,
                        CenterY = Bounds.CenterY,
                        Width = Bounds.Width,
                        Height = Bounds.Height
                    };
                }
            }
        }

        return MutableById.Values.ToList();
    }

    private static (double LeadingEdge, double Extent) GetComponentPackBounds(
        IReadOnlyList<string> NodeIds,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool PackOnFlowAxis,
        bool HorizontalFlow)
    {
        double LeadingEdge = double.MaxValue;
        double TrailingEdge = double.MinValue;

        foreach (string NodeId in NodeIds)
        {
            if (!BoundsById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            double Centre = GetPackPosition(Bounds, PackOnFlowAxis, HorizontalFlow);
            double HalfExtent = GetNodeExtentOnPackAxis(Bounds, PackOnFlowAxis, HorizontalFlow) / 2.0;
            double NodeLeadingEdge = Centre - HalfExtent;
            double NodeTrailingEdge = Centre + HalfExtent;

            if (NodeLeadingEdge < LeadingEdge)
            {
                LeadingEdge = NodeLeadingEdge;
            }

            if (NodeTrailingEdge > TrailingEdge)
            {
                TrailingEdge = NodeTrailingEdge;
            }
        }

        if (double.IsPositiveInfinity(LeadingEdge) || double.IsNegativeInfinity(TrailingEdge))
        {
            return (0.0, 0.0);
        }

        return (LeadingEdge, Math.Max(0.0, TrailingEdge - LeadingEdge));
    }

    private static double GetPackPosition(
        FlowchartLayoutNodeBounds Bounds,
        bool PackOnFlowAxis,
        bool HorizontalFlow)
    {
        if (PackOnFlowAxis)
        {
            return HorizontalFlow ? Bounds.CenterX : Bounds.CenterY;
        }

        return HorizontalFlow ? Bounds.CenterY : Bounds.CenterX;
    }

    private static double GetNodeExtentOnPackAxis(
        FlowchartLayoutNodeBounds Bounds,
        bool PackOnFlowAxis,
        bool HorizontalFlow)
    {
        if (PackOnFlowAxis)
        {
            return HorizontalFlow ? Bounds.Width : Bounds.Height;
        }

        return HorizontalFlow ? Bounds.Height : Bounds.Width;
    }

    private static FlowchartLayoutNodeBounds ShiftBoundsOnPackAxis(
        FlowchartLayoutNodeBounds Bounds,
        double Shift,
        bool PackOnFlowAxis,
        bool HorizontalFlow)
    {
        if (PackOnFlowAxis)
        {
            if (HorizontalFlow)
            {
                return new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = Bounds.CenterX + Shift,
                    CenterY = Bounds.CenterY,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                };
            }

            return new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY + Shift,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }

        if (HorizontalFlow)
        {
            return new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY + Shift,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }

        return new FlowchartLayoutNodeBounds
        {
            NodeId = Bounds.NodeId,
            CenterX = Bounds.CenterX + Shift,
            CenterY = Bounds.CenterY,
            Width = Bounds.Width,
            Height = Bounds.Height
        };
    }

    private static void UnionComponentRoots(
        Dictionary<string, string> ComponentRootByNodeId,
        string NodeA,
        string NodeB)
    {
        string RootA = FindComponentRoot(ComponentRootByNodeId, NodeA);
        string RootB = FindComponentRoot(ComponentRootByNodeId, NodeB);
        if (!string.Equals(RootA, RootB, StringComparison.Ordinal))
        {
            ComponentRootByNodeId[RootB] = RootA;
        }
    }

    private static string FindComponentRoot(Dictionary<string, string> ComponentRootByNodeId, string NodeId)
    {
        string Root = NodeId;
        while (!string.Equals(ComponentRootByNodeId[Root], Root, StringComparison.Ordinal))
        {
            Root = ComponentRootByNodeId[Root];
        }

        ComponentRootByNodeId[NodeId] = Root;
        return Root;
    }

    private sealed class ComponentLayoutGroup(
        List<string> NodeIds,
        int MinCreationIndex)
    {
        public List<string> NodeIds { get; } = NodeIds;

        public int MinCreationIndex { get; } = MinCreationIndex;
    }

    private static List<FlowchartLayoutNodeBounds> AlignRootNodesToCommonRank(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction)
    {
        if (Source.Count <= 1)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        HashSet<string> TargetIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> SourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TargetIds.Add(Edge.TargetId);
            SourceIds.Add(Edge.SourceId);
        }

        List<string> RootNodeIds = [];
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            if (!TargetIds.Contains(Bounds.NodeId) && SourceIds.Contains(Bounds.NodeId))
            {
                RootNodeIds.Add(Bounds.NodeId);
            }
        }

        if (RootNodeIds.Count <= 1)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool ReverseFlow = Direction == FlowchartDirection.RightToLeft
            || Direction == FlowchartDirection.BottomToTop;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        double RankGap = Math.Max(24.0, Options.RankSpacingPx);
        foreach (string RootNodeId in RootNodeIds)
        {
            FlowchartLayoutNodeBounds RootBounds = MutableById[RootNodeId];
            if (!TryGetFacingSuccessorBounds(RootNodeId, Contract, MutableById, HorizontalFlow, ReverseFlow, out FlowchartLayoutNodeBounds SuccessorBounds))
            {
                continue;
            }

            if (HorizontalFlow)
            {
                double SuccessorEdge = ReverseFlow
                    ? SuccessorBounds.CenterX + (SuccessorBounds.Width / 2.0)
                    : SuccessorBounds.CenterX - (SuccessorBounds.Width / 2.0);
                double RootHalf = RootBounds.Width / 2.0;
                double DesiredCenterX = ReverseFlow
                    ? SuccessorEdge + RankGap + RootHalf
                    : SuccessorEdge - RankGap - RootHalf;
                MutableById[RootNodeId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = RootBounds.NodeId,
                    CenterX = DesiredCenterX,
                    CenterY = RootBounds.CenterY,
                    Width = RootBounds.Width,
                    Height = RootBounds.Height
                };
            }
            else
            {
                double SuccessorEdge = ReverseFlow
                    ? SuccessorBounds.CenterY - (SuccessorBounds.Height / 2.0)
                    : SuccessorBounds.CenterY + (SuccessorBounds.Height / 2.0);
                double RootHalf = RootBounds.Height / 2.0;
                double DesiredCenterY = ReverseFlow
                    ? SuccessorEdge - RankGap - RootHalf
                    : SuccessorEdge + RankGap + RootHalf;
                MutableById[RootNodeId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = RootBounds.NodeId,
                    CenterX = RootBounds.CenterX,
                    CenterY = DesiredCenterY,
                    Width = RootBounds.Width,
                    Height = RootBounds.Height
                };
            }
        }

        return MutableById.Values.ToList();
    }

    private static bool TryGetFacingSuccessorBounds(
        string RootNodeId,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow,
        bool ReverseFlow,
        out FlowchartLayoutNodeBounds SuccessorBounds)
    {
        SuccessorBounds = new FlowchartLayoutNodeBounds();
        bool Found = false;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(Edge.SourceId, RootNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            if (!Found)
            {
                SuccessorBounds = TargetBounds;
                Found = true;
                continue;
            }

            bool PreferTarget;
            if (HorizontalFlow)
            {
                PreferTarget = ReverseFlow
                    ? TargetBounds.CenterX > SuccessorBounds.CenterX
                    : TargetBounds.CenterX < SuccessorBounds.CenterX;
            }
            else
            {
                PreferTarget = ReverseFlow
                    ? TargetBounds.CenterY < SuccessorBounds.CenterY
                    : TargetBounds.CenterY > SuccessorBounds.CenterY;
            }

            if (PreferTarget)
            {
                SuccessorBounds = TargetBounds;
            }
        }

        return Found;
    }

    private static List<FlowchartLayoutNodeBounds> ExpandFlowAxisGapForMinLength(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Source.Count == 0)
        {
            return [];
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool ReverseFlow = Direction == FlowchartDirection.RightToLeft
            || Direction == FlowchartDirection.BottomToTop;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        foreach (EdgeContract Edge in Contract.Edges.OrderBy(Edge => Edge.CreationIndex))
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int MinLength = Math.Max(1, Edge.MinLength);
            if (MinLength <= 1)
            {
                continue;
            }

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            double PushDelta = (MinLength - 1) * Options.RankSpacingPx;
            PushTargetAwayFromSource(
                MutableById,
                Edge.TargetId,
                SourceBounds,
                TargetBounds,
                HorizontalFlow,
                ReverseFlow,
                PushDelta);
        }

        return MutableById.Values.ToList();
    }

    private static void PushTargetAwayFromSource(
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        string TargetId,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        bool HorizontalFlow,
        bool ReverseFlow,
        double PushDelta)
    {
        if (PushDelta <= 0.0 || !MutableById.TryGetValue(TargetId, out FlowchartLayoutNodeBounds Bounds))
        {
            return;
        }

        double SourceFlowPosition = HorizontalFlow ? SourceBounds.CenterX : SourceBounds.CenterY;
        double TargetFlowPosition = HorizontalFlow ? TargetBounds.CenterX : TargetBounds.CenterY;
        double Shift = -Math.Sign(SourceFlowPosition - TargetFlowPosition) * PushDelta;
        if (ReverseFlow)
        {
            Shift = -Shift;
        }

        if (HorizontalFlow)
        {
            MutableById[TargetId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX + Shift,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }
        else
        {
            MutableById[TargetId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY + Shift,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }
    }

    private static HashSet<string> CollectTargetSubtree(string RootTargetId, DiagramContract Contract)
    {
        HashSet<string> SubtreeNodeIds = new HashSet<string>(StringComparer.Ordinal);
        Queue<string> Pending = new Queue<string>();
        Pending.Enqueue(RootTargetId);

        while (Pending.Count > 0)
        {
            string NodeId = Pending.Dequeue();
            if (!SubtreeNodeIds.Add(NodeId))
            {
                continue;
            }

            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (!string.Equals(Edge.SourceId, NodeId, StringComparison.Ordinal)
                    || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Pending.Enqueue(Edge.TargetId);
            }
        }

        return SubtreeNodeIds;
    }

    private static void PushSubtreeNodes(
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        HashSet<string> SubtreeNodeIds,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        bool HorizontalFlow,
        bool ReverseFlow,
        double PushDelta)
    {
        if (PushDelta <= 0.0)
        {
            return;
        }

        double TargetFlowPosition = HorizontalFlow ? TargetBounds.CenterX : TargetBounds.CenterY;
        double SourceFlowPosition = HorizontalFlow ? SourceBounds.CenterX : SourceBounds.CenterY;
        bool PushIncreasing = TargetFlowPosition >= SourceFlowPosition;
        if (ReverseFlow)
        {
            PushIncreasing = !PushIncreasing;
        }

        double Shift = PushIncreasing ? PushDelta : -PushDelta;
        foreach (string NodeId in SubtreeNodeIds)
        {
            if (!MutableById.TryGetValue(NodeId, out FlowchartLayoutNodeBounds Bounds))
            {
                continue;
            }

            if (HorizontalFlow)
            {
                MutableById[NodeId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = Bounds.CenterX + Shift,
                    CenterY = Bounds.CenterY,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                };
            }
            else
            {
                MutableById[NodeId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = Bounds.CenterX,
                    CenterY = Bounds.CenterY + Shift,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                };
            }
        }
    }

    private static List<FlowchartLayoutNodeBounds> CenterFanOutSources(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction) => CenterSourcesWithTargetCount(Contract, Source, Direction, MinimumTargetCount: 2);

    private static List<FlowchartLayoutNodeBounds> CenterFanInTargets(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction) => CenterTargetsWithSourceCount(Contract, Source, Direction, MinimumSourceCount: 2);

    private static List<FlowchartLayoutNodeBounds> CenterSingleSuccessorSources(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        HashSet<string> FanInSourceIds = CollectFanInSourceIds(Contract);
        return CenterSourcesWithTargetCount(
            Contract,
            Source,
            Direction,
            MinimumTargetCount: 1,
            MaximumTargetCount: 1,
            ExcludedSourceIds: FanInSourceIds,
            MoveTargetsOnCrossAxis: HorizontalFlow);
    }

    private static HashSet<string> CollectFanInSourceIds(DiagramContract Contract)
    {
        Dictionary<string, List<string>> SourceIdsByTargetId =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || BackEdges.Contains(Edge))
            {
                continue;
            }

            if (!SourceIdsByTargetId.TryGetValue(Edge.TargetId, out List<string>? SourceIds))
            {
                SourceIds = [];
                SourceIdsByTargetId[Edge.TargetId] = SourceIds;
            }

            if (!SourceIds.Contains(Edge.SourceId, StringComparer.Ordinal))
            {
                SourceIds.Add(Edge.SourceId);
            }
        }

        HashSet<string> FanInSourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<string>> Entry in SourceIdsByTargetId)
        {
            if (Entry.Value.Count < 2)
            {
                continue;
            }

            foreach (string SourceId in Entry.Value)
            {
                FanInSourceIds.Add(SourceId);
            }
        }

        return FanInSourceIds;
    }

    private static List<FlowchartLayoutNodeBounds> CenterSourcesWithTargetCount(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        int MinimumTargetCount,
        int MaximumTargetCount = int.MaxValue,
        HashSet<string>? ExcludedSourceIds = null,
        bool MoveTargetsOnCrossAxis = false)
    {
        if (Source.Count == 0)
        {
            return [];
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, double> CrossAxisAdjustments = new Dictionary<string, double>(StringComparer.Ordinal);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (ExcludedSourceIds is not null && ExcludedSourceIds.Contains(Node.Id))
            {
                continue;
            }

            if (IsDecisionShape(Node.Shape))
            {
                continue;
            }

            List<string> TargetIds = Contract.Edges
                .Where(Edge => string.Equals(Edge.SourceId, Node.Id, StringComparison.Ordinal)
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Edge))
                .Select(Edge => Edge.TargetId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (TargetIds.Count < MinimumTargetCount || TargetIds.Count > MaximumTargetCount)
            {
                continue;
            }

            List<double> CrossPositions = [];
            foreach (string TargetId in TargetIds)
            {
                if (!BoundsById.TryGetValue(TargetId, out FlowchartLayoutNodeBounds TargetBounds))
                {
                    continue;
                }

                CrossPositions.Add(HorizontalFlow ? TargetBounds.CenterY : TargetBounds.CenterX);
            }

            if (CrossPositions.Count == 0)
            {
                continue;
            }

            if (MoveTargetsOnCrossAxis)
            {
                if (!BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds SourceBounds))
                {
                    continue;
                }

                double SourceCross = HorizontalFlow ? SourceBounds.CenterY : SourceBounds.CenterX;
                foreach (string TargetId in TargetIds)
                {
                    CrossAxisAdjustments[TargetId] = SourceCross;
                }
            }
            else
            {
                double AverageCross = CrossPositions.Sum() / CrossPositions.Count;
                CrossAxisAdjustments[Node.Id] = AverageCross;
            }
        }

        if (CrossAxisAdjustments.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        List<FlowchartLayoutNodeBounds> Adjusted = [];
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            if (!CrossAxisAdjustments.TryGetValue(Bounds.NodeId, out double CrossPosition))
            {
                Adjusted.Add(Bounds);
                continue;
            }

            if (HorizontalFlow)
            {
                Adjusted.Add(new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = Bounds.CenterX,
                    CenterY = CrossPosition,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                });
            }
            else
            {
                Adjusted.Add(new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = CrossPosition,
                    CenterY = Bounds.CenterY,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                });
            }
        }

        return Adjusted;
    }

    /// <summary>
    /// Centres a hub between its fan-in sources and lines each fan-out target up with one source (EIP Canonical Data Model:
    /// two "To canonical" boxes feed the hub, which feeds two "From canonical" boxes). Applies only when the hub has the same
    /// number of forward sources and targets, the hub is each target's only forward predecessor, the targets lead nowhere else,
    /// and the source spacing leaves room for the targets. Targets keep their existing order across the flow.
    /// </summary>
    private static List<FlowchartLayoutNodeBounds> MirrorFanInSourcesThroughHub(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById = new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> ForwardEdges = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) && !BackEdges.Contains(Edge) && !string.Equals(Edge.SourceId, Edge.TargetId, StringComparison.Ordinal)).ToList();
        bool Changed = false;

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (IsDecisionShape(Node.Shape) || !MutableById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds HubBounds))
            {
                continue;
            }

            List<string> SourceIds = ForwardEdges.Where(Edge => string.Equals(Edge.TargetId, Node.Id, StringComparison.Ordinal)).Select(Edge => Edge.SourceId).Distinct(StringComparer.Ordinal).ToList();
            List<string> TargetIds = ForwardEdges.Where(Edge => string.Equals(Edge.SourceId, Node.Id, StringComparison.Ordinal)).Select(Edge => Edge.TargetId).Distinct(StringComparer.Ordinal).ToList();
            if (SourceIds.Count < 2 || SourceIds.Count != TargetIds.Count)
            {
                continue;
            }

            bool TargetsQualify = true;
            foreach (string TargetId in TargetIds)
            {
                bool OnlyFromHub = ForwardEdges.Where(Edge => string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal)).All(Edge => string.Equals(Edge.SourceId, Node.Id, StringComparison.Ordinal));
                bool LeadsNowhere = !ForwardEdges.Any(Edge => string.Equals(Edge.SourceId, TargetId, StringComparison.Ordinal));
                if (!OnlyFromHub || !LeadsNowhere || !MutableById.ContainsKey(TargetId) || SourceIds.Contains(TargetId, StringComparer.Ordinal))
                {
                    TargetsQualify = false;
                    break;
                }
            }

            if (!TargetsQualify || SourceIds.Any(SourceId => !MutableById.ContainsKey(SourceId)))
            {
                continue;
            }

            List<double> SourceCrosses = SourceIds.Select(SourceId => GetCrossCenter(MutableById[SourceId], HorizontalFlow)).OrderBy(Cross => Cross).ToList();
            List<FlowchartLayoutNodeBounds> OrderedTargets = TargetIds.Select(TargetId => MutableById[TargetId]).OrderBy(Bounds => GetCrossCenter(Bounds, HorizontalFlow)).ToList();

            bool RoomForTargets = true;
            for (int Index = 1; Index < OrderedTargets.Count; Index++)
            {
                double Needed = ((GetCrossAxisExtent(OrderedTargets[Index - 1], HorizontalFlow) + GetCrossAxisExtent(OrderedTargets[Index], HorizontalFlow)) / 2.0) + Options.NodeSpacingPx;
                if (SourceCrosses[Index] - SourceCrosses[Index - 1] < Needed - 0.5)
                {
                    RoomForTargets = false;
                    break;
                }
            }

            if (!RoomForTargets)
            {
                continue;
            }

            MutableById[Node.Id] = SetCrossAxisCenter(HubBounds, HorizontalFlow, SourceCrosses.Average());
            for (int Index = 0; Index < OrderedTargets.Count; Index++)
            {
                MutableById[OrderedTargets[Index].NodeId] = SetCrossAxisCenter(OrderedTargets[Index], HorizontalFlow, SourceCrosses[Index]);
            }

            Changed = true;
        }

        if (!Changed)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        return Source.Select(Bounds => MutableById[Bounds.NodeId]).ToList();
    }

    private static double GetCrossCenter(FlowchartLayoutNodeBounds Bounds, bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return Bounds.CenterY;
        }

        return Bounds.CenterX;
    }

    private static List<FlowchartLayoutNodeBounds> SpreadFanOutTargetsAroundHub(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Source.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (IsDecisionShape(Node.Shape))
            {
                continue;
            }

            List<EdgeContract> OutgoingEdges = Contract.Edges
                .Where(Edge => string.Equals(Edge.SourceId, Node.Id, StringComparison.Ordinal)
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Edge))
                .OrderBy(Edge => Edge.CreationIndex)
                .ToList();

            if (OutgoingEdges.Count < 2)
            {
                continue;
            }

            if (!MutableById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds HubBounds))
            {
                continue;
            }

            double HubCross = HorizontalFlow ? HubBounds.CenterY : HubBounds.CenterX;
            double Step = Options.NodeSpacingPx;
            foreach (EdgeContract Edge in OutgoingEdges)
            {
                if (!MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
                {
                    continue;
                }

                Step = Math.Max(Step, GetCrossAxisCenterToCenterStep(TargetBounds, HorizontalFlow, Options.NodeSpacingPx));
            }

            double StartCross = HubCross - ((OutgoingEdges.Count - 1) * Step / 2.0);
            for (int EdgeIndex = 0; EdgeIndex < OutgoingEdges.Count; EdgeIndex++)
            {
                EdgeContract Edge = OutgoingEdges[EdgeIndex];
                if (!MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
                {
                    continue;
                }

                double TargetCross = ResolveCreationOrderedCrossPosition(
                    StartCross,
                    EdgeIndex,
                    OutgoingEdges.Count,
                    Step,
                    HorizontalFlow);
                MutableById[Edge.TargetId] = SetCrossAxisCenter(TargetBounds, HorizontalFlow, TargetCross);
            }
        }

        return MutableById.Values.ToList();
    }

    private static List<FlowchartLayoutNodeBounds> SpreadFanInSourcesByCreationOrder(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Source.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (NodeContract Node in Contract.Nodes)
        {
            List<EdgeContract> IncomingEdges = Contract.Edges
                .Where(Edge => string.Equals(Edge.TargetId, Node.Id, StringComparison.Ordinal)
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Edge))
                .OrderBy(Edge => Edge.CreationIndex)
                .ToList();

            if (IncomingEdges.Count < 2)
            {
                continue;
            }

            if (!MutableById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds HubBounds))
            {
                continue;
            }

            double HubCross = HorizontalFlow ? HubBounds.CenterY : HubBounds.CenterX;
            double Step = Options.NodeSpacingPx;
            foreach (EdgeContract Edge in IncomingEdges)
            {
                if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
                {
                    continue;
                }

                Step = Math.Max(Step, GetCrossAxisCenterToCenterStep(SourceBounds, HorizontalFlow, Options.NodeSpacingPx));
            }

            double StartCross = HubCross - ((IncomingEdges.Count - 1) * Step / 2.0);
            for (int EdgeIndex = 0; EdgeIndex < IncomingEdges.Count; EdgeIndex++)
            {
                EdgeContract Edge = IncomingEdges[EdgeIndex];
                if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
                {
                    continue;
                }

                double SourceCross = ResolveCreationOrderedCrossPosition(
                    StartCross,
                    EdgeIndex,
                    IncomingEdges.Count,
                    Step,
                    HorizontalFlow);
                MutableById[Edge.SourceId] = SetCrossAxisCenter(SourceBounds, HorizontalFlow, SourceCross);
            }
        }

        return MutableById.Values.ToList();
    }

    private static List<FlowchartLayoutNodeBounds> OrderDecisionFanOutTargets(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Source.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!IsDecisionShape(Node.Shape))
            {
                continue;
            }

            List<EdgeContract> OutgoingEdges = Contract.Edges
                .Where(Edge => string.Equals(Edge.SourceId, Node.Id, StringComparison.Ordinal)
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Edge))
                .OrderBy(Edge => Edge.CreationIndex)
                .ToList();

            if (OutgoingEdges.Count < 2)
            {
                continue;
            }

            if (!MutableById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds HubBounds))
            {
                continue;
            }

            double HubCross = HorizontalFlow ? HubBounds.CenterY : HubBounds.CenterX;
            double Step = Options.NodeSpacingPx;
            foreach (EdgeContract Edge in OutgoingEdges)
            {
                if (!MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
                {
                    continue;
                }

                Step = Math.Max(Step, GetCrossAxisCenterToCenterStep(TargetBounds, HorizontalFlow, Options.NodeSpacingPx));
            }

            double StartCross = HubCross - ((OutgoingEdges.Count - 1) * Step / 2.0);
            for (int EdgeIndex = 0; EdgeIndex < OutgoingEdges.Count; EdgeIndex++)
            {
                EdgeContract Edge = OutgoingEdges[EdgeIndex];
                if (!MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
                {
                    continue;
                }

                double TargetCross = ResolveCreationOrderedCrossPosition(
                    StartCross,
                    EdgeIndex,
                    OutgoingEdges.Count,
                    Step,
                    HorizontalFlow);
                double PreviousCross = GetCrossAxisCenter(TargetBounds, HorizontalFlow);
                MutableById[Edge.TargetId] = SetCrossAxisCenter(TargetBounds, HorizontalFlow, TargetCross);
                MoveStraightChainWithBranch(Contract, BackEdges, MutableById, Edge.TargetId, PreviousCross, TargetCross, HorizontalFlow);
            }
        }

        return MutableById.Values.ToList();
    }

    /// <summary>
    /// Moves the straight chain that hangs below a decision branch target across the flow with that target, so the chain
    /// stays in the branch's column (EIP Channel Purger: Export, then Remove, then Log under one branch). A chain node
    /// follows when it is the only forward successor of the node above it, that node is its only forward predecessor, and
    /// it sat in the same column as that node before the move. The chain stops at a decision, because a decision's own
    /// branches are placed around it separately.
    /// </summary>
    private static void MoveStraightChainWithBranch(
        DiagramContract Contract,
        HashSet<EdgeContract> BackEdges,
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById,
        string BranchTargetId,
        double PreviousCross,
        double NewCross,
        bool HorizontalFlow)
    {
        const double SAME_COLUMN_TOLERANCE_PX = 1.0;
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal) { BranchTargetId };
        string CurrentId = BranchTargetId;
        while (true)
        {
            List<string> SuccessorIds = GetForwardNeighbourIds(Contract, BackEdges, CurrentId, Successors: true);
            if (SuccessorIds.Count != 1)
            {
                return;
            }

            string NextId = SuccessorIds[0];
            if (Visited.Contains(NextId) || GetForwardNeighbourIds(Contract, BackEdges, NextId, Successors: false).Count != 1)
            {
                return;
            }

            NodeContract? NextNode = Contract.Nodes.FirstOrDefault(Candidate => string.Equals(Candidate.Id, NextId, StringComparison.Ordinal));
            if (NextNode is null || IsDecisionShape(NextNode.Shape) || !MutableById.TryGetValue(NextId, out FlowchartLayoutNodeBounds NextBounds))
            {
                return;
            }

            if (Math.Abs(GetCrossAxisCenter(NextBounds, HorizontalFlow) - PreviousCross) > SAME_COLUMN_TOLERANCE_PX)
            {
                return;
            }

            MutableById[NextId] = SetCrossAxisCenter(NextBounds, HorizontalFlow, NewCross);
            Visited.Add(NextId);
            CurrentId = NextId;
        }
    }

    private static List<string> GetForwardNeighbourIds(DiagramContract Contract, HashSet<EdgeContract> BackEdges, string NodeId, bool Successors)
    {
        List<string> NeighbourIds = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || BackEdges.Contains(Edge) || string.Equals(Edge.SourceId, Edge.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            string OwnEndId = Successors ? Edge.SourceId : Edge.TargetId;
            string OtherEndId = Successors ? Edge.TargetId : Edge.SourceId;
            if (string.Equals(OwnEndId, NodeId, StringComparison.Ordinal) && !NeighbourIds.Contains(OtherEndId, StringComparer.Ordinal))
            {
                NeighbourIds.Add(OtherEndId);
            }
        }

        return NeighbourIds;
    }

    private static List<FlowchartLayoutNodeBounds> ApplyInvisibleEdgeStackPlacement(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        double MinimumGap = Math.Max(Options.NodeSpacingPx, 32.0);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            if (HorizontalFlow)
            {
                double TargetCenterY = SourceBounds.CenterY - (SourceBounds.Height / 2.0)
                    - MinimumGap
                    - (TargetBounds.Height / 2.0);
                MutableById[Edge.TargetId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = TargetBounds.NodeId,
                    CenterX = SourceBounds.CenterX,
                    CenterY = TargetCenterY,
                    Width = TargetBounds.Width,
                    Height = TargetBounds.Height
                };
            }
            else
            {
                double TargetCenterX = SourceBounds.CenterX + (SourceBounds.Width / 2.0)
                    + MinimumGap
                    + (TargetBounds.Width / 2.0);
                MutableById[Edge.TargetId] = new FlowchartLayoutNodeBounds
                {
                    NodeId = TargetBounds.NodeId,
                    CenterX = TargetCenterX,
                    CenterY = SourceBounds.CenterY,
                    Width = TargetBounds.Width,
                    Height = TargetBounds.Height
                };
            }
        }

        return MutableById.Values.ToList();
    }

    private static List<FlowchartLayoutNodeBounds> ApplyLrRetryCycleNodeLayout(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Direction != FlowchartDirection.LeftToRight
            && Direction != FlowchartDirection.RightToLeft)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        string? HubNodeId = null;
        List<EdgeContract> LabelledOutgoing = [];
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.IsNullOrWhiteSpace(Edge.Label))
            {
                continue;
            }

            if (!MutableById.ContainsKey(Edge.SourceId))
            {
                continue;
            }

            NodeContract? SourceNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.SourceId);
            if (SourceNode is null || !IsDecisionShape(SourceNode.Shape))
            {
                continue;
            }

            if (HubNodeId is null)
            {
                HubNodeId = Edge.SourceId;
            }
            else if (!string.Equals(HubNodeId, Edge.SourceId, StringComparison.Ordinal))
            {
                return MutableById.Values.ToList();
            }

            LabelledOutgoing.Add(Edge);
        }

        if (HubNodeId is null || LabelledOutgoing.Count != 2)
        {
            return MutableById.Values.ToList();
        }

        bool HasRetryLoop = false;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                continue;
            }

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            NodeContract? SourceNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.SourceId);
            NodeContract? TargetNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.TargetId);
            if (FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                    Edge,
                    Contract,
                    SourceBounds,
                    TargetBounds,
                    SourceNode?.Shape,
                    TargetNode?.Shape))
            {
                HasRetryLoop = true;
                break;
            }
        }

        if (!HasRetryLoop || !MutableById.TryGetValue(HubNodeId, out FlowchartLayoutNodeBounds HubBounds))
        {
            return MutableById.Values.ToList();
        }

        LabelledOutgoing.Sort((Left, Right) => Left.CreationIndex.CompareTo(Right.CreationIndex));
        if (!MutableById.TryGetValue(LabelledOutgoing[0].TargetId, out FlowchartLayoutNodeBounds FirstTarget)
            || !MutableById.TryGetValue(LabelledOutgoing[1].TargetId, out FlowchartLayoutNodeBounds SecondTarget))
        {
            return MutableById.Values.ToList();
        }

        double HubY = HubBounds.CenterY;
        double MinimumGap = Math.Max(Options.NodeSpacingPx, 32.0);
        double HubHalfWidth = HubBounds.Width / 2.0;
        double HubHalfHeight = HubBounds.Height / 2.0;
        double MaxTargetWidth = Math.Max(FirstTarget.Width, SecondTarget.Width);
        double BranchColumnX = HubBounds.CenterX + HubHalfWidth + MinimumGap + (MaxTargetWidth / 2.0);
        double CrossAxisGap = Math.Max(MinimumGap / 2.0, HubHalfHeight + 12.0);

        MutableById[LabelledOutgoing[0].TargetId] = new FlowchartLayoutNodeBounds
        {
            NodeId = FirstTarget.NodeId,
            CenterX = BranchColumnX,
            CenterY = HubY + CrossAxisGap,
            Width = FirstTarget.Width,
            Height = FirstTarget.Height
        };
        MutableById[LabelledOutgoing[1].TargetId] = new FlowchartLayoutNodeBounds
        {
            NodeId = SecondTarget.NodeId,
            CenterX = BranchColumnX,
            CenterY = HubY - CrossAxisGap,
            Width = SecondTarget.Width,
            Height = SecondTarget.Height
        };

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.Equals(Edge.TargetId, HubNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds UpstreamBounds))
            {
                continue;
            }

            NodeContract? UpstreamNode = Contract.Nodes.FirstOrDefault(Node => Node.Id == Edge.SourceId);
            if (UpstreamNode is null || IsDecisionShape(UpstreamNode.Shape))
            {
                continue;
            }

            MutableById[Edge.SourceId] = new FlowchartLayoutNodeBounds
            {
                NodeId = UpstreamBounds.NodeId,
                CenterX = UpstreamBounds.CenterX,
                CenterY = HubY,
                Width = UpstreamBounds.Width,
                Height = UpstreamBounds.Height
            };
        }

        return MutableById.Values.ToList();
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

    private static List<FlowchartLayoutNodeBounds> CenterTargetsWithSourceCount(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        int MinimumSourceCount)
    {
        if (Source.Count == 0)
        {
            return [];
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        Dictionary<string, double> CrossAxisAdjustments = new Dictionary<string, double>(StringComparer.Ordinal);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);

        foreach (NodeContract Node in Contract.Nodes)
        {
            List<string> SourceIds = Contract.Edges
                .Where(Edge => string.Equals(Edge.TargetId, Node.Id, StringComparison.Ordinal)
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Edge))
                .Select(Edge => Edge.SourceId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (SourceIds.Count < MinimumSourceCount)
            {
                continue;
            }

            List<double> CrossPositions = [];
            foreach (string SourceId in SourceIds)
            {
                if (!BoundsById.TryGetValue(SourceId, out FlowchartLayoutNodeBounds SourceBounds))
                {
                    continue;
                }

                CrossPositions.Add(HorizontalFlow ? SourceBounds.CenterY : SourceBounds.CenterX);
            }

            if (CrossPositions.Count == 0)
            {
                continue;
            }

            string? SharedFanOutHubId = TryGetSharedFanOutHubId(Contract, SourceIds);
            if (SharedFanOutHubId is not null
                && BoundsById.TryGetValue(SharedFanOutHubId, out FlowchartLayoutNodeBounds HubBounds))
            {
                CrossAxisAdjustments[Node.Id] = HorizontalFlow ? HubBounds.CenterY : HubBounds.CenterX;
            }
            else if (IsDecisionShape(Node.Shape)
                && TryGetMiddleFanInSourceCrossAxis(
                    Contract,
                    Node.Id,
                    BoundsById,
                    HorizontalFlow,
                    out double MiddleSourceCross))
            {
                CrossAxisAdjustments[Node.Id] = MiddleSourceCross;
            }
            else
            {
                double AverageCross = CrossPositions.Sum() / CrossPositions.Count;
                CrossAxisAdjustments[Node.Id] = AverageCross;
            }
        }

        if (CrossAxisAdjustments.Count == 0)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        List<FlowchartLayoutNodeBounds> Adjusted = [];
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            if (!CrossAxisAdjustments.TryGetValue(Bounds.NodeId, out double CrossPosition))
            {
                Adjusted.Add(Bounds);
                continue;
            }

            if (HorizontalFlow)
            {
                Adjusted.Add(new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = Bounds.CenterX,
                    CenterY = CrossPosition,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                });
            }
            else
            {
                Adjusted.Add(new FlowchartLayoutNodeBounds
                {
                    NodeId = Bounds.NodeId,
                    CenterX = CrossPosition,
                    CenterY = Bounds.CenterY,
                    Width = Bounds.Width,
                    Height = Bounds.Height
                });
            }
        }

        return Adjusted;
    }

    private static bool TryGetMiddleFanInSourceCrossAxis(
        DiagramContract Contract,
        string TargetId,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        bool HorizontalFlow,
        out double MiddleSourceCross)
    {
        MiddleSourceCross = 0.0;
        List<EdgeContract> IncomingEdges = Contract.Edges
            .Where(Edge => string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal)
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Edge => Edge.CreationIndex)
            .ToList();

        if (IncomingEdges.Count < 3)
        {
            return false;
        }

        int MiddleIndex = (IncomingEdges.Count - 1) / 2;
        string MiddleSourceId = IncomingEdges[MiddleIndex].SourceId;
        if (!BoundsById.TryGetValue(MiddleSourceId, out FlowchartLayoutNodeBounds MiddleSourceBounds))
        {
            return false;
        }

        MiddleSourceCross = HorizontalFlow ? MiddleSourceBounds.CenterY : MiddleSourceBounds.CenterX;
        return true;
    }

    private static string? TryGetSharedFanOutHubId(DiagramContract Contract, IReadOnlyList<string> SourceNodeIds)
    {
        string? SharedHubId = null;
        foreach (string SourceNodeId in SourceNodeIds)
        {
            List<string> PredecessorIds = Contract.Edges
                .Where(Edge => string.Equals(Edge.TargetId, SourceNodeId, StringComparison.Ordinal)
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                .Select(Edge => Edge.SourceId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (PredecessorIds.Count != 1)
            {
                return null;
            }

            string PredecessorId = PredecessorIds[0];
            if (SharedHubId is null)
            {
                SharedHubId = PredecessorId;
            }
            else if (!string.Equals(SharedHubId, PredecessorId, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return SharedHubId;
    }

    private static List<FlowchartLayoutNodeBounds> ExpandFlowAxisGapForEdgeLabels(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction,
        FlowchartRenderOptions Options)
    {
        if (Source.Count == 0)
        {
            return [];
        }

        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        bool ReverseFlow = Direction == FlowchartDirection.RightToLeft
            || Direction == FlowchartDirection.BottomToTop;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        foreach (EdgeContract Edge in Contract.Edges.OrderBy(Edge => Edge.CreationIndex))
        {
            if (string.IsNullOrWhiteSpace(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MutableById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                || !MutableById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label,
                Options,
                Options.EdgeLabelFontSizePx);
            double RequiredFlowGap = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    LabelDimensions,
                    Options.EdgeLabelFontSizePx,
                    HorizontalFlow)
                + EDGE_LABEL_FLOW_AXIS_MARGIN_PX;

            double SourceExtent = HorizontalFlow ? SourceBounds.Width : SourceBounds.Height;
            double TargetExtent = HorizontalFlow ? TargetBounds.Width : TargetBounds.Height;
            double CenterDelta = HorizontalFlow
                ? Math.Abs(TargetBounds.CenterX - SourceBounds.CenterX)
                : Math.Abs(TargetBounds.CenterY - SourceBounds.CenterY);

            double AvailableGap = CenterDelta - ((SourceExtent + TargetExtent) / 2.0);
            if (AvailableGap >= RequiredFlowGap)
            {
                continue;
            }

            double PushDelta = RequiredFlowGap - AvailableGap;
            HashSet<string> SubtreeNodeIds = CollectTargetSubtree(Edge.TargetId, Contract);
            PushSubtreeNodes(
                MutableById,
                SubtreeNodeIds,
                SourceBounds,
                TargetBounds,
                HorizontalFlow,
                ReverseFlow,
                PushDelta);
        }

        return MutableById.Values.ToList();
    }

    /// <summary>
    /// Ensures at least <see cref="FlowchartRenderOptions.NodeSpacingPx"/> between node bounds on the cross axis
    /// for nodes whose flow-axis spans overlap (same rank band). Only spreads under-separated neighbours;
    /// intentional large gaps are preserved.
    /// </summary>
    private static List<FlowchartLayoutNodeBounds> EnforceMinimumCrossAxisSeparation(
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
        double MinimumGapPx = Options.NodeSpacingPx;

        Dictionary<string, FlowchartLayoutNodeBounds> MutableById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            MutableById[Bounds.NodeId] = Bounds;
        }

        List<FlowchartLayoutNodeBounds> Nodes = MutableById.Values.ToList();
        Dictionary<string, string> RankBandRootByNodeId =
            new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Nodes)
        {
            RankBandRootByNodeId[Bounds.NodeId] = Bounds.NodeId;
        }

        for (int LeftIndex = 0; LeftIndex < Nodes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Nodes.Count; RightIndex++)
            {
                FlowchartLayoutNodeBounds LeftBounds = Nodes[LeftIndex];
                FlowchartLayoutNodeBounds RightBounds = Nodes[RightIndex];
                if (SharesFlowAxisRankBand(LeftBounds, RightBounds, HorizontalFlow))
                {
                    UnionComponentRoots(RankBandRootByNodeId, LeftBounds.NodeId, RightBounds.NodeId);
                }
            }
        }

        Dictionary<string, List<string>> NodeIdsByRankBandRoot =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> Entry in RankBandRootByNodeId)
        {
            string Root = FindComponentRoot(RankBandRootByNodeId, Entry.Key);
            if (!NodeIdsByRankBandRoot.TryGetValue(Root, out List<string>? NodeIds))
            {
                NodeIds = [];
                NodeIdsByRankBandRoot[Root] = NodeIds;
            }

            NodeIds.Add(Entry.Key);
        }

        foreach (KeyValuePair<string, List<string>> RankBand in NodeIdsByRankBandRoot)
        {
            List<string> NodeIds = RankBand.Value;
            if (NodeIds.Count <= 1)
            {
                continue;
            }

            List<string> OrderedNodeIds = NodeIds
                .OrderBy(NodeId => GetCrossAxisCenter(MutableById[NodeId], HorizontalFlow))
                .ToList();

            for (int Index = 1; Index < OrderedNodeIds.Count; Index++)
            {
                string PreviousNodeId = OrderedNodeIds[Index - 1];
                string CurrentNodeId = OrderedNodeIds[Index];
                FlowchartLayoutNodeBounds PreviousBounds = MutableById[PreviousNodeId];
                FlowchartLayoutNodeBounds CurrentBounds = MutableById[CurrentNodeId];
                double PreviousHalfExtent = GetCrossAxisExtent(PreviousBounds, HorizontalFlow) / 2.0;
                double CurrentHalfExtent = GetCrossAxisExtent(CurrentBounds, HorizontalFlow) / 2.0;
                double MinimumCurrentCenter = GetCrossAxisCenter(PreviousBounds, HorizontalFlow)
                    + PreviousHalfExtent
                    + MinimumGapPx
                    + CurrentHalfExtent;
                double CurrentCenter = GetCrossAxisCenter(CurrentBounds, HorizontalFlow);
                if (CurrentCenter < MinimumCurrentCenter)
                {
                    double Shift = MinimumCurrentCenter - CurrentCenter;
                    for (int ShiftIndex = Index; ShiftIndex < OrderedNodeIds.Count; ShiftIndex++)
                    {
                        string ShiftNodeId = OrderedNodeIds[ShiftIndex];
                        FlowchartLayoutNodeBounds ShiftBounds = MutableById[ShiftNodeId];
                        MutableById[ShiftNodeId] = SetCrossAxisCenter(
                            ShiftBounds,
                            HorizontalFlow,
                            GetCrossAxisCenter(ShiftBounds, HorizontalFlow) + Shift);
                    }
                }
            }

            for (int Index = OrderedNodeIds.Count - 2; Index >= 0; Index--)
            {
                string CurrentNodeId = OrderedNodeIds[Index];
                string NextNodeId = OrderedNodeIds[Index + 1];
                FlowchartLayoutNodeBounds CurrentBounds = MutableById[CurrentNodeId];
                FlowchartLayoutNodeBounds NextBounds = MutableById[NextNodeId];
                double CurrentHalfExtent = GetCrossAxisExtent(CurrentBounds, HorizontalFlow) / 2.0;
                double NextHalfExtent = GetCrossAxisExtent(NextBounds, HorizontalFlow) / 2.0;
                double MaximumCurrentCenter = GetCrossAxisCenter(NextBounds, HorizontalFlow)
                    - NextHalfExtent
                    - MinimumGapPx
                    - CurrentHalfExtent;
                double CurrentCenter = GetCrossAxisCenter(CurrentBounds, HorizontalFlow);
                if (CurrentCenter > MaximumCurrentCenter)
                {
                    double Shift = MaximumCurrentCenter - CurrentCenter;
                    for (int ShiftIndex = 0; ShiftIndex <= Index; ShiftIndex++)
                    {
                        string ShiftNodeId = OrderedNodeIds[ShiftIndex];
                        FlowchartLayoutNodeBounds ShiftBounds = MutableById[ShiftNodeId];
                        MutableById[ShiftNodeId] = SetCrossAxisCenter(
                            ShiftBounds,
                            HorizontalFlow,
                            GetCrossAxisCenter(ShiftBounds, HorizontalFlow) + Shift);
                    }
                }
            }
        }

        return MutableById.Values.ToList();
    }

    private static double GetCrossAxisCenterToCenterStep(
        FlowchartLayoutNodeBounds Bounds,
        bool HorizontalFlow,
        double NodeSpacingPx) => GetCrossAxisExtent(Bounds, HorizontalFlow) + NodeSpacingPx;

    /// <summary>
    /// Maps creation-order edge indices to cross-axis slots around a hub.
    /// LR/RL: earlier declarations sit higher (larger layout Y). TB/BT: earlier declarations sit further left.
    /// </summary>
    private static double ResolveCreationOrderedCrossPosition(
        double StartCross,
        int EdgeIndex,
        int EdgeCount,
        double Step,
        bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return StartCross + ((EdgeCount - 1 - EdgeIndex) * Step);
        }

        return StartCross + (EdgeIndex * Step);
    }

    private static double GetCrossAxisCenter(FlowchartLayoutNodeBounds Bounds, bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return Bounds.CenterY;
        }

        return Bounds.CenterX;
    }

    private static double GetCrossAxisExtent(FlowchartLayoutNodeBounds Bounds, bool HorizontalFlow) => GetNodeExtentOnPackAxis(Bounds, false, HorizontalFlow);

    private static FlowchartLayoutNodeBounds SetCrossAxisCenter(
        FlowchartLayoutNodeBounds Bounds,
        bool HorizontalFlow,
        double CrossCenter)
    {
        if (HorizontalFlow)
        {
            return new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = CrossCenter,
                Width = Bounds.Width,
                Height = Bounds.Height
            };
        }

        return new FlowchartLayoutNodeBounds
        {
            NodeId = Bounds.NodeId,
            CenterX = CrossCenter,
            CenterY = Bounds.CenterY,
            Width = Bounds.Width,
            Height = Bounds.Height
        };
    }

    private static bool SharesFlowAxisRankBand(
        FlowchartLayoutNodeBounds Left,
        FlowchartLayoutNodeBounds Right,
        bool HorizontalFlow)
    {
        double LeftMin = GetFlowAxisMin(Left, HorizontalFlow);
        double LeftMax = GetFlowAxisMax(Left, HorizontalFlow);
        double RightMin = GetFlowAxisMin(Right, HorizontalFlow);
        double RightMax = GetFlowAxisMax(Right, HorizontalFlow);
        return LeftMin <= RightMax && RightMin <= LeftMax;
    }

    private static double GetFlowAxisMin(FlowchartLayoutNodeBounds Bounds, bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return Bounds.CenterX - (Bounds.Width / 2.0);
        }

        return Bounds.CenterY - (Bounds.Height / 2.0);
    }

    private static double GetFlowAxisMax(FlowchartLayoutNodeBounds Bounds, bool HorizontalFlow)
    {
        if (HorizontalFlow)
        {
            return Bounds.CenterX + (Bounds.Width / 2.0);
        }

        return Bounds.CenterY + (Bounds.Height / 2.0);
    }
}
