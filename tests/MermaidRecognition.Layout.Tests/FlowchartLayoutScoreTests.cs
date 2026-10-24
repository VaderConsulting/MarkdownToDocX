using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartLayoutScoreTests
{
    [Test]
    public void EachBendAddsTenPoints()
    {
        Assert.That(FlowchartLayoutScore.BEND_PENALTY, Is.EqualTo(10.0));
    }

    [Test]
    public void ScoreWeightsAndParametersComeFromCanonicalRulesYaml()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlowchartLayoutRules.BendScore, Is.EqualTo(10.0));
            Assert.That(FlowchartLayoutRules.CrossingScore, Is.EqualTo(20.0));
            Assert.That(FlowchartLayoutRules.DisconnectedEndScore, Is.EqualTo(20.0));
            Assert.That(FlowchartLayoutRules.DiagramUsableWidthPx, Is.EqualTo(548.0));
            Assert.That(FlowchartLayoutRules.EmptyPointsLow, Is.EqualTo(10.0));
            Assert.That(FlowchartLayoutRules.EmptyThreshold, Is.EqualTo(30.0));
            Assert.That(FlowchartLayoutRules.EmptyPointsHigh, Is.EqualTo(40.0));
            Assert.That(FlowchartLayoutRules.NodeGroupHintScore, Is.EqualTo(-30.0));
            Assert.That(FlowchartLayoutRules.LineHintScore, Is.EqualTo(-15.0));
            Assert.That(FlowchartLayoutRules.CrossEndpointStandoff, Is.EqualTo(2.0));
            Assert.That(FlowchartLayoutRules.GroupMemberMinimumInset, Is.EqualTo(30.0));
        });
    }

    [Test]
    public void RouteCandidateScore_SkipsOnlyTheRouteIndependentArrangementCheck()
    {
        (DiagramContract Contract, FlowchartLayoutResult Layout) = BuildLayout(90, 30);

        FlowchartLayoutScore.Result Full = FlowchartLayoutScore.Measure(Contract, Layout);
        FlowchartLayoutScore.Result Candidate = FlowchartLayoutScore.Measure(
            Contract, Layout, IncludeArrangementCheck: false);

        Assert.Multiple(() =>
        {
            Assert.That(Candidate.Score, Is.EqualTo(Full.Score));
            Assert.That(Candidate.OptimizationScore, Is.EqualTo(Full.OptimizationScore));
            Assert.That(Candidate.Crossings, Is.EqualTo(Full.Crossings));
            Assert.That(Candidate.Overlaps, Is.EqualTo(Full.Overlaps));
        });
    }

    [Test]
    public void IncrementalScore_RechecksChangedRouteFaultsAgainstTheFullScorer()
    {
        (DiagramContract Contract, FlowchartLayoutResult Layout) = BuildLayout(90, 30);
        var Context = new FlowchartLayoutScore.IncrementalContext(
            Contract, Layout, IncludeArrangementCheck: false);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single();
        Route.Points[^1].X = 15;

        FlowchartLayoutScore.Result Incremental = Context.MeasureCandidate(Route);
        FlowchartLayoutScore.Result Full = FlowchartLayoutScore.Measure(
            Contract, Layout, IncludeArrangementCheck: false);

        Assert.Multiple(() =>
        {
            Assert.That(Incremental.Score, Is.EqualTo(Full.Score));
            Assert.That(Incremental.OptimizationScore, Is.EqualTo(Full.OptimizationScore));
            Assert.That(Incremental.Crossings, Is.EqualTo(Full.Crossings));
            Assert.That(Incremental.Bends, Is.EqualTo(Full.Bends));
            Assert.That(Incremental.Disconnections, Is.EqualTo(Full.Disconnections));
            Assert.That(Incremental.Violations.Select(FailureKey).OrderBy(Key => Key, StringComparer.Ordinal),
                Is.EqualTo(Full.Violations.Select(FailureKey).OrderBy(Key => Key, StringComparer.Ordinal)));
            Assert.That(Incremental.Violations.Any(Failure => Failure.Kind == "LineShape"), Is.True);
        });

        Context.CommitCurrent(Route);
        Route.Points[^1].X = 0;
        Incremental = Context.MeasureCandidate(Route);
        Full = FlowchartLayoutScore.Measure(Contract, Layout, IncludeArrangementCheck: false);
        Assert.Multiple(() =>
        {
            Assert.That(Incremental.Score, Is.EqualTo(Full.Score));
            Assert.That(Incremental.OptimizationScore, Is.EqualTo(Full.OptimizationScore));
            Assert.That(Incremental.Crossings, Is.EqualTo(Full.Crossings));
            Assert.That(Incremental.Bends, Is.EqualTo(Full.Bends));
            Assert.That(Incremental.Disconnections, Is.EqualTo(Full.Disconnections));
            Assert.That(Incremental.Violations.Select(FailureKey).OrderBy(Key => Key, StringComparer.Ordinal),
                Is.EqualTo(Full.Violations.Select(FailureKey).OrderBy(Key => Key, StringComparer.Ordinal)));
            Assert.That(Incremental.Violations.Any(Failure => Failure.Kind == "LineShape"), Is.False);
        });
    }

    [Test]
    public void CrossingCount_UsesIndependentRoutesAndIgnoresSharedGraphEndpoints()
    {
        var Layout = new FlowchartLayoutResult();
        Layout.EdgeRoutesMutable.Add(Route("vertical", "a", "b", (5, 0), (5, 10)));
        Layout.EdgeRoutesMutable.Add(Route("horizontal", "c", "d", (0, 5), (10, 5)));
        Layout.EdgeRoutesMutable.Add(Route("shared", "a", "e", (5, 0), (15, 0)));

        Assert.That(FlowchartLayoutScore.CountCrossings(Layout), Is.EqualTo(1));
    }

    [Test]
    public void CrossingCount_HandlesManyRoutePairs()
    {
        var Layout = new FlowchartLayoutResult();
        const int TrackCount = 32;
        for (int Index = 0; Index < TrackCount; Index++)
        {
            double Track = Index * 10;
            Layout.EdgeRoutesMutable.Add(Route("v" + Index, "vs" + Index, "vt" + Index,
                (Track, -10), (Track, 320)));
            Layout.EdgeRoutesMutable.Add(Route("h" + Index, "hs" + Index, "ht" + Index,
                (-10, Track), (320, Track)));
        }

        Assert.That(FlowchartLayoutScore.CountCrossings(Layout), Is.EqualTo(TrackCount * TrackCount));
    }

    [Test]
    public void DisconnectedRelationshipEnds_AddTwentyPointsEach()
    {
        (DiagramContract Contract, FlowchartLayoutResult Connected) = BuildLayout(90, 30);
        (_, FlowchartLayoutResult TargetDisconnected) = BuildLayout(90, 45);
        (_, FlowchartLayoutResult BothDisconnected) = BuildLayout(75, 45);

        FlowchartLayoutScore.Result ConnectedScore = FlowchartLayoutScore.Measure(Contract, Connected);
        FlowchartLayoutScore.Result TargetScore = FlowchartLayoutScore.Measure(Contract, TargetDisconnected);
        FlowchartLayoutScore.Result BothScore = FlowchartLayoutScore.Measure(Contract, BothDisconnected);

        Assert.Multiple(() =>
        {
            Assert.That(ConnectedScore.Disconnections, Is.Zero);
            Assert.That(TargetScore.Disconnections, Is.EqualTo(1));
            Assert.That(BothScore.Disconnections, Is.EqualTo(2));
            Assert.That(TargetScore.Score, Is.EqualTo(ConnectedScore.Score + 20.0).Within(0.01));
            Assert.That(BothScore.Score, Is.EqualTo(ConnectedScore.Score + 40.0).Within(0.01));
        });
    }

    private static (DiagramContract Contract, FlowchartLayoutResult Layout) BuildLayout(
        double SourceEndpointY, double TargetEndpointY)
    {
        var Contract = new DiagramContract { Kind = "flowchart", Direction = "TB" };
        Contract.NodesMutable.Add(new NodeContract { Id = "source", Shape = "rect" });
        Contract.NodesMutable.Add(new NodeContract { Id = "target", Shape = "rect" });
        Contract.EdgesMutable.Add(new EdgeContract
        {
            Id = "edge", SourceId = "source", TargetId = "target", MarkerEnd = EdgeEndpointMarker.Point
        });

        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            { NodeId = "source", CenterX = 0, CenterY = 120, Width = 120, Height = 60 });
        Layout.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            { NodeId = "target", CenterX = 0, CenterY = 0, Width = 120, Height = 60 });
        var Route = new FlowchartLayoutEdgeRoute
        {
            EdgeId = "edge", SourceId = "source", TargetId = "target",
            ExitSide = "bottom", EntrySide = "top"
        };
        Route.Points.Add(new FlowchartLayoutPoint { X = 0, Y = SourceEndpointY });
        Route.Points.Add(new FlowchartLayoutPoint { X = 0, Y = TargetEndpointY });
        Layout.EdgeRoutesMutable.Add(Route);
        return (Contract, Layout);
    }

    private static FlowchartLayoutEdgeRoute Route(string EdgeId, string SourceId, string TargetId,
        (double X, double Y) Start, (double X, double Y) End)
    {
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = EdgeId, SourceId = SourceId, TargetId = TargetId };
        Route.Points.Add(new FlowchartLayoutPoint { X = Start.X, Y = Start.Y });
        Route.Points.Add(new FlowchartLayoutPoint { X = End.X, Y = End.Y });
        return Route;
    }

    private static string FailureKey(FlowchartLayoutQualityFailure Failure) =>
        Failure.Kind + "|" + Failure.Description + "|" + string.Join(",", Failure.InvolvedIds)
        + "|" + Failure.Justification;
}
