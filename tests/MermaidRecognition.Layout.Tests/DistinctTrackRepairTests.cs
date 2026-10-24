using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Protects the joint overlap/crossing admission and the distinction between terminal and travel hints.</summary>
[TestFixture]
public sealed class DistinctTrackRepairTests
{
    /// <summary>An internal shared track can move without changing its allocated fractional ports or terminal hints.</summary>
    /// <param name="Hinted">Whether entry and exit sides are explicitly requested.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void InternalTrackSeparatesWithoutNewCrossingOrTerminalChange(bool Hinted)
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] --> C[C]\nB[B] --> D[D]");
        if (Hinted) foreach (var Edge in Contract.Edges)
            Edge.RoutingHint = new RoutingHintContract { Leave = LayoutHintDirection.East, Enter = LayoutHintDirection.West };
        var Layout = new FlowchartLayoutResult();
        var Placed = new List<DiagramGridLayout.GridNode>();
        foreach (var (Id, X, Y) in new[] { ("A", 0, 0), ("B", 0, 60), ("C", 300, 0), ("D", 300, -60) })
        {
            Layout.NodeBoundsMutable.Add(new() { NodeId = Id, CenterX = X, CenterY = Y, Width = 60, Height = 30 });
            Placed.Add(new() { Id = Id, X = X, Y = Y, Width = 60, Height = 30 });
        }
        var Paths = new (double X, double Y)[][] {
            [(30, 0), (60, 0), (60, 150), (240, 150), (240, 0), (270, 0)],
            [(30, 60), (90, 60), (90, 150), (210, 150), (210, -60), (270, -60)] };
        for (int Index = 0; Index < Paths.Length; Index++)
        {
            var Edge = Contract.Edges[Index];
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId,
                TargetId = Edge.TargetId, ExitSide = "right", EntrySide = "left", ExitNormalX = 1, EntryNormalX = -1 };
            foreach (var Point in Paths[Index]) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            Layout.EdgeRoutesMutable.Add(Route);
        }
        var Before = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.That(Before.Count(Finding => Finding.Rule == "5.1"), Is.EqualTo(1));
        Assert.That(FlowchartDistinctTrackRepair.Apply(Contract, Layout, Placed), Is.True);
        var After = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.That(After.Where(Finding => Finding.Rule is "5.1" or "10" or "3.1" or "4.1"), Is.Empty);
        Assert.That(After.Count(Finding => Finding.Rule == "5.9"),
            Is.LessThanOrEqualTo(Before.Count(Finding => Finding.Rule == "5.9")));
        for (int Index = 0; Index < Paths.Length; Index++)
        {
            var Route = Layout.EdgeRoutes[Index];
            Assert.That((Route.Points[0].X, Route.Points[0].Y), Is.EqualTo(Paths[Index][0]));
            Assert.That((Route.Points[^1].X, Route.Points[^1].Y), Is.EqualTo(Paths[Index][^1]));
            Assert.That((Route.ExitSide, Route.EntrySide), Is.EqualTo(("right", "left")));
        }
    }
}
