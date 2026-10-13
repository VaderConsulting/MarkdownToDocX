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
    public void RouteCandidateScore_SkipsOnlyTheRouteIndependentArrangementCheck()
    {
        (DiagramContract Contract, FlowchartLayoutResult Layout) = BuildLayout(90, 30);

        FlowchartLayoutScore.Result Full = FlowchartLayoutScore.Measure(Contract, Layout);
        FlowchartLayoutScore.Result Candidate = FlowchartLayoutScore.Measure(
            Contract, Layout, IncludeArrangementCheck: false);

        Assert.Multiple(() =>
        {
            Assert.That(Candidate.Score, Is.EqualTo(Full.Score));
            Assert.That(Candidate.Crossings, Is.EqualTo(Full.Crossings));
            Assert.That(Candidate.Overlaps, Is.EqualTo(Full.Overlaps));
        });
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
}
