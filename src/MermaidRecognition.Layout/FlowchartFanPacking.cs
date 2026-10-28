using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Wraps a simple many-side fan into a measured block beside its hub.</summary>
internal static partial class FlowchartFanPacking
{
    private const int MinimumFan = DiagramGridMetrics.TRACKS_PER_GAP + 1;
    private sealed record Plan(int Columns, int Rows, int ColumnPitch, int RowPitch,
        double Width, double Height, double Score);

    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        if (Nodes.Count <= MinimumFan || Nodes.Any(Node => Node.GroupId != null))
        {
            return false;
        }

        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        List<EdgeContract> Visible = [.. Contract.Edges.Where(Edge => ById.ContainsKey(Edge.SourceId)
            && ById.ContainsKey(Edge.TargetId)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        Dictionary<string, int> Order = Contract.Nodes.Select((Node, Index) => (Node.Id, Index))
            .GroupBy(Item => Item.Id, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First().Index, StringComparer.Ordinal);
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        bool Reverse = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.RightToLeft or FlowchartDirection.BottomToTop;

        foreach (DiagramGridLayout.GridNode Hub in Nodes.OrderBy(Node => Order.GetValueOrDefault(Node.Id, int.MaxValue)))
        {
            List<DiagramGridLayout.GridNode> Out = [.. Visible.Where(Edge => Edge.SourceId == Hub.Id)
                .Select(Edge => ById[Edge.TargetId]).Distinct().OrderBy(Node => Order.GetValueOrDefault(Node.Id, int.MaxValue))];
            List<DiagramGridLayout.GridNode> In = [.. Visible.Where(Edge => Edge.TargetId == Hub.Id)
                .Select(Edge => ById[Edge.SourceId]).Distinct().OrderBy(Node => Order.GetValueOrDefault(Node.Id, int.MaxValue))];
            bool FanOut = Out.Count >= MinimumFan && In.Count < MinimumFan;
            List<DiagramGridLayout.GridNode> Leaves = FanOut ? Out : In;
            if (Leaves.Count < MinimumFan || !SimpleComponent(Hub, Leaves, Visible))
            {
                continue;
            }

            int NodeWidth = Leaves.Max(Node => Node.Width);
            int NodeHeight = Leaves.Max(Node => Node.Height);
            FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
            List<EdgeContract> FanEdges = [.. Visible.Where(Edge => (Edge.SourceId == Hub.Id && ById.ContainsKey(Edge.TargetId)
                    && Leaves.Contains(ById[Edge.TargetId]))
                || (Edge.TargetId == Hub.Id && ById.ContainsKey(Edge.SourceId) && Leaves.Contains(ById[Edge.SourceId])))];
            double PortPitch = FlowchartLayoutRules.LineSpacing;
            double LabelHeight = 0;
            double LabelTrackPitch = 6;
            foreach (EdgeContract Edge in FanEdges)
            {
                if (string.IsNullOrWhiteSpace(Edge.Label))
                {
                    continue;
                }

                FlowchartLabelDimensions Label = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                    Edge.Label, Options, Options.EdgeLabelFontSizePx);
                double LabelWidthPitch = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Label, Options.EdgeLabelFontSizePx, Horizontal: !Horizontal);
                PortPitch = Math.Max(PortPitch, LabelWidthPitch);
                LabelTrackPitch = Math.Max(LabelTrackPitch, LabelWidthPitch);
                LabelHeight = Math.Max(LabelHeight,
                    FlowchartEdgeLabelPlacementHelper.ComputeRenderedEdgeLabelHeight(Label, Options.EdgeLabelFontSizePx));
            }
            int NeededSide = DiagramGridMetrics.CeilToGrid(
                Math.Max(DiagramGridMetrics.MinSideLengthForPorts(Leaves.Count),
                    (int)Math.Ceiling((Leaves.Count * PortPitch) + (2 * DiagramGridMetrics.NODE_GAP_PX))));
            if (Hub.CellNode == null)
            {
                if (Horizontal)
                {
                    Hub.Height = Math.Max(Hub.Height, NeededSide);
                }
                else
                {
                    Hub.Width = Math.Max(Hub.Width, NeededSide);
                }
            }
            int TrackGap = Math.Max(DiagramGridMetrics.NODE_GAP_PX,
                DiagramGridMetrics.CeilToGrid(DiagramGridMetrics.GapWidthForTracks(Leaves.Count)));
            if (LabelHeight > 0)
            {
                TrackGap = Math.Max(TrackGap, DiagramGridMetrics.CeilToGrid(
                    (int)Math.Ceiling(LabelHeight + (2 * FlowchartEdgeLabelGapHelper.ComputeCharacterGapPx(
                        Options.EdgeLabelFontSizePx)))));
            }

            if (Hub.CellNode != null)
            {
                if (ApplyCellFan(Nodes, Hub, Leaves, FanOut, Horizontal, Reverse, TrackGap, LabelTrackPitch))
                {
                    return true;
                }

                continue;
            }
            Plan Best = Enumerable.Range(1, Leaves.Count).Select(Columns =>
                Measure(Hub, Leaves.Count, Columns, NodeWidth, NodeHeight, TrackGap, LabelTrackPitch, Horizontal))
                .OrderBy(Plan => Plan.Score).ThenBy(Plan => Plan.Columns).First();

            double OldLeft = Leaves.Append(Hub).Min(Node => Node.X - (Node.Width / 2.0));
            double OldRight = Leaves.Append(Hub).Max(Node => Node.X + (Node.Width / 2.0));
            double OldTop = Leaves.Append(Hub).Min(Node => Node.Y - (Node.Height / 2.0));
            double OldBottom = Leaves.Append(Hub).Max(Node => Node.Y + (Node.Height / 2.0));
            double Used = Leaves.Sum(Node => (Node.Width + DiagramGridMetrics.NODE_GAP_PX)
                    * (Node.Height + DiagramGridMetrics.NODE_GAP_PX))
                + ((Hub.Width + DiagramGridMetrics.NODE_GAP_PX) * (Hub.Height + DiagramGridMetrics.NODE_GAP_PX));
            double OldEmpty = Math.Max(0.0, 1.0 - (Used / Math.Max(1.0, (OldRight - OldLeft) * (OldBottom - OldTop))));
            double OldScore = FlowchartLayoutScore.Calculate(OldRight - OldLeft, OldBottom - OldTop, 0, OldEmpty);
            if (Best.Score >= OldScore - 0.0001)
            {
                continue;
            }

            double BlockWidth = (Best.Columns * NodeWidth) + (Math.Max(0, Best.Columns - 1) * (Best.ColumnPitch - NodeWidth));
            double BlockHeight = (Best.Rows * NodeHeight) + (Math.Max(0, Best.Rows - 1) * (Best.RowPitch - NodeHeight));
            if (!Horizontal)
            {
                double BlockTop = Reverse == FanOut ? OldTop : OldTop + Hub.Height + TrackGap;
                Hub.X = OldLeft + (BlockWidth / 2.0);
                Hub.Y = Reverse == FanOut ? BlockTop + BlockHeight + TrackGap + (Hub.Height / 2.0) : OldTop + (Hub.Height / 2.0);
                Place(Leaves, OldLeft, BlockTop, Best.Columns, Best.ColumnPitch, Best.RowPitch, NodeWidth, NodeHeight);
            }
            else
            {
                double BlockLeft = Reverse == FanOut ? OldLeft : OldLeft + Hub.Width + TrackGap;
                Hub.X = Reverse == FanOut ? BlockLeft + BlockWidth + TrackGap + (Hub.Width / 2.0) : OldLeft + (Hub.Width / 2.0);
                Hub.Y = OldTop + (BlockHeight / 2.0);
                Place(Leaves, BlockLeft, OldTop, Best.Columns, Best.ColumnPitch, Best.RowPitch, NodeWidth, NodeHeight);
            }
            return true;
        }
        return false;
    }

    private static bool SimpleComponent(DiagramGridLayout.GridNode Hub,
        IReadOnlyCollection<DiagramGridLayout.GridNode> Leaves, IReadOnlyCollection<EdgeContract> Edges)
    {
        HashSet<string> LeafIds = Leaves.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> Ids = LeafIds.Append(Hub.Id).ToHashSet(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Leaf in Leaves)
        {
            List<EdgeContract> LeafEdges = [.. Edges.Where(Edge => Edge.SourceId == Leaf.Id || Edge.TargetId == Leaf.Id)];
            if (LeafEdges.Count > 2)
            {
                return false;
            }

            if (LeafEdges.Count == 2
                && !LeafEdges.Any(Edge => Edge.SourceId == Hub.Id || Edge.TargetId == Hub.Id))
            {
                return false;
            }
        }

        return Edges.All(Edge =>
        {
            bool SourceInside = Ids.Contains(Edge.SourceId);
            bool TargetInside = Ids.Contains(Edge.TargetId);
            if (SourceInside == TargetInside)
            {
                return true;
            }

            string LeafId = SourceInside && LeafIds.Contains(Edge.SourceId) ? Edge.SourceId
                : TargetInside && LeafIds.Contains(Edge.TargetId) ? Edge.TargetId : string.Empty;
            return LeafId.Length > 0
                && Edges.Count(Other => Other.SourceId == LeafId || Other.TargetId == LeafId) == 2;
        });
    }

    private static Plan Measure(DiagramGridLayout.GridNode Hub, int Count, int Columns,
        int NodeWidth, int NodeHeight, int TrackGap, double LabelTrackPitch, bool Horizontal)
    {
        int Rows = (Count + Columns - 1) / Columns;
        int ColumnGap = Horizontal ? DiagramGridMetrics.NODE_GAP_PX
            : DiagramGridMetrics.CeilToGrid((int)Math.Ceiling(Math.Max(DiagramGridMetrics.NODE_GAP_PX,
                (Rows - 1) * LabelTrackPitch)));
        int RowGap = Horizontal
            ? DiagramGridMetrics.CeilToGrid((int)Math.Ceiling(Math.Max(DiagramGridMetrics.NODE_GAP_PX,
                (Columns - 1) * LabelTrackPitch))) : DiagramGridMetrics.NODE_GAP_PX;
        int ColumnPitch = NodeWidth + ColumnGap;
        int RowPitch = NodeHeight + RowGap;
        double BlockWidth = (Columns * NodeWidth) + (Math.Max(0, Columns - 1) * ColumnGap);
        double BlockHeight = (Rows * NodeHeight) + (Math.Max(0, Rows - 1) * RowGap);
        double Width = Horizontal ? Hub.Width + TrackGap + BlockWidth : Math.Max(Hub.Width, BlockWidth);
        double Height = Horizontal ? Math.Max(Hub.Height, BlockHeight) : Hub.Height + TrackGap + BlockHeight;
        double Used = (Count * (NodeWidth + DiagramGridMetrics.NODE_GAP_PX)
                * (NodeHeight + DiagramGridMetrics.NODE_GAP_PX))
            + ((Hub.Width + DiagramGridMetrics.NODE_GAP_PX) * (Hub.Height + DiagramGridMetrics.NODE_GAP_PX));
        Used += Count * (Horizontal ? Width : Height) * DiagramGridMetrics.GRID_UNIT_PX;
        double Empty = Math.Max(0.0, 1.0 - (Used / Math.Max(1.0, Width * Height)));
        return new(Columns, Rows, ColumnPitch, RowPitch, Width, Height,
            FlowchartLayoutScore.Calculate(Width, Height, 0, Empty));
    }

    private static void Place(IReadOnlyList<DiagramGridLayout.GridNode> Leaves, double Left, double Top,
        int Columns, int ColumnPitch, int RowPitch, int NodeWidth, int NodeHeight)
    {
        for (int Index = 0; Index < Leaves.Count; Index++)
        {
            int Column = Index % Columns, Row = Index / Columns;
            Leaves[Index].X = Left + (Column * ColumnPitch) + (NodeWidth / 2.0);
            Leaves[Index].Y = Top + (Row * RowPitch) + (NodeHeight / 2.0);
        }
    }
}
