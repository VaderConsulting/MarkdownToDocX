using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridBypassObstacleTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RoundTargetInsideItsBounds_KeepsObstacleAvoidanceAtTheTerminal(bool Mirrored)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\n a[Input] --> b((Output))\n c[Blocker]");
        double Sign = Mirrored ? -1 : 1;
        FlowchartLayoutEdgeRoute Route = new()
        {
            SourceId = "a", TargetId = "b",
            ExitSide = Mirrored ? "left" : "right", EntrySide = Mirrored ? "right" : "left"
        };
        Route.Points.Add(new() { X = Sign * 90, Y = 300 });
        Route.Points.Add(new() { X = Sign * 425, Y = 0 });
        FlowchartLayoutResult Layout = new();
        Layout.EdgeRoutesMutable.Add(Route);
        List<DiagramGridLayout.GridNode> Nodes =
        [
            new() { Id = "a", X = 0, Y = 300, Width = 180, Height = 60 },
            new() { Id = "b", X = Sign * 500, Y = 0, Width = 180, Height = 150 },
            new() { Id = "c", X = Sign * 250, Y = 0, Width = 100, Height = 120 }
        ];
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Nodes, Contract.Nodes.ToDictionary(Node => Node.Id),
            Contract, [], Mirrored ? FlowchartDirection.RightToLeft : FlowchartDirection.LeftToRight);
        FlowchartLayoutNodeBounds Blocker = new()
        { NodeId = "c", CenterX = Sign * 250, CenterY = 0, Width = 100, Height = 120 };
        foreach (var Pair in Route.Points.Zip(Route.Points.Skip(1)))
        {
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Blocker), Is.False,
                string.Join("; ", Route.Points.Select(Point => $"({Point.X},{Point.Y})")));
        }
        Assert.That(Route.Points[^2].Y, Is.EqualTo(Route.Points[^1].Y));
        Assert.That(Route.Points[^1].X, Is.EqualTo(Sign * 425));
    }

    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void DirectLinkAlongsideForwardChain_ClearsIntermediateNodes(string Direction)
    {
        string Source = $$"""
            flowchart {{Direction}}
              a[Input] --> b[Split]
              b --> c[Intermediate]
              b --> d[Collector]
              c --> d
            """;
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate(
            "forward-chain-bypass", Source);
        Assert.That(Report.Failures.Where(Failure => Failure.Kind == "NodeIntersection"), Is.Empty,
            string.Join("\n", Report.Failures.Select(Failure => Failure.Kind + ": " + Failure.Description)));
    }
}
