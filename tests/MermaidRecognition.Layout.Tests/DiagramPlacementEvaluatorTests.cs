using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class DiagramPlacementEvaluatorTests
{
    [Test]
    public void ExplorationTriesPathsFirstThenEndpointsAndDemonstratedObstructionsWithinItsBound()
    {
        var Session = Fixture(Obstacle: true);
        var Snapshot = Session.CopySnapshot();
        string Before = Geometry(Snapshot.Layout);
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        DiagramPlacementExploration Result;
        try { Result = new DiagramPlacementEvaluator(Snapshot).Explore("ab"); }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
        Assert.Multiple(() =>
        {
            Assert.That(Placements, Is.Zero);
            Assert.That(Result.Trials, Has.Length.EqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            Assert.That(Result.BudgetExhausted, Is.True);
            Assert.That(Result.Trials[0].Kind, Is.EqualTo(DiagramPlacementTrialKind.RouteOnly));
            Assert.That(Result.Trials.Skip(1).All(Trial => Trial.Kind == DiagramPlacementTrialKind.NodeMove), Is.True);
            Assert.That(Result.Trials.SelectMany(Trial => Trial.Targets.Keys).Distinct(), Is.EquivalentTo(new[] { "a", "b", "obstacle" }));
            Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        });
        foreach (var Trial in Result.Trials.Where(Trial => Trial.FailureReason == null))
        {
            var Candidate = Trial.CopyCandidate()!;
            Assert.That(FlowchartRouteRuleChecker.Check(Candidate.RoutingModel, Candidate.Layout), Is.Empty);
            Assert.That(Component(Candidate.Layout), Is.EqualTo(Component(Snapshot.Layout)));
            var Cells = Candidate.Layout.CellNodes!.Select(Node => Node.CellNode!.Region).ToArray();
            for (int A = 0; A < Cells.Length; A++) for (int B = A + 1; B < Cells.Length; B++) Assert.That(Cells[A].Overlaps(Cells[B]), Is.False);
        }
    }

    [Test]
    public void ExplicitLocksAndMandatoryHintsAreRespectedWithoutMovingTheirDependents()
    {
        var Snapshot = Fixture().CopySnapshot();
        Snapshot.RoutingModel.Nodes.Single(Node => Node.Id == "b").PlacementHint = new() { Kind = PlacementHintKind.At, CellRow = 5, CellColumn = 2 };
        Snapshot.Layout.CellNodes!.Single(Node => Node.Id == "b").PlacementHintLocked = true;
        var Evaluator = new DiagramPlacementEvaluator(Snapshot, new[] { "a" });
        var Result = Evaluator.Explore("ab");
        Assert.That(Result.Trials, Has.Length.EqualTo(1));
        Assert.That(Result.BudgetExhausted, Is.False);
        Assert.That(Result.SkippedNodes.Keys, Is.EquivalentTo(new[] { "a", "b" }));
        Assert.That(Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(1, 2, 1, 1) }).FailureReason, Does.Contain("locked"));
        Assert.That(Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["b"] = new(6, 2, 1, 1) }).FailureReason, Does.Contain("locked"));
    }

    [Test]
    public void RejectedOccupiedMovesAndRouteBudgetFailuresHaveNoPublishableCandidate()
    {
        var Session = Fixture();
        Add(Session, "c", 4, 4);
        Commit(Session, Session.PrepareAddRelationship(0, "ac", new() { SourceId = "a", TargetId = "c" }));
        var Snapshot = Session.CopySnapshot();
        var Evaluator = new DiagramPlacementEvaluator(Snapshot);
        var Occupied = Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(5, 2, 1, 1) });
        Assert.That(Occupied.FailureReason, Is.Not.Null);
        Assert.That(Occupied.CopyCandidate(), Is.Null);
        var Limited = Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(1, 2, 1, 1) }, RouteLimit: 1);
        Assert.That(Limited.FailureReason, Does.Contain("limit"));
        Assert.That(Limited.AffectedRouteIds, Has.Length.EqualTo(2));
        Assert.That(Limited.RebuiltRouteIds, Is.Empty);
        Assert.That(Limited.CopyCandidate(), Is.Null);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Geometry(Snapshot.Layout)));
    }

    [Test]
    public void CallerAndCandidateCopiesCannotMutateOtherTrialsOrTheirProvenance()
    {
        var Snapshot = Fixture().CopySnapshot() with { DocumentId = "owned", SourceRevision = 9, Revision = 27 };
        var Evaluator = new DiagramPlacementEvaluator(Snapshot);
        Snapshot.Layout.EdgeRoutes[0].Points.Clear();
        Snapshot.RoutingModel.NodesMutable.Clear();
        var Trial = Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["b"] = new(6, 2, 1, 1) });
        Assert.That(Trial.FailureReason, Is.Null);
        Assert.That(Trial.Origin, Is.EqualTo(new DiagramEvaluationOrigin("owned", 9, 27)));
        var First = Trial.CopyCandidate()!;
        string Before = Geometry(First.Layout);
        First.Layout.EdgeRoutes[0].Points.Clear();
        First.Layout.CellNodes![0].CellNode!.Grid.TryMove(new[] { "a" }, 90, 90);
        Assert.That(Geometry(Trial.CopyCandidate()!.Layout), Is.EqualTo(Before));
    }

    [TestCase(0)]
    [TestCase(int.MaxValue)]
    public void CandidateBudgetCannotBecomeAnUnboundedSearch(int Budget)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DiagramPlacementEvaluator(Fixture().CopySnapshot()).Explore("ab", Budget));

    [Test]
    public void AOneTrialBudgetDoesNotEnterNodeMovement()
    {
        var Result = new DiagramPlacementEvaluator(Fixture().CopySnapshot()).Explore("ab", 1);
        Assert.That(Result.Trials.Select(Trial => Trial.Kind), Is.EqualTo(new[] { DiagramPlacementTrialKind.RouteOnly }));
        Assert.That(Result.BudgetExhausted, Is.True);
    }

    [Test]
    public void RouteFirstTrialsKeepMandatoryExactTerminalsIntact()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Edge = Snapshot.RoutingModel.Edges.Single(Edge => Edge.Id == "ab");
        Edge.RoutingHint = new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North, LeavePoint = 0, EnterPoint = 0 };
        var Result = new DiagramPlacementEvaluator(Snapshot, new[] { "a", "b" }).Explore("ab");
        var Trial = Result.Trials.Single();
        Assert.That(Trial.FailureReason, Is.Null);
        var Candidate = Trial.CopyCandidate()!;
        Assert.That(JsonSerializer.Serialize(Candidate.RoutingModel.Edges.Single(Item => Item.Id == "ab").RoutingHint), Is.EqualTo(JsonSerializer.Serialize(Edge.RoutingHint)));
        Assert.That(FlowchartRouteRuleChecker.Check(Candidate.RoutingModel, Candidate.Layout), Is.Empty);
    }

    [Test]
    public void ExplicitMovesAcceptIndependentRowAndColumnChangesAndKeepOrthogonalRoutes()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["b"] = new(6, 4, 1, 1) });
        Assert.That(Trial.FailureReason, Is.Null);
        var Candidate = Trial.CopyCandidate()!;
        Assert.That(Candidate.Layout.CellNodes!.Single(Node => Node.Id == "b").CellNode!.Region, Is.EqualTo(new FlowchartCellRegion(6, 4, 1, 1)));
        Assert.That(Component(Candidate.Layout), Is.EqualTo(Component(Snapshot.Layout)));
        Assert.That(FlowchartRouteRuleChecker.Check(Candidate.RoutingModel, Candidate.Layout), Is.Empty);
        Assert.That(Candidate.Layout.EdgeRoutes.All(Route => Route.Points.Zip(Route.Points.Skip(1)).All(Pair =>
            Pair.First.X == Pair.Second.X || Pair.First.Y == Pair.Second.Y)), Is.True);
    }

    [Test]
    public void AFixedRelativeDependentPreventsMovingItsPeerInsideTheRequiredGap()
    {
        var Snapshot = Fixture().CopySnapshot();
        Snapshot.RoutingModel.Nodes.Single(Node => Node.Id == "b").PlacementHint = new() { Kind = PlacementHintKind.Below, PeerId = "a", Offset = 2 };
        Snapshot.Layout.CellNodes!.Single(Node => Node.Id == "b").PlacementHintLocked = true;
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(3, 2, 1, 1) });
        Assert.That(Trial.FailureReason, Does.Contain("constraints"));
        Assert.That(Trial.CopyCandidate(), Is.Null);
    }

    [Test]
    public void ContainedMemberTrialsPreserveNestedFramesAndRejectLeavingThem()
    {
        var Snapshot = Grouped();
        string Frames = JsonSerializer.Serialize(Snapshot.Layout.ClusterBounds);
        var Evaluator = new DiagramPlacementEvaluator(Snapshot);
        var Inside = Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(3, 2, 1, 1) });
        Assert.That(Inside.FailureReason, Is.Null);
        var Candidate = Inside.CopyCandidate()!;
        Assert.That(JsonSerializer.Serialize(Candidate.Layout.ClusterBounds), Is.EqualTo(Frames));
        Assert.That(Candidate.RoutingModel.Nodes.Single(Node => Node.Id == "a").ParentGroupId, Is.EqualTo("inner"));
        Assert.That(FlowchartRouteRuleChecker.Check(Candidate.RoutingModel, Candidate.Layout), Is.Empty);
        var Outside = Evaluator.EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(-1, 2, 1, 1) });
        Assert.That(Outside.FailureReason, Does.Contain("containing group"));
        Assert.That(Outside.CopyCandidate(), Is.Null);
        var TallTitle = Grouped();
        TallTitle.RoutingModel.Groups.Single(Group => Group.Id == "inner").Title = string.Join("\n", Enumerable.Repeat("Members", 10));
        var Title = new DiagramPlacementEvaluator(TallTitle).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(1, 2, 1, 1) });
        Assert.That(Title.FailureReason, Does.Contain("title band"));
        Assert.That(Title.CopyCandidate(), Is.Null);
    }

    private static DiagramLayoutSession.Snapshot Grouped()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Model = DiagramSourceModel.Copy(Snapshot.RoutingModel);
        var Outer = new GroupContract { Id = "outer", Title = "Container" };
        var Inner = new GroupContract { Id = "inner", Title = "Members", ParentGroupId = "outer" };
        Outer.ChildGroupIdsMutable.Add("inner");
        foreach (string Id in new[] { "a", "b" })
        {
            Inner.ChildNodeIdsMutable.Add(Id);
            Model.Nodes.Single(Node => Node.Id == Id).ParentGroupId = "inner";
            Snapshot.Layout.CellNodes!.Single(Node => Node.Id == Id).GroupId = "inner";
        }
        Model.GroupsMutable.AddRange(new[] { Outer, Inner });
        Snapshot.Layout.ClusterBoundsMutable.Add(new() { NodeId = "outer", CenterX = 525, CenterY = -525, Width = 1050, Height = 1500 });
        Snapshot.Layout.ClusterBoundsMutable.Add(new() { NodeId = "inner", CenterX = 525, CenterY = -570, Width = 750, Height = 1110 });
        return Snapshot with { RoutingModel = Model };
    }

    private static DiagramLayoutSession Fixture(bool Obstacle = false)
    {
        var Session = new DiagramLayoutSession("placement", 0);
        Add(Session, "a", 2, 2); Add(Session, "b", 5, 2);
        if (Obstacle) Add(Session, "obstacle", 4, 2);
        Add(Session, "x", 0, 12); Add(Session, "y", 4, 12);
        Commit(Session, Session.PrepareAddRelationship(0, "ab", new() { SourceId = "a", TargetId = "b" }));
        Commit(Session, Session.PrepareAddRelationship(0, "xy", new() { SourceId = "x", TargetId = "y" }));
        return Session;
    }
    private static void Add(DiagramLayoutSession Session, string Id, int Row, int Column)
        => Commit(Session, Session.PrepareAddNode(0, new() { Id = Id, Label = "item", Shape = "rect" }, new(Row, Column, 1, 1)));
    private static void Commit(DiagramLayoutSession Session, DiagramLayoutSession.Update Candidate)
    { Assert.That(Candidate.FailureReason, Is.Null); Assert.That(Session.TryCommit(Candidate, out var Reason), Is.True, Reason); }
    private static string Geometry(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes });
    private static string Component(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new
    { Nodes = Layout.NodeBounds.Where(Node => Node.NodeId is "x" or "y"), Routes = Layout.EdgeRoutes.Where(Route => Route.EdgeId == "xy") });
}
