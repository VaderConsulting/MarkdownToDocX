using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Structural edge path construction for native Sugiyama SVG (spec §11.8–11.9).
/// </summary>
public static class FlowchartEdgePathBuilder
{
    private const double LR_RETRY_LOOP_CLEARANCE_PX = 28.0;

    private const double LR_RETRY_LOOP_CORNER_APPROACH_RATIO = 0.4;

    private const double LR_RETRY_LOOP_FACE_STUB_PX = 12.0;

    private const double NESTED_LR_RETRY_LOOP_LEVEL_STEP_PX = 22.0;

    private const double NESTED_LR_RETRY_LOOP_FIRST_CORRIDOR_PX = 33.0;

    private const double NESTED_LR_RETRY_LOOP_RUN_CLEARANCE_PX = 12.0;

    /// <summary>
    /// Returns true when an LR/RL edge is an unlabelled retry loop (F34).
    /// </summary>
    public static bool IsLrRetryLoopEdge(
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        if (Direction != FlowchartDirection.LeftToRight
            && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        if (IsDecisionShape(SourceShape) || IsDecisionShape(TargetShape))
        {
            return false;
        }

        const double MIN_BACKWARD_FLOW_PX = 0.75;
        if (Direction == FlowchartDirection.LeftToRight)
        {
            return TargetBounds.CenterX < SourceBounds.CenterX - MIN_BACKWARD_FLOW_PX;
        }

        return TargetBounds.CenterX > SourceBounds.CenterX + MIN_BACKWARD_FLOW_PX;
    }

    /// <summary>
    /// Builds guide points for an LR/RL back-edge loop corridor in layout coordinates (MSAGL Y-up).
    /// Render with <see cref="FlowchartCurvePathBuilder"/> using <c>flowchart.curve</c> (default curveBasis).
    /// </summary>
    public static bool TryBuildLrRetryLoopPath(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyList<FlowchartLayoutNodeBounds> AllBounds,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints,
        EdgeEndpointMarker MarkerStart = EdgeEndpointMarker.None,
        EdgeEndpointMarker MarkerEnd = EdgeEndpointMarker.None)
    {
        LayoutPoints = [];

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double SourceHalfWidth = SourceBounds.Width / 2.0;
        double TargetHalfHeight = TargetBounds.Height / 2.0;
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(MarkerStart);
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(MarkerEnd);

        double SourceAttachX;
        double SourceAttachY = SourceBounds.CenterY;
        double TargetAttachX = TargetBounds.CenterX;
        // A10/B17: enter the middle of the target's free side facing the below return track (bottom in LR).
        double TargetAttachY = TargetBounds.CenterY - TargetHalfHeight - TargetGapPx;

        if (RightToLeft)
        {
            SourceAttachX = SourceBounds.CenterX - SourceHalfWidth - SourceGapPx;
        }
        else
        {
            SourceAttachX = SourceBounds.CenterX + SourceHalfWidth + SourceGapPx;
        }

        // A10/B17: return track runs below every node (layout Y-up: smaller Y is visually lower).
        double CorridorY = Math.Min(SourceAttachY, TargetAttachY);
        foreach (FlowchartLayoutNodeBounds Bounds in AllBounds)
        {
            double NodeBottom = Bounds.CenterY - (Bounds.Height / 2.0);
            if (NodeBottom < CorridorY)
            {
                CorridorY = NodeBottom;
            }
        }

        CorridorY -= LR_RETRY_LOOP_CLEARANCE_PX;

        double ExitCornerY = InterpolateRetryLoopCornerY(SourceAttachY, CorridorY);
        double EntryCornerY = InterpolateRetryLoopCornerY(CorridorY, TargetAttachY);
        double SourceStubX = RightToLeft
            ? SourceAttachX - LR_RETRY_LOOP_FACE_STUB_PX
            : SourceAttachX + LR_RETRY_LOOP_FACE_STUB_PX;

        AddLayoutPointIfDistinct(LayoutPoints, SourceAttachX, SourceAttachY);
        AddLayoutPointIfDistinct(LayoutPoints, SourceStubX, SourceAttachY);
        AddLayoutPointIfDistinct(LayoutPoints, SourceStubX, ExitCornerY);
        AddLayoutPointIfDistinct(LayoutPoints, SourceStubX, CorridorY);
        AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, CorridorY);
        AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, EntryCornerY);
        AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, TargetAttachY);

        return LayoutPoints.Count >= 2;
    }

    /// <summary>
    /// Builds guide points for one LR/RL retry loop when several retry loops share a node (EIP Smart Proxy: Service back to
    /// Smart proxy, and Smart proxy back to Caller). Loops whose horizontal runs sit side by side share one corridor height
    /// above the nodes, so they look alike; loops whose runs would overlap get stacked corridors so they never merge.
    /// A loop whose source already sends a forward edge out of its downstream face leaves from the top of the source instead,
    /// and those loops are placed first, so when stacking is needed they read as smaller loops inside the others.
    /// Loop ends on a shared top face get separate ports, ordered by where the other end of each loop lies.
    /// Returns false when the edge is not a retry loop or no other retry loop shares its span; the caller then uses
    /// <see cref="TryBuildLrRetryLoopPath"/>.
    /// </summary>
    public static bool TryBuildNestedLrRetryLoopPath(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints)
    {
        LayoutPoints = [];
        List<EdgeContract> Loops = CollectLrRetryLoopEdges(Contract, BoundsById, NodeById);
        if (!Loops.Contains(Edge))
        {
            return false;
        }

        List<EdgeContract> Group = Loops.Where(Candidate => LoopSpansOverlap(Candidate, Edge, BoundsById)).ToList();
        if (Group.Count < 2)
        {
            return false;
        }

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double DownstreamSign = RightToLeft ? -1.0 : 1.0;
        List<EdgeContract> Ordered = Group.OrderBy(Candidate => LoopLeavesFromTop(Candidate, Contract, Loops) ? 0 : 1).ThenBy(Candidate => Math.Abs(BoundsById[Candidate.SourceId].CenterX - BoundsById[Candidate.TargetId].CenterX)).ThenBy(Candidate => Candidate.CreationIndex).ToList();

        double GroupLeft = double.MaxValue;
        double GroupRight = double.MinValue;
        foreach (EdgeContract Loop in Group)
        {
            GroupLeft = Math.Min(GroupLeft, Math.Min(BoundsById[Loop.SourceId].CenterX, BoundsById[Loop.TargetId].CenterX));
            GroupRight = Math.Max(GroupRight, Math.Max(BoundsById[Loop.SourceId].CenterX, BoundsById[Loop.TargetId].CenterX));
        }

        double NodeTop = double.MinValue;
        double NodeBottom = double.MaxValue;
        foreach (FlowchartLayoutNodeBounds Bounds in BoundsById.Values)
        {
            double Left = Bounds.CenterX - (Bounds.Width / 2.0);
            double Right = Bounds.CenterX + (Bounds.Width / 2.0);
            if (Right >= GroupLeft && Left <= GroupRight)
            {
                NodeTop = Math.Max(NodeTop, Bounds.CenterY + (Bounds.Height / 2.0));
                NodeBottom = Math.Min(NodeBottom, Bounds.CenterY - (Bounds.Height / 2.0));
            }
        }

        int CorridorLevel = ResolveCorridorLevel(Edge, Ordered, Contract, Loops, BoundsById, DownstreamSign);
        bool LeavesFromTop = LoopLeavesFromTop(Edge, Contract, Loops);
        double CorridorY = LeavesFromTop
            ? NodeTop + NESTED_LR_RETRY_LOOP_FIRST_CORRIDOR_PX + (NESTED_LR_RETRY_LOOP_LEVEL_STEP_PX * CorridorLevel)
            : NodeBottom - NESTED_LR_RETRY_LOOP_FIRST_CORRIDOR_PX - (NESTED_LR_RETRY_LOOP_LEVEL_STEP_PX * CorridorLevel);
        FlowchartLayoutNodeBounds SourceBounds = BoundsById[Edge.SourceId];
        FlowchartLayoutNodeBounds TargetBounds = BoundsById[Edge.TargetId];
        double TargetPortX = ResolveTopPortX(Edge.TargetId, Edge, Loops, Contract, BoundsById);
        double TargetAttachY = TargetBounds.CenterY + (TargetBounds.Height / 2.0) + FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);
        double SourceRunX = ResolveLoopSourceRunX(Edge, Contract, Loops, BoundsById, DownstreamSign);

        if (LeavesFromTop)
        {
            double SourceAttachY = SourceBounds.CenterY + (SourceBounds.Height / 2.0) + FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart);
            AddLayoutPointIfDistinct(LayoutPoints, SourceRunX, SourceAttachY);
            AddLayoutPointIfDistinct(LayoutPoints, SourceRunX, CorridorY);
        }
        else
        {
            double SourceAttachX = SourceBounds.CenterX + (DownstreamSign * ((SourceBounds.Width / 2.0) + FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart)));
            AddLayoutPointIfDistinct(LayoutPoints, SourceAttachX, SourceBounds.CenterY);
            AddLayoutPointIfDistinct(LayoutPoints, SourceRunX, SourceBounds.CenterY);
            AddLayoutPointIfDistinct(LayoutPoints, SourceRunX, CorridorY);
        }

        AddLayoutPointIfDistinct(LayoutPoints, TargetPortX, CorridorY);
        if (LeavesFromTop)
        {
            AddLayoutPointIfDistinct(LayoutPoints, TargetPortX, TargetAttachY);
        }
        else
        {
            double TargetAttachX = TargetBounds.CenterX;
            double TargetAttachYBottom = TargetBounds.CenterY - (TargetBounds.Height / 2.0) - FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);
            double EntryCornerY = InterpolateRetryLoopCornerY(CorridorY, TargetAttachYBottom);
            AddLayoutPointIfDistinct(LayoutPoints, TargetPortX, EntryCornerY);
            AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, TargetAttachYBottom);
        }
        return LayoutPoints.Count >= 3;
    }

    private static List<EdgeContract> CollectLrRetryLoopEdges(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        IReadOnlyDictionary<string, NodeContract> NodeById)
    {
        List<EdgeContract> Loops = [];
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(Candidate.SourceId, out FlowchartLayoutNodeBounds SourceBounds) || !BoundsById.TryGetValue(Candidate.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            string? SourceShape = NodeById.TryGetValue(Candidate.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = NodeById.TryGetValue(Candidate.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
            if (IsLrRetryLoopEdge(Candidate, Contract, SourceBounds, TargetBounds, SourceShape, TargetShape))
            {
                Loops.Add(Candidate);
            }
        }

        return Loops;
    }

    /// <summary>
    /// Returns the corridor level for a nested retry loop. Loops are placed in <paramref name="Ordered"/> order, and each
    /// takes the lowest level where its horizontal run does not come within
    /// <see cref="NESTED_LR_RETRY_LOOP_RUN_CLEARANCE_PX"/> of a run already placed on that level. Loops whose runs are
    /// side by side therefore share one corridor height.
    /// </summary>
    private static int ResolveCorridorLevel(
        EdgeContract Edge,
        List<EdgeContract> Ordered,
        DiagramContract Contract,
        List<EdgeContract> Loops,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double DownstreamSign)
    {
        List<(double Min, double Max, int Level)> Placed = [];
        foreach (EdgeContract Loop in Ordered)
        {
            double SourceRunX = ResolveLoopSourceRunX(Loop, Contract, Loops, BoundsById, DownstreamSign);
            double TargetRunX = ResolveTopPortX(Loop.TargetId, Loop, Loops, Contract, BoundsById);
            double RunMin = Math.Min(SourceRunX, TargetRunX);
            double RunMax = Math.Max(SourceRunX, TargetRunX);

            int Level = 0;
            while (Placed.Any(Run => Run.Level == Level && RunMax + NESTED_LR_RETRY_LOOP_RUN_CLEARANCE_PX > Run.Min && Run.Max + NESTED_LR_RETRY_LOOP_RUN_CLEARANCE_PX > RunMin))
            {
                Level++;
            }

            if (ReferenceEquals(Loop, Edge))
            {
                return Level;
            }

            Placed.Add((RunMin, RunMax, Level));
        }

        return 0;
    }

    /// <summary>
    /// Returns the X where a nested retry loop's horizontal run starts at its source: a top port when the loop leaves from
    /// the top of the source, otherwise the end of the short stub off the source's downstream face.
    /// </summary>
    private static double ResolveLoopSourceRunX(
        EdgeContract Loop,
        DiagramContract Contract,
        List<EdgeContract> Loops,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        double DownstreamSign)
    {
        if (LoopLeavesFromTop(Loop, Contract, Loops))
        {
            return ResolveTopPortX(Loop.SourceId, Loop, Loops, Contract, BoundsById);
        }

        FlowchartLayoutNodeBounds SourceBounds = BoundsById[Loop.SourceId];
        double SourceAttachX = SourceBounds.CenterX + (DownstreamSign * ((SourceBounds.Width / 2.0) + FlowchartEdgeAttachConstants.GetExteriorGapPx(Loop.MarkerStart)));
        return SourceAttachX + (DownstreamSign * LR_RETRY_LOOP_FACE_STUB_PX);
    }

    private static bool LoopSpansOverlap(EdgeContract First, EdgeContract Second, IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        double FirstMin = Math.Min(BoundsById[First.SourceId].CenterX, BoundsById[First.TargetId].CenterX);
        double FirstMax = Math.Max(BoundsById[First.SourceId].CenterX, BoundsById[First.TargetId].CenterX);
        double SecondMin = Math.Min(BoundsById[Second.SourceId].CenterX, BoundsById[Second.TargetId].CenterX);
        double SecondMax = Math.Max(BoundsById[Second.SourceId].CenterX, BoundsById[Second.TargetId].CenterX);
        return FirstMax >= SecondMin - 0.75 && SecondMax >= FirstMin - 0.75;
    }

    private static bool LoopLeavesFromTop(EdgeContract Loop, DiagramContract Contract, List<EdgeContract> Loops)
    {
        return Contract.Edges.Any(Candidate => string.Equals(Candidate.SourceId, Loop.SourceId, StringComparison.Ordinal) && !Loops.Contains(Candidate) && !string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) && !string.Equals(Candidate.SourceId, Candidate.TargetId, StringComparison.Ordinal));
    }

    private static double ResolveTopPortX(
        string NodeId,
        EdgeContract Edge,
        List<EdgeContract> Loops,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        List<(EdgeContract Loop, double OtherX)> TopEnds = [];
        foreach (EdgeContract Loop in Loops)
        {
            if (string.Equals(Loop.TargetId, NodeId, StringComparison.Ordinal))
            {
                TopEnds.Add((Loop, BoundsById[Loop.SourceId].CenterX));
            }
            else if (string.Equals(Loop.SourceId, NodeId, StringComparison.Ordinal) && LoopLeavesFromTop(Loop, Contract, Loops))
            {
                TopEnds.Add((Loop, BoundsById[Loop.TargetId].CenterX));
            }
        }

        FlowchartLayoutNodeBounds Bounds = BoundsById[NodeId];
        List<(EdgeContract Loop, double OtherX)> Ordered = TopEnds.OrderBy(End => End.OtherX).ThenBy(End => End.Loop.CreationIndex).ToList();
        int Index = Ordered.FindIndex(End => ReferenceEquals(End.Loop, Edge));
        if (Index < 0 || Ordered.Count < 2)
        {
            return Bounds.CenterX;
        }

        double Left = Bounds.CenterX - (Bounds.Width / 2.0);
        return Left + (Bounds.Width * (Index + 1) / (Ordered.Count + 1));
    }

    private static double InterpolateRetryLoopCornerY(double FromY, double ToY) => FromY + ((ToY - FromY) * LR_RETRY_LOOP_CORNER_APPROACH_RATIO);

    private static void AddLayoutPointIfDistinct(List<(double X, double Y)> Points, double X, double Y)
    {
        if (Points.Count == 0)
        {
            Points.Add((X, Y));
            return;
        }

        (double LastX, double LastY) = Points[Points.Count - 1];
        if (Math.Abs(LastX - X) > 0.75 || Math.Abs(LastY - Y) > 0.75)
        {
            Points.Add((X, Y));
        }
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
