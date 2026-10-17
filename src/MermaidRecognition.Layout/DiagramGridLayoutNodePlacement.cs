using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Node rank, placement, alignment and compaction helpers for the placement stages.</summary>
public static partial class DiagramGridLayout
{    private static bool UsesCompactLabelSizing(DiagramContract Contract)
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
        if (CoordinatesPlaced && Placed.Any(Node => Node.CellNode != null))
        {
            AlignCellTerminalDecisionTargets(Contract, Placed, Horizontal);
            return;
        }
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
        if (CoordinatesPlaced && Placed.Any(Node => Node.CellNode != null))
        {
            CentreCellTerminalFans(Contract, Placed, Horizontal);
            return;
        }
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
        if (CoordinatesPlaced && Placed.Any(Node => Node.CellNode != null))
        {
            AlignCellTerminalChildren(Contract, Placed, Horizontal);
            return;
        }
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
        if (Placed.Any(Node => Node.CellNode != null))
        {
            AlignCellDecisionInputs(Contract, Placed, Horizontal);
            return;
        }
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
        if (Placed.Any(Node => Node.CellNode != null))
        {
            CompactCellIndependentFanIn(Contract, Placed, Horizontal);
            return;
        }
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
        if (Placed.Any(Node => Node.CellNode != null))
        {
            FlowchartSpineColumnPostProcessor.ApplyCells(Contract, Placed, Direction);
            return;
        }
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
        if (Placed.Any(Node => Node.CellNode != null))
        {
            if (!FlowchartCellInitialPlacement.TryAssign(Placed, Horizontal))
                throw new InvalidOperationException("Initial whole-cell placement could not publish its complete region plan.");
            return;
        }
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
        bool Cells = Placed.Any(Node => Node.CellNode != null);
        var Minimums = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
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
            if (Cells)
            {
                foreach (var Node in new[] { Source, Target })
                {
                    var Previous = Minimums.GetValueOrDefault(Node.Id, (Width: (double)Node.Width, Height: (double)Node.Height));
                    Minimums[Node.Id] = (Horizontal ? Previous.Width : Math.Max(Previous.Width, Needed),
                        Horizontal ? Math.Max(Previous.Height, Needed) : Previous.Height);
                }
                continue;
            }
            if (Horizontal) Source.Height = Target.Height = Math.Max(Needed, Math.Max(Source.Height, Target.Height));
            else Source.Width = Target.Width = Math.Max(Needed, Math.Max(Source.Width, Target.Width));
        }
        if (Cells) GrowCellMinimumExtents(Placed, Minimums);
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

        if (Placed.Any(Node => Node.CellNode != null))
        {
            GrowCellCrowdedSides(Placed, Counts, BoxWidth, BoxHeight);
            return;
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
        HashSet<string> SeenStates = new(StringComparer.Ordinal) { PositionSignature(Placed) };
        bool Moved;
        do
        {
            Moved = false;
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

                        if (Current.PlacementHintLocked)
                        {
                            if (!Previous.PlacementHintLocked)
                            {
                                SetCross(Previous, (int)Math.Round(Cross(Previous, Horizontal, Old: false)) - Push, Horizontal);
                                Moved = true;
                            }
                        }
                        else
                        {
                            SetCross(Current, (int)Math.Round(Cross(Current, Horizontal, Old: false)) + Push, Horizontal);
                            Moved = true;
                        }
                    }
                }
            }

            for (int Rank = 1; Rank < RankCount; Rank++)
            {
                List<GridNode> Earlier = NodesInRank(Placed, Rank - 1);
                List<GridNode> Later = NodesInRank(Placed, Rank);
                int Deficit = 0;
                GridNode? DeficientBefore = null;
                GridNode? DeficientAfter = null;
                foreach (GridNode Before in Earlier)
                {
                    foreach (GridNode After in Later)
                    {
                        int Needed = MainHalf(Before, Horizontal, Leading: false) + MainHalf(After, Horizontal, Leading: true) + DiagramGridMetrics.NODE_GAP_PX;
                        int Have = (int)Math.Round(Main(After, Horizontal, Old: false) - Main(Before, Horizontal, Old: false));
                        if (Have < Needed)
                        {
                            int CandidateDeficit = Needed - Have;
                            if (CandidateDeficit > Deficit)
                            {
                                Deficit = CandidateDeficit;
                                DeficientBefore = Before;
                                DeficientAfter = After;
                            }
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

                    bool MoveEarlier = DeficientAfter?.PlacementHintLocked == true
                        && DeficientBefore?.PlacementHintLocked != true;
                    bool MoveLater = !MoveEarlier && DeficientAfter?.PlacementHintLocked != true;
                    if (MoveEarlier)
                    {
                        SetMain(DeficientBefore!, (int)Math.Round(Main(DeficientBefore!, Horizontal, Old: false)) - Push, Horizontal);
                        Moved = true;
                    }
                    else if (MoveLater)
                    {
                        SetMain(DeficientAfter!, (int)Math.Round(Main(DeficientAfter!, Horizontal, Old: false)) + Push, Horizontal);
                        Moved = true;
                    }
                }
            }

            if (!Moved)
            {
                break;
            }
        }
        while (SeenStates.Add(PositionSignature(Placed)));
    }

}
