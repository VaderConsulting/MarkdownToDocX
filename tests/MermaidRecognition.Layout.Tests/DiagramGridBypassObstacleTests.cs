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
    public void EntryFromBesideAFrame_DoesNotDetourBeyondItsOppositeSide(bool Mirrored)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\n a[Input]\n subgraph g[Container]\n b[Output]\n end\n a --> b");
        double Sign = Mirrored ? -1 : 1;
        FlowchartLayoutEdgeRoute Route = new()
        {
            SourceId = "a", TargetId = "b", ExitSide = Mirrored ? "left" : "right", EntrySide = "top"
        };
        Route.Points.Add(new() { X = Sign * 50, Y = 200 });
        Route.Points.Add(new() { X = Sign * 300, Y = 30 });
        FlowchartLayoutResult Layout = new();
        Layout.EdgeRoutesMutable.Add(Route);
        List<DiagramGridLayout.GridNode> Nodes =
        [new() { Id = "a", X = 0, Y = 200, Width = 100, Height = 60 },
         new() { Id = "b", X = Sign * 300, Y = 0, Width = 100, Height = 60 }];
        List<DiagramGridLayout.GridFrame> Frames =
        [new() { Id = "g", Left = Mirrored ? -450 : 150, Right = Mirrored ? -150 : 450, Top = 400, Bottom = -100 }];
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Nodes, Contract.Nodes.ToDictionary(Node => Node.Id),
            Contract, Frames, FlowchartDirection.TopToBottom);
        Assert.That(Route.Points.All(Point => Point.Y <= 400 && Point.Y >= -100), Is.True,
            "An approach from beside the frame must not leave and reenter through its top.");
        Assert.That(Route.Points, Has.Count.EqualTo(3), "The clear approach needs one elbow.");
        Assert.That(Route.Points[^2].X, Is.EqualTo(Route.Points[^1].X));
        Assert.That(Route.Points[^2].Y - Route.Points[^1].Y, Is.GreaterThanOrEqualTo(30));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FrameEntry_TurnsBeforeAnUnrelatedMemberInsteadOfExtendingThroughIt(bool Mirrored)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
              a[Input]
              subgraph g[Container]
                b[Output]
                c[Blocker]
              end
              a --> b
            """);
        double Sign = Mirrored ? -1 : 1;
        FlowchartLayoutEdgeRoute Route = new()
        {
            SourceId = "a", TargetId = "b",
            ExitSide = Mirrored ? "right" : "left", EntrySide = Mirrored ? "right" : "left"
        };
        Route.Points.Add(new() { X = Sign * 700, Y = 0 });
        Route.Points.Add(new() { X = Sign * 850, Y = 200 });
        FlowchartLayoutResult Layout = new();
        Layout.EdgeRoutesMutable.Add(Route);
        List<DiagramGridLayout.GridNode> Nodes =
        [
            new() { Id = "a", X = Sign * 750, Y = 0, Width = 100, Height = 60 },
            new() { Id = "b", X = Sign * 900, Y = 200, Width = 100, Height = 60 },
            new() { Id = "c", X = Sign * 600, Y = 200, Width = 100, Height = 60 }
        ];
        List<DiagramGridLayout.GridFrame> Frames =
        [new() { Id = "g", Left = Mirrored ? -1000 : 400, Right = Mirrored ? -400 : 1000, Top = 400, Bottom = 100 }];
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Nodes, Contract.Nodes.ToDictionary(Node => Node.Id),
            Contract, Frames, FlowchartDirection.TopToBottom);
        FlowchartLayoutNodeBounds Blocker = new()
        { NodeId = "c", CenterX = Sign * 600, CenterY = 200, Width = 100, Height = 60 };
        foreach ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair in Route.Points.Zip(Route.Points.Skip(1)))
        {
            Assert.That(FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
                Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Blocker), Is.False);
        }
        List<bool> Axes = [.. Route.Points.Zip(Route.Points.Skip(1))
            .Where(Pair => Math.Abs(Pair.First.X - Pair.Second.X) + Math.Abs(Pair.First.Y - Pair.Second.Y) > 0.5)
            .Select(Pair => Math.Abs(Pair.First.X - Pair.Second.X) > 0.5)];
        Assert.That(Axes.Zip(Axes.Skip(1)).Count(Pair => Pair.First != Pair.Second), Is.LessThanOrEqualTo(2));
        Assert.That(Route.Points[^1].X, Is.EqualTo(Sign * 850));
        Assert.That(Route.Points[^1].Y, Is.EqualTo(200));
    }

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
        foreach ((FlowchartLayoutPoint First, FlowchartLayoutPoint Second) Pair in Route.Points.Zip(Route.Points.Skip(1)))
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
