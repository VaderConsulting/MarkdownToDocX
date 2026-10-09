using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class DiagramGridTerminalRouteTests
{
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
