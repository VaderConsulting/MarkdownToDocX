using System.Text.Json;
using MermaidRecognition.Contract;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the five requested bend faults on fixed production-owned cell state; full movement activation is separately gated by catalogue results.</summary>
[TestFixture, NonParallelizable]
public sealed class FinishedBendGeometryTests
{
    /// <summary>A neighbour retains a straight connection when two other lines use the same source side.</summary>
    /// <param name="Direction">Declared TB or LR routing policy; the fixture's cell reservations stay fixed.</param>
    [TestCase("TB"), TestCase("LR")]
    public void RowNeighbourGetsAStraightPortBeforeOtherBranches(string Direction)
    {
        var (Contract, Layout) = Fixture(Direction, [("a", 0, 0), ("b", 0, 2), ("c", 2, 2), ("d", 4, 2)]);
        AddLine(Contract, Layout, "ab", "a", "b", "right", "left", 0.25);
        AddLine(Contract, Layout, "ac", "a", "c", "right", "left", 0.5);
        AddLine(Contract, Layout, "ad", "a", "d", "right", "left", 0.75);
        Repair(Contract, Layout);
        var Line = Layout.EdgeRoutes.Single(Route => Route.EdgeId == "ab");
        Assert.That(Line.Points, Has.Count.EqualTo(2));
        Assert.That(Line.Points[0].Y, Is.EqualTo(Line.Points[^1].Y));
    }

    /// <summary>Both links from a gap to its row neighbours use the facing sides without a hump above the row.</summary>
    /// <param name="Direction">Declared TB or LR policy on fixed row reservations.</param>
    [TestCase("TB"), TestCase("LR")]
    public void PlateauGapPlateauLinksStayInTheirRow(string Direction)
    {
        var (Contract, Layout) = Fixture(Direction, [("p", 0, 0), ("g", 0, 1), ("q", 0, 2)]);
        AddLine(Contract, Layout, "gp", "g", "p", "top", "top");
        AddLine(Contract, Layout, "gq", "g", "q", "bottom", "bottom");
        FlowchartLayoutAlternatives.RejectionObserver = TestContext.Out.WriteLine;
        try { Repair(Contract, Layout); }
        finally { FlowchartLayoutAlternatives.RejectionObserver = null; }
        TestContext.Out.WriteLine(JsonSerializer.Serialize(Layout.EdgeRoutes));
        foreach (var Line in Layout.EdgeRoutes)
        {
            Assert.That(Line.Points, Has.Count.EqualTo(2), Line.EdgeId);
            Assert.That(Line.ExitSide, Is.EqualTo(Line.TargetId == "p" ? "left" : "right"));
            Assert.That(Line.Points.All(Point => Point.Y == Layout.NodeBounds[0].CenterY), Is.True);
        }
    }

    /// <summary>A one-column S-jog is removed by an atomic whole-cell move, with exactly derived final bounds.</summary>
    /// <param name="Direction">Declared TB or LR policy on the fixed staggered pair.</param>
    [TestCase("TB"), TestCase("LR")]
    public void WholeCellMoveStraightensAStaggeredPair(string Direction)
    {
        var (Contract, Layout) = Fixture(Direction, [("a", 0, 0), ("b", 2, 1)]);
        AddLine(Contract, Layout, "ab", "a", "b", "bottom", "top");
        Repair(Contract, Layout);
        Assert.That(Layout.NodeBounds[0].CenterX, Is.EqualTo(Layout.NodeBounds[1].CenterX));
        Assert.That(Layout.EdgeRoutes.Single().Points, Has.Count.EqualTo(2));
        foreach (var Node in Layout.CellNodes!)
        {
            var Derived = Node.CellNode!.Bounds();
            var Published = Layout.NodeBounds.Single(Box => Box.NodeId == Node.Id);
            Assert.That((Published.CenterX, Published.CenterY), Is.EqualTo((Derived.CenterX, Derived.CenterY)));
        }
    }

    /// <summary>An avoidable outside-margin hump is replaced by the straight legal route inside the node envelope.</summary>
    /// <param name="Direction">Declared TB or LR policy on fixed row reservations.</param>
    [TestCase("TB"), TestCase("LR")]
    public void LegalInteriorRouteNeverUsesTheOutsideMargin(string Direction)
    {
        var (Contract, Layout) = Fixture(Direction, [("a", 0, 0), ("b", 0, 2)]);
        var Line = AddLine(Contract, Layout, "ab", "a", "b", "right", "left");
        var A = Line.Points[0]; var B = Line.Points[^1];
        double Third = (B.X - A.X) / 3;
        double Above = Layout.NodeBounds.Max(Box => Box.CenterY + Box.Height / 2) + FlowchartLayoutRules.TrackSpacing;
        Line.Points.Clear();
        Line.Points.AddRange(new[] { (A.X, A.Y), (A.X + Third, A.Y), (A.X + Third, Above),
            (B.X - Third, Above), (B.X - Third, B.Y), (B.X, B.Y) }.Select(Point => new FlowchartLayoutPoint { X = Point.Item1, Y = Point.Item2 }));
        Repair(Contract, Layout);
        Assert.That(Line.Points, Has.Count.EqualTo(2));
        Assert.That(Line.Points.All(Point => Point.Y <= Layout.NodeBounds.Max(Box => Box.CenterY + Box.Height / 2)
            && Point.Y >= Layout.NodeBounds.Min(Box => Box.CenterY - Box.Height / 2)), Is.True);
    }

    /// <summary>Four cardinal neighbours enter the hub's facing sides with no more than one corner each.</summary>
    /// <param name="Direction">Declared TB or LR policy on the same cardinal reservations.</param>
    [TestCase("TB"), TestCase("LR")]
    public void CardinalSourcesEnterTheFacingHubSides(string Direction)
    {
        var (Contract, Layout) = Fixture(Direction, [("hub", 2, 2), ("left", 2, 0), ("right", 2, 4), ("top", 0, 2), ("bottom", 4, 2)]);
        foreach (var Pair in new[] { ("left", "right"), ("right", "left"), ("top", "bottom"), ("bottom", "top") })
            AddLine(Contract, Layout, Pair.Item1, Pair.Item1, "hub", Pair.Item2, Pair.Item2);
        FlowchartLayoutAlternatives.RejectionObserver = TestContext.Out.WriteLine;
        try { Repair(Contract, Layout); }
        finally { FlowchartLayoutAlternatives.RejectionObserver = null; }
        TestContext.Out.WriteLine(JsonSerializer.Serialize(Layout.EdgeRoutes));
        foreach (var Line in Layout.EdgeRoutes)
        {
            Assert.That(Line.EntrySide, Is.EqualTo(Line.SourceId), Line.EdgeId);
            Assert.That(FlowchartLayoutScore.CountCorners(Line.Points), Is.LessThanOrEqualTo(1), Line.EdgeId);
        }
    }

    /// <summary>Runs the shared finished-model repair stages and checks that every published route remains declared and legal.</summary>
    /// <param name="Contract">Fixture declarations.</param>
    /// <param name="Layout">Fixed cell-owned input with deliberately inefficient routes.</param>
    private static void Repair(DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        using var Active = FlowchartRenderContext.Activate(Layout.RenderContext);
        FlowchartBendRemovingPortRepair.Apply(Contract, Layout);
        FlowchartFinishedLayoutBendRepair.Apply(Contract, Layout);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "10" or "4.1" or "5.1"), Is.Empty);
    }

    /// <summary>Builds cell-owned rectangular nodes with the current production dimensions and routing coordinate boundary.</summary>
    /// <param name="Direction">Declared direction, independent of the fixed cell reservations.</param>
    /// <param name="Cells">Unique identities and complete starting rows and columns.</param>
    /// <returns>Production-owned state whose pixel bounds are projections of whole cells.</returns>
    internal static (DiagramContract Contract, FlowchartLayoutResult Layout) Fixture(string Direction, (string Id, int Row, int Column)[] Cells)
    {
        var Contract = new DiagramContract { Kind = "flowchart", Direction = Direction };
        var Layout = new FlowchartLayoutResult { UsesGridRoutes = true, RenderContext = new() { UseCellRegions = true } };
        var Grid = new FlowchartLogicalGrid(FlowchartLogicalGridConfiguration.FromRules());
        foreach (var Cell in Cells) Assert.That(Grid.TryPlace(Cell.Id, new(Cell.Row, Cell.Column, 1, 1)), Is.True);
        var Nodes = new List<DiagramGridLayout.GridNode>();
        foreach (var Cell in Cells)
        {
            Contract.NodesMutable.Add(new NodeContract { Id = Cell.Id, Shape = "rect" });
            var Node = new DiagramGridLayout.GridNode { Id = Cell.Id };
            Node.BindCells(Grid, Layout.RenderContext); Node.CellNode!.UseRoutingCoordinates(0);
            Nodes.Add(Node); Layout.NodeBoundsMutable.Add(Node.CellNode.Bounds());
        }
        Layout.CellNodes = Nodes;
        return (Contract, Layout);
    }

    /// <summary>Allocates explicit legal outline ports and constructs an inefficient but legal input route with the existing router.</summary>
    /// <param name="Contract">Fixture topology.</param>
    /// <param name="Layout">Owning routing model.</param>
    /// <param name="Id">Unique declared link identity.</param>
    /// <param name="Source">Source node.</param>
    /// <param name="Target">Target node.</param>
    /// <param name="Exit">Initial allocated source side.</param>
    /// <param name="Entry">Initial allocated target side.</param>
    /// <param name="Fraction">Exact source slot; the target starts at its centre.</param>
    /// <returns>The independently owned declared route.</returns>
    internal static FlowchartLayoutEdgeRoute AddLine(DiagramContract Contract, FlowchartLayoutResult Layout,
        string Id, string Source, string Target, string Exit, string Entry, double Fraction = 0.5)
    {
        Contract.EdgesMutable.Add(new EdgeContract { Id = Id, SourceId = Source, TargetId = Target });
        var Bounds = Layout.NodeBounds.ToDictionary(Box => Box.NodeId);
        var A = Port(Bounds[Source], Exit, Fraction); var B = Port(Bounds[Target], Entry, 0.5);
        var Line = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Source, TargetId = Target, ExitSide = Exit, EntrySide = Entry,
            ExitNormalX = A.Nx, ExitNormalY = A.Ny, EntryNormalX = B.Nx, EntryNormalY = B.Ny };
        Line.Points.Add(new() { X = A.X, Y = A.Y }); Line.Points.Add(new() { X = B.X, Y = B.Y });
        var Path = FlowchartAllocatedPortFallback.Build(Contract, Bounds, Contract.Nodes.ToDictionary(Node => Node.Id), Line, true);
        Assert.That(Path.Count, Is.GreaterThanOrEqualTo(2), "The deliberately inefficient fixture must start with a real legal route.");
        Line.Points.Clear(); Line.Points.AddRange(Path.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        Layout.EdgeRoutesMutable.Add(Line);
        return Line;
    }

    /// <summary>Projects a rectangular connection-boundary slot and its outward normal in routing coordinates.</summary>
    /// <param name="Box">Fixed cell-derived rectangle.</param>
    /// <param name="Side">Allocated side.</param>
    /// <param name="Fraction">Fraction along the declared boundary side.</param>
    /// <returns>Exact position and outward normal.</returns>
    private static (double X, double Y, int Nx, int Ny) Port(FlowchartLayoutNodeBounds Box, string Side, double Fraction) => Side switch
    {
        "left" => (Box.CenterX - Box.Width / 2, Box.CenterY + Box.Height * (0.5 - Fraction), -1, 0),
        "right" => (Box.CenterX + Box.Width / 2, Box.CenterY + Box.Height * (0.5 - Fraction), 1, 0),
        "top" => (Box.CenterX + Box.Width * (Fraction - 0.5), Box.CenterY + Box.Height / 2, 0, 1),
        _ => (Box.CenterX + Box.Width * (Fraction - 0.5), Box.CenterY - Box.Height / 2, 0, -1)
    };
}
