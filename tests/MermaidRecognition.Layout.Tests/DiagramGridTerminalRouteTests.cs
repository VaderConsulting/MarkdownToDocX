using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridTerminalRouteTests
{
    [Test]
    public void Layout_FanOut_KeepsTheStraightContinuationInTheCentreSlot()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            Source --> First
            Source --> Second
            Source --> Third
            """);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
        FlowchartLayoutNodeBounds Source = Layout.NodeBounds.Single(Node => Node.NodeId == "Source");
        FlowchartLayoutEdgeRoute Straight = Layout.EdgeRoutes.Single(Edge => Edge.TargetId == "First");
        Assert.That(Straight.Points.Count, Is.EqualTo(2));
        Assert.That(Straight.Points[0].X, Is.EqualTo(Source.CenterX));
        Assert.That(Straight.Points[1].X, Is.EqualTo(Source.CenterX));
        Assert.That(Layout.EdgeRoutes.Select(Edge => (Edge.Points[0].X, Edge.Points[0].Y)).Distinct().Count(),
            Is.EqualTo(3), "Every outgoing line keeps its own port.");
    }

    [Test]
    public void RebuildRoutes_NestedFrameExit_TurnsAfterTheOuterBorder()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart TB
            subgraph Outer
                subgraph Inner
                    A[Source]
                end
            end
            A --> B[Target]
            """);
        FlowchartLayoutEdgeRoute Route = new()
        {
            SourceId = "A", TargetId = "B", ExitSide = "bottom", EntrySide = "top"
        };
        Route.Points.Add(new() { X = 100, Y = 270 });
        Route.Points.Add(new() { X = 400, Y = 90 });
        FlowchartLayoutResult Layout = new();
        Layout.EdgeRoutesMutable.Add(Route);
        List<DiagramGridLayout.GridNode> Nodes =
        [new() { Id = "A", X = 100, Y = 300, Width = 150, Height = 60 },
         new() { Id = "B", X = 400, Y = 60, Width = 150, Height = 60 }];
        List<DiagramGridLayout.GridFrame> Frames =
        [new() { Id = "Inner", Left = 0, Right = 200, Top = 400, Bottom = 240 },
         new() { Id = "Outer", Left = -30, Right = 230, Top = 460, Bottom = 210 }];

        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Nodes,
            Contract.Nodes.ToDictionary(Node => Node.Id), Contract, Frames, FlowchartDirection.TopToBottom);

        Assert.That(Route.Points[1].X, Is.EqualTo(100));
        Assert.That(Route.Points[1].Y + DiagramGridRouteBuilder.CORNER_RADIUS_PX, Is.LessThanOrEqualTo(210));
        Assert.That(DiagramGridLayout.IsInsideGroup(Contract, "Inner", "Outer"), Is.True);
    }

    [Test]
    public void TranslateTrack_RejectsAnObstacleWithoutChangingTheRoute()
    {
        FlowchartLayoutEdgeRoute Route = new() { SourceId = "Source", TargetId = "Target" };
        foreach ((double X, double Y) in new[] { (0d, 100d), (50d, 100d), (50d, 0d), (0d, 0d) })
        {
            Route.Points.Add(new() { X = X, Y = Y });
        }
        List<DiagramGridLayout.GridNode> Obstacles =
        [
            new() { Id = "Obstacle", X = 75, Y = 50, Width = 20, Height = 20 }
        ];

        Assert.That(DiagramGridLayout.TryTranslateInternalRun(Route, 1, true, 25, Obstacles), Is.False);
        Assert.That(Route.Points.Select(Point => (Point.X, Point.Y)),
            Is.EqualTo(new[] { (0d, 100d), (50d, 100d), (50d, 0d), (0d, 0d) }));

        Assert.That(DiagramGridLayout.TryTranslateInternalRun(Route, 1, true, 6, Obstacles), Is.True);
        Assert.That(Route.Points.Select(Point => (Point.X, Point.Y)),
            Is.EqualTo(new[] { (0d, 100d), (56d, 100d), (56d, 0d), (0d, 0d) }));
    }

    [Test]
    public void RebuildRoutes_AlignedSidePorts_LeaveAndEnterNormally()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nA[Source] --> B[Target]");
        FlowchartLayoutEdgeRoute Route = new()
        {
            SourceId = "A", TargetId = "B", ExitSide = "right", EntrySide = "right"
        };
        Route.Points.Add(new() { X = 175, Y = 400 });
        Route.Points.Add(new() { X = 175, Y = 100 });
        FlowchartLayoutResult Layout = new();
        Layout.EdgeRoutesMutable.Add(Route);
        List<DiagramGridLayout.GridNode> Nodes =
        [
            new() { Id = "A", X = 100, Y = 400, Width = 150, Height = 60 },
            new() { Id = "B", X = 100, Y = 100, Width = 150, Height = 60 }
        ];

        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Nodes,
            Contract.Nodes.ToDictionary(Node => Node.Id), Contract, [], FlowchartDirection.TopToBottom);

        Assert.That(Route.Points.Count, Is.GreaterThanOrEqualTo(4));
        Assert.Multiple(() =>
        {
            Assert.That(Route.Points[1].Y, Is.EqualTo(Route.Points[0].Y));
            Assert.That(Route.Points[1].X - Route.Points[0].X, Is.GreaterThanOrEqualTo(40));
            Assert.That(Route.Points[^2].Y, Is.EqualTo(Route.Points[^1].Y));
            Assert.That(Route.Points[^2].X - Route.Points[^1].X, Is.GreaterThanOrEqualTo(30));
        });
    }
}
