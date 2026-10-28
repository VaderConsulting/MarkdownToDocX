using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks allocation during construction, preserving exact terminals and the caller's unpublished seed.</summary>
[TestFixture]
public sealed class FallbackCorridorTrackTests
{
    /// <summary>Parallel line clearance uses the new pixel parameter without altering the independent grid-track setting.</summary>
    [Test]
    public void LineSpacingReadsPixelsWithoutRescalingTrackSpacing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlowchartLayoutRules.LineSpacing, Is.EqualTo(15));
            Assert.That(FlowchartLayoutRules.TrackSpacing, Is.EqualTo(30));
            Assert.That(DiagramGridMetrics.GRID_UNIT_PX, Is.EqualTo(30));
        });
    }

    /// <summary>The close-pair check detects subminimum parallel runs and permits exact or greater separation.</summary>
    /// <param name="Distance">Perpendicular distance in unscaled page pixels.</param>
    /// <param name="Expected">Whether the overlapping runs violate the configured clearance.</param>
    [TestCase(14.5, true), TestCase(15.0, false), TestCase(15.5, false)]
    public void ClosePairThresholdUsesPixelLineSpacing(double Distance, bool Expected)
    {
        Assert.That(FlowchartFallbackCorridorTracks.CloseParallelSegments(
            [(0, 0), (0, 100)], [(Distance, 25), (Distance, 75)]) > 0, Is.EqualTo(Expected));
    }

    /// <summary>Reserved corridor tracks separate coincident runs in all four directions without adding corners or crossings.</summary>
    /// <param name="Turns">Number of quarter turns applied to the fixed geometry.</param>
    [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
    public void AllocatesFreeTrackBeforePublicationWithoutChangingPorts(int Turns)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\nA --> C\nB --> D");
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = [];
        foreach ((string? Id, double X, double Y) in new[] { ("A", 0d, 0d), ("B", 0d, 180d), ("C", 300d, 480d), ("D", 300d, 300d) })
        {
            (double X, double Y) P = Rotate((X, Y), Turns);
            Bounds.Add(Id, new() { NodeId = Id, CenterX = P.X, CenterY = P.Y,
                Width = Turns % 2 == 0 ? 150 : 90, Height = Turns % 2 == 0 ? 90 : 150 });
        }
        (double X, double Y)[][] Paths = [
            [(75, 0), (150, 0), (150, 480), (225, 480)],
            [(75, 180), (150, 180), (150, 300), (225, 300)] ];
        FlowchartLayoutEdgeRoute[] Routes = [.. Contract.Edges.Select((Edge, Index) =>
        {
            (double X, double Y) Normal = Rotate((1, 0), Turns);
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = Side(Normal), EntrySide = Side((-Normal.X, -Normal.Y)),
                ExitNormalX = Normal.X, ExitNormalY = Normal.Y, EntryNormalX = -Normal.X, EntryNormalY = -Normal.Y };
            foreach ((double X, double Y) P in Paths[Index].Select(Point => Rotate(Point, Turns))) { Route.Points.Add(new() { X = P.X, Y = P.Y }); } return Route;
        })];
        List<(double X, double Y)> Seed = [.. Routes[0].Points.Select(Point => (Point.X, Point.Y))];
        (double X, double Y)[] Original = [.. Seed];
        (double X, double Y)[] Other = [.. Routes[1].Points.Select(Point => (Point.X, Point.Y))];
        (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Seed, Other);
        Assert.That(Before.Overlap, Is.GreaterThan(0));
        List<(double X, double Y)> Result = FlowchartFallbackCorridorTracks.Allocate(Contract, Bounds, Routes[0], Seed, Routes, null);
        (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Result, Other);
        (double X, double Y) Axis = Rotate((1, 0), Turns);
        Assert.Multiple(() =>
        {
            Assert.That(After.Overlap, Is.Zero);
            Assert.That(After.Crossings, Is.LessThanOrEqualTo(Before.Crossings));
            Assert.That(Result.Count, Is.EqualTo(Original.Length));
            Assert.That(Result[0], Is.EqualTo(Original[0]));
            Assert.That(Result[^1], Is.EqualTo(Original[^1]));
            Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Routes[0], Result), Is.True);
            Assert.That(Math.Abs(((Result[1].X - Other[1].X) * Axis.X) + ((Result[1].Y - Other[1].Y) * Axis.Y)),
                Is.GreaterThanOrEqualTo(FlowchartLayoutRules.LineSpacing));
            Assert.That(((Result[1].X * Axis.X) + (Result[1].Y * Axis.Y)) % FlowchartLayoutRules.LineSpacing, Is.Zero);
            Assert.That(Seed, Is.EqualTo(Original), "A rejected or selected allocation must never mutate the cached seed.");
            Assert.That(Routes[0].Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Original));
        });
    }

    /// <summary>Reserved corridor tracks separate distinct but under-spaced runs in all four directions without adding corners or crossings.</summary>
    /// <param name="Turns">Number of quarter turns applied to the fixed geometry.</param>
    [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
    public void AllocatesSeparatedTrackForCloseParallelRuns(int Turns)
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\nA --> C\nB --> D");
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = [];
        foreach ((string? Id, double X, double Y) in new[] { ("A", 0d, 0d), ("B", 0d, 180d), ("C", 300d, 480d), ("D", 300d, 300d) })
        {
            (double X, double Y) P = Rotate((X, Y), Turns);
            Bounds.Add(Id, new() { NodeId = Id, CenterX = P.X, CenterY = P.Y,
                Width = Turns % 2 == 0 ? 150 : 90, Height = Turns % 2 == 0 ? 90 : 150 });
        }
        (double X, double Y)[][] Paths = [
            [(75, 0), (150, 0), (150, 480), (225, 480)],
            [(75, 180), (157.5, 180), (157.5, 300), (225, 300)] ];
        FlowchartLayoutEdgeRoute[] Routes = [.. Contract.Edges.Select((Edge, Index) =>
        {
            (double X, double Y) Normal = Rotate((1, 0), Turns);
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = Side(Normal), EntrySide = Side((-Normal.X, -Normal.Y)),
                ExitNormalX = Normal.X, ExitNormalY = Normal.Y, EntryNormalX = -Normal.X, EntryNormalY = -Normal.Y };
            foreach ((double X, double Y) P in Paths[Index].Select(Point => Rotate(Point, Turns))) { Route.Points.Add(new() { X = P.X, Y = P.Y }); } return Route;
        })];
        List<(double X, double Y)> Seed = [.. Routes[0].Points.Select(Point => (Point.X, Point.Y))];
        (double X, double Y)[] Original = [.. Seed];
        (double X, double Y)[] Other = [.. Routes[1].Points.Select(Point => (Point.X, Point.Y))];
        (int Crossings, double Overlap) Before = FlowchartBranchRouteOrder.Intersections(Seed, Other);
        Assert.That(Before.Overlap, Is.Zero);
        Assert.That(FlowchartFallbackCorridorTracks.CloseParallelSegments(Seed, Other), Is.GreaterThan(0));
        List<(double X, double Y)> Result = FlowchartFallbackCorridorTracks.Allocate(Contract, Bounds, Routes[0], Seed, Routes, null);
        (int Crossings, double Overlap) After = FlowchartBranchRouteOrder.Intersections(Result, Other);
        (double X, double Y) Axis = Rotate((1, 0), Turns);
        Assert.Multiple(() =>
        {
            Assert.That(After.Overlap, Is.Zero);
            Assert.That(FlowchartFallbackCorridorTracks.CloseParallelSegments(Result, Other), Is.Zero);
            Assert.That(After.Crossings, Is.LessThanOrEqualTo(Before.Crossings));
            Assert.That(Result.Count, Is.EqualTo(Original.Length));
            Assert.That(Result[0], Is.EqualTo(Original[0]));
            Assert.That(Result[^1], Is.EqualTo(Original[^1]));
            Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Routes[0], Result), Is.True);
            Assert.That(Math.Abs(((Result[1].X - Other[1].X) * Axis.X) + ((Result[1].Y - Other[1].Y) * Axis.Y)),
                Is.GreaterThanOrEqualTo(FlowchartLayoutRules.LineSpacing));
            Assert.That(((Result[1].X * Axis.X) + (Result[1].Y * Axis.Y)) % FlowchartLayoutRules.LineSpacing, Is.Zero);
            Assert.That(Seed, Is.EqualTo(Original), "A rejected or selected allocation must never mutate the cached seed.");
            Assert.That(Routes[0].Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(Original));
        });
    }

    /// <summary>Exploratory placement and route repair keep their existing geometry and ordering inputs.</summary>
    /// <param name="Stage">Existing named pipeline stage.</param>
    /// <param name="Expected">Whether settled final ports may consume the reserved tracks here.</param>
    [TestCase("joint-port-allocation-4.7", false)]
    [TestCase("coupled-route-repair", false)]
    [TestCase("model-route-completion-and-rule-checks", true)]
    public void ReservedTrackAllocationRunsOnlyDuringModelConstruction(string Stage, bool Expected)
    {
        FlowchartRenderContext Context = new FlowchartRenderContext { ActiveLayoutStage = Stage };
        Assert.That(FlowchartFallbackCorridorTracks.IsConstructionStage(Context), Is.False);
        Context.BandSpaceDiagnostics.Add("INITIAL_CORRIDOR_CAPACITY|columns=True|after=0");
        Context.HasInitialCorridorCapacity = true;
        Assert.That(FlowchartFallbackCorridorTracks.IsConstructionStage(Context), Is.EqualTo(Expected));
    }

    /// <summary>Equal-rank leaf ports nest when a crossing is measured, and repeated allocation preserves that validated order.</summary>
    [Test]
    public void EqualRankFanKeepsItsCorrectedPortOrderOnRepeatedAllocation()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart LR\nHub --> Near\nHub --> Far");
        FlowchartRenderContext Context = new FlowchartRenderContext { ActiveLayoutStage = "model-route-completion-and-rule-checks" };
        Context.BandSpaceDiagnostics.Add("INITIAL_CORRIDOR_CAPACITY|columns=True|after=0");
        Context.HasInitialCorridorCapacity = true;
        FlowchartLayoutResult Layout = new FlowchartLayoutResult { RenderContext = Context };
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = [];
        foreach ((string? Id, double X, double Y) in new[] { ("Hub", 0d, 0d), ("Near", 300d, -180d), ("Far", 600d, -180d) })
        {
            FlowchartLayoutNodeBounds Box = new FlowchartLayoutNodeBounds { NodeId = Id, CenterX = X, CenterY = Y, Width = 150, Height = 90 };
            Bounds.Add(Id, Box); Layout.NodeBoundsMutable.Add(Box);
        }
        foreach (EdgeContract Edge in Contract.Edges)
        {
            double Y = Edge.TargetId == "Near" ? 15 : -15;
            double X = Bounds[Edge.TargetId].CenterX;
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = "right", EntrySide = "top", ExitNormalX = 1, EntryNormalY = 1 };
            foreach ((double, double) P in new[] { (75d, Y), (X, Y), (X, -135d) })
            {
                Route.Points.Add(new() { X = P.Item1, Y = P.Item2 });
            }

            Layout.EdgeRoutesMutable.Add(Route);
        }
        Dictionary<string, NodeContract> Nodes = Contract.Nodes.ToDictionary(Node => Node.Id);
        FlowchartLayoutEdgeRoute Near = Layout.EdgeRoutes.Single(Route => Route.TargetId == "Near");
        FlowchartLayoutEdgeRoute Far = Layout.EdgeRoutes.Single(Route => Route.TargetId == "Far");
        Assert.That(FlowchartBranchRouteOrder.Intersections(Near.Points.Select(P => (P.X, P.Y)).ToArray(),
            Far.Points.Select(P => (P.X, P.Y)).ToArray()).Crossings, Is.EqualTo(1));
        DiagramGridLayout.ReallocateBendRepairPorts(Contract, Layout, Bounds, Nodes, "Hub");
        Assert.That(Far.Points[0].Y, Is.GreaterThan(Near.Points[0].Y));
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            Route.Points[1].Y = Route.Points[0].Y;
        }

        DiagramGridLayout.ReallocateBendRepairPorts(Contract, Layout, Bounds, Nodes, "Hub");
        Assert.That(Far.Points[0].Y, Is.GreaterThan(Near.Points[0].Y), "A second allocation must not undo nesting.");
    }

    /// <summary>Rotates one immutable fixed-geometry point in model coordinates.</summary>
    /// <param name="Point">Original point or direction.</param>
    /// <param name="Turns">Quarter-turn count.</param>
    /// <returns>The transformed value without rounding.</returns>
    private static (double X, double Y) Rotate((double X, double Y) Point, int Turns)
    {
        for (int Index = 0; Index < Turns; Index++)
        {
            Point = (-Point.Y, Point.X);
        }

        return Point;
    }

    /// <summary>Maps a model-space cardinal normal to the allocated side name.</summary>
    /// <param name="Normal">Unit outward direction.</param>
    /// <returns>Corresponding rectangle side.</returns>
    private static string Side((double X, double Y) Normal) => Normal.X > 0 ? "right" : Normal.X < 0 ? "left" : Normal.Y > 0 ? "top" : "bottom";
}
