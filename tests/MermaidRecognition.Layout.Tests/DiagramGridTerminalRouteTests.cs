using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridTerminalRouteTests
{
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
