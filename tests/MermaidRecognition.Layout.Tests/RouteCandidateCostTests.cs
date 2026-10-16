using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks candidate generation and score consistency independently of later repair passes.</summary>
[TestFixture]
public sealed class RouteCandidateCostTests
{
    /// <summary>Reusing prepared obstacles must preserve every terminal candidate, including diamond normals and title strips.</summary>
    [Test]
    public void PreparedTerminalObstaclesPreserveCandidateGeometry()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nsubgraph G\nA[A]\nD{Choice}\nend\nB[B]\nA --> B\nD --> B");
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds> {
            ["A"] = new() { NodeId = "A", CenterX = 0, CenterY = 90, Width = 60, Height = 60 },
            ["D"] = new() { NodeId = "D", CenterX = 0, CenterY = 0, Width = 60, Height = 60 },
            ["B"] = new() { NodeId = "B", CenterX = 240, CenterY = 0, Width = 60, Height = 60 },
            ["G"] = new() { NodeId = "G", CenterX = 0, CenterY = 60, Width = 120, Height = 240 } };
        var Prepared = DiagramGridRouteBuilder.PrepareTerminalObstacles(Contract, Bounds);
        foreach (string Id in new[] { "A", "D" })
        {
            var Start = new FlowchartLayoutPoint { X = 30, Y = Bounds[Id].CenterY };
            var End = new FlowchartLayoutPoint { X = 210, Y = 0 };
            var Original = DiagramGridRouteBuilder.MinimumBendTerminalCandidates(Start, End,
                Bounds[Id], Bounds["B"], "right", "left", Nodes, Bounds, Contract).ToArray();
            var Reused = DiagramGridRouteBuilder.MinimumBendTerminalCandidates(Start, End,
                Bounds[Id], Bounds["B"], "right", "left", Nodes, Bounds, Contract, Prepared).ToArray();
            Assert.That(Original, Is.Not.Empty);
            Assert.That(Reused, Is.EqualTo(Original));
        }
        Assert.That(Bounds.ContainsKey("G"), Is.True, "Preparing obstacles must not mutate authoritative frame bounds.");
    }
    /// <summary>A stored diamond edge normal is a direction and must not scale a pixel clearance.</summary>
    [Test]
    public void DiamondStubNormalMagnitudeDoesNotChangeClearance()
    {
        var Diamond = new FlowchartLayoutNodeBounds { NodeId = "Decision", CenterX = 300, CenterY = 210, Width = 150, Height = 120 };
        var Scaled = DiagramGridRouteBuilder.ResolvePortStub(262, 180, Diamond, "diamond", "left", 20, -120, -150);
        double Magnitude = Math.Sqrt(120 * 120 + 150 * 150);
        var Unit = DiagramGridRouteBuilder.ResolvePortStub(262, 180, Diamond, "diamond", "left", 20,
            -120 / Magnitude, -150 / Magnitude);
        Assert.That(Scaled.X, Is.EqualTo(Unit.X).Within(0.000001));
        Assert.That(Scaled.Y, Is.EqualTo(Unit.Y).Within(0.000001));
        Assert.That(Math.Sqrt(Math.Pow(Scaled.X - 262, 2) + Math.Pow(Scaled.Y - 180, 2)), Is.EqualTo(20).Within(0.000001));
    }

    /// <summary>A cached straight route must be discarded when an unrelated obstacle moves into its track.</summary>
    [Test]
    public void AutomaticRouteCacheIncludesUnrelatedObstacleGeometry()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] --> B[B]\nC[C]");
        var Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        var Placed = new List<DiagramGridLayout.GridNode> {
            new() { Id = "A", X = 0, Y = 0, Width = 60, Height = 60 },
            new() { Id = "B", X = 300, Y = 0, Width = 60, Height = 60 },
            new() { Id = "C", X = 150, Y = 150, Width = 60, Height = 60 } };
        var Layout = new FlowchartLayoutResult();
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Contract.Edges[0].Id!, SourceId = "A", TargetId = "B",
            ExitSide = "right", EntrySide = "left", ExitNormalX = 1, EntryNormalX = -1 };
        ReplacePoints(Route, [(30, 0), (270, 0)]);
        Layout.EdgeRoutesMutable.Add(Route);
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Placed, Nodes, Contract, [], FlowchartDirection.LeftToRight);
        Assert.That(Route.Points.Count, Is.EqualTo(2));
        Placed[2].Y = 0;
        DiagramGridRouteBuilder.RebuildAllRoutes(Layout, Placed, Nodes, Contract, [], FlowchartDirection.LeftToRight);
        Assert.That(Route.Points.Count, Is.GreaterThan(2));
        var Obstacle = new FlowchartLayoutNodeBounds { NodeId = "C", CenterX = 150, CenterY = 0, Width = 60, Height = 60 };
        Assert.That(Route.Points.Zip(Route.Points.Skip(1)).Any(Pair => FlowchartRouteObstacleHelper.SegmentIntersectsNodeInterior(
            Pair.First.X, Pair.First.Y, Pair.Second.X, Pair.Second.Y, Obstacle)), Is.False);
    }

    /// <summary>Boundary sweeping must count exactly the cells marked by the original occupancy definition.</summary>
    [Test]
    public void OccupiedAreaSweepMatchesInclusiveGridCells()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] --> B[B]\nC[C]");
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.AddRange([
            new() { NodeId = "A", CenterX = 0, CenterY = 0, Width = 60, Height = 60 },
            new() { NodeId = "B", CenterX = 330, CenterY = 210, Width = 120, Height = 60 },
            new() { NodeId = "C", CenterX = 150, CenterY = 90, Width = 60, Height = 120 }]);
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Contract.Edges[0].Id!, SourceId = "A", TargetId = "B" };
        ReplacePoints(Route, [(30, 0), (450, 0), (450, 210), (270, 210)]);
        Layout.EdgeRoutesMutable.Add(Route);
        const int Columns = 16, Rows = 9;
        var Used = new HashSet<(int Column, int Row)>();
        foreach (var Box in Layout.NodeBounds)
            Mark(Box.CenterX - Box.Width / 2, Box.CenterY - Box.Height / 2,
                Box.CenterX + Box.Width / 2, Box.CenterY + Box.Height / 2);
        foreach (var Pair in Route.Points.Zip(Route.Points.Skip(1)))
            Mark(Math.Min(Pair.First.X, Pair.Second.X) - 15, Math.Min(Pair.First.Y, Pair.Second.Y) - 15,
                Math.Max(Pair.First.X, Pair.Second.X) + 15, Math.Max(Pair.First.Y, Pair.Second.Y) + 15);
        var Score = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
        Assert.That(Score.EmptyShare, Is.EqualTo(1 - Used.Count / (double)(Columns * Rows)).Within(0.0000001));

        // Reference definition intentionally visits cells. Production uses the
        // exact interval union so long excursions do not multiply scoring work.
        /// <summary>Marks the reference definition's inclusive grid cells inside the diagram bounds.</summary>
        /// <param name="Left">The occupied rectangle's minimum X.</param>
        /// <param name="Bottom">The occupied rectangle's minimum Y.</param>
        /// <param name="Right">The occupied rectangle's maximum X.</param>
        /// <param name="Top">The occupied rectangle's maximum Y.</param>
        void Mark(double Left, double Bottom, double Right, double Top)
        {
            for (int Column = 0; Column < Columns; Column++)
            for (int Row = 0; Row < Rows; Row++)
                if (Column >= Math.Floor((Left + 30) / 30) && Column <= Math.Floor((Right + 30) / 30)
                    && Row >= Math.Floor((Bottom + 30) / 30) && Row <= Math.Floor((Top + 30) / 30)) Used.Add((Column, Row));
        }
    }

    /// <summary>A diamond terminal must be compared as its actual diagonal, not as a horizontal run at its first Y.</summary>
    [Test]
    public void Intersections_UsesActualDiamondTerminalGeometry()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlowchartBranchRouteOrder.Intersections([(0, 0), (100, 100)], [(20, 0), (80, 0)]),
                Is.EqualTo((0, 0.0)), "The separated horizontal run shares no diagonal track.");
            Assert.That(FlowchartBranchRouteOrder.Intersections([(0, 0), (100, 100)], [(50, 50), (150, 150)]).Overlap,
                Is.EqualTo(Math.Sqrt(5000)).Within(0.001));
            Assert.That(FlowchartBranchRouteOrder.Intersections([(0, 0), (100, 100)], [(0, 100), (100, 0)]).Crossings,
                Is.EqualTo(1));
        });
    }

    /// <summary>Hard legality must detect a straight route through an unrelated node even if its ports are correct.</summary>
    [Test]
    public void RouteChecker_RejectsNodeInteriorsAndUndeclaredPaths()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] --> B[B]\nC[C]");
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "A", CenterX = 0, CenterY = 0, Width = 60, Height = 60 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "B", CenterX = 300, CenterY = 0, Width = 60, Height = 60 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "C", CenterX = 150, CenterY = 0, Width = 60, Height = 60 });
        var Route = new FlowchartLayoutEdgeRoute
        {
            EdgeId = Contract.Edges.Single().Id!, SourceId = "A", TargetId = "B", ExitSide = "right", EntrySide = "left"
        };
        ReplacePoints(Route, [(30, 0), (270, 0)]);
        Layout.EdgeRoutesMutable.Add(Route);
        var Phantom = new FlowchartLayoutEdgeRoute { EdgeId = "phantom", SourceId = "C", TargetId = "C" };
        ReplacePoints(Phantom, [(150, 30), (180, 60), (150, 30)]);
        Layout.EdgeRoutesMutable.Add(Phantom);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.Multiple(() =>
        {
            Assert.That(Findings.Any(Finding => Finding.Rule == "10" && Finding.Subject == Route.EdgeId
                && Finding.Description.Contains("node 'C'")), Is.True);
            Assert.That(Findings.Any(Finding => Finding.Rule == "10" && Finding.Subject == "phantom"), Is.True);
        });
    }

    /// <summary>Declared routes sharing a graph endpoint must still own separate positive-length tracks.</summary>
    [Test]
    public void RouteChecker_RejectsSharedTerminalRuns()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] --> B[B]\nA --> C[C]");
        var Layout = new FlowchartLayoutResult();
        foreach (var Edge in Contract.Edges)
        {
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId };
            ReplacePoints(Route, [(0, 0), (100, 0), (100, Edge.TargetId == "B" ? 100 : -100)]);
            Layout.EdgeRoutesMutable.Add(Route);
        }
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Count(Finding => Finding.Rule == "5.1"), Is.EqualTo(1));
    }

    /// <summary>Changing a diagonal's occupied cells must affect incremental and full scores identically.</summary>
    [Test]
    public void IncrementalSummary_RecomputesOccupiedAreaAfterRouteChanges()
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nS{Choose} --> T[Target]");
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "S", CenterX = 0, CenterY = 0, Width = 200, Height = 200 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "T", CenterX = 300, CenterY = -350, Width = 100, Height = 100 });
        var Route = new FlowchartLayoutEdgeRoute
        {
            EdgeId = Contract.Edges.Single().Id!, SourceId = "S", TargetId = "T",
            ExitSide = "right", EntrySide = "top", ExitNormalX = 1, ExitNormalY = -1, EntryNormalY = 1
        };
        ReplacePoints(Route, [(50, -50), (100, -100), (100, -200), (300, -200), (300, -300)]);
        Layout.EdgeRoutesMutable.Add(Route);
        var Context = new FlowchartLayoutScore.IncrementalContext(Contract, Layout, IncludeArrangementCheck: false);
        ReplacePoints(Route, [(50, -50), (200, -200), (300, -200), (300, -300)]);
        AssertSummaryMatchesFull();
        Context.CommitCurrent(Route);
        ReplacePoints(Route, [(50, -50), (100, -100), (100, -200), (300, -200), (300, -300)]);
        AssertSummaryMatchesFull();

        // Check both directions: committing the candidate must not silently
        // change which occupancy baseline the next comparison uses.
        void AssertSummaryMatchesFull()
        {
            var Summary = Context.MeasureCandidateSummary(Route, null);
            var Full = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
            Assert.That(Summary.OptimizationScore, Is.EqualTo(Full.OptimizationScore).Within(0.000001));
            Assert.That(Summary.Score, Is.EqualTo(Full.Score));
        }
    }

    /// <summary>The initial builder must offer the extended square departure without relying on final repair.</summary>
    [Test]
    public void InitialSearch_ExtendsDiamondNormalToTheTwoBendBridge()
    {
        var Contract = MermaidRecognizer.Parse("flowchart TB\nS{Choose} --> T[Target]");
        var Source = new FlowchartLayoutNodeBounds { NodeId = "S", CenterX = 0, CenterY = 0, Width = 200, Height = 200 };
        var Target = new FlowchartLayoutNodeBounds { NodeId = "T", CenterX = 300, CenterY = -350, Width = 100, Height = 100 };
        var Bounds = new Dictionary<string, FlowchartLayoutNodeBounds> { ["S"] = Source, ["T"] = Target };
        var Points = DiagramGridRouteBuilder.BuildRoute(50, -50, Source, "diamond", "right",
            300, -300, Target, "rect", "top", Bounds, "S", "T", Contract,
            FlowchartDirection.TopToBottom, Horizontal: false);
        Assert.That(Points, Has.Count.EqualTo(4), "The fixed-stub candidate has an avoidable third bend.");
        Assert.That(Points[1].X - 50, Is.EqualTo(-(Points[1].Y + 50)).Within(0.001));
        Assert.That(Points[1].Y, Is.EqualTo(Points[2].Y).Within(0.001));
        Assert.That(Points[2].X, Is.EqualTo(300).Within(0.001));
    }

    /// <summary>Replaces a test route while retaining the identity used by the incremental scorer.</summary>
    /// <param name="Route">The route owned by the test layout.</param>
    /// <param name="Points">The candidate coordinates.</param>
    private static void ReplacePoints(FlowchartLayoutEdgeRoute Route, (double X, double Y)[] Points)
    {
        Route.Points.Clear();
        foreach (var Point in Points) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
    }
}
