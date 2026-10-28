using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class DiagramRouteDiagnosticsTests
{
    [Test]
    public void CrossingsUseStablePartnersAndCountEachPairOnce()
    {
        var (Model, Layout) = Fixture();
        Add(Model, Layout, "vertical x stable", "c", "d", "top", "bottom", (300, -255), (300, 255));
        string Before = JsonSerializer.Serialize(Layout);
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        DiagramRouteReport Report;
        try { Report = DiagramRouteDiagnostics.Measure(Model, Layout); }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
        Assert.Multiple(() =>
        {
            Assert.That(Placements, Is.Zero);
            Assert.That(JsonSerializer.Serialize(Layout), Is.EqualTo(Before));
            Assert.That(Report.CrossingPairs, Is.EqualTo(1));
            Assert.That(Report.SharedTrackPairs, Is.Zero);
            Assert.That(Report.Routes.Single(Route => Route.Identity == "horizontal").CrossingPartners, Is.EqualTo(new[] { "vertical x stable" }));
            Assert.That(Report.Routes.Single(Route => Route.Identity == "vertical x stable").CrossingPartners, Is.EqualTo(new[] { "horizontal" }));
            Assert.That(Report.Routes.All(Route => Route.Assessment == DiagramRouteAssessment.Invalid), Is.True);
        });
        Layout.EdgeRoutes[0].Points.Clear();
        Assert.That(Report.Routes.Single(Route => Route.Identity == "horizontal").LengthPx, Is.EqualTo(450));
    }

    [Test]
    public void ParallelPathsReportSharedTracksWithoutInventingProperCrossings()
    {
        var (Model, Layout) = Fixture();
        Add(Model, Layout, "parallel", "a", "b", "right", "left", (75, 0), (525, 0));
        var Report = DiagramRouteDiagnostics.Measure(Model, Layout);
        Assert.That(Report.SharedTrackPairs, Is.EqualTo(1));
        Assert.That(Report.CrossingPairs, Is.Zero);
        Assert.That(Report.Routes.Single(Route => Route.Identity == "horizontal").SharedTrackPartners, Is.EqualTo(new[] { "parallel" }));
        Assert.That(Report.Routes.All(Route => Route.Findings.Any(Item => Item.Rule == "5.1")), Is.True);
    }

    [Test]
    public void HighBendMeasurementsAloneDoNotProveInvalidityOrAnImprovement()
    {
        var (Model, Layout) = Fixture();
        var Route = Layout.EdgeRoutes.Single();
        Route.Points.Clear();
        Route.Points.AddRange(new[] { (75d, 0d), (150d, 0d), (150d, 180d), (450d, 180d), (450d, 0d), (525d, 0d) }
            .Select(Point => new FlowchartLayoutPoint { X = Point.Item1, Y = Point.Item2 }));
        Layout.BumpGeometryVersion();
        var Measurement = DiagramRouteDiagnostics.Measure(Model, Layout).Routes.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Measurement.Findings, Is.Empty);
            Assert.That(Measurement.Assessment, Is.EqualTo(DiagramRouteAssessment.Investigate));
            Assert.That(Measurement.Bends, Is.EqualTo(4));
            Assert.That(Measurement.LengthPx, Is.EqualTo(810));
            Assert.That(Measurement.ExcessLengthPx, Is.EqualTo(360));
            Assert.That(Measurement.DetourRatio, Is.EqualTo(1.8).Within(1e-9));
        });
    }

    [Test]
    public void MissingAndNonfiniteRoutesRemainVisibleAsInvalidMeasurements()
    {
        var (Model, Layout) = Fixture();
        Layout.EdgeRoutesMutable.Clear();
        Layout.BumpGeometryVersion();
        var Missing = DiagramRouteDiagnostics.Measure(Model, Layout).Routes.Single();
        Assert.That(Missing.Assessment, Is.EqualTo(DiagramRouteAssessment.Invalid));
        Assert.That(Missing.Findings.Select(Item => Item.Rule), Does.Contain("3.1"));
        Assert.That(Missing.LengthPx, Is.Null);
        Model = DiagramSourceModel.Copy(Model);
        Add(Model, Layout, "invalid", "c", "d", "top", "bottom", (double.NaN, 0), (0, 0));
        Layout.BumpGeometryVersion();
        var Invalid = DiagramRouteDiagnostics.Measure(Model, Layout).Routes.Single(Route => Route.Identity == "invalid");
        Assert.That(Invalid.Assessment, Is.EqualTo(DiagramRouteAssessment.Invalid));
        Assert.That(Invalid.LengthPx, Is.Null);
        Assert.That(Invalid.DetourRatio, Is.Null);
    }

    [Test]
    public void InvisibleRelationshipsDoNotPolluteVisibleMeasurements()
    {
        var (Model, Layout) = Fixture();
        Add(Model, Layout, "constraint", "c", "d", "top", "bottom", (300, -255), (300, 255));
        Model.EdgesMutable.Last().Stroke = "invisible";
        Layout.BumpGeometryVersion();
        var Report = DiagramRouteDiagnostics.Measure(Model, Layout);
        Assert.That(Report.Routes.Select(Route => Route.Identity), Is.EqualTo(new[] { "horizontal" }));
        Assert.That(Report.CrossingPairs, Is.Zero);
        Assert.That(Report.Routes.Single().Assessment, Is.EqualTo(DiagramRouteAssessment.Measured));
    }

    [Test]
    public void DuplicateOrMissingIdentitiesCannotProduceMisleadingPartners()
    {
        var (Model, Layout) = Fixture();
        Model.EdgesMutable.Add(new() { Id = "horizontal", SourceId = "c", TargetId = "d" });
        Assert.Throws<ArgumentException>(() => DiagramRouteDiagnostics.Measure(Model, Layout));
        Model.EdgesMutable.Last().Id = null;
        Assert.Throws<ArgumentException>(() => DiagramRouteDiagnostics.Measure(Model, Layout));
    }

    private static (DiagramContract, FlowchartLayoutResult) Fixture()
    {
        var Model = new DiagramContract { Kind = "flowchart", Direction = "LR" };
        var Layout = new FlowchartLayoutResult();
        foreach (var (Id, X, Y) in new[] { ("a", 0d, 0d), ("b", 600d, 0d), ("c", 300d, -300d), ("d", 300d, 300d) })
        {
            Model.NodesMutable.Add(new() { Id = Id, Shape = "rect", Label = "item" });
            Layout.NodeBoundsMutable.Add(new() { NodeId = Id, CenterX = X, CenterY = Y, Width = 150, Height = 90 });
        }
        Add(Model, Layout, "horizontal", "a", "b", "right", "left", (75, 0), (525, 0));
        return (Model, Layout);
    }

    private static void Add(DiagramContract Model, FlowchartLayoutResult Layout, string Id, string Source, string Target,
        string Exit, string Entry, params (double X, double Y)[] Points)
    {
        static LayoutHintDirection Side(string Name) => Name switch { "right" => LayoutHintDirection.East, "left" => LayoutHintDirection.West,
            "top" => LayoutHintDirection.North, _ => LayoutHintDirection.South };
        Model.EdgesMutable.Add(new() { Id = Id, SourceId = Source, TargetId = Target, RoutingHint = new() { Leave = Side(Exit), Enter = Side(Entry) } });
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Source, TargetId = Target, ExitSide = Exit, EntrySide = Entry };
        Route.Points.AddRange(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        Layout.EdgeRoutesMutable.Add(Route);
    }
}
