using System.Text;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class PartnerRankRoutingTests
{
    [TestCase("TB", 2, false)]
    [TestCase("LR", 2, false)]
    [TestCase("TB", 3, false)]
    [TestCase("TB", 3, true)]
    [TestCase("BT", 3, true)]
    [TestCase("LR", 3, true)]
    [TestCase("RL", 3, true)]
    [TestCase("TB", 6, false)]
    [TestCase("LR", 6, false)]
    [TestCase("BT", 6, false)]
    [TestCase("RL", 6, false)]
    [TestCase("TB", 6, true)]
    [TestCase("LR", 6, true)]
    [TestCase("TB", 12, false)]
    [TestCase("LR", 12, false)]
    [TestCase("TB", 20, false)]
    public void CompletePartnerRanks_KeepAlignedDropsDistinctPortsAndNestedTracks(string Direction, int Count, bool Grouped)
    {
        var Contract = MermaidRecognizer.Parse(Source(Direction, Count, Grouped));
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        bool Horizontal = Direction is "LR" or "RL";
        double Cross(FlowchartLayoutNodeBounds Box) => Horizontal ? Box.CenterY : Box.CenterX;
        double Main(FlowchartLayoutPoint Point) => Horizontal ? Point.X : Point.Y;
        var Parents = Layout.NodeBounds.Where(Box => Box.NodeId.StartsWith("p")).OrderBy(Cross).ToArray();
        var Children = Layout.NodeBounds.Where(Box => Box.NodeId.StartsWith("c")).OrderBy(Cross).ToArray();
        Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(Count * Count));
        for (int Index = 0; Index < Count; Index++)
        {
            Assert.That(Cross(Parents[Index]), Is.EqualTo(Cross(Children[Index])).Within(0.01));
            var Straight = Layout.EdgeRoutes.Single(Route => Route.SourceId == Parents[Index].NodeId && Route.TargetId == Children[Index].NodeId);
            Assert.That(Straight.Points, Has.Count.EqualTo(2));
        }
        var Lanes = new List<double>();
        foreach (var Route in Layout.EdgeRoutes)
        {
            var Parent = Parents.Single(Box => Box.NodeId == Route.SourceId);
            var Child = Children.Single(Box => Box.NodeId == Route.TargetId);
            bool Aligned = Math.Abs(Cross(Parent) - Cross(Child)) < 0.01;
            Assert.That(Route.Points, Has.Count.EqualTo(Aligned ? 2 : 4), $"{Route.SourceId} -> {Route.TargetId}");
            Assert.That(Route.ExitSide, Is.EqualTo(Direction switch { "TB" => "bottom", "BT" => "top", "LR" => "right", _ => "left" }));
            Assert.That(Route.EntrySide, Is.EqualTo(Direction switch { "TB" => "top", "BT" => "bottom", "LR" => "left", _ => "right" }));
            var First = Route.Points[0]; var Last = Route.Points[^1];
            Assert.That(Horizontal ? First.Y : First.X, Is.EqualTo(Horizontal ? Route.Points[1].Y : Route.Points[1].X).Within(0.01));
            Assert.That(Horizontal ? Last.Y : Last.X, Is.EqualTo(Horizontal ? Route.Points[^2].Y : Route.Points[^2].X).Within(0.01));
            Assert.That(Math.Abs(Main(Route.Points[1]) - Main(First)), Is.GreaterThanOrEqualTo(20));
            Assert.That(Math.Abs(Main(Last) - Main(Route.Points[^2])), Is.GreaterThanOrEqualTo(20));
            if (!Aligned) Lanes.Add(Main(Route.Points[1]));
            foreach (var Segment in Route.Points.Zip(Route.Points.Skip(1)))
            {
                Assert.That(Math.Abs(Segment.First.X - Segment.Second.X) < 0.01
                    || Math.Abs(Segment.First.Y - Segment.Second.Y) < 0.01, Is.True);
                foreach (var Box in Layout.NodeBounds.Where(Box => Box != Parent && Box != Child))
                    Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                        Segment.First.X, Segment.First.Y, Segment.Second.X, Segment.Second.Y, Box), Is.False);
            }
        }
        var Ordered = Lanes.Order().ToArray();
        Assert.That(Ordered.Zip(Ordered.Skip(1)).All(Pair => Pair.Second - Pair.First >= 6 - 0.01), Is.True);
        foreach (var Box in Parents.Concat(Children))
        {
            var Ends = Layout.EdgeRoutes.Where(Route => Route.SourceId == Box.NodeId || Route.TargetId == Box.NodeId)
                .Select(Route => Route.SourceId == Box.NodeId ? Route.Points[0] : Route.Points[^1]).ToList();
            Assert.That(Ends.Select(Point => (Point.X, Point.Y)).Distinct().Count(), Is.EqualTo(Count));
            Assert.That(Ends.All(Point => Math.Abs(Horizontal ? Point.Y - Box.CenterY : Point.X - Box.CenterX)
                < (Horizontal ? Box.Height : Box.Width) / 2), Is.True);
        }
        for (int First = 0; First < Layout.EdgeRoutes.Count; First++)
        for (int Second = First + 1; Second < Layout.EdgeRoutes.Count; Second++)
        {
            var A = Layout.EdgeRoutes[First]; var B = Layout.EdgeRoutes[Second];
            var Intersections = FlowchartBranchRouteOrder.Intersections(Path(A), Path(B));
            Assert.That(Intersections.Overlap, Is.Zero, "No two connectors may share a run.");
            if (A.SourceId == B.SourceId || A.TargetId == B.TargetId)
                Assert.That(Intersections.Crossings, Is.Zero, "Individual fans must nest without crossings.");
        }
        if (Grouped)
        {
            foreach (var Route in Layout.EdgeRoutes.Where(Route => Route.Points.Count == 4))
            foreach (var Frame in Layout.ClusterBounds)
                Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                    Route.Points[1].X, Route.Points[1].Y, Route.Points[2].X, Route.Points[2].Y, Frame), Is.False,
                    "Cross-rank tracks must occupy the clear gap between frames.");
        }
    }

    internal static string Source(string Direction, int Count, bool Grouped)
    {
        var Text = new StringBuilder($"flowchart {Direction}\n");
        foreach (string Prefix in new[] { "p", "c" })
        {
            if (Grouped) Text.AppendLine($"subgraph {Prefix}group[\"Partners\"]");
            for (int Index = 0; Index < Count; Index++) Text.AppendLine($"{Prefix}{Index}[\"Partner {Index}\"]");
            if (Grouped) Text.AppendLine("end");
        }
        for (int Parent = 0; Parent < Count; Parent++)
        for (int Child = 0; Child < Count; Child++) Text.AppendLine($"p{Parent} --> c{Child}");
        return Text.ToString();
    }

    private static List<(double X, double Y)> Path(FlowchartLayoutEdgeRoute Route) => Route.Points.Select(Point => (Point.X, Point.Y)).ToList();
}
