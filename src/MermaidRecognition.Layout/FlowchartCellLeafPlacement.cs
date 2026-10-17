using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Shares complete whole-cell leaf placement across local grouped leaves and outside leaves.</summary>
internal static class FlowchartCellLeafPlacement
{
    /// <summary>Plans all selected leaves and measured external gaps privately, then publishes one complete legal assignment.</summary>
    /// <param name="Contract">Declared relationships, membership and renderer measurements.</param>
    /// <param name="Nodes">Complete diagram ownership.</param>
    /// <param name="Frames">Derived cache replaced only after acceptance.</param>
    /// <param name="Horizontal">Whether the cross axis uses logical rows.</param>
    /// <param name="External">Whether leaves are outside their grouped peer rather than inside the same group.</param>
    /// <returns>Whether accepted regions, gaps or logical ranks changed.</returns>
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, bool Horizontal, bool External)
    {
        var Before = FlowchartCellRepair.Capture(Nodes); var Grid = Nodes[0].CellNode!.Grid;
        var Context = Nodes[0].PixelWriteContext ?? FlowchartRenderContext.Current;
        var Regions = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var Growth = new Dictionary<int, double>(); var Ranks = new Dictionary<DiagramGridLayout.GridNode, int>();
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Edges = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Options = FlowchartRenderOptions.FromContract(Contract);
        var State = Before; bool Changed = false;
        foreach (var Leaf in Nodes.Where(Node => (External ? Node.GroupId == null : Node.GroupId != null) && !Node.IsDiamond && !Node.PlacementHintLocked))
        {
            if (Context.CellLeafCandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves) break;
            var Incident = Edges.Where(Edge => Edge.SourceId == Leaf.Id || Edge.TargetId == Leaf.Id).ToArray();
            if (Incident.Length != 1) continue;
            var Edge = Incident[0];
            if (!ById.TryGetValue(Edge.SourceId == Leaf.Id ? Edge.TargetId : Edge.SourceId, out var Parent) || Parent.IsDiamond
                || (External ? Parent.GroupId == null : Parent.GroupId != Leaf.GroupId)) continue;
            var CurrentFrames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, State);
            var Owner = External ? CurrentFrames.Where(Frame => FlowchartCompoundEndpointHelper.Contains(Contract, Frame.Id, Parent.Id))
                .OrderByDescending(Frame => Math.Abs((double)(Frame.Right - Frame.Left) * (Frame.Bottom - Frame.Top))).FirstOrDefault() : null;
            if (External && Owner == null) continue;
            if (!Edges.Any(Other => Other != Edge && (Edge.SourceId == Leaf.Id ? Other.TargetId == Parent.Id : Other.SourceId == Parent.Id)
                && ById.ContainsKey(Other.SourceId == Parent.Id ? Other.TargetId : Other.SourceId)
                && (External ? FlowchartCompoundEndpointHelper.Contains(Contract, Owner!.Id, Other.SourceId == Parent.Id ? Other.TargetId : Other.SourceId)
                    : !FlowchartCompoundEndpointHelper.Contains(Contract, Leaf.GroupId!, Other.SourceId == Parent.Id ? Other.TargetId : Other.SourceId)))) continue;
            var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? string.Empty, Options, Options.EdgeLabelFontSizePx);
            int Needed = DiagramGridMetrics.CeilToGrid(Math.Max(DiagramGridMetrics.GRID_UNIT_PX,
                string.IsNullOrWhiteSpace(Edge.Label) ? 0 : (Horizontal ? Size.Height : Size.Width) + 12));
            var CurrentBounds = Project(Nodes, State);
            var ExistingConflicts = DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, CurrentFrames, CurrentBounds);
            var Anchor = Regions[Parent.Id]; var LeafRegion = Regions[Leaf.Id];
            var Unit = External ? Nodes.Where(Node => FlowchartCompoundEndpointHelper.Contains(Contract, Owner!.Id, Node.Id)).ToArray() : [Parent];
            int Low = Unit.Min(Node => Horizontal ? Regions[Node.Id].StartRow : Regions[Node.Id].StartColumn);
            int High = Unit.Max(Node => Horizontal ? Regions[Node.Id].EndRow : Regions[Node.Id].EndColumn);
            FlowchartCellRepair.Assignment? Best = null; double BestCost = double.PositiveInfinity;
            Dictionary<string, FlowchartCellRegion>? BestRegions = null;
            var SettledSides = new HashSet<int>();
            for (int Extra = 0; SettledSides.Count < 2 && Context.CellLeafCandidateMoves < FlowchartLayoutRules.RepairCandidateMoves; Extra++)
            foreach (int Sign in new[] { -1, 1 })
            {
                if (SettledSides.Contains(Sign) || Context.CellLeafCandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves) continue;
                Context.CellLeafCandidateMoves++;
                int Cross = Sign < 0 ? checked(Low - (Horizontal ? LeafRegion.RowSpan : LeafRegion.ColumnSpan) - Extra) : checked(High + Extra);
                var Trial = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal)
                    { [Leaf.Id] = new(Horizontal ? Cross : Anchor.StartRow, Horizontal ? Anchor.StartColumn : Cross, LeafRegion.RowSpan, LeafRegion.ColumnSpan) };
                var TrialGrowth = new Dictionary<int, double>(Growth);
                var A = CurrentBounds[Parent.Id]; var B = Leaf.CellNode!.Bounds(Trial[Leaf.Id], Leaf.GeometryProvider, State);
                string Side = Horizontal ? (Sign < 0 ? "top" : "bottom") : (Sign < 0 ? "left" : "right");
                if (!TryChord(Parent, Leaf, A, B, Side, out var Start, out var End)) { SettledSides.Add(Sign); continue; }
                double Missing = Needed - Math.Abs(Horizontal ? End.Y - Start.Y : End.X - Start.X);
                if (External)
                {
                    int WorldSign = Horizontal && Parent.CellNode!.UsesRoutingCoordinates ? -Sign : Sign;
                    double Border = Horizontal ? (WorldSign < 0 ? Math.Min(Owner!.Top, Owner.Bottom) : Math.Max(Owner!.Top, Owner.Bottom))
                        : (Sign < 0 ? Owner!.Left : Owner!.Right);
                    double Near = Horizontal ? (WorldSign < 0 ? B.CenterY + B.Height / 2 : B.CenterY - B.Height / 2)
                        : (Sign < 0 ? B.CenterX + B.Width / 2 : B.CenterX - B.Width / 2);
                    Missing = Math.Max(Missing, 2 * DiagramGridMetrics.NODE_GAP_PX - WorldSign * (Near - Border));
                }
                int After = Sign < 0 ? Low - 1 : High - 1;
                if (Missing > 0)
                {
                    if (Trial.Values.Any(Region => Horizontal ? Region.StartRow <= After && Region.EndRow > After + 1
                        : Region.StartColumn <= After && Region.EndColumn > After + 1)) continue;
                    TrialGrowth[After] = TrialGrowth.GetValueOrDefault(After) + Missing;
                }
                var Plan = FlowchartCellRepair.PlanAssignment(Nodes, Trial,
                    TrialGrowth.Select(Pair => new FlowchartCellGapGrowth(!Horizontal, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray());
                if (Plan == null) continue;
                var Bounds = Project(Nodes, Plan.After); var ProjectedFrames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Plan.After);
                if (DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, ProjectedFrames, Bounds).Any(Key => !ExistingConflicts.Contains(Key))) continue;
                if (!TryChord(Parent, Leaf, Bounds[Parent.Id], Bounds[Leaf.Id], Side, out Start, out End)
                    || FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(Start.X, Start.Y, End.X, End.Y, Bounds, Parent.Id, Leaf.Id)) continue;
                bool Routing = Parent.CellNode!.UsesRoutingCoordinates;
                if (ProjectedFrames.Any(Frame => Frame.TitleStrip > 0 && FlowchartRouteObstacleHelper.SegmentIntersectsRectangleInterior(
                    Start.X, Start.Y, End.X, End.Y, Frame.Left, Routing ? Math.Max(Frame.Top, Frame.Bottom) - Frame.TitleStrip : Math.Min(Frame.Top, Frame.Bottom),
                    Frame.Right, Routing ? Math.Max(Frame.Top, Frame.Bottom) : Math.Min(Frame.Top, Frame.Bottom) + Frame.TitleStrip))) continue;
                double Begin = Horizontal ? Start.Y : Start.X, Finish = Horizontal ? End.Y : End.X;
                double ParentMain = Horizontal ? Bounds[Parent.Id].CenterX : Bounds[Parent.Id].CenterY;
                int Crossings = Edges.Count(Other => Other.SourceId != Parent.Id && Other.TargetId != Parent.Id && Other.SourceId != Leaf.Id && Other.TargetId != Leaf.Id
                    && Bounds.TryGetValue(Other.SourceId, out var First) && Bounds.TryGetValue(Other.TargetId, out var Last)
                    && (Horizontal ? First.CenterY : First.CenterX) > Math.Min(Begin, Finish) && (Horizontal ? First.CenterY : First.CenterX) < Math.Max(Begin, Finish)
                    && ((Horizontal ? First.CenterX : First.CenterY) - ParentMain) * ((Horizontal ? Last.CenterX : Last.CenterY) - ParentMain) < 0);
                double Move = Math.Abs((Horizontal ? Bounds[Leaf.Id].CenterY : Bounds[Leaf.Id].CenterX)
                    - (Horizontal ? CurrentBounds[Leaf.Id].CenterY : CurrentBounds[Leaf.Id].CenterX));
                // Preserve the two existing leaf passes' established cost terms.
                double Cost = Crossings * 1000 + (External ? Math.Abs(Finish - Begin) + Move / 100 : Move);
                if (Cost < BestCost) { Best = Plan; BestCost = Cost; BestRegions = Trial; }
                SettledSides.Add(Sign); // nearest legal whole-cell placement on this side
            }
            if (Best == null) continue;
            var ActualGaps = Horizontal ? Best.After.RowGaps : Best.After.ColumnGaps;
            var OriginalGaps = Horizontal ? Before.RowGaps : Before.ColumnGaps;
            double BaseGap = Horizontal ? Grid.Configuration.RowGap : Grid.Configuration.ColumnGap;
            var BestGrowth = ActualGaps.Where(Pair => Pair.Value > OriginalGaps.GetValueOrDefault(Pair.Key, BaseGap))
                .ToDictionary(Pair => Pair.Key, Pair => Pair.Value - OriginalGaps.GetValueOrDefault(Pair.Key, BaseGap));
            Changed |= BestRegions![Leaf.Id] != Regions[Leaf.Id] || BestGrowth.Any(Pair => Pair.Value != Growth.GetValueOrDefault(Pair.Key)) || Leaf.Rank != Parent.Rank;
            Regions = BestRegions!; Growth = BestGrowth; State = Best.After; Ranks[Leaf] = Parent.Rank;
        }
        if (!Changed) return false;
        var Complete = FlowchartCellRepair.PlanAssignment(Nodes, Regions,
            Growth.Select(Pair => new FlowchartCellGapGrowth(!Horizontal, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray());
        if (Complete == null) return false;
        var FinalFrames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Complete.After);
        if (!Complete.TryApply()) return false;
        foreach (var Pair in Ranks) Pair.Key.Rank = Pair.Value;
        Frames.Clear(); Frames.AddRange(FinalFrames);
        return true;
    }

    /// <summary>Projects complete immutable node geometry for clearance and crossing comparisons.</summary>
    /// <param name="Nodes">Authoritative node views.</param>
    /// <param name="State">Complete candidate ownership and gaps.</param>
    /// <returns>Renderer-facing bounds without live mutation.</returns>
    private static Dictionary<string, FlowchartLayoutNodeBounds> Project(List<DiagramGridLayout.GridNode> Nodes, FlowchartLogicalGrid.Snapshot State) =>
        Nodes.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(State.Regions[Node.Id], Node.GeometryProvider, State), StringComparer.Ordinal);

    /// <summary>Obtains eligible renderer-owned facing ports without shape-specific placement exceptions.</summary>
    /// <param name="Parent">Grouped peer and its coordinate view.</param>
    /// <param name="Leaf">Single-link leaf and its coordinate view.</param>
    /// <param name="A">Projected parent bounds.</param>
    /// <param name="B">Projected leaf bounds.</param>
    /// <param name="Side">Parent side facing the candidate.</param>
    /// <param name="Start">Receives the parent boundary point.</param>
    /// <param name="End">Receives the leaf boundary point.</param>
    /// <returns>Whether both connection sides are eligible.</returns>
    private static bool TryChord(DiagramGridLayout.GridNode Parent, DiagramGridLayout.GridNode Leaf,
        FlowchartLayoutNodeBounds A, FlowchartLayoutNodeBounds B, string Side, out (double X, double Y) Start, out (double X, double Y) End)
    {
        Start = End = default;
        string Opposite = Side switch { "left" => "right", "right" => "left", "top" => "bottom", _ => "top" };
        var ABoundary = (A.Geometry ?? NodeGeometry.Rectangle(A.Width, A.Height)).ConnectionBoundary;
        var BBoundary = (B.Geometry ?? NodeGeometry.Rectangle(B.Width, B.Height)).ConnectionBoundary;
        if (!ABoundary.TryPort(Side, 0.5, out var P) || !BBoundary.TryPort(Opposite, 0.5, out var Q)) return false;
        Start = (A.CenterX + P.X, A.CenterY + (Parent.CellNode!.UsesRoutingCoordinates ? -P.Y : P.Y));
        End = (B.CenterX + Q.X, B.CenterY + (Leaf.CellNode!.UsesRoutingCoordinates ? -Q.Y : Q.Y));
        return true;
    }
}
