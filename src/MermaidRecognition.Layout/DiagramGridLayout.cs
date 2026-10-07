using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Tidies a Mermaid dagre placement onto the shared grid.
/// Rank order, group membership, and the side each line uses stay as Mermaid decided them.
/// </summary>
public static class DiagramGridLayout
{
    private const int STUB_PX = 15;

    private const int MAX_SEPARATION_PASSES = 32;

    /// <summary>
    /// Returns a Y-up copy of <paramref name="LaidOut"/> with shared box sizes, grid positions,
    /// group frames that include a title strip, and smooth routes from the new ports.
    /// </summary>
    /// <param name="Contract">The diagram contract.</param>
    /// <param name="LaidOut">Mermaid dagre placement in Y-down coordinates.</param>
    /// <returns>The gridded layout.</returns>
    public static FlowchartLayoutResult Apply(DiagramContract Contract, FlowchartLayoutResult LaidOut)
    {
        FlowchartLayoutResult Copy = CopyResult(LaidOut);
        if (Contract == null || Copy.NodeBounds.Count == 0)
        {
            return Copy;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        (int BoxWidth, int BoxHeight) = ResolveStandardBox(Contract);
        int DiamondHeight = BoxHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR;
        int MainPitch = Horizontal
            ? BoxWidth + Math.Max(DiagramGridMetrics.NODE_GAP_PX, DiagramGridMetrics.MIN_LR_RANK_GAP_PX)
            : DiagramGridMetrics.PitchY(BoxHeight);
        int CrossPitch = Horizontal ? DiagramGridMetrics.PitchY(BoxHeight) : DiagramGridMetrics.PitchX(BoxWidth);

        Dictionary<string, NodeContract> Nodes = IndexNodes(Contract);
        List<GridNode> Placed = new List<GridNode>();
        foreach (FlowchartLayoutNodeBounds Bounds in Copy.NodeBounds)
        {
            bool Diamond = Nodes.TryGetValue(Bounds.NodeId, out NodeContract? Node)
                && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
            GridNode Item = new GridNode
            {
                Id = Bounds.NodeId,
                Shape = Nodes.TryGetValue(Bounds.NodeId, out NodeContract? ShapeNode) ? ShapeNode.Shape : null,
                GroupId = GroupOf(Contract, Bounds.NodeId),
                OldX = Bounds.CenterX,
                OldY = Bounds.CenterY,
                Width = Diamond ? BoxWidth : BoxWidth,
                Height = Diamond ? DiamondHeight : BoxHeight,
                IsDiamond = Diamond
            };
            Placed.Add(Item);
        }

        AssignRanks(Placed, Horizontal);
        AssignColumns(Placed, Horizontal, CrossPitch);
        ResolveVerticalChordBlockers(Contract, Placed);
        AlignForwardMainChainColumn(Contract, Placed, Horizontal);
        AlignDecisionCentreChildColumn(Contract, Placed, Horizontal);
        CascadeColumns(Placed);
        PlaceOnGrid(Placed, Horizontal, BoxWidth, BoxHeight, MainPitch, CrossPitch, Copy, Contract);
        GrowCrowdedSides(Placed, Copy, BoxWidth, BoxHeight);
        SeparateOverlaps(Placed, Horizontal);
        List<GridFrame> Frames = new List<GridFrame>();
        CenterLoneNestedGroups(Contract, Placed, Horizontal);
        FitFrames(Contract, Placed, Frames, Horizontal);
        FlipToYUp(Placed, Frames, Copy);
        EnsureBackEdgeRoutes(Contract, Copy, Placed);
        AssignPortSides(Contract, Copy, Placed, Frames, Nodes, Direction);
        DiagramGridRouteBuilder.RebuildAllRoutes(Copy, Placed, Nodes, Contract, Frames, Direction);
        RebuildDecisionFanOutRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
        RebuildSoleForwardDecisionBranchRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
        RebuildOrdinaryFanOutRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
        RebuildLoopBackRoutes(Contract, Copy, Placed, Frames, Nodes, Direction);
        Copy.UsesGridRoutes = true;
        SeparateTracks(Copy, Placed);
        PushPointsOutOfNodes(Copy, Placed);
        WriteBack(Copy, Placed, Frames);
        return Copy;
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
        int UniformClearGap = ResolveUniformRankClearGap(GapPlans);

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
            int ClearGap = Math.Max(UniformClearGap, Plan.StructuralClearGap) + Plan.LabelClearGapBoost;
            MainCursor += Plan.HalfBelow + Plan.HalfAbove + ClearGap;
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
                int Span = Math.Max(PitchSpan, HalfBefore + HalfAfter + DiagramGridMetrics.NODE_GAP_PX);
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

    private static void GrowCrowdedSides(List<GridNode> Placed, FlowchartLayoutResult Copy, int BoxWidth, int BoxHeight)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            AddSideCount(Counts, Route.SourceId, Route.ExitSide);
            AddSideCount(Counts, Route.TargetId, Route.EntrySide);
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

    private static void CenterLoneNestedGroups(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        foreach (GroupContract Parent in Contract.Groups)
        {
            if (Parent.ChildGroupIds.Count != 1)
            {
                continue;
            }

            string ChildGroupId = Parent.ChildGroupIds[0];
            List<GridNode> Members = new List<GridNode>();
            foreach (GridNode Node in Placed)
            {
                if (IsInsideGroup(Contract, Node.Id, ChildGroupId))
                {
                    Members.Add(Node);
                }
            }

            if (Members.Count == 0)
            {
                continue;
            }

            double ContentLeft = double.PositiveInfinity;
            double ContentRight = double.NegativeInfinity;
            foreach (GridNode Node in Members)
            {
                ContentLeft = Math.Min(ContentLeft, Node.X - (Node.Width / 2.0));
                ContentRight = Math.Max(ContentRight, Node.X + (Node.Width / 2.0));
            }

            List<GridNode> ParentRank = new List<GridNode>();
            foreach (GridNode Node in Placed)
            {
                if (string.Equals(GroupOf(Contract, Node.Id), Parent.Id, StringComparison.Ordinal)
                    && !IsInsideGroup(Contract, Node.Id, ChildGroupId))
                {
                    ParentRank.Add(Node);
                }
            }

            double ParentLeft = double.PositiveInfinity;
            double ParentRight = double.NegativeInfinity;
            foreach (GridNode Node in ParentRank)
            {
                ParentLeft = Math.Min(ParentLeft, Node.X - (Node.Width / 2.0));
                ParentRight = Math.Max(ParentRight, Node.X + (Node.Width / 2.0));
            }

            if (ParentLeft > ContentRight || ParentRight < ContentLeft)
            {
                continue;
            }

            double ContentCentre = (ContentLeft + ContentRight) / 2.0;
            double ParentCentre = (ParentLeft + ParentRight) / 2.0;
            int Shift = (int)Math.Round(ParentCentre - ContentCentre);
            if (Shift == 0)
            {
                continue;
            }

            foreach (GridNode Node in Members)
            {
                SetCross(Node, (int)Math.Round(Cross(Node, Horizontal, Old: false)) + Shift, Horizontal);
            }
        }
    }

    private static void FitFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames, bool Horizontal)
    {
        for (int Pass = 0; Pass < MAX_SEPARATION_PASSES; Pass++)
        {
            Frames.Clear();
            BuildFrames(Contract, Placed, Frames);
            bool Moved = false;
            foreach (GridFrame Frame in Frames)
            {
                foreach (GridNode Node in Placed)
                {
                    if (IsInsideGroup(Contract, Node.Id, Frame.Id))
                    {
                        continue;
                    }

                    if (!Overlaps(Node, Frame))
                    {
                        continue;
                    }

                    int Push = DiagramGridMetrics.GRID_UNIT_PX;
                    if (Main(Node, Horizontal, Old: false) >= Frame.CenterMain(Horizontal))
                    {
                        foreach (GridNode Later in Placed)
                        {
                            if (Later.Rank >= Node.Rank)
                            {
                                SetMain(Later, (int)Math.Round(Main(Later, Horizontal, Old: false)) + Push, Horizontal);
                            }
                        }
                    }
                    else
                    {
                        foreach (GridNode Member in Placed)
                        {
                            if (IsInsideGroup(Contract, Member.Id, Frame.Id) || Member.Rank > Node.Rank)
                            {
                                SetMain(Member, (int)Math.Round(Main(Member, Horizontal, Old: false)) + Push, Horizontal);
                            }
                        }
                    }

                    Moved = true;
                    break;
                }

                if (Moved)
                {
                    break;
                }
            }

            if (!Moved)
            {
                break;
            }
        }
    }

    private static void BuildFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<GroupContract> Ordered = new List<GroupContract>(Contract.Groups);
        Ordered.Sort(delegate(GroupContract Left, GroupContract Right)
        {
            return DepthOf(Contract, Right.Id).CompareTo(DepthOf(Contract, Left.Id));
        });

        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GroupContract Group in Ordered)
        {
            double Left = double.PositiveInfinity;
            double Right = double.NegativeInfinity;
            double Top = double.PositiveInfinity;
            double Bottom = double.NegativeInfinity;
            bool Any = false;
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (ById.TryGetValue(ChildId, out GridNode? Child))
                {
                    IncludeNode(Child, ref Left, ref Right, ref Top, ref Bottom, ref Any);
                }
            }

            foreach (NodeContract Node in Contract.Nodes)
            {
                if (string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal)
                    && ById.TryGetValue(Node.Id, out GridNode? Child))
                {
                    IncludeNode(Child, ref Left, ref Right, ref Top, ref Bottom, ref Any);
                }
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (FrameById.TryGetValue(ChildGroupId, out GridFrame? ChildFrame))
                {
                    IncludeRect(ChildFrame.Left, ChildFrame.Top, ChildFrame.Right, ChildFrame.Bottom, ref Left, ref Right, ref Top, ref Bottom, ref Any);
                }
            }

            if (!Any)
            {
                continue;
            }

            string Title = string.IsNullOrWhiteSpace(Group.Title) ? Group.Id : Group.Title;
            double TitleWidth = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Title, Options.FontSizePx);
            int Strip = DiagramGridMetrics.GRID_UNIT_PX;
            double LineHeight = Options.FontSizePx * DiagramGridMetrics.TEXT_LINE_HEIGHT_EM;
            if (LineHeight > Strip)
            {
                Strip = DiagramGridMetrics.CeilToGrid(LineHeight);
            }
            int Padding = DiagramGridMetrics.NODE_GAP_PX;
            int FrameLeft = (int)Math.Floor(Left) - Padding;
            int FrameRight = (int)Math.Ceiling(Right) + Padding;
            int FrameTop = (int)Math.Floor(Top) - Padding - Strip;
            int FrameBottom = (int)Math.Ceiling(Bottom) + Padding;
            int NeededWidth = DiagramGridMetrics.CeilToGrid(TitleWidth + (2.0 * Padding));
            int HaveWidth = FrameRight - FrameLeft;
            if (NeededWidth > HaveWidth)
            {
                int Extra = NeededWidth - HaveWidth;
                FrameLeft -= Extra / 2;
                FrameRight += Extra - (Extra / 2);
            }

            GridFrame Frame = new GridFrame
            {
                Id = Group.Id,
                Left = FrameLeft,
                Top = FrameTop,
                Right = FrameRight,
                Bottom = FrameBottom,
                TitleStrip = Strip,
                Padding = Padding
            };
            Frames.Add(Frame);
            FrameById[Group.Id] = Frame;
        }
    }

    private static void IncludeNode(
        GridNode Child,
        ref double Left,
        ref double Right,
        ref double Top,
        ref double Bottom,
        ref bool Any)
    {
        IncludeRect(
            Child.X - (Child.Width / 2.0),
            Child.Y - (Child.Height / 2.0),
            Child.X + (Child.Width / 2.0),
            Child.Y + (Child.Height / 2.0),
            ref Left,
            ref Right,
            ref Top,
            ref Bottom,
            ref Any);
    }

    private static void IncludeRect(
        double RectLeft,
        double RectTop,
        double RectRight,
        double RectBottom,
        ref double Left,
        ref double Right,
        ref double Top,
        ref double Bottom,
        ref bool Any)
    {
        if (!Any)
        {
            Any = true;
            Left = RectLeft;
            Right = RectRight;
            Top = RectTop;
            Bottom = RectBottom;
            return;
        }

        Left = Math.Min(Left, RectLeft);
        Right = Math.Max(Right, RectRight);
        Top = Math.Min(Top, RectTop);
        Bottom = Math.Max(Bottom, RectBottom);
    }

    private static bool Overlaps(GridNode Node, GridFrame Frame)
    {
        double Left = Node.X - (Node.Width / 2.0);
        double Right = Node.X + (Node.Width / 2.0);
        double Top = Node.Y - (Node.Height / 2.0);
        double Bottom = Node.Y + (Node.Height / 2.0);
        return Right > Frame.Left && Left < Frame.Right && Bottom > Frame.Top && Top < Frame.Bottom;
    }

    private static void RewriteRoutes(FlowchartLayoutResult Copy, List<GridNode> Placed, List<GridFrame> Frames)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GridFrame Frame in Frames)
        {
            FrameById[Frame.Id] = Frame;
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            bool HasSource = TryCentre(Route.SourceId, ById, FrameById, out double OldSourceX, out double OldSourceY, out double NewSourceX, out double NewSourceY);
            bool HasTarget = TryCentre(Route.TargetId, ById, FrameById, out double OldTargetX, out double OldTargetY, out double NewTargetX, out double NewTargetY);
            if (!HasSource || !HasTarget)
            {
                continue;
            }

            double SourceDeltaX = NewSourceX - OldSourceX;
            double SourceDeltaY = NewSourceY - OldSourceY;
            double TargetDeltaX = NewTargetX - OldTargetX;
            double TargetDeltaY = NewTargetY - OldTargetY;
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                double SourceDistance = Math.Abs(Point.X - OldSourceX) + Math.Abs(Point.Y - OldSourceY);
                double TargetDistance = Math.Abs(Point.X - OldTargetX) + Math.Abs(Point.Y - OldTargetY);
                double Denominator = SourceDistance + TargetDistance;
                double TowardTarget = Denominator < 1e-6 ? 0.5 : TargetDistance / Denominator;
                Point.X = Math.Round(Point.X + (TowardTarget * SourceDeltaX) + ((1.0 - TowardTarget) * TargetDeltaX));
                Point.Y = Math.Round(Point.Y + (TowardTarget * SourceDeltaY) + ((1.0 - TowardTarget) * TargetDeltaY));
            }
        }
    }

    private static bool TryCentre(
        string Id,
        Dictionary<string, GridNode> ById,
        Dictionary<string, GridFrame> FrameById,
        out double OldX,
        out double OldY,
        out double NewX,
        out double NewY)
    {
        if (ById.TryGetValue(Id, out GridNode? Node))
        {
            OldX = Node.OldX;
            OldY = Node.OldY;
            NewX = Node.X;
            NewY = Node.Y;
            return true;
        }

        if (FrameById.TryGetValue(Id, out GridFrame? Frame))
        {
            OldX = Frame.CenterX();
            OldY = Frame.CenterY();
            NewX = OldX;
            NewY = OldY;
            return true;
        }

        OldX = 0;
        OldY = 0;
        NewX = 0;
        NewY = 0;
        return false;
    }

    private static void FlipToYUp(List<GridNode> Placed, List<GridFrame> Frames, FlowchartLayoutResult Copy)
    {
        double MaxY = 0.0;
        foreach (GridNode Node in Placed)
        {
            MaxY = Math.Max(MaxY, Node.Y + (Node.Height / 2.0));
        }

        foreach (GridFrame Frame in Frames)
        {
            MaxY = Math.Max(MaxY, Frame.Bottom);
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                MaxY = Math.Max(MaxY, Point.Y);
            }
        }

        int Flip = DiagramGridMetrics.CeilToGrid(MaxY);
        if (Flip < MaxY)
        {
            Flip += DiagramGridMetrics.GRID_UNIT_PX;
        }

        foreach (GridNode Node in Placed)
        {
            Node.Y = Flip - Node.Y;
            Node.OldY = Flip - Node.OldY;
        }

        foreach (GridFrame Frame in Frames)
        {
            int OldTop = Frame.Top;
            int OldBottom = Frame.Bottom;
            Frame.Top = Flip - OldTop;
            Frame.Bottom = Flip - OldBottom;
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Point.Y = Flip - Point.Y;
            }
        }
    }

    private static void AssignPortSides(
        DiagramContract Contract,
        FlowchartLayoutResult Copy,
        List<GridNode> Placed,
        List<GridFrame> Frames,
        Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GridFrame Frame in Frames)
        {
            FrameById[Frame.Id] = Frame;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> ObstacleBounds = BuildObstacleBounds(Placed, Frames);
        HashSet<string> ReservedCenterPorts = new HashSet<string>(StringComparer.Ordinal);
        List<PortEnd> Ends = new List<PortEnd>();
        List<(FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target)> Pending = [];
        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (!TryBox(Route.SourceId, ById, FrameById, out BoxRef Source) || !TryBox(Route.TargetId, ById, FrameById, out BoxRef Target))
            {
                continue;
            }

            if (TryAssignStraightPortSides(Source, Target, Direction, out string StraightExit, out string StraightEntry)
                && StraightChordIsClear(
                    Contract,
                    Source,
                    Target,
                    StraightExit,
                    StraightEntry,
                    ObstacleBounds,
                    Route.SourceId,
                    Route.TargetId))
            {
                Route.ExitSide = StraightExit;
                Route.EntrySide = StraightEntry;
                ReservedCenterPorts.Add(Source.Id + "|" + StraightExit);
                ReservedCenterPorts.Add(Target.Id + "|" + StraightEntry);
                Ends.Add(new PortEnd
                {
                    Route = Route,
                    IsSource = true,
                    Side = StraightExit,
                    Box = Source,
                    Other = Target
                });
                Ends.Add(new PortEnd
                {
                    Route = Route,
                    IsSource = false,
                    Side = StraightEntry,
                    Box = Target,
                    Other = Source
                });
                continue;
            }

            Pending.Add((Route, Source, Target));
        }

        Dictionary<string, FlowchartLayoutNodeBounds> FanOutBoundsById = BuildPlacedBounds(Placed, Frames);
        foreach ((FlowchartLayoutEdgeRoute Route, BoxRef Source, BoxRef Target) Item in Pending)
        {
            string Exit;
            string Entry;
            if (FlowchartOrdinaryFanOutRouteBuilder.TryGetFlowSides(Direction, out string FanOutExit, out string FanOutEntry)
                && Nodes.TryGetValue(Item.Source.Id, out NodeContract? SourceNode)
                && FlowchartOrdinaryFanOutRouteBuilder.TryIsOrdinaryFanOutHub(
                    Contract,
                    Item.Source.Id,
                    SourceNode.Shape,
                    Direction,
                    FanOutBoundsById))
            {
                Exit = FanOutExit;
                Entry = FanOutEntry;
            }
            else
            {
                Exit = string.IsNullOrEmpty(Item.Route.ExitSide)
                    ? ChooseElbowExitSide(
                        Contract,
                        Item.Source,
                        Item.Target,
                        Direction,
                        ReservedCenterPorts,
                        ObstacleBounds,
                        Item.Route.SourceId,
                        Item.Route.TargetId)
                    : Item.Route.ExitSide;
                Entry = string.IsNullOrEmpty(Item.Route.EntrySide)
                    ? ChooseElbowEntrySide(Item.Target, Item.Source, Direction, Exit)
                    : Item.Route.EntrySide;
            }
            Item.Route.ExitSide = Exit;
            Item.Route.EntrySide = Entry;
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = true,
                Side = Exit,
                Box = Item.Source,
                Other = Item.Target
            });
            Ends.Add(new PortEnd
            {
                Route = Item.Route,
                IsSource = false,
                Side = Entry,
                Box = Item.Target,
                Other = Item.Source
            });
        }

        Dictionary<string, List<PortEnd>> Groups = new Dictionary<string, List<PortEnd>>(StringComparer.Ordinal);
        foreach (PortEnd End in Ends)
        {
            string Key = End.Box.Id + "|" + End.Side;
            if (!Groups.TryGetValue(Key, out List<PortEnd>? Group))
            {
                Group = new List<PortEnd>();
                Groups[Key] = Group;
            }

            Group.Add(End);
        }

        foreach (KeyValuePair<string, List<PortEnd>> Pair in Groups)
        {
            List<PortEnd> Group = Pair.Value;
            Group.Sort(delegate(PortEnd Left, PortEnd Right)
            {
                bool VerticalSide = Left.Side == "left" || Left.Side == "right";
                if (VerticalSide)
                {
                    return Right.Other.CenterY.CompareTo(Left.Other.CenterY);
                }

                return Left.Other.CenterX.CompareTo(Right.Other.CenterX);
            });

            ApplySlots(Group, Nodes);
        }
    }

    private static void ApplySlots(List<PortEnd> Group, Dictionary<string, NodeContract> Nodes)
    {
        if (Group.Count == 0)
        {
            return;
        }

        BoxRef Box = Group[0].Box;
        string Side = Group[0].Side;
        bool Diamond = Nodes.TryGetValue(Box.Id, out NodeContract? Node) && FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
        if (Diamond && !Box.IsFrame)
        {
            List<(double X, double Y)> Destinations = new List<(double X, double Y)>();
            foreach (PortEnd End in Group)
            {
                Destinations.Add((End.Other.CenterX - Box.CenterX, End.Other.CenterY - Box.CenterY));
            }

            if (FlowchartDiamondPorts.TryAssign((int)Box.Width, (int)Box.Height, Destinations, out IReadOnlyList<FlowchartDiamondPort> Ports)
                && Ports.Count == Group.Count)
            {
                for (int Index = 0; Index < Group.Count; Index++)
                {
                    FlowchartDiamondPort Port = Ports[Index];
                    int X = (int)Math.Round(Box.CenterX + Port.OffsetX);
                    int Y = (int)Math.Round(Box.CenterY + Port.OffsetY);
                    SetEnd(Group[Index], X, Y, Port.DepartureX, Port.DepartureY);
                }

                return;
            }
        }

        bool VerticalSide = Side == "left" || Side == "right";
        int Length = (int)Math.Round(VerticalSide ? Box.Height : Box.Width);
        int SlotCount = Math.Max(DiagramGridMetrics.TRACKS_PER_GAP, Group.Count);
        int[] Centres = DiagramGridMetrics.EqualCentres(Math.Max(1, Length), SlotCount);
        int[] Indexes = DiagramGridMetrics.SelectSlotIndexes(Group.Count, SlotCount);
        for (int Index = 0; Index < Group.Count; Index++)
        {
            int Along = Centres[Indexes[Math.Min(Index, Indexes.Length - 1)]];
            int X;
            int Y;
            int OutX = 0;
            int OutY = 0;
            if (Side == "top")
            {
                X = (int)Math.Round(Box.Left + Along);
                Y = (int)Math.Round(Box.Top);
                OutY = 1;
            }
            else if (Side == "bottom")
            {
                X = (int)Math.Round(Box.Left + Along);
                Y = (int)Math.Round(Box.Bottom);
                OutY = -1;
            }
            else if (Side == "left")
            {
                X = (int)Math.Round(Box.Left);
                Y = (int)Math.Round(Box.Top - Along);
                OutX = -1;
            }
            else
            {
                X = (int)Math.Round(Box.Right);
                Y = (int)Math.Round(Box.Top - Along);
                OutX = 1;
            }

            if (!Box.IsFrame && Nodes.TryGetValue(Box.Id, out NodeContract? ShapeNode) && !IsRectangular(ShapeNode.Shape) && !Diamond)
            {
                (X, Y) = ProjectToOutline(Box, ShapeNode.Shape, X, Y, OutX, OutY);
            }

            SetEnd(Group[Index], X, Y, OutX, OutY);
        }
    }

    private static (int X, int Y) ProjectToOutline(BoxRef Box, string? Shape, int SlotX, int SlotY, int OutX, int OutY)
    {
        if (string.Equals(Shape, "circle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "doublecircle", StringComparison.OrdinalIgnoreCase))
        {
            double Radius = Math.Min(Box.Width, Box.Height) / 2.0;
            double Dx = SlotX - Box.CenterX;
            double Dy = SlotY - Box.CenterY;
            if (OutX != 0)
            {
                double Inside = (Radius * Radius) - (Dy * Dy);
                if (Inside > 0.0)
                {
                    double Offset = Math.Sqrt(Inside);
                    int X = (int)Math.Round(Box.CenterX + (OutX > 0 ? Offset : -Offset));
                    return (X, SlotY);
                }
            }

            if (OutY != 0)
            {
                double Inside = (Radius * Radius) - (Dx * Dx);
                if (Inside > 0.0)
                {
                    double Offset = Math.Sqrt(Inside);
                    int Y = (int)Math.Round(Box.CenterY + (OutY > 0 ? Offset : -Offset));
                    return (SlotX, Y);
                }
            }
        }

        FlowchartLayoutNodeBounds Bounds = new FlowchartLayoutNodeBounds
        {
            NodeId = Box.Id,
            CenterX = Box.CenterX,
            CenterY = Box.CenterY,
            Width = Box.Width,
            Height = Box.Height
        };
        (double AttachX, double AttachY) = FlowchartShapeBoundaryAttachHelper.ClipToShapeOutline(
            Bounds,
            Shape,
            SlotX + (OutX * 4.0),
            SlotY + (OutY * 4.0),
            EdgeEndpointMarker.None);
        return ((int)Math.Round(AttachX), (int)Math.Round(AttachY));
    }

    private static bool IsRectangular(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return true;
        }

        return string.Equals(Shape, "rect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rectangle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "square", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "round", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rounded", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetEnd(PortEnd End, int X, int Y, int OutX, int OutY)
    {
        End.OutX = OutX;
        End.OutY = OutY;
        FlowchartLayoutPoint Point = End.IsSource ? End.Route.Points[0] : End.Route.Points[End.Route.Points.Count - 1];
        Point.X = X;
        Point.Y = Y;
        End.Route.Points[End.IsSource ? 0 : End.Route.Points.Count - 1] = new FlowchartLayoutPoint
        {
            X = X,
            Y = Y
        };
    }

    private static void EnsureStub(FlowchartLayoutEdgeRoute Route, bool AtStart)
    {
        if (Route.Points.Count < 2)
        {
            return;
        }

        int EndIndex = AtStart ? 0 : Route.Points.Count - 1;
        int NextIndex = AtStart ? 1 : Route.Points.Count - 2;
        FlowchartLayoutPoint End = Route.Points[EndIndex];
        FlowchartLayoutPoint Next = Route.Points[NextIndex];
        double Dx = Next.X - End.X;
        double Dy = Next.Y - End.Y;
        bool Perpendicular = Math.Abs(Dx) <= 0.5 || Math.Abs(Dy) <= 0.5;
        if (Perpendicular && ((Dx * Dx) + (Dy * Dy)) >= (STUB_PX * STUB_PX))
        {
            return;
        }

        double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        double DirX = Length < 1e-6 ? 0.0 : Dx / Length;
        double DirY = Length < 1e-6 ? 1.0 : Dy / Length;
        if (Math.Abs(DirX) >= Math.Abs(DirY))
        {
            DirY = 0.0;
            DirX = DirX >= 0 ? 1.0 : -1.0;
        }
        else
        {
            DirX = 0.0;
            DirY = DirY >= 0 ? 1.0 : -1.0;
        }

        FlowchartLayoutPoint Stub = new FlowchartLayoutPoint
        {
            X = Math.Round(End.X + (DirX * STUB_PX)),
            Y = Math.Round(End.Y + (DirY * STUB_PX))
        };
        if (AtStart)
        {
            Route.Points.Insert(1, Stub);
        }
        else
        {
            Route.Points.Insert(Route.Points.Count - 1, Stub);
        }
    }

    private static void SeparateTracks(FlowchartLayoutResult Copy, List<GridNode> Placed)
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
                NudgeOffSharedRun(Routes[LeftIndex], Routes[RightIndex], Placed);
            }
        }
    }

    private static void NudgeOffSharedRun(FlowchartLayoutEdgeRoute Stay, FlowchartLayoutEdgeRoute Mover, List<GridNode> Placed)
    {
        if (Mover.Points.Count < 3 || Stay.Points.Count < 2)
        {
            return;
        }

        int Shared = 0;
        for (int Index = 1; Index < Mover.Points.Count - 1; Index++)
        {
            FlowchartLayoutPoint Point = Mover.Points[Index];
            if (DistanceToRoute(Point.X, Point.Y, Stay) < (DiagramGridMetrics.GRID_UNIT_PX / 2.0))
            {
                Shared++;
            }
        }

        if (Shared == 0)
        {
            return;
        }

        int Offset = DiagramGridMetrics.TrackOffsets(2, DiagramGridMetrics.GRID_UNIT_PX)[1]
            - DiagramGridMetrics.TrackOffsets(1, DiagramGridMetrics.GRID_UNIT_PX)[0];
        if (Offset == 0)
        {
            Offset = 6;
        }

        for (int Index = 1; Index < Mover.Points.Count - 1; Index++)
        {
            FlowchartLayoutPoint Point = Mover.Points[Index];
            if (DistanceToRoute(Point.X, Point.Y, Stay) >= (DiagramGridMetrics.GRID_UNIT_PX / 2.0))
            {
                continue;
            }

            if (InsideAny(Placed, Point.X + Offset, Point.Y))
            {
                Point.X -= Offset;
            }
            else
            {
                Point.X += Offset;
            }
        }
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

                    double ToLeft = Point.X - Left;
                    double ToRight = Right - Point.X;
                    double ToBottom = Point.Y - Bottom;
                    double ToTop = Top - Point.Y;
                    double Best = Math.Min(Math.Min(ToLeft, ToRight), Math.Min(ToBottom, ToTop));
                    if (Best == ToLeft)
                    {
                        Point.X = Math.Round(Left - STUB_PX);
                    }
                    else if (Best == ToRight)
                    {
                        Point.X = Math.Round(Right + STUB_PX);
                    }
                    else if (Best == ToBottom)
                    {
                        Point.Y = Math.Round(Bottom - STUB_PX);
                    }
                    else
                    {
                        Point.Y = Math.Round(Top + STUB_PX);
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

        string? StartId = null;
        foreach (GridNode Node in Placed)
        {
            if (!HasForwardIncoming.Contains(Node.Id))
            {
                StartId = Node.Id;
                break;
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
            CurrentId = ForwardOutgoing[0].TargetId;
        }

        int SpineColumn = StartNode.Column;
        foreach (string SpineId in SpineIds)
        {
            if (ById.TryGetValue(SpineId, out GridNode? SpineNode))
            {
                SpineNode.Column = SpineColumn;
            }
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

            if (!FlowchartLoopBackRouteHelper.TryResolveDecisionLoopBackRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    Nodes,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints)
                && !FlowchartFanInPortAllocator.TryResolveLoopBackRoute(
                    Edge,
                    Contract,
                    BoundsById,
                    Nodes.TryGetValue(Route.TargetId, out NodeContract? TargetNode) ? TargetNode.Shape : null,
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

    private static EdgeContract? FindContractEdge(DiagramContract Contract, FlowchartLayoutEdgeRoute Route)
    {
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.SourceId, Route.SourceId, StringComparison.Ordinal)
                && string.Equals(Candidate.TargetId, Route.TargetId, StringComparison.Ordinal))
            {
                return Candidate;
            }
        }

        return null;
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
            if (!FlowchartDecisionPortAllocator.TryResolveEdgeRoute(
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
        string? Current = GroupOf(Contract, NodeId);
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
            GraphHeight = Source.GraphHeight
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
            int Gap = DiagramGridMetrics.NODE_GAP_PX;
            int Crossings = CountRankCrossings(Copy, RankNodes, NextNodes);
            int TrackUnits = Math.Max(1, (Crossings + DiagramGridMetrics.TRACKS_PER_GAP - 1) / DiagramGridMetrics.TRACKS_PER_GAP);
            Gap = Math.Max(Gap, TrackUnits * DiagramGridMetrics.GRID_UNIT_PX);
            double MermaidDelta = MermaidMain(NextNodes, Horizontal) - MermaidMain(RankNodes, Horizontal);
            if (MermaidDelta > MainPitch)
            {
                int MermaidSpan = DiagramGridMetrics.CeilToGrid(MermaidDelta);
                int MermaidGap = MermaidSpan - HalfBelow - HalfAbove;
                Gap = Math.Max(Gap, MermaidGap);
            }

            int LabelGapBoost = ComputeLabelledEdgeRankGapBoost(Contract, Rank, RankNodes, NextNodes);
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

    /// <summary>
    /// A8: every rank pair shares the same base clear gap, measured from each rank's tallest node.
    /// </summary>
    private static int ResolveUniformRankClearGap(List<RankPairGapPlan> Plans)
    {
        int UniformClearGap = DiagramGridMetrics.NODE_GAP_PX;
        foreach (RankPairGapPlan Plan in Plans)
        {
            UniformClearGap = Math.Max(UniformClearGap, Plan.StructuralClearGap);
        }

        return UniformClearGap;
    }

    private static int ComputeLabelledEdgeRankGapBoost(
        DiagramContract Contract,
        int Rank,
        IReadOnlyList<GridNode> RankNodes,
        IReadOnlyList<GridNode> NextNodes)
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

            if (!RankIds.Contains(Edge.SourceId) || !NextIds.Contains(Edge.TargetId))
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
                    Options.EdgeLabelFontSizePx));
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

        public string Side = string.Empty;

        public BoxRef Box;

        public BoxRef Other;

        public int OutX;

        public int OutY;
    }
}
