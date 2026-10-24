using System.Text.Json;
using MermaidRecognition.Contract;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks that rejected complete movement alternatives leave cell ownership, gaps, ports, paths and frame bounds exact.</summary>
[TestFixture, NonParallelizable]
public sealed class FinishedBendCandidateRollbackTests
{
    /// <summary>A fixed row/column convention rejects both alignment moves without publishing a partial region or route.</summary>
    /// <param name="Direction">Declared TB or LR policy.</param>
    [TestCase("TB"), TestCase("LR")]
    public void FixedBandReservationsRejectMovementWithoutAnyStateChange(string Direction)
    {
        var (Contract, Layout) = FinishedBendGeometryTests.Fixture(Direction, [("a", 0, 0), ("b", 2, 1)]);
        var Line = FinishedBendGeometryTests.AddLine(Contract, Layout, "ab", "a", "b", "bottom", "top");
        var Grid = Layout.CellNodes![0].CellNode!.Grid;
        Grid.RegionConvention = Regions => Regions["a"].StartRow == 0 && Regions["a"].StartColumn == 0
            && Regions["b"].StartRow == 2 && Regions["b"].StartColumn == 1;
        AssertRejectedExactly(Contract, Layout, Line);
    }

    /// <summary>A straightening move that adds an unrelated crossing is rejected before any cells, routes or guides are published.</summary>
    /// <param name="Direction">Declared TB or LR policy.</param>
    [TestCase("TB"), TestCase("LR")]
    public void RemoteCrossingRejectsAnOtherwiseStraightCellMove(string Direction)
    {
        var (Contract, Layout) = FinishedBendGeometryTests.Fixture(Direction, [("a", 0, 0), ("b", 2, 1), ("c", 1, 0), ("d", 1, 2)]);
        var Line = FinishedBendGeometryTests.AddLine(Contract, Layout, "ab", "a", "b", "bottom", "top");
        FinishedBendGeometryTests.AddLine(Contract, Layout, "cd", "c", "d", "right", "left");
        var A = Layout.NodeBounds.Single(Box => Box.NodeId == "a");
        var B = Layout.NodeBounds.Single(Box => Box.NodeId == "b");
        var C = Layout.NodeBounds.Single(Box => Box.NodeId == "c");
        double Gap = Layout.CellNodes![0].CellNode!.Grid.Configuration.RowGap;
        double Outside = A.CenterX - A.Width / 2 - Gap;
        double FirstTurn = A.CenterY - A.Height / 2 - Gap / 2;
        double LastTurn = C.CenterY - C.Height / 2 - Gap / 2;
        Line.Points.Clear();
        Line.Points.AddRange(new[] { (A.CenterX, A.CenterY - A.Height / 2), (A.CenterX, FirstTurn), (Outside, FirstTurn),
            (Outside, LastTurn), (B.CenterX, LastTurn), (B.CenterX, B.CenterY + B.Height / 2) }
            .Select(Point => new FlowchartLayoutPoint { X = Point.Item1, Y = Point.Item2 }));
        Layout.CellNodes.Single(Node => Node.Id == "b").PlacementHintLocked = true;
        Layout.CellNodes[0].CellNode!.Grid.RegionConvention = Regions => Regions["a"].StartRow == 0;
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "10" or "5.1"), Is.Empty);
        AssertRejectedExactly(Contract, Layout, Line);
    }

    /// <summary>Captures all mutable drawing and cell state, including a declared empty frame and nonempty published guides, before rejection.</summary>
    /// <param name="Contract">Declared fixed geometry and honoured placement constraints.</param>
    /// <param name="Layout">Production-owned state used only by this regression.</param>
    /// <param name="Line">Bent line whose move must be rejected.</param>
    private static void AssertRejectedExactly(DiagramContract Contract, FlowchartLayoutResult Layout, FlowchartLayoutEdgeRoute Line)
    {
        Contract.GroupsMutable.Add(new GroupContract { Id = "empty" });
        Layout.ClusterBoundsMutable.Add(new() { NodeId = "empty", CenterX = 10000, CenterY = 10000, Width = 150, Height = 90 });
        Layout.ResolvedEdgeGuides = Contract.Edges.Select(Edge => new FlowchartResolvedGuide(Edge,
            Layout.EdgeRoutes.Single(Route => Route.EdgeId == Edge.Id).Points.Select(Point => (Point.X, Point.Y)).ToList(), FlowchartEdgeRouteKind.Grid)).ToList();
        Layout.RenderContext.CachedRouteGuides = Layout.ResolvedEdgeGuides;
        var Guides = Layout.RenderContext.CachedRouteGuides;
        var Regions = Layout.CellNodes![0].CellNode!.Grid.Capture();
        string Before = JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes, Regions.RowGaps, Regions.ColumnGaps });
        using var Active = FlowchartRenderContext.Activate(Layout.RenderContext);
        var Candidate = FlowchartLayoutAlternatives.Straighten(Contract, Layout, Line);
        Assert.That(Candidate, Is.Null);
        Assert.That(Layout.CellNodes[0].CellNode!.Grid.Capture(), Is.SameAs(Regions));
        Assert.That(Layout.RenderContext.CachedRouteGuides, Is.SameAs(Guides));
        Assert.That(JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes, Regions.RowGaps, Regions.ColumnGaps }), Is.EqualTo(Before));
    }
}
