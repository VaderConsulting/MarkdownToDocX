using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Routes inner rows of a wrapped fan on distinct tracks between leaf columns.</summary>
internal static class FlowchartWrappedFanRoutes
{
    private const int MinimumFan = DiagramGridMetrics.TRACKS_PER_GAP + 1;

    internal static void Apply(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        bool Horizontal = FlowchartDirectionNormalizer.Normalize(Contract.Direction)
            is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        var Visible = Contract.Edges.Where(Edge => Bounds.ContainsKey(Edge.SourceId)
            && Bounds.ContainsKey(Edge.TargetId)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (FlowchartLayoutNodeBounds Hub in Bounds.Values)
        {
            List<FlowchartLayoutNodeBounds> Out = Visible.Where(Edge => Edge.SourceId == Hub.NodeId)
                .Select(Edge => Bounds[Edge.TargetId]).Distinct().ToList();
            List<FlowchartLayoutNodeBounds> In = Visible.Where(Edge => Edge.TargetId == Hub.NodeId)
                .Select(Edge => Bounds[Edge.SourceId]).Distinct().ToList();
            bool FanOut = Out.Count >= MinimumFan && In.Count < MinimumFan;
            List<FlowchartLayoutNodeBounds> Leaves = FanOut ? Out : In;
            if (Leaves.Count < MinimumFan || !SimpleComponent(Hub, Leaves, Visible)) continue;

            double Main(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterX : Box.CenterY;
            double Cross(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterY : Box.CenterX;
            double MainHalf(FlowchartLayoutNodeBounds Box) => (Horizontal ? Box.Width : Box.Height) / 2.0;
            double CrossHalf(FlowchartLayoutNodeBounds Box) => (Horizontal ? Box.Height : Box.Width) / 2.0;
            List<IGrouping<double, FlowchartLayoutNodeBounds>> Ranks = Leaves
                .GroupBy(Box => Main(Box)).OrderBy(Group => Math.Abs(Group.Key - Main(Hub))).ToList();
            if (Ranks.Count < 2) continue;
            double Sign = Math.Sign(Ranks[0].Key - Main(Hub));
            if (Sign == 0.0) continue;
            var Columns = Leaves.GroupBy(Box => Cross(Box)).OrderBy(Group => Group.Key).ToList();
            if (Columns.Count < 2) continue;
            var Gaps = new List<(double Low, double High, int Used, List<FlowchartLayoutNodeBounds> Leaves)>();
            for (int Index = 0; Index < Columns.Count - 1; Index++)
            {
                double Low = Columns[Index].Max(Box => Cross(Box) + CrossHalf(Box));
                double High = Columns[Index + 1].Min(Box => Cross(Box) - CrossHalf(Box));
                if (High - Low >= DiagramGridMetrics.GRID_UNIT_PX - 0.5)
                    Gaps.Add((Low, High, 0, []));
            }
            if (Gaps.Count == 0) continue;

            List<FlowchartLayoutNodeBounds> Inner = Ranks.Skip(1).SelectMany(Group => Group)
                .OrderBy(Box => Math.Abs(Main(Box) - Main(Hub))).ThenBy(Cross).ToList();
            foreach (FlowchartLayoutNodeBounds Leaf in Inner)
            {
                var Choices = Gaps.Select((Gap, Index) => (Gap, Index))
                    .Where(Item => Item.Gap.High <= Cross(Leaf) + 0.5 || Item.Gap.Low >= Cross(Leaf) - 0.5)
                    .OrderBy(Item => Item.Gap.Leaves.Count)
                    .ThenBy(Item => Distance(Item.Gap, Cross(Leaf))).ThenBy(Item => Item.Index).ToList();
                if (Choices.Count == 0) continue;
                int Choice = Choices[0].Index;
                Gaps[Choice].Leaves.Add(Leaf);
            }

            double HubCrossLow = Cross(Hub) - CrossHalf(Hub), HubCrossHigh = Cross(Hub) + CrossHalf(Hub);
            foreach (var Gap in Gaps)
            {
                int[] Offsets = DiagramGridMetrics.TrackOffsets(Gap.Leaves.Count,
                    (int)Math.Round(Gap.High - Gap.Low));
                List<FlowchartLayoutNodeBounds> Ordered = Gap.Leaves.OrderBy(Box => Math.Abs(Main(Box) - Main(Hub)))
                    .ThenBy(Box => Cross(Box)).ToList();
                for (int Index = 0; Index < Ordered.Count; Index++)
                {
                    FlowchartLayoutNodeBounds Leaf = Ordered[Index];
                    double Track = Gap.Low + Offsets[Index];
                    if (Track <= HubCrossLow + 0.5 || Track >= HubCrossHigh - 0.5) continue;
                    FlowchartLayoutEdgeRoute? Route = Layout.EdgeRoutes.FirstOrDefault(Edge => FanOut
                        ? Edge.SourceId == Hub.NodeId && Edge.TargetId == Leaf.NodeId
                        : Edge.SourceId == Leaf.NodeId && Edge.TargetId == Hub.NodeId);
                    if (Route == null) continue;
                    double HubSide = Main(Hub) + Sign * MainHalf(Hub);
                    double LeafSide = Cross(Leaf) + Math.Sign(Track - Cross(Leaf)) * CrossHalf(Leaf);
                    var Points = new List<FlowchartLayoutPoint>
                    {
                        Point(HubSide, Track, Horizontal),
                        Point(Main(Leaf), Track, Horizontal),
                        Point(Main(Leaf), LeafSide, Horizontal)
                    };
                    string HubSideName = MainSide(Horizontal, Sign);
                    string LeafSideName = CrossSide(Horizontal, Math.Sign(Track - Cross(Leaf)));
                    Route.Points.Clear();
                    if (FanOut)
                    {
                        Route.Points.AddRange(Points);
                        Route.ExitSide = HubSideName;
                        Route.EntrySide = LeafSideName;
                    }
                    else
                    {
                        Points.Reverse();
                        Route.Points.AddRange(Points);
                        Route.ExitSide = LeafSideName;
                        Route.EntrySide = HubSideName;
                    }
                }
            }
        }
    }

    private static bool SimpleComponent(FlowchartLayoutNodeBounds Hub,
        IReadOnlyCollection<FlowchartLayoutNodeBounds> Leaves, IReadOnlyCollection<EdgeContract> Edges)
    {
        var Ids = Leaves.Select(Node => Node.NodeId).Append(Hub.NodeId).ToHashSet(StringComparer.Ordinal);
        return Edges.All(Edge => Ids.Contains(Edge.SourceId) == Ids.Contains(Edge.TargetId))
            && Leaves.All(Leaf => Edges.Count(Edge => Edge.SourceId == Leaf.NodeId || Edge.TargetId == Leaf.NodeId) == 1);
    }

    private static double Distance((double Low, double High, int Used, List<FlowchartLayoutNodeBounds> Leaves) Gap,
        double Cross) => Cross < Gap.Low ? Gap.Low - Cross : Cross > Gap.High ? Cross - Gap.High : 0.0;

    private static FlowchartLayoutPoint Point(double Main, double Cross, bool Horizontal) => Horizontal
        ? new FlowchartLayoutPoint { X = Main, Y = Cross }
        : new FlowchartLayoutPoint { X = Cross, Y = Main };

    private static string MainSide(bool Horizontal, double Sign) => Horizontal
        ? Sign > 0 ? "right" : "left"
        : Sign > 0 ? "top" : "bottom";

    private static string CrossSide(bool Horizontal, double Sign) => Horizontal
        ? Sign > 0 ? "top" : "bottom"
        : Sign > 0 ? "right" : "left";
}
