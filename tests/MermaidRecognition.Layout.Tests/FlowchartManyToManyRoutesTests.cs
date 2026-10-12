using MermaidRecognition.Contract;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartManyToManyRoutesTests
{
    [Test]
    public void Apply_ReversedHintRows_NestsTracksWithoutCrossings()
    {
        DiagramContract Contract = new();
        FlowchartLayoutResult Layout = new();
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = new(StringComparer.Ordinal);
        double[] SourceXs = [0, 100, 200];
        double[] TargetXs = [150, 250, 350];
        for (int Index = 0; Index < SourceXs.Length; Index++)
        {
            string SourceId = $"s{Index}";
            string TargetId = $"t{Index}";
            Bounds[SourceId] = new FlowchartLayoutNodeBounds
            {
                NodeId = SourceId, CenterX = SourceXs[Index], CenterY = 0, Width = 60, Height = 20
            };
            Contract.NodesMutable.Add(new NodeContract
            {
                Id = SourceId,
                PlacementHint = new PlacementHintContract { Kind = PlacementHintKind.RightOf, PeerId = "previous" }
            });
            Bounds[TargetId] = new FlowchartLayoutNodeBounds
            {
                NodeId = TargetId, CenterX = TargetXs[Index], CenterY = 100, Width = 60, Height = 20
            };
            Contract.EdgesMutable.Add(new EdgeContract { Id = $"e{Index}", SourceId = SourceId, TargetId = TargetId });
            FlowchartLayoutEdgeRoute Route = new()
            {
                EdgeId = $"e{Index}", SourceId = SourceId, TargetId = TargetId,
                ExitSide = "top", EntrySide = "bottom"
            };
            double Track = 70 - Index * 20;
            Route.Points.Add(new() { X = SourceXs[Index], Y = 10 });
            Route.Points.Add(new() { X = SourceXs[Index], Y = Track });
            Route.Points.Add(new() { X = TargetXs[Index], Y = Track });
            Route.Points.Add(new() { X = TargetXs[Index], Y = 90 });
            Layout.EdgeRoutesMutable.Add(Route);
        }

        FlowchartManyToManyRoutes.Apply(Layout, Contract, Bounds);

        int Crossings = 0;
        for (int First = 0; First < Layout.EdgeRoutes.Count; First++)
        for (int Second = First + 1; Second < Layout.EdgeRoutes.Count; Second++)
            Crossings += FlowchartBranchRouteOrder.Intersections(
                Layout.EdgeRoutes[First].Points.Select(Point => (Point.X, Point.Y)).ToList(),
                Layout.EdgeRoutes[Second].Points.Select(Point => (Point.X, Point.Y)).ToList()).Crossings;

        Assert.Multiple(() =>
        {
            Assert.That(Crossings, Is.Zero);
            Assert.That(Layout.EdgeRoutes.Select(Route => Route.ExitSide), Is.All.EqualTo("top"));
            Assert.That(Layout.EdgeRoutes.Select(Route => Route.EntrySide), Is.All.EqualTo("bottom"));
        });
    }
}
