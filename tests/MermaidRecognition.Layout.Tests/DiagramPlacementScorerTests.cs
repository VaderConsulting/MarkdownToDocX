using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class DiagramPlacementScorerTests
{
    [Test]
    public void CompleteMeasurementsMatchIndependentFullScoringAndRetainExactUnrelatedGeometry()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["b"] = new(3, 0, 1, 1) });
        var Scorer = new DiagramPlacementScorer(Snapshot, PaddingPx: 20);
        var Comparison = Scorer.Compare(Trial);
        Assert.That(Comparison.IsImprovement, Is.True, Comparison.Reason);
        var Candidate = Trial.CopyCandidate()!;
        var Full = FlowchartLayoutScore.Measure(Candidate.RoutingModel, Candidate.Layout, false);
        Assert.Multiple(() =>
        {
            Assert.That(Comparison.After!.Canonical.Cost, Is.EqualTo(Full.Cost));
            Assert.That(Comparison.After.Canonical.Bends, Is.EqualTo(Full.Bends));
            Assert.That(Comparison.After.WidthPx, Is.EqualTo(Full.Width + 40));
            Assert.That(Comparison.After.HeightPx, Is.EqualTo(Full.Height + 40));
            Assert.That(Comparison.After.MovedNodes, Is.EqualTo(1));
            Assert.That(Comparison.After.DisplacementCells, Is.EqualTo(2));
            Assert.That(Comparison.After.DisplacementPx, Is.EqualTo(360));
            Assert.That(Comparison.After.Routes.Bends, Is.Zero);
            Assert.That(Comparison.After.Routes.CrossingPairs, Is.Zero);
            Assert.That(Comparison.ChangedRoutes, Is.EqualTo(1));
            Assert.That(Comparison.RepairCharge, Is.EqualTo(FlowchartLayoutRules.RepairMovedLineWeight));
            Assert.That(Independent(Candidate.Layout), Is.EqualTo(Independent(Snapshot.Layout)));
            Assert.That(FlowchartRouteRuleChecker.Check(Candidate.RoutingModel, Candidate.Layout), Is.Empty);
        });
    }

    [Test]
    public void ImprovingTheSelectedRelationshipCannotHideDamageToAnotherIncidentRoute()
    {
        var Snapshot = Fixture(Tail: true).CopySnapshot();
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["b"] = new(3, 0, 1, 1) });
        Assert.That(Trial.FailureReason, Is.Null);
        var Comparison = new DiagramPlacementScorer(Snapshot).Compare(Trial);
        Assert.Multiple(() =>
        {
            Assert.That(Comparison.After!.Routes.Routes.Single(Route => Route.Identity == "ab").Bends,
                Is.LessThan(Comparison.Before.Routes.Routes.Single(Route => Route.Identity == "ab").Bends));
            Assert.That(Comparison.After.Routes.Routes.Single(Route => Route.Identity == "bc").Bends,
                Is.GreaterThan(Comparison.Before.Routes.Routes.Single(Route => Route.Identity == "bc").Bends));
            Assert.That(Comparison.After.Canonical.Bends, Is.EqualTo(Comparison.Before.Canonical.Bends));
            Assert.That(Comparison.IsImprovement, Is.False, Comparison.Reason);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CapturedAhPolicyCanPreferANarrowerLegalCandidateAtAVisibleBendCost(bool Ah)
    {
        var Session = new DiagramLayoutSession("width-policy", 0, Options: new() { EnableWidthConstrainedLayout = Ah, TargetWidthPx = 600 });
        Add(Session, "a", 0, 4); Add(Session, "b", 0, 0);
        Commit(Session, Session.PrepareAddRelationship(0, "ab", new() { SourceId = "a", TargetId = "b",
            RoutingHint = new() { Leave = LayoutHintDirection.West, Enter = LayoutHintDirection.East } }));
        var Snapshot = Session.CopySnapshot();
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["a"] = new(1, 2, 1, 1) });
        Assert.That(Trial.FailureReason, Is.Null);
        var Comparison = new DiagramPlacementScorer(Snapshot).Compare(Trial);
        Assert.Multiple(() =>
        {
            Assert.That(Comparison.After!.WidthPx, Is.LessThanOrEqualTo(600));
            Assert.That(Comparison.Before.WidthPx, Is.GreaterThan(600));
            Assert.That(Comparison.After.Canonical.Bends, Is.GreaterThan(Comparison.Before.Canonical.Bends));
            Assert.That(Comparison.After.Routes.Findings, Is.Empty);
            Assert.That(Comparison.After.Canonical.HintsHonoured, Is.EqualTo(Comparison.Before.Canonical.HintsHonoured));
            Assert.That(Comparison.IsImprovement, Is.EqualTo(Ah), Comparison.Reason);
        });
    }

    [Test]
    public void IndependentCrossingPairTotalsAndCanonicalCostAreNotSummedPerRoute()
    {
        var Session = new DiagramLayoutSession("pairs", 0);
        Add(Session, "a", 3, 0); Add(Session, "b", 3, 6); Add(Session, "c", 6, 3); Add(Session, "d", 0, 3);
        var Snapshot = Session.CopySnapshot();
        var Model = Snapshot.RoutingModel;
        AddPath("horizontal x identity", "a", "b", "right", "left", (150, -495), (1080, -495));
        AddPath("vertical", "c", "d", "top", "bottom", (615, -900), (615, -90));
        var Scorer = new DiagramPlacementScorer(Snapshot);
        Assert.That(Scorer.Before.Routes.CrossingPairs, Is.EqualTo(1));
        Assert.That(Scorer.Before.Routes.Routes.Sum(Route => Route.CrossingPartners.Length), Is.EqualTo(2));
        var Full = FlowchartLayoutScore.Measure(Model, Snapshot.Layout, false);
        Assert.That(Scorer.Before.Canonical.Crossings, Is.EqualTo(Full.Crossings));
        Assert.That(Scorer.Before.Canonical.Cost, Is.EqualTo(Full.Cost));
        void AddPath(string Id, string Source, string Target, string Exit, string Entry, params (double X, double Y)[] Points)
        {
            Model.EdgesMutable.Add(new() { Id = Id, SourceId = Source, TargetId = Target });
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Source, TargetId = Target, ExitSide = Exit, EntrySide = Entry };
            Route.Points.AddRange(Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
            Snapshot.Layout.EdgeRoutesMutable.Add(Route);
        }
    }

    [Test]
    public void StaleOrFailedTrialsCannotBecomeVerifiedImprovements()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Targets = new Dictionary<string, FlowchartCellRegion> { ["b"] = new(3, 0, 1, 1) };
        var Foreign = new DiagramPlacementEvaluator(Snapshot with { Revision = Snapshot.Revision + 1 }).EvaluateMove(Targets);
        var Comparison = new DiagramPlacementScorer(Snapshot).Compare(Foreign);
        Assert.That(Comparison.IsImprovement, Is.False);
        Assert.That(Comparison.Reason, Does.Contain("revision"));
        Assert.That(Comparison.After, Is.Null);
        var Rejected = new DiagramPlacementEvaluator(Snapshot, new[] { "b" }).EvaluateMove(Targets);
        Assert.That(new DiagramPlacementScorer(Snapshot).Compare(Rejected).IsImprovement, Is.False);
    }

    [Test]
    public void AnUnchangedCandidateIsNotAnImprovement()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion> { ["b"] = new(3, 2, 1, 1) });
        var Comparison = new DiagramPlacementScorer(Snapshot).Compare(Trial);
        Assert.That(Comparison.IsImprovement, Is.False);
        Assert.That(Comparison.After!.MovedNodes, Is.Zero);
        Assert.That(Comparison.ChangedRoutes, Is.Zero);
    }

    [Test]
    public void ScopeVerificationDetectsAnAccidentalFrozenRouteWriter()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Session = new DiagramLayoutSession(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.RoutingModel, Snapshot.Layout);
        var Targets = new Dictionary<string, FlowchartCellRegion> { ["b"] = new(3, 0, 1, 1) };
        var Candidate = Session.PrepareMove(Snapshot.SourceRevision, Targets);
        Assert.That(Candidate.FailureReason, Is.Null);
        Candidate.State!.Layout.EdgeRoutes.Single(Route => Route.EdgeId == "xy").Points.Clear();
        var Trial = new DiagramPlacementTrial(new(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision), DiagramPlacementTrialKind.NodeMove, Targets, Candidate, null);
        var Comparison = new DiagramPlacementScorer(Snapshot).Compare(Trial);
        Assert.That(Comparison.IsImprovement, Is.False);
        Assert.That(Comparison.Reason, Does.Contain("outside its routing dependencies"));
    }

    [Test]
    public void APlacementTrialCannotSilentlyChangeRelationshipTopology()
    {
        var Snapshot = Fixture().CopySnapshot();
        var Session = new DiagramLayoutSession(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.RoutingModel, Snapshot.Layout);
        var Targets = new Dictionary<string, FlowchartCellRegion> { ["b"] = new(3, 0, 1, 1) };
        var Candidate = Session.PrepareMove(Snapshot.SourceRevision, Targets);
        Assert.That(Candidate.FailureReason, Is.Null);
        Candidate.State!.Contract.Edges.Single(Edge => Edge.Id == "ab").TargetId = "x";
        var Trial = new DiagramPlacementTrial(new(Snapshot.DocumentId, Snapshot.SourceRevision, Snapshot.Revision), DiagramPlacementTrialKind.NodeMove, Targets, Candidate, null);
        var Comparison = new DiagramPlacementScorer(Snapshot).Compare(Trial);
        Assert.That(Comparison.IsImprovement, Is.False);
        Assert.That(Comparison.Reason, Does.Contain("topology"));
        Assert.That(Comparison.After, Is.Null);
    }

    [Test]
    public void NativeEnterpriseGeometryAndProvidersSurviveTheIndependentScoringBoundary()
    {
        const string Source = "enterprise\napplication-component a\napplication-component b\na -flow-> b\n";
        using var Scope = FlowchartRenderContext.Activate(new() { LayoutOptions = new() { EnableRouteAwareConstruction = true } });
        var Initial = EnterpriseLayoutEngine.Layout(EnterpriseValidator.Analyze(Source).Document!).RoutingContext!;
        var Snapshot = new DiagramLayoutSession("native", 0, Initial.Contract, Initial.Layout).CopySnapshot();
        var Old = Snapshot.Layout.CellNodes!.Single(Node => Node.Id == "b").CellNode!.Region;
        var Trial = new DiagramPlacementEvaluator(Snapshot).EvaluateMove(new Dictionary<string, FlowchartCellRegion>
            { ["b"] = new(Old.StartRow + 1, Old.StartColumn, Old.RowSpan, Old.ColumnSpan) });
        Assert.That(Trial.FailureReason, Is.Null);
        var Comparison = new DiagramPlacementScorer(Snapshot, Initial.Frame.DiagramPaddingPx).Compare(Trial);
        Assert.That(Comparison.After, Is.Not.Null, Comparison.Reason);
        var Candidate = Trial.CopyCandidate()!;
        Assert.That(Comparison.After!.Canonical.Cost, Is.EqualTo(FlowchartLayoutScore.Measure(Candidate.RoutingModel, Candidate.Layout, false).Cost));
        Assert.That(Comparison.After.Routes.Findings, Is.Empty);
    }

    private static DiagramLayoutSession Fixture(bool Tail = false)
    {
        var Session = new DiagramLayoutSession("scoring", 0);
        Add(Session, "a", 0, 0); Add(Session, "b", 3, 2);
        if (Tail) Add(Session, "c", 6, 2);
        Add(Session, "x", 0, 12); Add(Session, "y", 6, 12);
        Commit(Session, Session.PrepareAddRelationship(0, "ab", new() { SourceId = "a", TargetId = "b" }));
        if (Tail) Commit(Session, Session.PrepareAddRelationship(0, "bc", new() { SourceId = "b", TargetId = "c" }));
        Commit(Session, Session.PrepareAddRelationship(0, "xy", new() { SourceId = "x", TargetId = "y" }));
        return Session;
    }
    private static void Add(DiagramLayoutSession Session, string Id, int Row, int Column)
        => Commit(Session, Session.PrepareAddNode(0, new() { Id = Id, Label = "item", Shape = "rect" }, new(Row, Column, 1, 1)));
    private static void Commit(DiagramLayoutSession Session, DiagramLayoutSession.Update Candidate)
    { Assert.That(Candidate.FailureReason, Is.Null); Assert.That(Session.TryCommit(Candidate, out var Reason), Is.True, Reason); }
    private static string Independent(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new
    { Nodes = Layout.NodeBounds.Where(Box => Box.NodeId is "x" or "y"), Routes = Layout.EdgeRoutes.Where(Route => Route.EdgeId == "xy") });
}
