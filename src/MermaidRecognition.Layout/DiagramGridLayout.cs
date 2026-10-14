using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Tidies a Mermaid dagre placement onto the shared grid.
/// Group membership is preserved; placement and ports follow the grid routing rules.
/// </summary>
public static partial class DiagramGridLayout
{
    private const int STUB_PX = 15;

    private const int MAX_SEPARATION_PASSES = 32;

    /// <summary>
    /// Returns a Y-up copy of <paramref name="LaidOut"/> with shared box sizes, grid positions,
    /// group frames that include a title strip, and orthogonal routes from the new ports.
    /// </summary>
    /// <param name="Contract">The diagram contract.</param>
    /// <param name="LaidOut">Mermaid dagre placement in Y-down coordinates.</param>
    /// <param name="RenderContext">Mutable state owned by this layout and render.</param>
    /// <returns>The gridded layout.</returns>
    public static FlowchartLayoutResult Apply(
        DiagramContract Contract,
        FlowchartLayoutResult LaidOut,
        FlowchartRenderContext? RenderContext = null)
    {
        GridLayoutState State = new(Contract, LaidOut, RenderContext);
        if (!State.IsReady) return State.Copy;

        GridPreparationStage.Apply(State);
        NodePlacementStage.Apply(State);
        GroupFrameStage.Apply(State);
        RouteConstructionStage.Apply(State);
        RouteRepairStage.Apply(State);
        FinalHintStage.Apply(State);
        return State.Copy;
    }
    private static void RestoreContractRouteEndpoints(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        Dictionary<string, EdgeContract> ById = Contract.Edges
            .Where(Edge => !string.IsNullOrEmpty(Edge.Id))
            .GroupBy(Edge => Edge.Id!, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First(), StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (!ById.TryGetValue(Route.EdgeId, out EdgeContract? Edge))
            {
                continue;
            }

            Route.SourceId = Edge.SourceId;
            Route.TargetId = Edge.TargetId;
        }
    }

    /// <summary>
    /// Gives a labelled straight cross-axis connection enough measured room without
    /// disturbing the rest of its rank. Only a terminal target moves; connected
    /// structures continue to be handled by the scored placement passes.
    /// </summary>
    private static bool GrowTerminalSameRankLabelGaps(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool HorizontalFlow)
    {
        bool Changed = false;
        Dictionary<string, GridNode> ById = Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (EdgeContract Edge in Contract.Edges.OrderBy(Edge => Edge.CreationIndex))
        {
            if (string.IsNullOrWhiteSpace(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || !ById.TryGetValue(Edge.SourceId, out GridNode? Source)
                || !ById.TryGetValue(Edge.TargetId, out GridNode? Target)
                || Contract.Edges.Any(Other => Other.SourceId == Target.Id
                    && !string.Equals(Other.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            double SourceMain = HorizontalFlow ? Source.X : Source.Y;
            double TargetMain = HorizontalFlow ? Target.X : Target.Y;
            if (Math.Abs(SourceMain - TargetMain) > 0.5)
            {
                continue;
            }

            double SourceCross = HorizontalFlow ? Source.Y : Source.X;
            double TargetCross = HorizontalFlow ? Target.Y : Target.X;
            if (Math.Abs(TargetCross - SourceCross) < 0.5)
            {
                continue;
            }

            double SourceExtent = HorizontalFlow ? Source.Height : Source.Width;
            double TargetExtent = HorizontalFlow ? Target.Height : Target.Width;
            double ClearGap = Math.Abs(TargetCross - SourceCross) - (SourceExtent + TargetExtent) / 2.0;
            FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label, Options, Options.EdgeLabelFontSizePx);
            double RequiredGap = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Dimensions, Options.EdgeLabelFontSizePx, Horizontal: !HorizontalFlow)
                + DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            if (ClearGap + 0.5 >= RequiredGap)
            {
                continue;
            }

            double Delta = DiagramGridMetrics.CeilToGrid(RequiredGap - ClearGap);
            double CandidateCross = TargetCross + Math.Sign(TargetCross - SourceCross) * Delta;
            double CandidateX = HorizontalFlow ? Target.X : CandidateCross;
            double CandidateY = HorizontalFlow ? CandidateCross : Target.Y;
            bool Overlaps = Placed.Any(Other => Other != Target
                && Math.Abs(CandidateX - Other.X) < (Target.Width + Other.Width) / 2.0
                && Math.Abs(CandidateY - Other.Y) < (Target.Height + Other.Height) / 2.0);
            if (Overlaps)
            {
                continue;
            }

            if (HorizontalFlow) Target.Y = CandidateCross;
            else Target.X = CandidateCross;
            Changed = true;
        }
        return Changed;
    }

    private static void ApplyScoreGuardedTwoCornerRepair(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        // Sloping outlines need their terminal normals rebuilt with the route.
        // This pass deliberately preserves those specialised attachments.
        if (Contract.Nodes.Any(Node => FlowchartDiamondAttachHelper.IsDiamondShape(Node.Shape))) return;
        FlowchartLayoutScore.Result Before = FlowchartLayoutScore.Measure(Contract, Layout);
        var Snapshot = Layout.EdgeRoutes.Select(Route => (Route, Route.ExitSide, Route.EntrySide,
            Points: Route.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }).ToList())).ToList();
        FlowchartTwoCornerRouteShortener.Apply(Layout, Contract, Bounds, IncludeTwoCornerRoutes: true);
        FlowchartLayoutScore.Result After = FlowchartLayoutScore.Measure(Contract, Layout);
        bool Keep = Before.Score.HasValue
            ? After.Score.HasValue && After.Score.Value < Before.Score.Value - 0.001
            : After.Overlaps <= Before.Overlaps && After.Crossings <= Before.Crossings;
        if (Keep) return;
        foreach (var Saved in Snapshot)
        {
            Saved.Route.ExitSide = Saved.ExitSide;
            Saved.Route.EntrySide = Saved.EntrySide;
            Saved.Route.Points.Clear();
            Saved.Route.Points.AddRange(Saved.Points);
        }
    }

    internal static void GrowFacingDoglegGaps(FlowchartLayoutResult Layout, DiagramContract Contract,
        List<GridNode> Placed, List<GridFrame> Frames)
    {
        // Placement hints outrank automatic gap growth. Repositioning only later ranks
        // would split a source-ordered hinted chain after placement has been finalised.
        if (Contract.Nodes.Any(Node => Node.PlacementHint?.IsActive == true)
            || Contract.Groups.Any(Group => Group.PlacementHint?.IsActive == true))
            return;
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (var Route in Layout.EdgeRoutes)
        {
            if (Route.Points.Count < 6 || BackEdges.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)) continue;
            var Source = Placed.Find(Node => Node.Id == Route.SourceId);
            var Target = Placed.Find(Node => Node.Id == Route.TargetId);
            if (Source == null || Target == null || Source.IsDiamond || Target.IsDiamond) continue;
            var Start = Route.Points[0]; var Next = Route.Points[1];
            var End = Route.Points[^1]; var Previous = Route.Points[^2];
            bool Horizontal = Math.Abs(Start.Y - Next.Y) < 0.5 && Math.Abs(Previous.Y - End.Y) < 0.5;
            bool Vertical = Math.Abs(Start.X - Next.X) < 0.5 && Math.Abs(Previous.X - End.X) < 0.5;
            if (!Horizontal && !Vertical) continue;
            double Along(FlowchartLayoutPoint Point) => Horizontal ? Point.X : Point.Y;
            double Across(FlowchartLayoutPoint Point) => Horizontal ? Point.Y : Point.X;
            double Sign = Math.Sign(Along(Next) - Along(Start));
            double Gap = (Along(End) - Along(Start)) * Sign;
            double Needed = 3 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + 10;
            if (Sign == 0 || Math.Sign(Along(End) - Along(Previous)) != Sign || Gap <= 0 || Gap >= Needed
                || Math.Abs(Across(End) - Across(Start)) < 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) continue;
            double Cut = (Along(Start) + Along(End)) / 2;
            // Only open a genuine shared gap: never split a node body.
            if (Placed.Any(Node => (Horizontal ? Node.X - Node.Width / 2.0 : Node.Y - Node.Height / 2.0) < Cut
                && (Horizontal ? Node.X + Node.Width / 2.0 : Node.Y + Node.Height / 2.0) > Cut)) continue;
            int Shift = (int)Sign * DiagramGridMetrics.CeilToGrid(Needed - Gap);
            double Move(double Value) => (Value - Cut) * Sign >= 0 ? Value + Shift : Value;
            foreach (var Node in Placed)
            {
                if (Horizontal) Node.X = (int)Move(Node.X); else Node.Y = (int)Move(Node.Y);
            }
            foreach (var Frame in Frames)
            {
                if (Horizontal) { Frame.Left = (int)Move(Frame.Left); Frame.Right = (int)Move(Frame.Right); }
                else { Frame.Top = (int)Move(Frame.Top); Frame.Bottom = (int)Move(Frame.Bottom); }
            }
            foreach (var Other in Layout.EdgeRoutes)
            foreach (var Point in Other.Points)
            {
                if (Horizontal) Point.X = Move(Point.X); else Point.Y = Move(Point.Y);
            }
        }
    }

    /// <summary>
    /// Shared box for every node in the diagram. Diamonds use this width and twice this height.
    /// A circle never forces the box to become square.
    /// </summary>
    /// <param name="Contract">The diagram contract.</param>
    /// <returns>The box width and height in pixels.</returns>
    public static (int WidthPx, int HeightPx) ResolveStandardBox(DiagramContract Contract)
    {
        if (Contract == null)
        {
            return (DiagramGridMetrics.NODE_WIDTH_PX, DiagramGridMetrics.NODE_HEIGHT_PX);
        }

        int WidthPx = 0;
        int HeightPx = 0;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (NodeContract Node in Contract.Nodes)
        {
            FlowchartNodeDimensions Measured = FlowchartNodeSizeEstimator.Estimate(Node, Options);
            bool Diamond = FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
            if (Diamond)
            {
                int DiamondWidth = DiagramGridMetrics.RoundToGrid(Measured.Width);
                int DiamondHeight = DiagramGridMetrics.RoundToGrid(
                    Measured.Height / FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR);
                WidthPx = Math.Max(WidthPx, DiamondWidth);
                HeightPx = Math.Max(HeightPx, DiamondHeight);
                continue;
            }

            int NodeWidth = DiagramGridMetrics.RoundToGrid(Measured.Width);
            int NodeHeight = DiagramGridMetrics.RoundToGrid(Measured.Height);
            WidthPx = Math.Max(WidthPx, NodeWidth);
            HeightPx = Math.Max(HeightPx, NodeHeight);
        }

        if (WidthPx <= 0)
        {
            WidthPx = DiagramGridMetrics.NODE_WIDTH_PX;
        }

        if (HeightPx <= 0)
        {
            HeightPx = DiagramGridMetrics.NODE_HEIGHT_PX;
        }

        if (!UsesCompactLabelSizing(Contract))
        {
            WidthPx = Math.Max(WidthPx, DiagramGridMetrics.NODE_WIDTH_PX);
            HeightPx = Math.Max(HeightPx, DiagramGridMetrics.NODE_HEIGHT_PX);
        }

        // A diamond has twice the box height. Even grid-unit box heights keep
        // both centre-to-centre spans on the same grid with whole-unit gaps.
        HeightPx = 2 * DiagramGridMetrics.CeilToGrid(HeightPx / 2.0);
        return (WidthPx, HeightPx);
    }

    private static bool UsesCompactLabelSizing(DiagramContract Contract)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            string Label = string.IsNullOrEmpty(Node.Label) ? Node.Id : Node.Label;
            string NormalisedLabel = Label.Replace("<br>", "<br/>", StringComparison.OrdinalIgnoreCase)
                .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
            if (Label.Contains("<table", StringComparison.OrdinalIgnoreCase)
                || Label.Contains("<div", StringComparison.OrdinalIgnoreCase)
                || FlowchartRichLabelSvgWriter.IsMarkdownListLabel(NormalisedLabel))
            {
                return true;
            }
        }

        return false;
    }

    private static void MeasureLabel(
        string Label,
        FlowchartRenderOptions Options,
        out double NeededWidth,
        out double NeededHeight)
    {
        FlowchartLabelDimensions Measured = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Label ?? string.Empty, Options);
        string Text = string.IsNullOrEmpty(Measured.DisplayText) ? (Label ?? string.Empty) : Measured.DisplayText;
        string[] Lines = Text.Split('\n');
        double MaxWidth = 0.0;
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            MaxWidth = Math.Max(MaxWidth, FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Lines[Index], Options.FontSizePx));
        }

        double LineHeight = Options.FontSizePx * DiagramGridMetrics.TEXT_LINE_HEIGHT_EM;
        int LineCount = Math.Max(1, Lines.Length);
        NeededWidth = MaxWidth + (2.0 * DiagramGridMetrics.TEXT_INSET_PX);
        NeededHeight = (LineCount * LineHeight) + (2.0 * DiagramGridMetrics.TEXT_INSET_PX);
    }

    private static void AssignRanks(List<GridNode> Placed, bool Horizontal)
    {
        List<GridNode> Ordered = new List<GridNode>(Placed);
        Ordered.Sort(delegate(GridNode Left, GridNode Right)
        {
            return Main(Left, Horizontal, Old: true).CompareTo(Main(Right, Horizontal, Old: true));
        });

        int Rank = -1;
        double Band = double.NaN;
        foreach (GridNode Node in Ordered)
        {
            double Value = Main(Node, Horizontal, Old: true);
            if (Rank < 0 || Math.Abs(Value - Band) > DiagramGridMetrics.RANK_BAND_TOLERANCE_PX)
            {
                Rank++;
                Band = Value;
            }

            Node.Rank = Rank;
        }
    }

    private static List<ColumnBand> AssignColumns(List<GridNode> Placed, bool Horizontal, int CrossPitch)
    {
        List<GridNode> Ordered = new List<GridNode>(Placed);
        Ordered.Sort(delegate(GridNode Left, GridNode Right)
        {
            return Cross(Left, Horizontal, Old: true).CompareTo(Cross(Right, Horizontal, Old: true));
        });

        List<ColumnBand> Bands = new List<ColumnBand>();
        foreach (GridNode Node in Ordered)
        {
            ColumnBand? Match = null;
            for (int Index = Bands.Count - 1; Index >= 0; Index--)
            {
                ColumnBand Band = Bands[Index];
                if (Math.Abs(Band.Anchor - Cross(Node, Horizontal, Old: true)) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                {
                    continue;
                }

                if (BandContainsRank(Band, Node.Rank))
                {
                    continue;
                }

                Match = Band;
                break;
            }

            if (Match == null)
            {
                Match = new ColumnBand();
                Bands.Add(Match);
            }

            Match.Nodes.Add(Node);
            double Sum = 0.0;
            foreach (GridNode Member in Match.Nodes)
            {
                Sum += Cross(Member, Horizontal, Old: true);
            }

            Match.Anchor = Sum / Match.Nodes.Count;
        }

        Bands.Sort(delegate(ColumnBand Left, ColumnBand Right)
        {
            return Left.Anchor.CompareTo(Right.Anchor);
        });

        for (int Index = 0; Index < Bands.Count; Index++)
        {
            if (Index == 0)
            {
                Bands[Index].Index = 0;
            }
            else
            {
                double Delta = Bands[Index].Anchor - Bands[Index - 1].Anchor;
                int Steps = Math.Max(1, (int)Math.Round(Delta / CrossPitch, MidpointRounding.AwayFromZero));
                Bands[Index].Index = Bands[Index - 1].Index + Steps;
            }

            foreach (GridNode Node in Bands[Index].Nodes)
            {
                Node.Column = Bands[Index].Index;
            }
        }

        CascadeColumns(Placed);
        return Bands;
    }

    private static bool BandContainsRank(ColumnBand Band, int Rank)
    {
        foreach (GridNode Member in Band.Nodes)
        {
            if (Member.Rank == Rank)
            {
                return true;
            }
        }

        return false;
    }

    private static void CascadeColumns(List<GridNode> Placed)
    {
        int RankCount = 0;
        foreach (GridNode Node in Placed)
        {
            RankCount = Math.Max(RankCount, Node.Rank + 1);
        }

        for (int Rank = 0; Rank < RankCount; Rank++)
        {
            List<GridNode> RankNodes = NodesInRank(Placed, Rank);
            RankNodes.Sort(delegate(GridNode Left, GridNode Right)
            {
                int ColumnCompare = Left.Column.CompareTo(Right.Column);
                if (ColumnCompare != 0)
                {
                    return ColumnCompare;
                }

                return Left.OldX.CompareTo(Right.OldX);
            });

            for (int Index = 1; Index < RankNodes.Count; Index++)
            {
                if (RankNodes[Index].Column <= RankNodes[Index - 1].Column)
                {
                    RankNodes[Index].Column = RankNodes[Index - 1].Column + 1;
                }
            }
        }
    }

    /// <summary>
    /// Rulebook 2.6: two-way decisions place branch targets on opposite sides of the spine column.
    /// </summary>
    private static void AlignTwoWayDecisionSideTargets(DiagramContract Contract, List<GridNode> Placed)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (GridNode Decision in Placed)
        {
            if (!Decision.IsDiamond)
            {
                continue;
            }

            List<GridNode> Children = [];
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    || BackEdges.Contains(Edge)
                    || !string.Equals(Edge.SourceId, Decision.Id, StringComparison.Ordinal)
                    || !ById.TryGetValue(Edge.TargetId, out GridNode? Child))
                {
                    continue;
                }

                Children.Add(Child);
            }

            if (Children.Count != 2)
            {
                continue;
            }

            Children.Sort((Left, Right) => Left.OldX.CompareTo(Right.OldX));
            Children[0].Column = Decision.Column - 2;
            Children[1].Column = Decision.Column + 1;
        }
    }

    /// <summary>
    /// Two terminal outcomes belong on opposite sides of their diamond, so
    /// each facing vertex can reach its target with one rounded elbow.
    /// Preserve decisions whose children continue or participate in a merge.
    /// </summary>
    private static void AlignTerminalDecisionTargets(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool Horizontal,
        int CrossPitch,
        bool CoordinatesPlaced)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (GridNode Parent in Placed.Where(Node => Node.IsDiamond))
        {
            List<EdgeContract> Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToList();
            if (Outgoing.Count != 2 || Outgoing[0].TargetId == Outgoing[1].TargetId)
            {
                continue;
            }

            List<GridNode> Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId))
                .Select(Edge => ById[Edge.TargetId]).ToList();
            if (Children.Count != 2 || Children[0].Rank != Children[1].Rank
                || Children.Any(Child => Child.Rank == Parent.Rank || Child.GroupId != Parent.GroupId
                    || Visible.Any(Edge => Edge.SourceId == Child.Id)
                    || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1))
            {
                continue;
            }

            Children.Sort((Left, Right) => Cross(Left, Horizontal, Old: true)
                .CompareTo(Cross(Right, Horizontal, Old: true)));
            for (int Index = 0; Index < Children.Count; Index++)
            {
                int Side = Index == 0 ? -1 : 1;
                Children[Index].Column = Parent.Column + Side;
                if (CoordinatesPlaced)
                {
                    int Pitch = Math.Max(CrossPitch, DiagramGridMetrics.CeilToGrid(
                        CrossHalf(Parent, Horizontal, Leading: true)
                        + DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridMetrics.GRID_UNIT_PX));
                    SetCross(Children[Index], (int)Math.Round(Cross(Parent, Horizontal, Old: false))
                        + Side * Pitch, Horizontal);
                }
            }
        }
    }

    /// <summary>
    /// An odd fan of independent terminal children has a centre branch.
    /// Centre its ordinary parent, and any unbranched input chain, on that child.
    /// </summary>
    private static void CentreTerminalFans(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal, bool CoordinatesPlaced)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (GridNode Parent in Placed.Where(Node => !Node.IsDiamond))
        {
            List<EdgeContract> Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToList();
            if (Outgoing.Count < 3 || Outgoing.Count % 2 == 0
                || Outgoing.Select(Edge => Edge.TargetId).Distinct().Count() != Outgoing.Count)
            {
                continue;
            }

            List<GridNode> Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId))
                .Select(Edge => ById[Edge.TargetId]).ToList();
            if (Children.Count != Outgoing.Count || Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Child => Child.Rank == Parent.Rank || Child.GroupId != Parent.GroupId
                    || Visible.Any(Edge => Edge.SourceId == Child.Id)
                    || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1))
            {
                continue;
            }

            GridNode Middle = Children.OrderBy(Node => CoordinatesPlaced
                ? Cross(Node, Horizontal, Old: false) : Node.Column).ElementAt(Children.Count / 2);
            GridNode Current = Parent;
            HashSet<string> Visited = [];
            while (Visited.Add(Current.Id))
            {
                Current.Column = Middle.Column;
                if (CoordinatesPlaced)
                {
                    SetCross(Current, (int)Math.Round(Cross(Middle, Horizontal, Old: false)), Horizontal);
                }

                List<EdgeContract> Incoming = Visible.Where(Edge => Edge.TargetId == Current.Id).ToList();
                if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out GridNode? Previous)
                    || Previous.IsDiamond || Previous.GroupId != Parent.GroupId
                    || Visible.Count(Edge => Edge.SourceId == Previous.Id) != 1)
                {
                    break;
                }
                Current = Previous;
            }
        }
    }

    private static void AlignSingleTerminalChildren(DiagramContract Contract, List<GridNode> Placed,
        bool Horizontal, bool CoordinatesPlaced)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (GridNode Parent in Placed)
        {
            List<EdgeContract> Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToList();
            if (Outgoing.Count != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out GridNode? Child)
                || Child.GroupId != Parent.GroupId || Child.Rank == Parent.Rank
                || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1
                || Visible.Any(Edge => Edge.SourceId == Child.Id)) continue;
            double Desired = CoordinatesPlaced ? Cross(Parent, Horizontal, Old: false) : Parent.Column;
            bool Occupied = Placed.Any(Other => Other != Child && Other.Rank == Child.Rank
                && Math.Abs((CoordinatesPlaced ? Cross(Other, Horizontal, Old: false) : Other.Column) - Desired)
                    < (CoordinatesPlaced ? (CrossHalf(Other, Horizontal, true) + CrossHalf(Child, Horizontal, true)
                        + DiagramGridMetrics.NODE_GAP_PX) : 1));
            if (Occupied) continue;
            Child.Column = Parent.Column;
            if (CoordinatesPlaced) SetCross(Child, (int)Math.Round(Desired), Horizontal);
        }
    }

    /// <summary>
    /// A sole forward input enters a decision straight on its upstream vertex.
    /// Return edges into that input do not displace it from the decision's row.
    /// </summary>
    private static void AlignUnbranchedDecisionInputs(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        List<EdgeContract> Forward = Contract.Edges.Where(Edge => !BackEdges.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (GridNode Decision in Placed.Where(Node => Node.IsDiamond))
        {
            List<EdgeContract> Incoming = Forward.Where(Edge => Edge.TargetId == Decision.Id).ToList();
            if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out GridNode? Input)
                || Input.IsDiamond || Input.GroupId != Decision.GroupId
                || Forward.Count(Edge => Edge.SourceId == Input.Id) != 1)
            {
                continue;
            }

            HashSet<string> Visited = [];
            while (Visited.Add(Input.Id))
            {
                SetCross(Input, (int)Math.Round(Cross(Decision, Horizontal, Old: false)), Horizontal);
                Incoming = Forward.Where(Edge => Edge.TargetId == Input.Id).ToList();
                if (Incoming.Count != 1 || !ById.TryGetValue(Incoming[0].SourceId, out GridNode? Previous)
                    || Previous.IsDiamond || Previous.GroupId != Decision.GroupId
                    || Forward.Count(Edge => Edge.SourceId == Previous.Id) != 1)
                {
                    break;
                }
                Input = Previous;
            }
        }
    }

    /// <summary>
    /// A horizontal decision keeps its terminal outcome ahead and its retry below.
    /// </summary>
    private static void AlignHorizontalRetryOutcomes(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        if (!Horizontal)
        {
            return;
        }

        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (GridNode Decision in Placed.Where(Node => Node.IsDiamond))
        {
            List<EdgeContract> Incoming = Visible.Where(Edge => Edge.TargetId == Decision.Id).ToList();
            List<EdgeContract> Outgoing = Visible.Where(Edge => Edge.SourceId == Decision.Id).ToList();
            if (Incoming.Count != 1 || Outgoing.Count != 2
                || !ById.TryGetValue(Incoming[0].SourceId, out GridNode? Input)
                || Input.IsDiamond || Visible.Count(Edge => Edge.SourceId == Input.Id) != 1)
            {
                continue;
            }

            List<GridNode> Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId))
                .Select(Edge => ById[Edge.TargetId]).Distinct().ToList();
            GridNode? Retry = Children.FirstOrDefault(Child =>
                Visible.Count(Edge => Edge.SourceId == Child.Id) == 1
                && Visible.Any(Edge => Edge.SourceId == Child.Id && Edge.TargetId == Input.Id));
            GridNode? Finish = Children.FirstOrDefault(Child =>
                !Visible.Any(Edge => Edge.SourceId == Child.Id));
            if (Children.Count != 2 || Retry == null || Finish == null
                || Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId
                    || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1)
                || Input.GroupId != Decision.GroupId
                || Visible.Count(Edge => Edge.TargetId == Input.Id) != 1)
            {
                continue;
            }

            // Keep the successful outcome on the declared flow axis. The
            // returning branch occupies the side facing the lower return lane.
            SetCross(Finish, (int)Math.Round(Decision.Y), Horizontal);
            SetMain(Retry, (int)Math.Round(Decision.X), Horizontal);
            Retry.Rank = Decision.Rank;
            int Gap = DiagramGridMetrics.CeilToGrid(
                Decision.Height / 2 + Retry.Height / 2 + 3 * DiagramGridMetrics.GRID_UNIT_PX);
            SetCross(Retry, (int)Math.Round(Decision.Y) + Gap, Horizontal);
        }
    }

    private static void AlignVerticalLoopBranches(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal, int CrossPitch)
    {
        if (Horizontal) return;
        var ById = IndexPlaced(Placed);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Forward = Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Incoming = Forward.Where(Edge => Edge.TargetId == Decision.Id).ToList();
            var Outgoing = Forward.Where(Edge => Edge.SourceId == Decision.Id).ToList();
            if (Incoming.Count != 1 || Outgoing.Count != 2) continue;
            var Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).ToList();
            var Loop = Children.FirstOrDefault(Child => Back.Any(Edge => Edge.SourceId == Child.Id
                && Edge.TargetId == Incoming[0].SourceId));
            if (Children.Count != 2 || Children[0] == Children[1] || Loop == null
                || Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId
                    || Forward.Count(Edge => Edge.TargetId == Child.Id) != 1)) continue;
            var Continuation = Children.First(Child => Child != Loop);
            int Pitch = Math.Max(CrossPitch, DiagramGridMetrics.CeilToGrid(
                (Decision.Width + Math.Max(Loop.Width, Continuation.Width)) / 2 + DiagramGridMetrics.NODE_GAP_PX));
            SetCross(Loop, (int)Math.Round(Decision.X) + Pitch, Horizontal);
            SetCross(Continuation, (int)Math.Round(Decision.X) - Pitch, Horizontal);
            var Current = Continuation;
            var Visited = new HashSet<string>();
            while (Visited.Add(Current.Id))
            {
                var Next = Forward.Where(Edge => Edge.SourceId == Current.Id).ToList();
                if (Next.Count != 1 || !ById.TryGetValue(Next[0].TargetId, out var Child)
                    || Child.IsDiamond || Forward.Count(Edge => Edge.TargetId == Child.Id) != 1) break;
                SetCross(Child, (int)Math.Round(Continuation.X), Horizontal);
                Current = Child;
            }
        }
    }

    private static IEnumerable<(GridNode Decision, GridNode Detour, GridNode Merge)>
        DecisionDetours(DiagramContract Contract, List<GridNode> Placed)
    {
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Branches = Visible.Where(Edge => Edge.SourceId == Decision.Id && !Back.Contains(Edge)).ToList();
            if (Branches.Count != 2) continue;
            foreach (var Branch in Branches)
            {
                if (!ById.TryGetValue(Branch.TargetId, out var Detour) || Detour.IsDiamond
                    || Detour.GroupId != Decision.GroupId
                    || Visible.Count(Edge => Edge.TargetId == Detour.Id) != 1) continue;
                var Outputs = Visible.Where(Edge => Edge.SourceId == Detour.Id).ToList();
                if (Outputs.Count != 1 || Back.Contains(Outputs[0])
                    || !Branches.Any(Edge => Edge.TargetId == Outputs[0].TargetId)
                    || !ById.TryGetValue(Outputs[0].TargetId, out var Merge)
                    || Merge.IsDiamond || Merge.GroupId != Decision.GroupId) continue;
                yield return (Decision, Detour, Merge);
            }
        }
    }

    private static void AlignDecisionDetours(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal, int CrossPitch)
    {
        var Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var (Decision, Detour, Merge) in DecisionDetours(Contract, Placed))
        {
            int Centre = (int)Math.Round(Cross(Decision, Horizontal, Old: false));
            int Pitch = Math.Max(CrossPitch, DiagramGridMetrics.CeilToGrid(
                CrossHalf(Decision, Horizontal, Leading: true)
                + CrossHalf(Detour, Horizontal, Leading: true) + DiagramGridMetrics.NODE_GAP_PX));
            bool Terminal = !Visible.Any(Edge => Edge.SourceId == Merge.Id)
                && Visible.Count(Edge => Edge.TargetId == Merge.Id) > 2;
            SetCross(Detour, Centre + (Terminal ? Pitch : -Pitch), Horizontal);
            SetCross(Merge, Centre - (Terminal ? Pitch : 0), Horizontal);
            if (Terminal)
            {
                foreach (var Incoming in Visible.Where(Edge => Edge.TargetId == Merge.Id
                    && Edge.SourceId != Decision.Id && Edge.SourceId != Detour.Id))
                {
                    var EarlyExit = Placed.FirstOrDefault(Node => Node.Id == Incoming.SourceId);
                    if (EarlyExit == null || EarlyExit.IsDiamond || EarlyExit.GroupId != Merge.GroupId
                        || Visible.Count(Edge => Edge.SourceId == EarlyExit.Id) != 1
                        || Visible.Count(Edge => Edge.TargetId == EarlyExit.Id) != 1) continue;
                    var ParentEdge = Visible.Single(Edge => Edge.TargetId == EarlyExit.Id);
                    var Parent = Placed.FirstOrDefault(Node => Node.Id == ParentEdge.SourceId);
                    if (Parent == null || !Parent.IsDiamond || Math.Abs(Parent.Rank - Decision.Rank) < 2) continue;
                    SetCross(EarlyExit, Centre - 2 * Pitch, Horizontal);
                    SetMain(EarlyExit, (int)Math.Round(Main(Merge, Horizontal, Old: false)), Horizontal);
                    EarlyExit.Rank = Merge.Rank;
                }
            }
        }
    }

    private static void RebuildDecisionDetourRoutes(
        DiagramContract Contract, FlowchartLayoutResult Copy, List<GridNode> Placed,
        List<GridFrame> Frames, bool Horizontal)
    {
        var Bounds = BuildPlacedBounds(Placed, Frames);
        foreach (var (Decision, Detour, Merge) in DecisionDetours(Contract, Placed))
        {
            double Flow = Math.Sign(Main(Merge, Horizontal, Old: false) - Main(Decision, Horizontal, Old: false));
            double Side = Math.Sign(Cross(Detour, Horizontal, Old: false) - Cross(Merge, Horizontal, Old: false));
            (double X, double Y) Map(double Main, double Cross) => Horizontal ? (Main, Cross) : (Cross, Main);
            foreach (var Source in new[] { Decision, Detour })
            {
                var Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Source.Id && Route.TargetId == Merge.Id);
                if (Route == null) continue;
                double StartMain = Main(Source, Horizontal, Old: false);
                double StartCross = Cross(Source, Horizontal, Old: false);
                double EndMain = Main(Merge, Horizontal, Old: false);
                double EndCross = Cross(Merge, Horizontal, Old: false);
                var Path = new List<(double X, double Y)>();
                if (Source == Detour)
                {
                    StartMain += Flow * MainHalf(Source, Horizontal, Leading: true);
                    EndCross += Side * CrossHalf(Merge, Horizontal, Leading: true);
                    Path = [Map(StartMain, StartCross), Map(EndMain, StartCross), Map(EndMain, EndCross)];
                }
                else if (Math.Abs(StartCross - EndCross) < 0.5)
                {
                    StartMain += Flow * MainHalf(Source, Horizontal, Leading: true);
                    EndMain -= Flow * MainHalf(Merge, Horizontal, Leading: true);
                    Path = [Map(StartMain, StartCross), Map(EndMain, EndCross)];
                }
                else
                {
                    StartCross += Math.Sign(EndCross - StartCross) * CrossHalf(Source, Horizontal, Leading: true);
                    EndMain -= Flow * MainHalf(Merge, Horizontal, Leading: true);
                    Path = [Map(StartMain, StartCross), Map(StartMain, EndCross), Map(EndMain, EndCross)];
                }
                if (Path.Zip(Path.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds, Source.Id, Merge.Id))) continue;
                SetRouteEndpointSides(Route, Bounds[Source.Id], Bounds[Merge.Id], Path, Horizontal);
                Route.Points.Clear();
                foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
            foreach (var EarlyExit in Placed.Where(Node => Node.Id != Detour.Id
                && Node.GroupId == Merge.GroupId && Node.Rank == Merge.Rank
                && Contract.Edges.Count(Edge => Edge.SourceId == Node.Id) == 1
                && Contract.Edges.Any(Edge => Edge.SourceId == Node.Id && Edge.TargetId == Merge.Id)))
            {
                var Incoming = Contract.Edges.Where(Edge => Edge.TargetId == EarlyExit.Id).ToList();
                if (Incoming.Count != 1) continue;
                var Parent = Placed.FirstOrDefault(Node => Node.Id == Incoming[0].SourceId);
                if (Parent == null || !Parent.IsDiamond || Math.Abs(Parent.Rank - Decision.Rank) < 2) continue;
                var Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Parent.Id && Route.TargetId == EarlyExit.Id);
                if (Route == null) continue;
                double StartMain = Main(Parent, Horizontal, Old: false);
                double StartCross = Cross(Parent, Horizontal, Old: false) + Math.Sign(
                    Cross(EarlyExit, Horizontal, Old: false) - Cross(Parent, Horizontal, Old: false))
                    * CrossHalf(Parent, Horizontal, Leading: true);
                double EndMain = Main(EarlyExit, Horizontal, Old: false) - Flow * MainHalf(EarlyExit, Horizontal, Leading: true);
                double EndCross = Cross(EarlyExit, Horizontal, Old: false);
                var Path = new List<(double X, double Y)> { Map(StartMain, StartCross), Map(StartMain, EndCross), Map(EndMain, EndCross) };
                if (Path.Zip(Path.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds, Parent.Id, EarlyExit.Id))) continue;
                SetRouteEndpointSides(Route, Bounds[Parent.Id], Bounds[EarlyExit.Id], Path, Horizontal);
                Route.Points.Clear();
                foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
        }
    }

    private static IEnumerable<(GridNode Decision, GridNode Left, GridNode Right, GridNode Merge)>
        SimpleDecisionMerges(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Branches = Visible.Where(Edge => Edge.SourceId == Decision.Id).ToList();
            if (Branches.Count != 2 || Branches[0].TargetId == Branches[1].TargetId
                || Branches.Any(Edge => !ById.ContainsKey(Edge.TargetId))) continue;
            var Children = Branches.Select(Edge => ById[Edge.TargetId])
                .OrderBy(Node => Cross(Node, Horizontal, Old: true)).ToArray();
            if (Children.Any(Child => Child.IsDiamond || Child.GroupId != Decision.GroupId
                || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1
                || Visible.Count(Edge => Edge.SourceId == Child.Id) != 1)) continue;
            var Outputs = Children.Select(Child => Visible.Single(Edge => Edge.SourceId == Child.Id)).ToArray();
            if (Outputs[0].TargetId != Outputs[1].TargetId
                || !ById.TryGetValue(Outputs[0].TargetId, out var Merge)
                || Merge.IsDiamond || Merge.GroupId != Decision.GroupId
                || Visible.Any(Edge => Edge.SourceId == Merge.Id)
                || Visible.Count(Edge => Edge.TargetId == Merge.Id) != 2
                || Children[0].Rank != Children[1].Rank
                || Children[0].Rank == Decision.Rank || Merge.Rank == Decision.Rank) continue;
            yield return (Decision, Children[0], Children[1], Merge);
        }
    }

    private static void AlignSimpleDecisionMerges(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal, int CrossPitch)
    {
        foreach (var (Decision, Left, Right, Merge) in SimpleDecisionMerges(Contract, Placed, Horizontal))
        {
            int Pitch = Math.Max(CrossPitch, DiagramGridMetrics.CeilToGrid(
                CrossHalf(Decision, Horizontal, Leading: true)
                + Math.Max(CrossHalf(Left, Horizontal, Leading: true), CrossHalf(Right, Horizontal, Leading: true))
                + DiagramGridMetrics.NODE_GAP_PX));
            int Centre = (int)Math.Round(Cross(Decision, Horizontal, Old: false));
            SetCross(Left, Centre - Pitch, Horizontal);
            SetCross(Right, Centre + Pitch, Horizontal);
            SetCross(Merge, Centre + Pitch, Horizontal);
        }
    }

    private static void RebuildSimpleDecisionMergeRoutes(
        DiagramContract Contract, FlowchartLayoutResult Copy, List<GridNode> Placed,
        List<GridFrame> Frames, bool Horizontal)
    {
        var Bounds = BuildPlacedBounds(Placed, Frames);
        foreach (var (_, First, Second, Merge) in SimpleDecisionMerges(Contract, Placed, Horizontal))
        {
            var Left = Math.Abs(Cross(First, Horizontal, Old: false) - Cross(Merge, Horizontal, Old: false)) > 0.5 ? First : Second;
            var Edge = Contract.Edges.First(Edge => Edge.SourceId == Left.Id && Edge.TargetId == Merge.Id);
            var Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Left.Id && Route.TargetId == Merge.Id);
            if (Route == null) continue;
            double MainSign = Math.Sign(Main(Merge, Horizontal, Old: false) - Main(Left, Horizontal, Old: false));
            double CrossSign = Math.Sign(Cross(Left, Horizontal, Old: false) - Cross(Merge, Horizontal, Old: false));
            double StartMain = Main(Left, Horizontal, Old: false) + MainSign
                * (MainHalf(Left, Horizontal, Leading: true) + FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart));
            double StartCross = Cross(Left, Horizontal, Old: false);
            double EndMain = Main(Merge, Horizontal, Old: false);
            double EndCross = Cross(Merge, Horizontal, Old: false) + CrossSign
                * (CrossHalf(Merge, Horizontal, Leading: true) + FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd));
            (double X, double Y) Map(double Main, double Cross) => Horizontal ? (Main, Cross) : (Cross, Main);
            var Path = new List<(double X, double Y)> { Map(StartMain, StartCross), Map(EndMain, StartCross), Map(EndMain, EndCross) };
            if (Path.Zip(Path.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Bounds, Left.Id, Merge.Id))) continue;
            SetRouteEndpointSides(Route, Bounds[Left.Id], Bounds[Merge.Id], Path, Horizontal);
            Route.Points.Clear();
            foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
        }
    }

    /// <summary>
    /// Independent inputs in one rank occupy neighbouring columns around their merge.
    /// Empty columns inherited from the original placement need no reserved space.
    /// </summary>
    internal static void CompactIndependentFanIn(
        DiagramContract Contract, List<GridNode> Placed, bool Horizontal, int CrossPitch)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (GridNode Target in Placed)
        {
            List<EdgeContract> Incoming = Visible.Where(Edge => Edge.TargetId == Target.Id).ToList();
            if (Incoming.Count < 3 || Incoming.Count % 2 == 0
                || Incoming.Select(Edge => Edge.SourceId).Distinct().Count() != Incoming.Count)
            {
                continue;
            }
            List<GridNode> Parents = Incoming.Where(Edge => ById.ContainsKey(Edge.SourceId))
                .Select(Edge => ById[Edge.SourceId]).OrderBy(Node => Cross(Node, Horizontal, Old: false)).ToList();
            if (Parents.Count != Incoming.Count || Parents.Select(Node => Node.Rank).Distinct().Count() != 1
                || Parents.Any(Parent => Parent.Rank == Target.Rank || Parent.GroupId != Target.GroupId
                    || Visible.Count(Edge => Edge.SourceId == Parent.Id) != 1)
                || Placed.Any(Node => Node.Rank == Parents[0].Rank && !Parents.Contains(Node)))
            {
                continue;
            }
            int Pitch = Math.Max(CrossPitch, DiagramGridMetrics.CeilToGrid(
                Parents.Max(Node => 2 * CrossHalf(Node, Horizontal, Leading: true)) + DiagramGridMetrics.NODE_GAP_PX));
            var Anchored = Parents.Where(Parent => Visible.Any(Edge => Edge.TargetId == Parent.Id)).ToList();
            foreach (var Parent in Anchored)
            {
                int Offset = Parents.IndexOf(Parent) - Parents.Count / 2;
                if (Offset != 0) Pitch = Math.Max(Pitch, DiagramGridMetrics.CeilToGrid(
                    Math.Abs(Cross(Parent, Horizontal, Old: false) - Cross(Target, Horizontal, Old: false)) / Math.Abs(Offset)));
            }
            // An upstream chain anchors its parent. Independent inputs may
            // close around that anchor when it already occupies its fan slot.
            if (Anchored.Any(Parent => Math.Abs(Cross(Parent, Horizontal, Old: false)
                    - (Cross(Target, Horizontal, Old: false) + (Parents.IndexOf(Parent) - Parents.Count / 2) * Pitch)) > 0.5)) continue;
            for (int Index = 0; Index < Parents.Count; Index++)
            {
                SetCross(Parents[Index], (int)Math.Round(Cross(Target, Horizontal, Old: false))
                    + (Index - Parents.Count / 2) * Pitch, Horizontal);
            }
        }
    }

    /// <summary>Centres a shared fan between independent inputs and straightens its unbranched tails.</summary>
    internal static HashSet<string> AlignSharedFanSourcesAndTails(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Aligned = new HashSet<string>(StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Visible = Contract.Edges.Where(Edge => !BackEdges.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var Source in Placed.Where(Node => !Node.IsDiamond))
        {
            if (Visible.Any(Edge => Edge.TargetId == Source.Id)) continue;
            var Children = Visible.Where(Edge => Edge.SourceId == Source.Id && ById.ContainsKey(Edge.TargetId))
                .Select(Edge => ById[Edge.TargetId]).Distinct().ToList();
            if (Children.Count < 2 || Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Node => Node.IsDiamond || Node.Rank == Source.Rank || Node.GroupId != Source.GroupId)) continue;
            bool IndependentInputs = Children.All(Child =>
            {
                var Others = Visible.Where(Edge => Edge.TargetId == Child.Id && Edge.SourceId != Source.Id).ToList();
                return Others.Count > 0 && Others.All(Edge => ById.TryGetValue(Edge.SourceId, out var Parent)
                    && Parent.Rank == Source.Rank && Parent.GroupId == Source.GroupId && !Parent.IsDiamond
                    && !Visible.Any(Incoming => Incoming.TargetId == Parent.Id)
                    && Visible.Count(Outgoing => Outgoing.SourceId == Parent.Id) == 1);
            });
            if (!IndependentInputs) continue;
            double Centre = (Children.Min(Node => Cross(Node, Horizontal, Old: false))
                + Children.Max(Node => Cross(Node, Horizontal, Old: false))) / 2;
            double Anchor = Cross(Children[0], Horizontal, Old: false);
            int Position = (int)(Anchor + Math.Round((Centre - Anchor) / DiagramGridMetrics.GRID_UNIT_PX)
                * DiagramGridMetrics.GRID_UNIT_PX);
            if (TryMove(Source, Position)) Aligned.Add(Source.Id);
            foreach (var Child in Children)
            {
                var Current = Child;
                var Seen = new HashSet<string>();
                while (Seen.Add(Current.Id))
                {
                    var Outgoing = Visible.Where(Edge => Edge.SourceId == Current.Id).ToList();
                    if (Outgoing.Count != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out var Next)
                        || Next.IsDiamond || Next.Rank == Current.Rank || Next.GroupId != Current.GroupId
                        || Visible.Count(Edge => Edge.TargetId == Next.Id) != 1) break;
                    if (!TryMove(Next, (int)Cross(Current, Horizontal, Old: false))) break;
                    Current = Next;
                }
            }
        }

        return Aligned;

        bool TryMove(GridNode Node, int Position)
        {
            if (Placed.Any(Other => Other != Node && Math.Abs(Main(Other, Horizontal, Old: false) - Main(Node, Horizontal, Old: false))
                < MainHalf(Other, Horizontal, Leading: true) + MainHalf(Node, Horizontal, Leading: true)
                && Math.Abs(Cross(Other, Horizontal, Old: false) - Position)
                < CrossHalf(Other, Horizontal, Leading: true) + CrossHalf(Node, Horizontal, Leading: true) + DiagramGridMetrics.NODE_GAP_PX)) return false;
            SetCross(Node, Position, Horizontal);
            return true;
        }
    }

    private static bool ShouldUseSpineColumnLayout(
        DiagramContract Contract,
        List<GridNode> Placed,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.OldX,
                CenterY = Node.OldY,
                Width = Node.Width,
                Height = Node.Height
            };
        }

        return FlowchartSpineGraphHelper.TryBuildContext(
            Contract,
            BoundsById,
            Direction,
            FlowchartRenderOptions.FromContract(Contract),
            out _);
    }

    /// <summary>
    /// Rulebook 2.6: after grid placement, move decision side branches into margin columns on the spine layout.
    /// </summary>
    private static void ApplySpineColumnAdjustments(
        DiagramContract Contract,
        List<GridNode> Placed,
        FlowchartDirection Direction)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<FlowchartLayoutNodeBounds> Bounds = [];
        foreach (GridNode Node in Placed)
        {
            Bounds.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            });
        }

        List<FlowchartLayoutNodeBounds> Adjusted = FlowchartSpineColumnPostProcessor.Apply(
            Contract,
            Bounds,
            Direction,
            FlowchartRenderOptions.FromContract(Contract));
        foreach (FlowchartLayoutNodeBounds NodeBounds in Adjusted)
        {
            if (ById.TryGetValue(NodeBounds.NodeId, out GridNode? Node))
            {
                Node.X = (int)Math.Round(NodeBounds.CenterX);
                Node.Y = (int)Math.Round(NodeBounds.CenterY);
            }
        }
    }

    /// <summary>
    /// Rulebook 2.4 / 2.10: when several same-rank parents fan into one child, centre the child on the middle parent.
    /// </summary>
    private static void AlignFanInMergeColumn(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool Horizontal)
    {
        if (Horizontal)
        {
            return;
        }

        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (GridNode Target in Placed)
        {
            List<GridNode> Parents = [];
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    || BackEdges.Contains(Edge)
                    || !string.Equals(Edge.TargetId, Target.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ById.TryGetValue(Edge.SourceId, out GridNode? Parent))
                {
                    Parents.Add(Parent);
                }
            }

            if (Parents.Count < 2)
            {
                continue;
            }

            int ParentRank = Parents[0].Rank;
            if (Parents.Any(Parent => Parent.Rank != ParentRank))
            {
                continue;
            }

            Parents.Sort((Left, Right) => Cross(Left, Horizontal, Old: true).CompareTo(Cross(Right, Horizontal, Old: true)));
            Target.Column = Parents[Parents.Count / 2].Column;
        }
    }

    /// <summary>
    /// Rulebook 2.6 / decision 4: place the detour child beside the spine; the rejoin node stays on the spine.
    /// </summary>
    private static void AlignBypassDetourColumns(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool Horizontal)
    {
        if (Horizontal)
        {
            return;
        }

        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        Dictionary<string, HashSet<string>> Parents = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> Children = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || BackEdges.Contains(Edge))
            {
                continue;
            }

            if (!Parents.TryGetValue(Edge.TargetId, out HashSet<string>? ParentSet))
            {
                ParentSet = new HashSet<string>(StringComparer.Ordinal);
                Parents[Edge.TargetId] = ParentSet;
            }

            ParentSet.Add(Edge.SourceId);

            if (!Children.TryGetValue(Edge.SourceId, out HashSet<string>? ChildSet))
            {
                ChildSet = new HashSet<string>(StringComparer.Ordinal);
                Children[Edge.SourceId] = ChildSet;
            }

            ChildSet.Add(Edge.TargetId);
        }

        foreach (GridNode Candidate in Placed)
        {
            if (!Parents.TryGetValue(Candidate.Id, out HashSet<string>? IncomingParents) || IncomingParents.Count != 2)
            {
                continue;
            }

            GridNode? DecisionParent = null;
            GridNode? IntermediateChild = null;
            foreach (string ParentId in IncomingParents)
            {
                if (!ById.TryGetValue(ParentId, out GridNode? ParentNode))
                {
                    continue;
                }

                if (!ParentNode.IsDiamond)
                {
                    if (IntermediateChild != null)
                    {
                        continue;
                    }

                    IntermediateChild = ParentNode;
                    continue;
                }

                if (DecisionParent != null)
                {
                    continue;
                }

                DecisionParent = ParentNode;
            }

            if (DecisionParent is null || IntermediateChild is null)
            {
                continue;
            }

            if (!Children.TryGetValue(DecisionParent.Id, out HashSet<string>? DecisionChildren)
                || !DecisionChildren.Contains(IntermediateChild.Id)
                || !DecisionChildren.Contains(Candidate.Id)
                || !Children.TryGetValue(IntermediateChild.Id, out HashSet<string>? IntermediateChildren)
                || !IntermediateChildren.Contains(Candidate.Id))
            {
                continue;
            }

            bool BranchIsLeft = Cross(IntermediateChild, Horizontal, Old: true)
                < Cross(DecisionParent, Horizontal, Old: true) - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
            IntermediateChild.Column = BranchIsLeft
                ? DecisionParent.Column - 1
                : DecisionParent.Column + 1;
            Candidate.Column = DecisionParent.Column;
        }

        CascadeColumns(Placed);
    }

    /// <summary>
    /// B12b/B12c: for a three-way decision fan-out, the centre child keeps the parent column.
    /// </summary>
    private static void AlignDecisionCentreChildColumn(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool Horizontal)
    {
        if (Horizontal)
        {
            return;
        }

        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (GridNode Parent in Placed)
        {
            if (!Parent.IsDiamond)
            {
                continue;
            }

            List<GridNode> Children = [];
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    || BackEdges.Contains(Edge)
                    || !string.Equals(Edge.SourceId, Parent.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ById.TryGetValue(Edge.TargetId, out GridNode? Child) || Child.Rank <= Parent.Rank)
                {
                    continue;
                }

                Children.Add(Child);
            }

            if (Children.Count < 3)
            {
                continue;
            }

            double ParentCross = Parent.OldX;
            GridNode? CentreChild = null;
            double MinOffset = double.MaxValue;
            foreach (GridNode Child in Children)
            {
                double Offset = Math.Abs(Child.OldX - ParentCross);
                if (Offset < MinOffset)
                {
                    MinOffset = Offset;
                    CentreChild = Child;
                }
            }

            if (CentreChild == null)
            {
                continue;
            }

            CentreChild.Column = Parent.Column;
            List<GridNode> LeftChildren = [];
            List<GridNode> RightChildren = [];
            foreach (GridNode Child in Children)
            {
                if (string.Equals(Child.Id, CentreChild.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (Child.OldX < CentreChild.OldX - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                {
                    LeftChildren.Add(Child);
                }
                else if (Child.OldX > CentreChild.OldX + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                {
                    RightChildren.Add(Child);
                }
            }

            LeftChildren.Sort((Left, Right) => Left.OldX.CompareTo(Right.OldX));
            RightChildren.Sort((Left, Right) => Left.OldX.CompareTo(Right.OldX));
            int LeftColumn = Parent.Column - 1;
            foreach (GridNode Child in LeftChildren)
            {
                Child.Column = LeftColumn;
                LeftColumn--;
            }

            int RightColumn = Parent.Column + 1;
            foreach (GridNode Child in RightChildren)
            {
                Child.Column = RightColumn;
                RightColumn++;
            }
        }
    }

    /// <summary>
    /// Rules A3/A5: when a parent and child sit in different columns, any same-rank node
    /// that shares the parent column blocks their vertical chord and swaps aside.
    /// </summary>
    private static void ResolveVerticalChordBlockers(DiagramContract Contract, List<GridNode> Placed)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        Dictionary<string, int> ParentCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!ById.ContainsKey(Edge.SourceId) || !ById.ContainsKey(Edge.TargetId) || BackEdges.Contains(Edge))
            {
                continue;
            }

            ParentCount.TryGetValue(Edge.TargetId, out int Count);
            ParentCount[Edge.TargetId] = Count + 1;
        }

        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (BackEdges.Contains(Edge))
            {
                continue;
            }

            if (!ById.TryGetValue(Edge.SourceId, out GridNode Parent)
                || !ById.TryGetValue(Edge.TargetId, out GridNode Child))
            {
                continue;
            }

            if (!ParentCount.TryGetValue(Child.Id, out int Parents) || Parents < 2)
            {
                continue;
            }

            if (Parent.Rank >= Child.Rank || Parent.Column == Child.Column)
            {
                continue;
            }

            List<GridNode> RankNodes = NodesInRank(Placed, Child.Rank);
            foreach (GridNode Mate in RankNodes)
            {
                if (string.Equals(Mate.Id, Child.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (Mate.Column != Parent.Column)
                {
                    continue;
                }

                int ChildColumn = Child.Column;
                Child.Column = Parent.Column;
                Mate.Column = ChildColumn;
            }
        }
    }

    private static void PlaceOnGrid(
        List<GridNode> Placed,
        bool Horizontal,
        int BoxWidth,
        int BoxHeight,
        int MainPitch,
        int CrossPitch,
        FlowchartLayoutResult Copy,
        DiagramContract Contract)
    {
        int RankCount = 0;
        foreach (GridNode Node in Placed)
        {
            RankCount = Math.Max(RankCount, Node.Rank + 1);
        }

        List<RankPairGapPlan> GapPlans = BuildRankPairGapPlans(
            Placed,
            RankCount,
            Horizontal,
            MainPitch,
            Copy,
            Contract);

        int MainCursor = Horizontal ? BoxWidth / 2 : BoxHeight / 2;
        for (int Rank = 0; Rank < RankCount; Rank++)
        {
            List<GridNode> RankNodes = NodesInRank(Placed, Rank);
            foreach (GridNode Node in RankNodes)
            {
                SetMain(Node, MainCursor, Horizontal);
            }

            if (Rank + 1 >= RankCount)
            {
                continue;
            }

            RankPairGapPlan Plan = GapPlans[Rank];
            MainCursor += DiagramGridMetrics.CeilToGrid(Plan.HalfBelow + Plan.HalfAbove
                + Plan.StructuralClearGap + Plan.LabelClearGapBoost);
        }

        SortedDictionary<int, List<GridNode>> ByColumn = new SortedDictionary<int, List<GridNode>>();
        foreach (GridNode Node in Placed)
        {
            if (!ByColumn.TryGetValue(Node.Column, out List<GridNode>? Members))
            {
                Members = new List<GridNode>();
                ByColumn[Node.Column] = Members;
            }

            Members.Add(Node);
        }

        int CrossCursor = Horizontal ? BoxHeight / 2 : BoxWidth / 2;
        int PreviousColumn = int.MinValue;
        List<GridNode>? PreviousMembers = null;
        foreach (KeyValuePair<int, List<GridNode>> Pair in ByColumn)
        {
            if (PreviousMembers != null)
            {
                int IndexGap = Math.Max(1, Pair.Key - PreviousColumn);
                int PitchSpan = IndexGap * CrossPitch;
                int HalfBefore = MaxCrossHalf(PreviousMembers, Horizontal, Before: false);
                int HalfAfter = MaxCrossHalf(Pair.Value, Horizontal, Before: true);
                int MinimumCrossClearGap = Math.Max(
                    DiagramGridMetrics.NODE_GAP_PX,
                    (int)Math.Round(FlowchartRenderOptions.FromContract(Contract).NodeSpacingPx));
                int Span = DiagramGridMetrics.CeilToGrid(Math.Max(PitchSpan, HalfBefore + HalfAfter + MinimumCrossClearGap));
                CrossCursor += Span;
            }

            foreach (GridNode Node in Pair.Value)
            {
                SetCross(Node, CrossCursor, Horizontal);
            }

            PreviousColumn = Pair.Key;
            PreviousMembers = Pair.Value;
        }
    }

    private static double MermaidMain(List<GridNode> Nodes, bool Horizontal)
    {
        double Sum = 0.0;
        foreach (GridNode Node in Nodes)
        {
            Sum += Main(Node, Horizontal, Old: true);
        }

        if (Nodes.Count == 0)
        {
            return 0.0;
        }

        return Sum / Nodes.Count;
    }

    private static int CountRankCrossings(FlowchartLayoutResult Copy, List<GridNode> Earlier, List<GridNode> Later)
    {
        HashSet<string> EarlierIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> LaterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GridNode Node in Earlier)
        {
            EarlierIds.Add(Node.Id);
        }

        foreach (GridNode Node in Later)
        {
            LaterIds.Add(Node.Id);
        }

        int Count = 0;
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            bool Forward = EarlierIds.Contains(Route.SourceId) && LaterIds.Contains(Route.TargetId);
            bool Back = LaterIds.Contains(Route.SourceId) && EarlierIds.Contains(Route.TargetId);
            if (Forward || Back)
            {
                Count++;
            }
        }

        return Count;
    }

    private static void GrowParallelRelationshipSides(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var ById = IndexPlaced(Placed);
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (var Group in Contract.Edges.Where(Edge => Edge.SourceId != Edge.TargetId
            && !BackEdges.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .GroupBy(Edge => (Edge.SourceId, Edge.TargetId)).Where(Group => Group.Count() > 1))
        {
            if (!ById.TryGetValue(Group.Key.SourceId, out var Source)
                || !ById.TryGetValue(Group.Key.TargetId, out var Target)
                || Source.IsDiamond || Target.IsDiamond) continue;
            double Pitch = Group.Max(Edge =>
            {
                var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? string.Empty, Options, Options.EdgeLabelFontSizePx);
                double WordWidth = Size.DisplayText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Word => Word.Length * Options.EdgeLabelFontSizePx * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM)
                    .DefaultIfEmpty(0).Max();
                return (Horizontal ? Size.Height : WordWidth) + 12;
            });
            int Needed = DiagramGridMetrics.CeilToGrid(Group.Count() * Pitch + 30);
            if (Horizontal) Source.Height = Target.Height = Math.Max(Needed, Math.Max(Source.Height, Target.Height));
            else Source.Width = Target.Width = Math.Max(Needed, Math.Max(Source.Width, Target.Width));
        }
    }

    internal static void SeparateParallelReturnTracks(DiagramContract Contract, FlowchartLayoutResult Layout,
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (var Group in Layout.EdgeRoutes.Where(Route => Route.Points.Count == 4
            && Route.ExitSide == Route.EntrySide && Route.ExitSide is "left" or "right" or "top" or "bottom"
            && BackEdges.Any(Edge => Edge.Id == Route.EdgeId))
            .GroupBy(Route => (Route.SourceId, Route.TargetId, Route.ExitSide)).Where(Group => Group.Count() > 1))
        {
            bool Vertical = Group.Key.ExitSide is "left" or "right";
            double Sign = Group.Key.ExitSide is "right" or "top" ? 1 : -1;
            double Across(FlowchartLayoutPoint Point) => Vertical ? Point.X : Point.Y;
            double Along(FlowchartLayoutPoint Point) => Vertical ? Point.Y : Point.X;
            double AlongSign = Math.Sign(Along(Group.First().Points[0]) - Along(Group.First().Points[^1]));
            if (AlongSign == 0) continue;
            var Routes = Group.OrderBy(Route => Along(Route.Points[0]) * AlongSign).ToList();
            var Ends = Routes.Select(Route => Route.Points[^1]).OrderByDescending(Point => Along(Point) * AlongSign).ToList();
            double Track = Group.Min(Route => Across(Route.Points[1]) * Sign) * Sign;
            double PreviousHalf = 0;
            var Candidates = new List<(FlowchartLayoutEdgeRoute Route, double Track, FlowchartLayoutPoint End)>();
            foreach (var Route in Routes)
            {
                var Edge = Contract.Edges.First(Edge => Edge.Id == Route.EdgeId);
                var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label ?? string.Empty, Options, Options.EdgeLabelFontSizePx);
                double Half = string.IsNullOrWhiteSpace(Edge.Label) ? 0 : (Vertical ? Size.Width : Size.Height) / 2;
                if (Route != Routes[0]) Track += Sign * DiagramGridMetrics.CeilToGrid(Math.Max(6, PreviousHalf + Half + 12));
                Candidates.Add((Route, Track, Ends[Candidates.Count]));
                PreviousHalf = Half;
            }
            bool Clear = Candidates.All(Candidate =>
            {
                var Route = Candidate.Route;
                var Points = Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
                Points[1] = Vertical ? (Candidate.Track, Points[1].Y) : (Points[1].X, Candidate.Track);
                Points[2] = Vertical ? (Candidate.Track, Candidate.End.Y) : (Candidate.End.X, Candidate.Track);
                Points[3] = (Candidate.End.X, Candidate.End.Y);
                foreach (var Other in Layout.EdgeRoutes.Where(Other => !Routes.Contains(Other)))
                {
                    var Path = Other.Points.Select(Point => (Point.X, Point.Y)).ToList();
                    var Old = FlowchartBranchRouteOrder.Intersections(Route.Points.Select(Point => (Point.X, Point.Y)).ToList(), Path);
                    var New = FlowchartBranchRouteOrder.Intersections(Points, Path);
                    if (New.Crossings > Old.Crossings || New.Overlap > Old.Overlap + 0.01) return false;
                }
                foreach (var Frame in Contract.Groups.Where(Frame => IsInsideGroup(Contract, Route.SourceId, Frame.Id)
                    && IsInsideGroup(Contract, Route.TargetId, Frame.Id)))
                    if (Bounds.TryGetValue(Frame.Id, out var Box) && Points.Any(Point =>
                        Point.X < Box.CenterX - Box.Width / 2 || Point.X > Box.CenterX + Box.Width / 2
                        || Point.Y < Box.CenterY - Box.Height / 2 || Point.Y > Box.CenterY + Box.Height / 2)) return false;
                var Obstacles = Bounds.Where(Pair => !Contract.Groups.Any(Frame => Frame.Id == Pair.Key
                    && (IsInsideGroup(Contract, Route.SourceId, Frame.Id) || IsInsideGroup(Contract, Route.TargetId, Frame.Id))))
                    .ToDictionary(Pair => Pair.Key, Pair => Pair.Value);
                return Points.Zip(Points.Skip(1)).All(Segment => !FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Segment.First.X, Segment.First.Y, Segment.Second.X, Segment.Second.Y, Obstacles, Route.SourceId, Route.TargetId));
            });
            if (!Clear) continue;
            foreach (var Candidate in Candidates)
            {
                if (Vertical)
                {
                    Candidate.Route.Points[1].X = Candidate.Route.Points[2].X = Candidate.Track;
                    Candidate.Route.Points[2].Y = Candidate.End.Y;
                }
                else
                {
                    Candidate.Route.Points[1].Y = Candidate.Route.Points[2].Y = Candidate.Track;
                    Candidate.Route.Points[2].X = Candidate.End.X;
                }
                Candidate.Route.Points[3] = new() { X = Candidate.End.X, Y = Candidate.End.Y };
            }
        }
    }

    private static void GrowCrowdedSides(DiagramContract Contract, List<GridNode> Placed,
        FlowchartLayoutResult Copy, int BoxWidth, int BoxHeight)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            EdgeContract? Edge = FindContractEdge(Contract, Route);
            AddSideCount(Counts, Route.SourceId,
                Edge?.RoutingHint?.IsActive == true && Edge.RoutingHint.Leave.HasValue
                    ? HintSide(Edge.RoutingHint.Leave.Value) : Route.ExitSide);
            AddSideCount(Counts, Route.TargetId,
                Edge?.RoutingHint?.IsActive == true && Edge.RoutingHint.Enter.HasValue
                    ? HintSide(Edge.RoutingHint.Enter.Value) : Route.EntrySide);
        }

        foreach (KeyValuePair<string, int> Pair in Counts)
        {
            if (Pair.Value <= DiagramGridMetrics.TRACKS_PER_GAP)
            {
                continue;
            }

            string[] Parts = Pair.Key.Split('|');
            if (Parts.Length != 2 || !ById.TryGetValue(Parts[0], out GridNode? Node))
            {
                continue;
            }

            bool AlongWidth = Parts[1] == "top" || Parts[1] == "bottom";
            int MinPitch = AlongWidth ? Math.Max(1, BoxWidth / DiagramGridMetrics.TRACKS_PER_GAP) : Math.Max(1, BoxHeight / DiagramGridMetrics.TRACKS_PER_GAP);
            int Needed = DiagramGridMetrics.CeilToGrid(Pair.Value * MinPitch);
            if (AlongWidth)
            {
                Node.Width = Math.Max(Node.Width, Needed);
            }
            else
            {
                Node.Height = Math.Max(Node.Height, Needed);
            }
        }
    }

    private static void AddSideCount(Dictionary<string, int> Counts, string NodeId, string Side)
    {
        if (string.IsNullOrEmpty(NodeId) || string.IsNullOrEmpty(Side))
        {
            return;
        }

        string Key = NodeId + "|" + Side;
        if (!Counts.TryGetValue(Key, out int Count))
        {
            Count = 0;
        }

        Counts[Key] = Count + 1;
    }

    private static void SeparateOverlaps(List<GridNode> Placed, bool Horizontal)
    {
        for (int Pass = 0; Pass < MAX_SEPARATION_PASSES; Pass++)
        {
            bool Moved = false;
            int RankCount = 0;
            foreach (GridNode Node in Placed)
            {
                RankCount = Math.Max(RankCount, Node.Rank + 1);
            }

            for (int Rank = 0; Rank < RankCount; Rank++)
            {
                List<GridNode> RankNodes = NodesInRank(Placed, Rank);
                RankNodes.Sort(delegate(GridNode Left, GridNode Right)
                {
                    return Cross(Left, Horizontal, Old: false).CompareTo(Cross(Right, Horizontal, Old: false));
                });

                for (int Index = 1; Index < RankNodes.Count; Index++)
                {
                    GridNode Previous = RankNodes[Index - 1];
                    GridNode Current = RankNodes[Index];
                    int Needed = CrossHalf(Previous, Horizontal, Leading: false) + CrossHalf(Current, Horizontal, Leading: true) + DiagramGridMetrics.NODE_GAP_PX;
                    int Have = (int)Math.Round(Cross(Current, Horizontal, Old: false) - Cross(Previous, Horizontal, Old: false));
                    if (Have < Needed)
                    {
                        int Push = DiagramGridMetrics.CeilToGrid(Needed - Have);
                        if (Push < DiagramGridMetrics.GRID_UNIT_PX)
                        {
                            Push = DiagramGridMetrics.GRID_UNIT_PX;
                        }

                        ShiftCrossFrom(Placed, Cross(Current, Horizontal, Old: false) - 0.5, Push, Horizontal);
                        Moved = true;
                    }
                }
            }

            for (int Rank = 1; Rank < RankCount; Rank++)
            {
                List<GridNode> Earlier = NodesInRank(Placed, Rank - 1);
                List<GridNode> Later = NodesInRank(Placed, Rank);
                int Deficit = 0;
                foreach (GridNode Before in Earlier)
                {
                    foreach (GridNode After in Later)
                    {
                        int Needed = MainHalf(Before, Horizontal, Leading: false) + MainHalf(After, Horizontal, Leading: true) + DiagramGridMetrics.NODE_GAP_PX;
                        int Have = (int)Math.Round(Main(After, Horizontal, Old: false) - Main(Before, Horizontal, Old: false));
                        if (Have < Needed)
                        {
                            Deficit = Math.Max(Deficit, Needed - Have);
                        }
                    }
                }

                if (Deficit > 0)
                {
                    int Push = DiagramGridMetrics.CeilToGrid(Deficit);
                    if (Push < DiagramGridMetrics.GRID_UNIT_PX)
                    {
                        Push = DiagramGridMetrics.GRID_UNIT_PX;
                    }

                    foreach (GridNode Node in Placed)
                    {
                        if (Node.Rank >= Rank)
                        {
                            SetMain(Node, (int)Math.Round(Main(Node, Horizontal, Old: false)) + Push, Horizontal);
                        }
                    }

                    Moved = true;
                }
            }

            if (!Moved)
            {
                break;
            }
        }
    }

    private static void ShiftCrossFrom(List<GridNode> Placed, double FromCross, int Push, bool Horizontal)
    {
        foreach (GridNode Node in Placed)
        {
            if (Cross(Node, Horizontal, Old: false) >= FromCross)
            {
                SetCross(Node, (int)Math.Round(Cross(Node, Horizontal, Old: false)) + Push, Horizontal);
            }
        }
    }

    internal static void SeparateTracks(FlowchartLayoutResult Copy, List<GridNode> Placed, DiagramContract? Contract = null)
    {
        List<FlowchartLayoutEdgeRoute> Routes = new List<FlowchartLayoutEdgeRoute>();
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count >= 3)
            {
                Routes.Add(Route);
            }
        }

        Routes.Sort(delegate(FlowchartLayoutEdgeRoute Left, FlowchartLayoutEdgeRoute Right)
        {
            int Compare = Left.Points[0].X.CompareTo(Right.Points[0].X);
            if (Compare != 0)
            {
                return Compare;
            }

            return Left.Points[0].Y.CompareTo(Right.Points[0].Y);
        });

        for (int LeftIndex = 0; LeftIndex < Routes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < Routes.Count; RightIndex++)
            {
                NudgeOffSharedRun(Routes[LeftIndex], Routes[RightIndex], Placed, Contract);
            }
        }
    }

    private static void NudgeOffSharedRun(FlowchartLayoutEdgeRoute Stay, FlowchartLayoutEdgeRoute Mover, List<GridNode> Placed, DiagramContract? Contract)
    {
        for (int Index = 1; Index < Mover.Points.Count - 2; Index++)
        {
            FlowchartLayoutPoint Start = Mover.Points[Index];
            FlowchartLayoutPoint End = Mover.Points[Index + 1];
            bool Vertical = Math.Abs(Start.X - End.X) < 0.5;
            bool Horizontal = Math.Abs(Start.Y - End.Y) < 0.5;
            if (!Vertical && !Horizontal)
            {
                continue;
            }
            double TrackSeparation = 6;
            if (Contract != null)
            {
                var Options = FlowchartRenderOptions.FromContract(Contract);
                double CaptionHalf(FlowchartLayoutEdgeRoute Route)
                {
                    var Edge = Contract.Edges.FirstOrDefault(Edge => Edge.Id == Route.EdgeId);
                    if (string.IsNullOrWhiteSpace(Edge?.Label)) return 0;
                    var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label, Options, Options.EdgeLabelFontSizePx);
                    return (Vertical ? Size.Width : Size.Height) / 2;
                }
                TrackSeparation = Math.Max(6, CaptionHalf(Stay) + CaptionHalf(Mover) + 6);
            }

            for (int OtherIndex = 1; OtherIndex < Stay.Points.Count; OtherIndex++)
            {
                FlowchartLayoutPoint OtherStart = Stay.Points[OtherIndex - 1];
                FlowchartLayoutPoint OtherEnd = Stay.Points[OtherIndex];
                bool Parallel = Vertical
                    ? Math.Abs(OtherStart.X - OtherEnd.X) < 0.5
                    : Math.Abs(OtherStart.Y - OtherEnd.Y) < 0.5;
                double Gap = Vertical ? Start.X - OtherStart.X : Start.Y - OtherStart.Y;
                double Overlap = Vertical
                    ? Math.Min(Math.Max(Start.Y, End.Y), Math.Max(OtherStart.Y, OtherEnd.Y))
                        - Math.Max(Math.Min(Start.Y, End.Y), Math.Min(OtherStart.Y, OtherEnd.Y))
                    : Math.Min(Math.Max(Start.X, End.X), Math.Max(OtherStart.X, OtherEnd.X))
                        - Math.Max(Math.Min(Start.X, End.X), Math.Min(OtherStart.X, OtherEnd.X));
                if (!Parallel || Overlap <= 0 || Math.Abs(Gap) >= TrackSeparation)
                {
                    continue;
                }

                double Offset = (Gap < 0 ? -TrackSeparation : TrackSeparation) - Gap;
                var Obstacles = Contract == null ? Placed : Placed.Where(Node =>
                    !FlowchartCompoundEndpointHelper.Contains(Contract, Node.Id, Mover.SourceId)
                    && !FlowchartCompoundEndpointHelper.Contains(Contract, Node.Id, Mover.TargetId)).ToList();
                if (!TryTranslateInternalRun(Mover, Index, Vertical, Offset, Obstacles))
                {
                    TryTranslateInternalRun(Mover, Index, Vertical, -Offset, Obstacles);
                }
            }
        }
    }

    // A bend belongs to both adjoining runs. Moving just one point creates a
    // diagonal, so only translate a complete internal run between two elbows.
    internal static bool TryTranslateInternalRun(
        FlowchartLayoutEdgeRoute Route, int Index, bool Vertical, double Offset, List<GridNode> Placed)
    {
        if (Index < 1 || Index + 2 >= Route.Points.Count)
        {
            return false;
        }

        FlowchartLayoutPoint Before = Route.Points[Index - 1];
        FlowchartLayoutPoint Start = Route.Points[Index];
        FlowchartLayoutPoint End = Route.Points[Index + 1];
        FlowchartLayoutPoint After = Route.Points[Index + 2];
        if (Vertical
            ? Math.Abs(Start.X - End.X) > 0.5 || Math.Abs(Before.Y - Start.Y) > 0.5 || Math.Abs(End.Y - After.Y) > 0.5
            : Math.Abs(Start.Y - End.Y) > 0.5 || Math.Abs(Before.X - Start.X) > 0.5 || Math.Abs(End.X - After.X) > 0.5)
        {
            return false;
        }

        double OldStart = Vertical ? Start.X : Start.Y;
        double OldEnd = Vertical ? End.X : End.Y;
        if (Vertical)
        {
            Start.X += Offset;
            End.X += Offset;
        }
        else
        {
            Start.Y += Offset;
            End.Y += Offset;
        }

        bool Clear = true;
        for (int Segment = Index; Segment <= Index + 2 && Clear; Segment++)
        {
            foreach (GridNode Node in Placed)
            {
                if (Node.Id == Route.SourceId || Node.Id == Route.TargetId)
                {
                    continue;
                }

                FlowchartLayoutNodeBounds Bounds = new()
                {
                    CenterX = Node.X, CenterY = Node.Y, Width = Node.Width, Height = Node.Height
                };
                FlowchartLayoutPoint A = Route.Points[Segment - 1];
                FlowchartLayoutPoint B = Route.Points[Segment];
                if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(A.X, A.Y, B.X, B.Y, Bounds))
                {
                    Clear = false;
                    break;
                }
            }
        }

        if (!Clear)
        {
            if (Vertical) { Start.X = OldStart; End.X = OldEnd; }
            else { Start.Y = OldStart; End.Y = OldEnd; }
        }

        return Clear;
    }

    private static bool InsideAny(List<GridNode> Placed, double X, double Y)
    {
        foreach (GridNode Node in Placed)
        {
            if (X > Node.X - (Node.Width / 2.0) && X < Node.X + (Node.Width / 2.0)
                && Y > Node.Y - (Node.Height / 2.0) && Y < Node.Y + (Node.Height / 2.0))
            {
                return true;
            }
        }

        return false;
    }

    private static double DistanceToRoute(double X, double Y, FlowchartLayoutEdgeRoute Route)
    {
        double Best = double.MaxValue;
        for (int Index = 1; Index < Route.Points.Count; Index++)
        {
            Best = Math.Min(Best, DistanceToSegment(X, Y, Route.Points[Index - 1], Route.Points[Index]));
        }

        return Best;
    }

    private static double DistanceToSegment(double X, double Y, FlowchartLayoutPoint Start, FlowchartLayoutPoint End)
    {
        double Dx = End.X - Start.X;
        double Dy = End.Y - Start.Y;
        double LengthSquared = (Dx * Dx) + (Dy * Dy);
        if (LengthSquared < 1e-6)
        {
            double EndDx = X - Start.X;
            double EndDy = Y - Start.Y;
            return Math.Sqrt((EndDx * EndDx) + (EndDy * EndDy));
        }

        double T = (((X - Start.X) * Dx) + ((Y - Start.Y) * Dy)) / LengthSquared;
        T = Math.Clamp(T, 0.0, 1.0);
        double NearX = Start.X + (T * Dx);
        double NearY = Start.Y + (T * Dy);
        double GapX = X - NearX;
        double GapY = Y - NearY;
        return Math.Sqrt((GapX * GapX) + (GapY * GapY));
    }

    private static void PushPointsOutOfNodes(FlowchartLayoutResult Copy, List<GridNode> Placed)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            for (int Index = 1; Index < Route.Points.Count - 1; Index++)
            {
                FlowchartLayoutPoint Point = Route.Points[Index];
                foreach (GridNode Node in Placed)
                {
                    if (string.Equals(Node.Id, Route.SourceId, StringComparison.Ordinal)
                        || string.Equals(Node.Id, Route.TargetId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double Left = Node.X - (Node.Width / 2.0);
                    double Right = Node.X + (Node.Width / 2.0);
                    double Bottom = Node.Y - (Node.Height / 2.0);
                    double Top = Node.Y + (Node.Height / 2.0);
                    if (Point.X <= Left || Point.X >= Right || Point.Y <= Bottom || Point.Y >= Top)
                    {
                        continue;
                    }

                    for (int Run = Math.Max(1, Index - 1); Run <= Index && Run < Route.Points.Count - 2; Run++)
                    {
                        bool Vertical = Math.Abs(Route.Points[Run].X - Route.Points[Run + 1].X) < 0.5;
                        double Near = Vertical ? Left - STUB_PX - Point.X : Bottom - STUB_PX - Point.Y;
                        double Far = Vertical ? Right + STUB_PX - Point.X : Top + STUB_PX - Point.Y;
                        if (Math.Abs(Far) < Math.Abs(Near)) { (Near, Far) = (Far, Near); }
                        if (TryTranslateInternalRun(Route, Run, Vertical, Near, Placed)
                            || TryTranslateInternalRun(Route, Run, Vertical, Far, Placed))
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildObstacleBounds(
        List<GridNode> Placed,
        List<GridFrame> Frames)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            };
        }

        foreach (GridFrame Frame in Frames)
        {
            BoundsById[Frame.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            };
        }

        return BoundsById;
    }

    private static bool StraightChordIsClear(
        DiagramContract Contract,
        BoxRef Source,
        BoxRef Target,
        string ExitSide,
        string EntrySide,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds,
        string SourceId,
        string TargetId)
    {
        (double StartX, double StartY) = StraightAttachPoint(Source, ExitSide);
        (double EndX, double EndY) = StraightAttachPoint(Target, EntrySide);
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Entry in ObstacleBounds)
        {
            if (string.Equals(Entry.Key, SourceId, StringComparison.Ordinal)
                || string.Equals(Entry.Key, TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            bool IsGroupFrame = false;
            foreach (GroupContract Group in Contract.Groups)
            {
                if (string.Equals(Group.Id, Entry.Key, StringComparison.Ordinal))
                {
                    IsGroupFrame = true;
                    break;
                }
            }

            if (IsGroupFrame
                && (IsInsideGroup(Contract, SourceId, Entry.Key) || IsInsideGroup(Contract, TargetId, Entry.Key)))
            {
                continue;
            }

            if (FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    StartX,
                    StartY,
                    EndX,
                    EndY,
                    Entry.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static (double X, double Y) StraightAttachPoint(BoxRef Box, string Side)
    {
        return Side switch
        {
            "top" => (Box.CenterX, Box.Top),
            "bottom" => (Box.CenterX, Box.Bottom),
            "left" => (Box.Left, Box.CenterY),
            "right" => (Box.Right, Box.CenterY),
            _ => (Box.CenterX, Box.CenterY)
        };
    }

    private static bool TryAssignStraightPortSides(
        BoxRef Source,
        BoxRef Target,
        FlowchartDirection Direction,
        out string ExitSide,
        out string EntrySide)
    {
        ExitSide = string.Empty;
        EntrySide = string.Empty;
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            if (Math.Abs(Source.CenterY - Target.CenterY) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return false;
            }

            if (Source.CenterX > Target.CenterX)
            {
                ExitSide = "left";
                EntrySide = "right";
            }
            else if (Source.CenterX < Target.CenterX)
            {
                ExitSide = "right";
                EntrySide = "left";
            }
            else
            {
                return false;
            }

            return true;
        }

        if (Math.Abs(Source.CenterY - Target.CenterY) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (Source.CenterX < Target.CenterX)
            {
                ExitSide = "right";
                EntrySide = "left";
                return true;
            }

            if (Source.CenterX > Target.CenterX)
            {
                ExitSide = "left";
                EntrySide = "right";
                return true;
            }

            return false;
        }

        if (Math.Abs(Source.CenterX - Target.CenterX) > DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (Target.IsFrame)
            {
                if (Source.CenterX < Target.Left - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                    || Source.CenterX > Target.Right + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (Source.CenterY <= Target.CenterY)
        {
            return false;
        }

        ExitSide = "bottom";
        EntrySide = "top";
        return true;
    }

    private static string ChooseElbowExitSide(
        DiagramContract Contract,
        BoxRef Source,
        BoxRef Target,
        FlowchartDirection Direction,
        HashSet<string> ReservedCenterPorts,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds,
        string SourceId,
        string TargetId)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            return FacingSide(Source, Target);
        }

        bool BottomReserved = ReservedCenterPorts.Contains(Source.Id + "|bottom");
        bool BottomBusy = BottomReserved
            || BottomSideBusyWithNextRankChild(Contract, Source, ObstacleBounds);
        if (!BottomBusy
            && Target.CenterY < Source.CenterY
            && StraightChordIsClear(Contract, Source, Target, "bottom", "top", ObstacleBounds, SourceId, TargetId))
        {
            return "bottom";
        }

        if (BottomBusy && Target.CenterY < Source.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            if (Target.CenterX > Source.CenterX + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return "right";
            }

            if (Target.CenterX < Source.CenterX - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                return "left";
            }
        }

        if (Target.CenterX < Source.CenterX - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            return "left";
        }

        if (Target.CenterX > Source.CenterX + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
        {
            return "right";
        }

        return "left";
    }

    private static bool BottomSideBusyWithNextRankChild(
        DiagramContract Contract,
        BoxRef Source,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds)
    {
        double BottomBand = (Source.Width / 2.0) + DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!string.Equals(Edge.SourceId, Source.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!ObstacleBounds.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                continue;
            }

            if (TargetBounds.CenterY >= Source.CenterY - DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX)
            {
                continue;
            }

            if (Math.Abs(TargetBounds.CenterX - Source.CenterX) <= BottomBand)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildPlacedBounds(
        List<GridNode> Placed,
        List<GridFrame> Frames)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            };
        }

        foreach (GridFrame Frame in Frames)
        {
            BoundsById[Frame.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            };
        }

        return BoundsById;
    }

    private static void RebuildOrdinaryFanOutRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (!Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode)
                || FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape))
            {
                continue;
            }

            if (!FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract,
                    Route.SourceId,
                    SourceNode.Shape,
                    Direction,
                    BoundsById))
            {
                continue;
            }

            EdgeContract? Edge = null;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
                {
                    Edge = Candidate;
                    break;
                }
            }

            if (Edge == null)
            {
                continue;
            }

            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode)
                ? TargetNode.Shape
                : null;
            if (!FlowchartOrdinaryFanOutRouteBuilder.TryBuildEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceNode.Shape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            if (FlowchartOrdinaryFanOutRouteBuilder.TryGetFlowSides(Direction, out string ExitSide, out string EntrySide))
            {
                Route.ExitSide = ExitSide;
                Route.EntrySide = EntrySide;
            }

            // Keep the preallocated endpoints, including a centre port already
            // reserved by a straight continuation. The route builder supplies
            // the rank-gap track between those jointly selected ports.
            if (Route.Points.Count >= 2)
            {
                FlowchartLayoutPoint Start = Route.Points[0];
                FlowchartLayoutPoint End = Route.Points[^1];
                LayoutPoints = [(Start.X, Start.Y)];
                if (Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft)
                {
                    if (Math.Abs(Start.Y - End.Y) > 0.5)
                    {
                        double Track = (Start.X + End.X) / 2;
                        LayoutPoints.Add((Track, Start.Y));
                        LayoutPoints.Add((Track, End.Y));
                    }
                }
                else if (Math.Abs(Start.X - End.X) > 0.5)
                {
                    double Track = (Start.Y + End.Y) / 2;
                    LayoutPoints.Add((Start.X, Track));
                    LayoutPoints.Add((End.X, Track));
                }
                LayoutPoints.Add((End.X, End.Y));
            }

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static void RebuildAlignedSimpleFanRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        if (Direction is not (FlowchartDirection.TopToBottom or FlowchartDirection.BottomToTop)) return;
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = BuildPlacedBounds(Placed, Frames);
        bool Eligible(EdgeContract Edge) => string.IsNullOrWhiteSpace(Edge.Label)
            && Edge.RoutingHint?.IsActive != true
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.SourceId, Contract) == null
            && FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Edge.TargetId, Contract) == null;
        bool AlignedAndSymmetric(IReadOnlyList<EdgeContract> Edges, bool Outgoing)
        {
            if (Edges.Count != 3) return false;
            string HubId = Outgoing ? Edges[0].SourceId : Edges[0].TargetId;
            if (!Bounds.TryGetValue(HubId, out FlowchartLayoutNodeBounds Hub)) return false;
            List<FlowchartLayoutNodeBounds> Leaves = [];
            foreach (EdgeContract Edge in Edges)
            {
                string Id = Outgoing ? Edge.TargetId : Edge.SourceId;
                if (!Bounds.TryGetValue(Id, out FlowchartLayoutNodeBounds Leaf)) return false;
                Leaves.Add(Leaf);
            }
            return Leaves.All(Leaf => Math.Abs(Leaf.CenterY - Leaves[0].CenterY) < 0.5)
                && Math.Abs(Leaves.Min(Leaf => Leaf.CenterX) + Leaves.Max(Leaf => Leaf.CenterX)
                    - (2 * Hub.CenterX)) < 0.5
                && Math.Abs(Leaves.OrderBy(Leaf => Math.Abs(Leaf.CenterX - Hub.CenterX)).First().CenterX
                    - Hub.CenterX) < 0.5;
        }

        foreach (IGrouping<string, EdgeContract> Group in Contract.Edges.Where(Eligible).GroupBy(Edge => Edge.SourceId))
        {
            List<EdgeContract> Edges = Group.OrderBy(Edge => Edge.CreationIndex).ToList();
            if (!AlignedAndSymmetric(Edges, Outgoing: true)
                || !Nodes.TryGetValue(Group.Key, out NodeContract? Hub)
                || IsEnterpriseJunction(Hub)
                || !FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract, Group.Key, Hub.Shape, Direction, Bounds)) continue;
            foreach (EdgeContract Edge in Edges)
            {
                FlowchartLayoutEdgeRoute? Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Edge.SourceId
                    && Route.TargetId == Edge.TargetId);
                if (Route == null || !FlowchartOrdinaryFanOutRouteBuilder.TryBuildEdgeRoute(
                        Edge, Contract, Bounds, Hub.Shape,
                        Nodes.TryGetValue(Edge.TargetId, out NodeContract? Target) ? Target.Shape : null,
                        Direction, out List<(double X, double Y)> Points)) continue;
                Route.Points.Clear();
                foreach ((double X, double Y) in Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }

        foreach (IGrouping<string, EdgeContract> Group in Contract.Edges.Where(Eligible).GroupBy(Edge => Edge.TargetId))
        {
            List<EdgeContract> Edges = Group.OrderBy(Edge => Edge.CreationIndex).ToList();
            if (!AlignedAndSymmetric(Edges, Outgoing: false)
                || !Nodes.TryGetValue(Group.Key, out NodeContract? Hub)
                || IsEnterpriseJunction(Hub)
                || FlowchartSharedPortAllocator.IsDecisionShape(Hub.Shape)) continue;
            foreach (EdgeContract Edge in Edges)
            {
                FlowchartLayoutEdgeRoute? Route = Copy.EdgeRoutes.FirstOrDefault(Route => Route.SourceId == Edge.SourceId
                    && Route.TargetId == Edge.TargetId);
                if (Route == null || !FlowchartFanInPortAllocator.TryResolveEdgeRoute(
                        Edge, Contract, Bounds,
                        Nodes.TryGetValue(Edge.SourceId, out NodeContract? Source) ? Source.Shape : null,
                        Hub.Shape, Direction, out List<(double X, double Y)> Points)) continue;
                Route.Points.Clear();
                foreach ((double X, double Y) in Points)
                    Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(X), Y = Math.Round(Y) });
            }
        }
    }

    private static bool IsEnterpriseJunction(NodeContract Node) =>
        Node.Metadata.TryGetValue("enterpriseElementType", out MermaidMetadataValue? Metadata)
        && Metadata is RawMetadata Raw
        && string.Equals(Raw.RawValue, "Junction", StringComparison.Ordinal);

    /// <summary>
    /// B15: keeps the forward main chain in one column; back edges never pull nodes sideways.
    /// </summary>
    private static void AlignForwardMainChainColumn(
        DiagramContract Contract,
        List<GridNode> Placed,
        bool Horizontal)
    {
        if (Horizontal || Placed.Count == 0)
        {
            return;
        }

        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        HashSet<string> HasForwardIncoming = new HashSet<string>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || BackEdges.Contains(Edge))
            {
                continue;
            }

            HasForwardIncoming.Add(Edge.TargetId);
        }

        Dictionary<string, int> DeclaredOrder = Contract.Nodes
            .Select((Node, Index) => (Node.Id, Index))
            .ToDictionary(Item => Item.Id, Item => Item.Index, StringComparer.Ordinal);
        string? StartId = null;
        int LongestRun = -1;
        foreach (GridNode Node in Placed.Where(Node => !HasForwardIncoming.Contains(Node.Id))
            .OrderBy(Node => DeclaredOrder.TryGetValue(Node.Id, out int Order) ? Order : int.MaxValue))
        {
            int RunLength = MainRunLength(Node.Id);
            if (RunLength > LongestRun)
            {
                StartId = Node.Id;
                LongestRun = RunLength;
            }
        }

        if (StartId == null || !ById.TryGetValue(StartId, out GridNode? StartNode))
        {
            return;
        }

        HashSet<string> SpineIds = new HashSet<string>(StringComparer.Ordinal);
        string? CurrentId = StartId;
        while (CurrentId is not null && ById.TryGetValue(CurrentId, out GridNode? CurrentNode))
        {
            if (!SpineIds.Add(CurrentId))
            {
                break;
            }

            List<EdgeContract> ForwardOutgoing = [];
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    || BackEdges.Contains(Edge)
                    || !string.Equals(Edge.SourceId, CurrentId, StringComparison.Ordinal))
                {
                    continue;
                }

                ForwardOutgoing.Add(Edge);
            }

            if (ForwardOutgoing.Count == 0)
            {
                break;
            }

            ForwardOutgoing.Sort((Left, Right) => Left.CreationIndex.CompareTo(Right.CreationIndex));
            string NextId = ForwardOutgoing[0].TargetId;
            if (!ById.TryGetValue(NextId, out GridNode? NextNode) || NextNode.Rank <= CurrentNode.Rank)
            {
                break;
            }

            CurrentId = NextId;
        }

        int SpineColumn = StartNode.Column;
        foreach (string SpineId in SpineIds)
        {
            if (ById.TryGetValue(SpineId, out GridNode? SpineNode))
            {
                SpineNode.Column = SpineColumn;
            }
        }

        int MainRunLength(string RootId)
        {
            HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
            string? Current = RootId;
            while (Current is not null && ById.ContainsKey(Current) && Visited.Add(Current))
            {
                EdgeContract? Next = Contract.Edges
                    .Where(Edge => string.Equals(Edge.SourceId, Current, StringComparison.Ordinal)
                        && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                        && !BackEdges.Contains(Edge)
                        && ById.ContainsKey(Edge.TargetId))
                    .OrderBy(Edge => Edge.CreationIndex)
                    .FirstOrDefault();
                if (Next is null || !ById.TryGetValue(Next.TargetId, out GridNode? NextNode)
                    || NextNode.Rank <= ById[Current].Rank)
                {
                    break;
                }

                Current = Next.TargetId;
            }

            return Visited.Count;
        }
    }

    /// <summary>
    /// B15: dagre omits back edges from placement, so add placeholder routes before port assignment.
    /// </summary>
    private static void EnsureBackEdgeRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase) || !BackEdges.Contains(Edge))
            {
                continue;
            }

            bool Found = false;
            foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
            {
                if (string.Equals(Route.SourceId, Edge.SourceId, StringComparison.Ordinal)
                    && string.Equals(Route.TargetId, Edge.TargetId, StringComparison.Ordinal))
                {
                    Found = true;
                    break;
                }
            }

            if (Found
                || !ById.TryGetValue(Edge.SourceId, out GridNode? Source)
                || !ById.TryGetValue(Edge.TargetId, out GridNode? Target))
            {
                continue;
            }

            FlowchartLayoutEdgeRoute Placeholder = new FlowchartLayoutEdgeRoute
            {
                EdgeId = Edge.Id ?? string.Empty,
                SourceId = Edge.SourceId,
                TargetId = Edge.TargetId,
                ExitSide = "right",
                EntrySide = "right"
            };
            Placeholder.Points.Add(new FlowchartLayoutPoint { X = Math.Round(Source.X), Y = Math.Round(Source.Y) });
            Placeholder.Points.Add(new FlowchartLayoutPoint { X = Math.Round(Target.X), Y = Math.Round(Target.Y) });
            Copy.EdgeRoutesMutable.Add(Placeholder);
        }
    }

    private static void RebuildSoleForwardDecisionBranchRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            EdgeContract? Edge = FindContractEdge(Contract, Route);
            if (Edge == null || BackEdges.Contains(Edge))
            {
                continue;
            }

            if (!FlowchartLoopBackRouteHelper.TryResolveSoleForwardDecisionBranch(
                    Edge,
                    Contract,
                    BoundsById,
                    Nodes,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            if (BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                && BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                SetRouteEndpointSides(Route, SourceBounds, TargetBounds, LayoutPoints, HorizontalFlow);
            }

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static void RebuildLoopBackRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        bool HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            EdgeContract? Edge = FindContractEdge(Contract, Route);
            if (Edge == null || !BackEdges.Contains(Edge))
            {
                continue;
            }

            string? SourceShape = Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode) ? SourceNode.Shape : null;
            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null;
            if (!HorizontalFlow && Route.ExitSide is "left" or "right" && Route.EntrySide == Route.ExitSide
                && BoundsById.TryGetValue(Route.SourceId, out var AlignedSource)
                && BoundsById.TryGetValue(Route.TargetId, out var AlignedTarget)
                && Math.Abs(HorizontalFlow ? AlignedSource.CenterY - AlignedTarget.CenterY
                    : AlignedSource.CenterX - AlignedTarget.CenterX) <= DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX
                && Route.Points.Count is >= 4 and <= 5
                && Route.Points.Zip(Route.Points.Skip(1)).All(Pair =>
                    (Math.Abs(Pair.First.X - Pair.Second.X) < 0.5 || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.5)
                    && !FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                        Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y,
                        BoundsById, Route.SourceId, Route.TargetId)))
            {
                // Keep the side ports allocated with the other ends in the diagram.
                continue;
            }
            List<(double X, double Y)> LayoutPoints = [];
            bool Resolved = false;

            if (HorizontalFlow
                && BoundsById.TryGetValue(Route.SourceId, out var RetryBounds)
                && BoundsById.TryGetValue(Route.TargetId, out var InputBounds)
                && RetryBounds.CenterY < InputBounds.CenterY
                && Contract.Edges.Any(ParentEdge => ParentEdge.TargetId == Route.SourceId
                    && Nodes.TryGetValue(ParentEdge.SourceId, out var ParentNode)
                    && FlowchartDagrePresentation.IsDiamondShape(ParentNode.Shape)
                    && BoundsById.TryGetValue(ParentEdge.SourceId, out var ParentBounds)
                    && Math.Abs(ParentBounds.CenterX - RetryBounds.CenterX) < 0.5))
            {
                double StartY = RetryBounds.CenterY - RetryBounds.Height / 2
                    - FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerStart);
                double EndY = InputBounds.CenterY - InputBounds.Height / 2
                    - FlowchartEdgeAttachConstants.GetExteriorGapPx(Edge.MarkerEnd);
                double LaneY = BoundsById.Values.Min(Bounds => Bounds.CenterY - Bounds.Height / 2)
                    - 2 * DiagramGridMetrics.GRID_UNIT_PX;
                LayoutPoints = [(RetryBounds.CenterX, StartY), (RetryBounds.CenterX, LaneY),
                    (InputBounds.CenterX, LaneY), (InputBounds.CenterX, EndY)];
                Resolved = true;
            }

            if (!Resolved && HorizontalFlow
                && BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds LrSourceBounds)
                && BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds LrTargetBounds)
                && FlowchartEdgePathBuilder.IsLrRetryLoopEdge(
                    Edge,
                    Contract,
                    LrSourceBounds,
                    LrTargetBounds,
                    SourceShape,
                    TargetShape))
            {
                Resolved = FlowchartEdgePathBuilder.TryBuildNestedLrRetryLoopPath(
                        Edge,
                        Contract,
                        BoundsById,
                        Nodes,
                        Direction,
                        out LayoutPoints)
                    || FlowchartEdgePathBuilder.TryBuildLrRetryLoopPath(
                        LrSourceBounds,
                        LrTargetBounds,
                        BoundsById.Values.ToList(),
                        Direction,
                        out LayoutPoints,
                        Edge.MarkerStart,
                        Edge.MarkerEnd);
            }

            if (!Resolved
                && !FlowchartLoopBackRouteHelper.TryResolveDecisionLoopBackRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    Nodes,
                    Direction,
                    out LayoutPoints)
                && !FlowchartFanInPortAllocator.TryResolveLoopBackRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    TargetShape,
                    Direction,
                    out LayoutPoints))
            {
                continue;
            }

            if (LayoutPoints.Count < 2)
            {
                continue;
            }

            if (BoundsById.TryGetValue(Route.SourceId, out FlowchartLayoutNodeBounds SourceBounds)
                && BoundsById.TryGetValue(Route.TargetId, out FlowchartLayoutNodeBounds TargetBounds))
            {
                SetRouteEndpointSides(Route, SourceBounds, TargetBounds, LayoutPoints, HorizontalFlow);
            }

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static void SetRouteEndpointSides(
        FlowchartLayoutEdgeRoute Route,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        List<(double X, double Y)> LayoutPoints,
        bool HorizontalFlow)
    {
        if (LayoutPoints.Count == 0)
        {
            return;
        }

        (double StartX, double StartY) = LayoutPoints[0];
        (double EndX, double EndY) = LayoutPoints[^1];
        double SourceOffsetX = Math.Abs(StartX - SourceBounds.CenterX);
        double SourceOffsetY = Math.Abs(StartY - SourceBounds.CenterY);
        double TargetOffsetX = Math.Abs(EndX - TargetBounds.CenterX);
        double TargetOffsetY = Math.Abs(EndY - TargetBounds.CenterY);

        if (HorizontalFlow)
        {
            Route.ExitSide = SourceOffsetY > SourceOffsetX
                ? (StartY >= SourceBounds.CenterY ? "top" : "bottom")
                : (StartX >= SourceBounds.CenterX ? "right" : "left");
            Route.EntrySide = TargetOffsetY > TargetOffsetX
                ? (EndY >= TargetBounds.CenterY ? "top" : "bottom")
                : (EndX >= TargetBounds.CenterX ? "right" : "left");
            return;
        }

        Route.ExitSide = SourceOffsetX > SourceOffsetY
            ? (StartX >= SourceBounds.CenterX ? "right" : "left")
            : (StartY <= SourceBounds.CenterY ? "bottom" : "top");
        Route.EntrySide = TargetOffsetX > TargetOffsetY
            ? (EndX >= TargetBounds.CenterX ? "right" : "left")
            : (EndY <= TargetBounds.CenterY ? "bottom" : "top");
    }

    private static void RebuildDecisionFanOutRoutes(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildPlacedBounds(Placed, Frames);
        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (!Nodes.TryGetValue(Route.SourceId, out NodeContract? SourceNode)
                || !FlowchartDagrePresentation.IsDiamondShape(SourceNode.Shape))
            {
                continue;
            }

            int Outgoing = 0;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && !string.Equals(Candidate.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    && !BackEdges.Contains(Candidate))
                {
                    Outgoing++;
                }
            }

            if (Outgoing < 2)
            {
                continue;
            }

            EdgeContract? Edge = null;
            foreach (EdgeContract Candidate in Contract.Edges)
            {
                if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                    && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
                {
                    Edge = Candidate;
                    break;
                }
            }

            if (Edge == null || BackEdges.Contains(Edge))
            {
                continue;
            }

            string? TargetShape = Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode)
                ? TargetNode.Shape
                : null;
            FlowchartDecisionPortAllocator.TryResolveEdgeRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    SourceNode.Shape,
                    TargetShape,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    IncomingPorts: Copy.EdgeRoutes.Where(Inbound => Inbound.TargetId == Route.SourceId
                        && Inbound.Points.Count >= 2).Select(Inbound =>
                            (Inbound.Points[^1].X, Inbound.Points[^1].Y)).ToList());

            bool Horizontal = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
            var Source = BoundsById[Route.SourceId];
            var Target = BoundsById[Route.TargetId];
            double Main(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterX : Node.CenterY;
            double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
            double Side = Math.Sign(Cross(Target) - Cross(Source));
            double Flow = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.BottomToTop ? 1 : -1;
            double SourceCross = Cross(Source) + Side * (Horizontal ? Source.Height : Source.Width) / 2;
            double TargetMain = Main(Target) - Flow * (Horizontal ? Target.Width : Target.Height) / 2;
            (double X, double Y) Map(double Main, double Cross) => Horizontal ? (Main, Cross) : (Cross, Main);
            var Vertex = Map(Main(Source), SourceCross);
            bool Occupied = Copy.EdgeRoutes.Any(Inbound => Inbound.TargetId == Route.SourceId
                && Inbound.Points.Count >= 2 && Math.Abs(Inbound.Points[^1].X - Vertex.X) < 1
                && Math.Abs(Inbound.Points[^1].Y - Vertex.Y) < 1);
            int SameSide = Contract.Edges.Count(Candidate => Candidate.SourceId == Route.SourceId
                && !BackEdges.Contains(Candidate) && BoundsById.TryGetValue(Candidate.TargetId, out var Other)
                && Math.Sign(Cross(Other) - Cross(Source)) == Side);
            if (Side != 0 && !Occupied && SameSide == 1
                && !FlowchartDagrePresentation.IsDiamondShape(TargetShape)
                && Math.Abs(Cross(Target) - SourceCross) >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
                && (TargetMain - Main(Source)) * Flow >= 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX)
            {
                var Direct = new List<(double X, double Y)> { Vertex, Map(Main(Source), Cross(Target)), Map(TargetMain, Cross(Target)) };
                if (!Direct.Zip(Direct.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsUnrelatedNodeBodies(
                    Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, BoundsById.Where(Pair => !Frames.Any(Frame => Frame.Id == Pair.Key
                        && IsInsideGroup(Contract, Route.SourceId, Frame.Id) && IsInsideGroup(Contract, Route.TargetId, Frame.Id)))
                        .ToDictionary(Pair => Pair.Key, Pair => Pair.Value), Route.SourceId, Route.TargetId)))
                {
                    LayoutPoints = Direct;
                    SetRouteEndpointSides(Route, Source, Target, Direct, Horizontal);
                }
            }
            if (LayoutPoints.Count < 2) continue;

            Route.Points.Clear();
            foreach ((double PointX, double PointY) in LayoutPoints)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Math.Round(PointX),
                    Y = Math.Round(PointY)
                });
            }
        }
    }

    private static string ChooseElbowEntrySide(
        BoxRef Target,
        BoxRef Source,
        FlowchartDirection Direction,
        string ExitSide)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            return FacingSide(Target, Source);
        }

        if (ExitSide == "left" || ExitSide == "right")
        {
            return Source.CenterY > Target.CenterY ? "top" : "bottom";
        }

        return FacingSide(Target, Source);
    }

    private static string FacingSide(BoxRef From, BoxRef To)
    {
        double Dx = To.CenterX - From.CenterX;
        double Dy = To.CenterY - From.CenterY;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            return Dx >= 0 ? "right" : "left";
        }

        return Dy >= 0 ? "top" : "bottom";
    }

    private static bool TryBox(
        string Id,
        Dictionary<string, GridNode> ById,
        Dictionary<string, GridFrame> FrameById,
        out BoxRef Box)
    {
        if (ById.TryGetValue(Id, out GridNode? Node))
        {
            Box = BoxRef.FromNode(Node);
            return true;
        }

        if (FrameById.TryGetValue(Id, out GridFrame? Frame))
        {
            Box = BoxRef.FromFrame(Frame);
            return true;
        }

        Box = default;
        return false;
    }

    private static void WriteBack(FlowchartLayoutResult Copy, List<GridNode> Placed, List<GridFrame> Frames)
    {
        Copy.NodeBoundsMutable.Clear();
        foreach (GridNode Node in Placed)
        {
            Copy.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            });
        }

        Copy.ClusterBoundsMutable.Clear();
        double MaxX = 0.0;
        double MaxY = 0.0;
        foreach (GridFrame Frame in Frames)
        {
            Copy.ClusterBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom
            });
            MaxX = Math.Max(MaxX, Frame.Right);
            MaxY = Math.Max(MaxY, Frame.Top);
        }

        foreach (GridNode Node in Placed)
        {
            MaxX = Math.Max(MaxX, Node.X + (Node.Width / 2.0));
            MaxY = Math.Max(MaxY, Node.Y + (Node.Height / 2.0));
        }
    }

    private static List<GridNode> NodesInRank(List<GridNode> Placed, int Rank)
    {
        List<GridNode> RankNodes = new List<GridNode>();
        foreach (GridNode Node in Placed)
        {
            if (Node.Rank == Rank)
            {
                RankNodes.Add(Node);
            }
        }

        return RankNodes;
    }

    private static int MaxHalf(List<GridNode> Nodes, bool Horizontal, bool Below)
    {
        int Half = 0;
        foreach (GridNode Node in Nodes)
        {
            Half = Math.Max(Half, MainHalf(Node, Horizontal, Leading: !Below));
        }

        return Half;
    }

    private static int MaxCrossHalf(List<GridNode> Nodes, bool Horizontal, bool Before)
    {
        int Half = 0;
        foreach (GridNode Node in Nodes)
        {
            Half = Math.Max(Half, CrossHalf(Node, Horizontal, Leading: Before));
        }

        return Half;
    }

    private static int MainHalf(GridNode Node, bool Horizontal, bool Leading)
    {
        int Size = Horizontal ? Node.Width : Node.Height;
        return Size / 2;
    }

    private static int CrossHalf(GridNode Node, bool Horizontal, bool Leading)
    {
        int Size = Horizontal ? Node.Height : Node.Width;
        return Size / 2;
    }

    private static double Main(GridNode Node, bool Horizontal, bool Old)
    {
        if (Horizontal)
        {
            return Old ? Node.OldX : Node.X;
        }

        return Old ? Node.OldY : Node.Y;
    }

    private static double Cross(GridNode Node, bool Horizontal, bool Old)
    {
        if (Horizontal)
        {
            return Old ? Node.OldY : Node.Y;
        }

        return Old ? Node.OldX : Node.X;
    }

    private static void SetMain(GridNode Node, int Value, bool Horizontal)
    {
        if (Horizontal)
        {
            Node.X = Value;
        }
        else
        {
            Node.Y = Value;
        }
    }

    private static void SetCross(GridNode Node, int Value, bool Horizontal)
    {
        if (Horizontal)
        {
            Node.Y = Value;
        }
        else
        {
            Node.X = Value;
        }
    }

    private static string? GroupOf(DiagramContract Contract, string NodeId)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (string.Equals(Node.Id, NodeId, StringComparison.Ordinal))
            {
                return Node.ParentGroupId;
            }
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (string.Equals(ChildId, NodeId, StringComparison.Ordinal))
                {
                    return Group.Id;
                }
            }
        }

        return null;
    }

    internal static bool IsInsideGroup(DiagramContract Contract, string NodeId, string GroupId)
    {
        string? Current = FlowchartSubgraphMembershipHelper.FindInnermostGroupId(NodeId, Contract);
        int Guard = 0;
        while (!string.IsNullOrEmpty(Current) && Guard < 32)
        {
            if (string.Equals(Current, GroupId, StringComparison.Ordinal))
            {
                return true;
            }

            Current = ParentGroup(Contract, Current);
            Guard++;
        }

        return false;
    }

    private static string? ParentGroup(DiagramContract Contract, string GroupId)
    {
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, GroupId, StringComparison.Ordinal))
            {
                return Group.ParentGroupId;
            }
        }

        return null;
    }

    private static int DepthOf(DiagramContract Contract, string GroupId)
    {
        int Depth = 0;
        string? Parent = ParentGroup(Contract, GroupId);
        while (!string.IsNullOrEmpty(Parent))
        {
            Depth++;
            Parent = ParentGroup(Contract, Parent);
        }

        return Depth;
    }

    private static Dictionary<string, GridNode> IndexPlaced(List<GridNode> Placed)
    {
        Dictionary<string, GridNode> ById = new Dictionary<string, GridNode>(StringComparer.Ordinal);
        foreach (GridNode Node in Placed)
        {
            ById[Node.Id] = Node;
        }

        return ById;
    }

    private static Dictionary<string, NodeContract> IndexNodes(DiagramContract Contract)
    {
        Dictionary<string, NodeContract> Nodes = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            Nodes[Node.Id] = Node;
        }

        return Nodes;
    }

    private static FlowchartLayoutResult CopyResult(FlowchartLayoutResult Source)
    {
        FlowchartLayoutResult Copy = new FlowchartLayoutResult
        {
            GraphWidth = Source.GraphWidth,
            GraphHeight = Source.GraphHeight,
            RenderContext = Source.RenderContext
        };
        foreach (FlowchartLayoutNodeBounds Bounds in Source.NodeBounds)
        {
            Copy.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            });
        }

        foreach (FlowchartLayoutNodeBounds Bounds in Source.ClusterBounds)
        {
            Copy.ClusterBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Bounds.NodeId,
                CenterX = Bounds.CenterX,
                CenterY = Bounds.CenterY,
                Width = Bounds.Width,
                Height = Bounds.Height
            });
        }

        foreach (FlowchartLayoutEdgeRoute Route in Source.EdgeRoutes)
        {
            FlowchartLayoutEdgeRoute Clone = new FlowchartLayoutEdgeRoute
            {
                EdgeId = Route.EdgeId,
                SourceId = Route.SourceId,
                TargetId = Route.TargetId,
                ExitSide = Route.ExitSide,
                EntrySide = Route.EntrySide
            };
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Clone.Points.Add(new FlowchartLayoutPoint
                {
                    X = Point.X,
                    Y = Point.Y
                });
            }

            Copy.EdgeRoutesMutable.Add(Clone);
        }

        return Copy;
    }

    internal sealed class GridNode
    {
        public string Id = string.Empty;

        public string? Shape;

        public string? GroupId;

        public double OldX;

        public double OldY;

        public double X;

        public double Y;

        public int Width;

        public int Height;

        public int Rank;

        public int Column;

        public bool IsDiamond;
    }

    private sealed class ColumnBand
    {
        public readonly List<GridNode> Nodes = new List<GridNode>();

        public double Anchor;

        public int Index;
    }

    internal sealed class GridFrame
    {
        public string Id = string.Empty;

        public int Left;

        public int Top;

        public int Right;

        public int Bottom;

        public int TitleStrip;

        public int Padding;

        public double CenterX()
        {
            return (Left + Right) / 2.0;
        }

        public double CenterY()
        {
            return (Top + Bottom) / 2.0;
        }

        public double CenterMain(bool Horizontal)
        {
            if (Horizontal)
            {
                return CenterX();
            }

            return (Top + Bottom) / 2.0;
        }
    }

    private struct BoxRef
    {
        public string Id;

        public double CenterX;

        public double CenterY;

        public double Width;

        public double Height;

        public double Left;

        public double Right;

        public double Top;

        public double Bottom;

        public bool IsFrame;

        public static BoxRef FromNode(GridNode Node)
        {
            BoxRef Box = new BoxRef
            {
                Id = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height,
                IsFrame = false
            };
            Box.Left = Node.X - (Node.Width / 2.0);
            Box.Right = Node.X + (Node.Width / 2.0);
            Box.Bottom = Node.Y - (Node.Height / 2.0);
            Box.Top = Node.Y + (Node.Height / 2.0);
            return Box;
        }

        public static BoxRef FromFrame(GridFrame Frame)
        {
            return new BoxRef
            {
                Id = Frame.Id,
                CenterX = Frame.CenterX(),
                CenterY = Frame.CenterY(),
                Width = Frame.Right - Frame.Left,
                Height = Frame.Top - Frame.Bottom,
                Left = Frame.Left,
                Right = Frame.Right,
                Top = Frame.Top,
                Bottom = Frame.Bottom,
                IsFrame = true
            };
        }
    }

    /// <summary>
    /// A8: the clear span between ranks is uniform; label boosts apply only to their own pair.
    /// </summary>
    private static List<RankPairGapPlan> BuildRankPairGapPlans(
        List<GridNode> Placed,
        int RankCount,
        bool Horizontal,
        int MainPitch,
        FlowchartLayoutResult Copy,
        DiagramContract Contract)
    {
        List<RankPairGapPlan> Plans = [];
        for (int Rank = 0; Rank + 1 < RankCount; Rank++)
        {
            List<GridNode> RankNodes = NodesInRank(Placed, Rank);
            List<GridNode> NextNodes = NodesInRank(Placed, Rank + 1);
            int HalfBelow = MaxHalf(RankNodes, Horizontal, Below: true);
            int HalfAbove = MaxHalf(NextNodes, Horizontal, Below: false);
            int Gap = Math.Max(2 * DiagramGridMetrics.GRID_UNIT_PX,
                DiagramGridMetrics.CeilToGrid(FlowchartRenderOptions.FromContract(Contract).RankSpacingPx));
            foreach (EdgeContract Edge in Contract.Edges)
            {
                GridNode? Source = RankNodes.Find(Node => Node.Id == Edge.SourceId || Node.Id == Edge.TargetId);
                GridNode? Target = NextNodes.Find(Node => Node.Id == Edge.TargetId || Node.Id == Edge.SourceId);
                if (Source != null && Target != null && Source.Column != Target.Column
                    && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
                {
                    // Two rounded turns need a source run, two radii and an
                    // arrow-length target run. Grow the gap in whole units.
                    Gap = Math.Max(Gap, DiagramGridMetrics.CeilToGrid(
                        (3 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) + 10));
                }
            }
            int Crossings = CountRankCrossings(Copy, RankNodes, NextNodes);
            int TrackUnits = Math.Max(1, (Crossings + DiagramGridMetrics.TRACKS_PER_GAP - 1) / DiagramGridMetrics.TRACKS_PER_GAP);
            Gap = Math.Max(Gap, TrackUnits * DiagramGridMetrics.GRID_UNIT_PX);
            double MermaidDelta = MermaidMain(NextNodes, Horizontal) - MermaidMain(RankNodes, Horizontal);
            bool HasExplicitLength = Contract.Edges.Any(Edge => Edge.MinLength > 1
                && RankNodes.Any(Node => Node.Id == Edge.SourceId || Node.Id == Edge.TargetId)
                && NextNodes.Any(Node => Node.Id == Edge.TargetId || Node.Id == Edge.SourceId));
            if (HasExplicitLength && MermaidDelta > MainPitch)
            {
                int MermaidSpan = DiagramGridMetrics.CeilToGrid(MermaidDelta);
                int MermaidGap = MermaidSpan - HalfBelow - HalfAbove;
                Gap = Math.Max(Gap, MermaidGap);
            }

            int LabelGapBoost = ComputeLabelledEdgeRankGapBoost(Contract, Rank, RankNodes, NextNodes, Horizontal);
            int Span = HalfBelow + HalfAbove + Gap;
            if (Span < MainPitch)
            {
                Gap = Math.Max(Gap, MainPitch - HalfBelow - HalfAbove);
                Span = MainPitch;
            }

            Plans.Add(new RankPairGapPlan
            {
                HalfBelow = HalfBelow,
                HalfAbove = HalfAbove,
                StructuralClearGap = Span - HalfBelow - HalfAbove,
                LabelClearGapBoost = Math.Max(0, LabelGapBoost - Gap)
            });
        }

        return Plans;
    }

    private static int ComputeLabelledEdgeRankGapBoost(
        DiagramContract Contract,
        int Rank,
        IReadOnlyList<GridNode> RankNodes,
        IReadOnlyList<GridNode> NextNodes,
        bool Horizontal)
    {
        if (RankNodes.Count == 0 || NextNodes.Count == 0)
        {
            return 0;
        }

        HashSet<string> RankIds = new HashSet<string>(RankNodes.Select(Node => Node.Id), StringComparer.Ordinal);
        HashSet<string> NextIds = new HashSet<string>(NextNodes.Select(Node => Node.Id), StringComparer.Ordinal);
        int MaxBoost = 0;
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.IsNullOrWhiteSpace(Edge.Label)
                || string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!(RankIds.Contains(Edge.SourceId) && NextIds.Contains(Edge.TargetId))
                && !(RankIds.Contains(Edge.TargetId) && NextIds.Contains(Edge.SourceId)))
            {
                continue;
            }

            FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                Edge.Label,
                Options,
                Options.EdgeLabelFontSizePx);
            int Required = DiagramGridMetrics.CeilToGrid(
                FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    LabelDimensions,
                    Options.EdgeLabelFontSizePx,
                    Horizontal) + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            MaxBoost = Math.Max(MaxBoost, Required);
        }

        return MaxBoost;
    }

    private sealed class RankPairGapPlan
    {
        public int HalfBelow;

        public int HalfAbove;

        /// <summary>
        /// Clear gap this pair needs before any diagram-wide uniform floor is applied.
        /// </summary>
        public int StructuralClearGap;

        /// <summary>
        /// Extra clear gap for labels on edges between these ranks only.
        /// </summary>
        public int LabelClearGapBoost;
    }

    private sealed class PortEnd
    {
        public FlowchartLayoutEdgeRoute Route = null!;

        public bool IsSource;

        public bool KeepsCentre;

        public string Side = string.Empty;

        public BoxRef Box;

        public BoxRef Other;

        public int OutX;

        public int OutY;
    }
}
