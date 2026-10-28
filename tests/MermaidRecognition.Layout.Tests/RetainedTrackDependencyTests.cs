using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
[NonParallelizable]
public sealed class RetainedTrackDependencyTests
{
    [TestCase(1)]
    [TestCase(2)]
    public void NewCrossingExpandsOnlyTheDemonstratedFrozenDependency(int Limit)
    {
        var Session = Fixture();
        var Before = Session.CopySnapshot();
        Assert.That(FlowchartRouteRuleChecker.Check(Before.RoutingModel, Before.Layout), Is.Empty);
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        DiagramLayoutSession.Update Candidate;
        try { Candidate = Session.PrepareAddRelationship(0, "new", new()
            { SourceId = "A", TargetId = "B", RoutingHint = Exact() }, RouteLimit: Limit); }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
        Assert.That(Placements, Is.Zero);
        Assert.That(Candidate.AffectedRouteIds, Is.EquivalentTo(new[] { "new", "frozen" }), Candidate.FailureReason);
        if (Limit == 1)
        {
            Assert.That(Candidate.FailureReason, Does.Contain("limit"));
            Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { "new" }));
            Assert.That(Session.TryCommit(Candidate, out _), Is.False);
            Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Geometry(Before.Layout)));
            return;
        }
        Commit(Session, Candidate);
        var After = Session.CopySnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(Candidate.RebuiltRouteIds, Is.EquivalentTo(Candidate.AffectedRouteIds));
            Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
            Assert.That(Path(After.Layout, "independent"), Is.EqualTo(Path(Before.Layout, "independent")));
            Assert.That(Path(After.Layout, "frozen"), Is.Not.EqualTo(Path(Before.Layout, "frozen")));
            Assert.That(FlowchartRouteRuleChecker.Check(After.RoutingModel, After.Layout), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AnUnchangedBaselineCrossingDoesNotExpandTheDependencySet(bool ReverseDeclarations)
    {
        var Seed = Fixture().CopySnapshot();
        const string Id = "existing x relationship";
        Seed.RoutingModel.EdgesMutable.Add(new() { Id = Id, SourceId = "A", TargetId = "B", RoutingHint = Exact() });
        var A = Seed.Layout.NodeBounds.Single(Box => Box.NodeId == "A");
        var B = Seed.Layout.NodeBounds.Single(Box => Box.NodeId == "B");
        var Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = "A", TargetId = "B", ExitSide = "right", EntrySide = "left",
            ExitNormalX = 1, EntryNormalX = -1 };
        Route.Points.Add(new() { X = A.CenterX + A.Width / 2, Y = A.CenterY });
        Route.Points.Add(new() { X = B.CenterX - B.Width / 2, Y = B.CenterY });
        Seed.Layout.EdgeRoutesMutable.Add(Route);
        if (ReverseDeclarations) Seed.RoutingModel.EdgesMutable.Reverse();
        Seed.Layout.BumpGeometryVersion();
        Assert.That(FlowchartRouteRuleChecker.Check(Seed.RoutingModel, Seed.Layout).Select(Item => Item.Rule), Is.EqualTo(new[] { "5.9" }));
        var Session = new DiagramLayoutSession("baseline", 0, Seed.RoutingModel, Seed.Layout);
        var Candidate = Session.PreparePorts(0, Id, Exact(), RouteLimit: 1);
        Commit(Session, Candidate);
        Assert.Multiple(() =>
        {
            Assert.That(Candidate.AffectedRouteIds, Is.EqualTo(new[] { Id }));
            Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { Id }));
            Assert.That(Path(Session.CopyLayout(), "frozen"), Is.EqualTo(Path(Seed.Layout, "frozen")));
            Assert.That(Path(Session.CopyLayout(), "independent"), Is.EqualTo(Path(Seed.Layout, "independent")));
            Assert.That(FlowchartRouteRuleChecker.Check(Session.CopySnapshot().RoutingModel, Session.CopyLayout()).Select(Item => Item.Rule), Is.EqualTo(new[] { "5.9" }));
        });
    }

    private static DiagramLayoutSession Fixture()
    {
        var Session = new DiagramLayoutSession("tracks", 0, Direction: "LR");
        Add("A", 3, 0); Add("B", 3, 10); Add("C", 6, 2); Add("D", 0, 8);
        Add("X", 0, 16); Add("Y", 4, 16);
        Commit(Session, Session.PrepareAddRelationship(0, "frozen", new()
        {
            SourceId = "C", TargetId = "D", RoutingHint = new() { Leave = LayoutHintDirection.East, Enter = LayoutHintDirection.East }
        }));
        Commit(Session, Session.PrepareAddRelationship(0, "independent", new() { SourceId = "X", TargetId = "Y" }));
        return Session;
        void Add(string Id, int Row, int Column) => Commit(Session, Session.PrepareAddNode(0,
            new() { Id = Id, Label = "item", Shape = "rect" }, new(Row, Column, 1, 1)));
    }

    private static RoutingHintContract Exact() => new()
        { Leave = LayoutHintDirection.East, Enter = LayoutHintDirection.West, LeavePoint = 0, EnterPoint = 0 };
    private static void Commit(DiagramLayoutSession Session, DiagramLayoutSession.Update Candidate)
    {
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Session.TryCommit(Candidate, out var Reason), Is.True, Reason);
    }
    private static string Geometry(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.EdgeRoutes, Layout.ResolvedEdgeGuides });
    private static string Path(FlowchartLayoutResult Layout, string Id) => JsonSerializer.Serialize(Layout.EdgeRoutes.Single(Route => Route.EdgeId == Id));
}
