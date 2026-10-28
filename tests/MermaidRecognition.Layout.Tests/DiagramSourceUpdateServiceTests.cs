using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
[NonParallelizable]
public sealed class DiagramSourceUpdateServiceTests
{
    [Test]
    public void RelationshipTextUsesStableIdentityAndLeavesRoutingToMeasuredDependencies()
    {
        var (Service, _, _) = Fixture();
        string Before = Geometry(Service.CopySnapshot().Layout);
        var Model = Parsed(Service);
        Model.Edges[0].Label = "Changed relationship text";
        AssertNoPlacement(() =>
        {
            var Update = Prepare(Service, 1, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.Redraw));
            Assert.That(Update.ChangedLabelIds, Is.EqualTo(new[] { "relationship:1" }));
            Assert.That(Update.AffectedRouteIds, Is.EqualTo(Update.ChangedLabelIds));
            Assert.That(Update.RebuiltRouteIds, Is.Empty);
            Commit(Service, Update);
        });
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
        Service.ObserveSourceRevision(2);
        var Dependency = Service.Prepare("document", 2, "measured dependency", Parsed(Service), Service.Options,
            AdditionalRouteSeeds: new[] { "relationship:1" });
        Assert.That(Dependency.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:1" }));
        Commit(Service, Dependency);
    }

    [Test]
    public void RecordedRoutesCannotOverrideChangedBodyGeometryOrMandatoryPoints()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Recorded = RetainedRouteCheckpoint.Capture(Before.Model, Before.Layout, null);
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 3);
        Service.ObserveSourceRevision(1);
        var Moved = Service.Prepare("document", 1, "moved", Model, Service.Options, RestoreRoutes: Recorded);
        Assert.That(Moved.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Moved.FailureReason, Does.Contain("different node/cell geometry"));
        Assert.That(Service.TryCommit(Moved, out _), Is.False);
        Model = Parsed(Service);
        Model.Edges[0].RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 1 };
        Service.ObserveSourceRevision(2);
        var Pinned = Service.Prepare("document", 2, "pinned", Model, Service.Options, RestoreRoutes: Recorded);
        Assert.That(Pinned.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Pinned.FailureReason, Is.Not.Null);
        Assert.That(Service.TryCommit(Pinned, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Before.Layout)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForeignNodeMoveOrResizeCannotEnterFrozenGroupSpace(bool Resize)
    {
        const string Script = "enterprise\ngroup g\napplication-component a in g\n"
            + "application-component b in g below a +6\napplication-component x\n";
        var Analysis = EnterpriseValidator.Analyze(Script);
        Assert.That(Analysis.HasErrors, Is.False, string.Join("; ", Analysis.Errors.Select(Error => Error.Message)));
        var Model = EnterpriseFlowchartContractMapper.Build(Analysis.Document);
        Assert.That(Model.Groups.Count, Is.EqualTo(1));
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Model, new() { UseCellRegions = true }, out var Layout), Is.True);
        Assert.That(Layout.ClusterBounds.Count, Is.EqualTo(1));
        var Service = new DiagramSourceUpdateService("document", 0, Script, Model, Layout);
        var A = Region(Service, "a");
        int Row = A.EndRow + 1, Column = A.StartColumn;
        Model = Parsed(Service);
        Node(Model, "x").PlacementHint = new() { Kind = PlacementHintKind.At, CellRow = Row, CellColumn = Column - 3 };
        Commit(Service, Prepare(Service, 1, Model));
        string Before = Geometry(Service.CopySnapshot().Layout);
        Model = Parsed(Service);
        if (Resize) { Node(Model, "x").Label = "larger"; Node(Model, "x").LayoutWidth = 1000; }
        else Node(Model, "x").PlacementHint = new() { Kind = PlacementHintKind.At, CellRow = Row, CellColumn = Column };
        var Candidate = Prepare(Service, 2, Model);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Candidate.FailureReason, Does.Contain("frame"));
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void MeasuredLabelGrowthMovesOnlyHintDependentsAndCanUndoExactly(bool Dependents)
    {
        var (Service, _, _) = Fixture();
        if (Dependents)
        {
            var Hinted = Parsed(Service); Node(Hinted, "B").PlacementHint = Below("A", 2); Node(Hinted, "C").PlacementHint = Below("B", 1);
            Commit(Service, Prepare(Service, 1, Hinted));
        }
        var Before = Service.CopySnapshot();
        var Original = Parsed(Service);
        var Model = Parsed(Service); var A = Node(Model, "A");
        A.Label = "A longer measured label"; A.LayoutWidth = 210; A.LayoutHeight = 100;
        AssertNoPlacement(() =>
        {
            var Candidate = Prepare(Service, 2, Model);
            Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting), Candidate.FailureReason);
            Commit(Service, Candidate);
            Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:1" }));
            Assert.That(Region(Service, "A").RowSpan, Is.EqualTo(2));
            Assert.That(Region(Service, "B").StartRow, Is.EqualTo(Dependents ? 4 : 3));
            Assert.That(Component(Service.CopySnapshot().Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
            string Grown = Geometry(Service.CopySnapshot().Layout);
            Commit(Service, Prepare(Service, 3, Original));
            Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Before.Layout)));
            Commit(Service, Prepare(Service, 4, Model));
            Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Grown));
        });
    }

    [Test]
    public void LabelWithinItsReservationRebuildsNoRoutesAndCollisionRejectsAtomically()
    {
        var (Service, _, _) = Fixture();
        string Before = Geometry(Service.CopySnapshot().Layout);
        var Model = Parsed(Service); Node(Model, "A").Label = "another label";
        var Candidate = Prepare(Service, 1, Model);
        Commit(Service, Candidate);
        Assert.That(Candidate.RebuiltRouteIds, Is.Empty);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
        Model = Parsed(Service); Node(Model, "B").Label = "large"; Node(Model, "B").LayoutHeight = 300;
        Candidate = Prepare(Service, 2, Model);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Service.TryCommit(Candidate, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void NodeRemovalAndUndoRetainRegionsAndSurvivingGeometry(bool All)
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Original = Parsed(Service);
        var Model = Parsed(Service);
        Model.NodesMutable.RemoveAll(Node => All || Node.Id == "B");
        Model.EdgesMutable.RemoveAll(Edge => All || Edge.TargetId == "B");
        AssertNoPlacement(() =>
        {
            var Candidate = Prepare(Service, 1, Model);
            Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting), Candidate.FailureReason);
            Commit(Service, Candidate);
            Assert.That(Candidate.RebuiltRouteIds, Is.Empty);
            if (All)
            {
                Assert.That(Service.CopySnapshot().Layout.CellNodes, Is.Empty);
                Assert.That(Service.CopySnapshot().Layout.NodeBounds, Is.Empty);
            }
            else Assert.That(Component(Service.CopySnapshot().Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
            Commit(Service, Prepare(Service, 2, Original));
            foreach (var Node in Before.Layout.CellNodes!) Assert.That(Region(Service, Node.Id), Is.EqualTo(Node.CellNode!.Region));
        });
        var After = Service.CopySnapshot();
        Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
        foreach (var Route in Before.Layout.EdgeRoutes)
            Assert.That(After.Layout.EdgeRoutes.Single(Edge => Edge.SourceId == Route.SourceId).Points.Select(Point => (Point.X, Point.Y)),
                Is.EqualTo(Route.Points.Select(Point => (Point.X, Point.Y))));
    }

    [Test]
    public void RemovingNodeWithDeclaredRelationshipsRejectsWithoutLosingGeometry()
    {
        var (Service, _, _) = Fixture();
        string Before = Geometry(Service.CopySnapshot().Layout);
        var Model = Parsed(Service); Model.NodesMutable.RemoveAll(Node => Node.Id == "B");
        var Candidate = Prepare(Service, 1, Model);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Candidate.FailureReason, Does.Contain("declared relationships"));
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void SourceNodeAdditionRetainsExistingGeometryAndRoutesOnlyItsNewRelationship(bool ExplicitHint)
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Model.NodesMutable.Add(new() { Id = "new", Shape = "rect", LayoutWidth = 210, LayoutHeight = 100,
            PlacementHint = ExplicitHint ? new() { Kind = PlacementHintKind.At, CellRow = 8, CellColumn = 2 } : null });
        Model.EdgesMutable.Add(new() { Id = "new-line", SourceId = "C", TargetId = "new", MarkerEnd = EdgeEndpointMarker.Point });
        AssertNoPlacement(() =>
        {
            var Candidate = Prepare(Service, 1, Model);
            Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting), Candidate.FailureReason);
            Commit(Service, Candidate);
            Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:3" }));
        });
        var After = Service.CopySnapshot();
        Assert.That(Component(After.Layout, "A", "B", "X", "Y"), Is.EqualTo(Component(Before.Layout, "A", "B", "X", "Y")));
        Assert.That(After.Layout.NodeBounds.Single(Box => Box.NodeId == "new").Width, Is.GreaterThanOrEqualTo(210));
        Assert.That(After.Layout.NodeBounds.Single(Box => Box.NodeId == "new").Height, Is.GreaterThanOrEqualTo(100));
        if (ExplicitHint) Assert.That(Region(Service, "new").StartRow, Is.EqualTo(8));
        Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
    }

    [Test]
    public void CollidingAddedNodeRejectsSourceAndGeometryTogether()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Model.NodesMutable.Add(new() { Id = "collision", Shape = "rect",
            PlacementHint = new() { Kind = PlacementHintKind.At, CellRow = 0, CellColumn = 0 } });
        var Candidate = Prepare(Service, 1, Model);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Service.TryCommit(Candidate, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Before.Layout)));
        Assert.That(Service.CopySnapshot().SourceText, Is.EqualTo(Before.SourceText));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AddedObstacleRebuildsOnlyTheObstructedComponent(bool Obstructs)
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Model.NodesMutable.Add(new() { Id = "obstacle", Shape = "rect", PlacementHint = new()
            { Kind = PlacementHintKind.At, CellRow = Obstructs ? 1 : 8, CellColumn = 0 } });
        var Candidate = Prepare(Service, 1, Model);
        Commit(Service, Candidate);
        Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(Obstructs ? new[] { "relationship:1" } : Array.Empty<string>()));
        var After = Service.CopySnapshot();
        Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
        Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
    }

    [Test]
    public void TimelineRemovalRequestsAnExplicitSemanticRebuild()
    {
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(
            "enterprise\napplication-component app\nplateau before\nplateau after\nbefore -triggering-> after").Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Service = new DiagramSourceUpdateService("document", 0, "before", Contract, Layout);
        var Model = Parsed(Service); Model.EdgesMutable.Clear();
        var Candidate = Prepare(Service, 1, Model);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.RebuildRequired));
        Assert.That(Candidate.FailureReason, Does.Contain("semantic constraint"));
        Assert.That(Candidate.RebuiltRouteIds, Is.Empty);
        Assert.That(Service.CopySnapshot().Model.Edges.Count, Is.EqualTo(1));
    }
    [Test]
    public void RelationshipRemovalPreservesSurvivingGeometryAndCanRemoveTheLastRoute()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var OriginalModel = Parsed(Service);
        AssertNoPlacement(() =>
        {
            var Model = Parsed(Service); Model.EdgesMutable.RemoveAt(0);
            var Update = Prepare(Service, 1, Model, "removed");
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting), Update.FailureReason);
            Assert.That(Update.AffectedRouteIds.Concat(Update.RebuiltRouteIds), Is.Empty);
            Commit(Service, Update);
            var After = Service.CopySnapshot();
            Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
            Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
            Assert.That(After.Layout.ResolvedEdgeGuides!.Single().Edge.Id, Is.EqualTo("relationship:2"));
            Assert.That(After.Identities.Bindings.Single().Id, Is.EqualTo("relationship:2"));
            // Readdition is a new identity under the documented text-reconciliation policy.
            Commit(Service, Prepare(Service, 2, OriginalModel, "undo"));
            Assert.That(Service.CopySnapshot().Model.Edges.Count, Is.EqualTo(2));
            Assert.That(Component(Service.CopySnapshot().Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
            Model = Parsed(Service); Model.EdgesMutable.Clear();
            Update = Prepare(Service, 3, Model, "none");
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting));
            Assert.That(Update.RebuiltRouteIds, Is.Empty); Commit(Service, Update);
            After = Service.CopySnapshot();
            Assert.That(After.Model.Edges, Is.Empty);
            Assert.That(After.Layout.EdgeRoutes, Is.Empty);
            Assert.That(After.Layout.ResolvedEdgeGuides, Is.Empty);
            Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
        });
    }

    [Test]
    public void RemovalMixedWithAnImpossiblePointChangeRejectsTheWholeEdit()
    {
        var (Service, _, _) = Fixture();
        string Before = Geometry(Service.CopySnapshot().Layout);
        var Model = Parsed(Service); Model.EdgesMutable.RemoveAt(0);
        Model.Edges[0].RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 10000 };
        var Candidate = Prepare(Service, 1, Model);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Service.TryCommit(Candidate, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
        Assert.That(Service.CopySnapshot().Model.Edges.Count, Is.EqualTo(2));
    }
    [TestCase(-2, 1)]
    [TestCase(0, 0)]
    [TestCase(2, -1)]
    public void ExactEndpointMovesRetainTheirPointsAndIndependentGeometry(int Leave, int Enter)
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        AssertNoPlacement(() =>
        {
            var Model = Parsed(Service);
            Model.Edges[0].RoutingHint = new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North,
                LeavePoint = Leave, EnterPoint = Enter };
            var Update = Prepare(Service, 1, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting), Update.FailureReason);
            Commit(Service, Update);
            var After = Service.CopySnapshot();
            Assert.That(FlowchartConnectionPoints.Validate(After.Model, After.Layout, CheckRoutes: true), Is.Null);
            Assert.That(After.Model.Edges[0].RoutingHint!.LeavePoint, Is.EqualTo(Leave));
            Assert.That(Update.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:1" }));
            Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
            Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
            Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
        });
    }

    [Test]
    public void UnavailableAndConflictingExactPointsRejectAtomically()
    {
        var (Service, _, _) = Fixture();
        var Before = Geometry(Service.CopySnapshot().Layout);
        var Model = Parsed(Service);
        Model.Edges[0].RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 10000 };
        var Invalid = Prepare(Service, 1, Model);
        Assert.That(Invalid.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Invalid.FailureReason, Does.Contain("connection point"));
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
        Model = Parsed(Service);
        Model.Edges[0].RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 0 };
        Model.EdgesMutable.Add(new() { Id = "ac", SourceId = "A", TargetId = "C", MarkerEnd = EdgeEndpointMarker.Point,
            RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 0 } });
        Invalid = Prepare(Service, 2, Model);
        Assert.That(Invalid.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Invalid.FailureReason, Does.Contain("same exact connection point"));
        Assert.That(Service.TryCommit(Invalid, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
    }
    [Test]
    public void CaptureBindsDetachedRoutesAndFullSourceMetadata()
    {
        var (Service, Model, Layout) = Fixture();
        var Snapshot = Service.CopySnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(Layout.EdgeRoutes.Select(Route => Route.EdgeId), Is.EqualTo(new[] { "ab", "xy" }));
            Assert.That(Model.Edges[0].Id, Is.EqualTo("ab"));
            Assert.That(Snapshot.Layout.EdgeRoutes.Select(Route => Route.EdgeId), Is.EqualTo(new[] { "relationship:1", "relationship:2" }));
            Assert.That(Snapshot.Model.Nodes[0].Declarations[0].SourceSpan.StartLine, Is.EqualTo(2));
            Assert.That(Snapshot.Model.Nodes[0].InlineStyle.Fill, Is.EqualTo("blue"));
            Assert.That(Snapshot.Layout.ResolvedEdgeGuides!.Select(Guide => Guide.Edge), Is.SubsetOf(Snapshot.Model.Edges));
            Assert.That(Snapshot.SourceText, Is.EqualTo("initial"));
        });
        Snapshot.Model.Nodes[0].InlineStyle.Fill = "corrupted";
        Snapshot.Layout.EdgeRoutes[0].Points.Clear();
        Model.Nodes[0].DeclarationsMutable.Clear();
        Assert.That(Service.CopySnapshot().Model.Nodes[0].Declarations, Has.Count.EqualTo(1));
        Assert.That(Service.CopySnapshot().Model.Nodes[0].InlineStyle.Fill, Is.EqualTo("blue"));
        Assert.That(Service.CopySnapshot().Layout.EdgeRoutes[0].Points, Is.Not.Empty);
    }

    [Test]
    public void CommentAndSourceLineEditsPublishMetadataWithNoRoutingOrPlacement()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Model.Edges[0].Id = "reparsed:99";
        Model.Edges[0].SourceSpan = new(0, 0, 45, 1, 45, 20);
        Model.SourceStatementsMutable.Add(new() { LineNumber = 44, RawLine = "%% comment" });
        AssertNoPlacement(() =>
        {
            var Update = Prepare(Service, 1, Model, "comment source");
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.MetadataOnly));
            Assert.That(Update.RebuiltRouteIds, Is.Empty);
            Commit(Service, Update);
        });
        var After = Service.CopySnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(Geometry(After.Layout), Is.EqualTo(Geometry(Before.Layout)));
            Assert.That(After.SourceRevision, Is.EqualTo(1));
            Assert.That(After.SourceText, Is.EqualTo("comment source"));
            Assert.That(After.Model.Edges[0].SourceSpan!.StartLine, Is.EqualTo(45));
            Assert.That(After.Identities.Bindings[0].ParserId, Is.EqualTo("reparsed:99"));
            Assert.That(After.Identities.Bindings[0].Id, Is.EqualTo(Before.Identities.Bindings[0].Id));
            Assert.That(After.IsStale, Is.False);
        });
    }

    [Test]
    public void ColourOnlyChangeRedrawsAcceptedGeometry()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Model.Nodes[0].InlineStyle.Fill = "red";
        var Update = Prepare(Service, 1, Model);
        Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.Redraw));
        Assert.That(Update.RebuiltRouteIds, Is.Empty);
        Commit(Service, Update);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Before.Layout)));
        Assert.That(Service.CopySnapshot().Model.Nodes[0].InlineStyle.Fill, Is.EqualTo("red"));
    }

    [Test]
    public void TypedHintReplacesLockedHintAndPreservesIndependentGeometry()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        AssertNoPlacement(() =>
        {
            var Model = Parsed(Service);
            Node(Model, "B").PlacementHint = Below("A", 3);
            var Update = Prepare(Service, 1, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting));
            Assert.That(Update.AffectedRouteIds, Is.EqualTo(new[] { "relationship:1" }));
            Assert.That(Update.RebuiltRouteIds, Is.EqualTo(Update.AffectedRouteIds));
            Commit(Service, Update);
            Assert.That(Region(Service, "B").StartRow, Is.EqualTo(4));
            Model = Parsed(Service);
            Node(Model, "B").PlacementHint = Below("A", 2);
            Commit(Service, Prepare(Service, 2, Model));
        });
        var After = Service.CopySnapshot();
        Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
        Assert.That(Region(Service, "B").StartRow, Is.EqualTo(3));
        Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
    }

    [Test]
    public void HintRemovalKeepsGeometryAndUnlocksOwner()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 2);
        Commit(Service, Prepare(Service, 1, Model));
        string Before = Geometry(Service.CopySnapshot().Layout);
        Model = Parsed(Service);
        Node(Model, "B").PlacementHint = null;
        var Update = Prepare(Service, 2, Model);
        Commit(Service, Update);
        Assert.That(Update.RebuiltRouteIds, Is.Empty);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
        Assert.That(Service.CopySnapshot().Layout.CellNodes!.Single(Node => Node.Id == "B").PlacementHintLocked, Is.False);
    }

    [Test]
    public void HintDependencyClosureMovesAttachedNodeInOneTransaction()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Node(Model, "C").PlacementHint = Below("B", 1);
        Commit(Service, Prepare(Service, 1, Model));
        Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 3);
        Commit(Service, Prepare(Service, 2, Model));
        Assert.That(Region(Service, "B").StartRow, Is.EqualTo(4));
        Assert.That(Region(Service, "C").StartRow, Is.EqualTo(6));
    }

    [TestCase("occupied")]
    [TestCase("peer")]
    [TestCase("cycle")]
    [TestCase("overflow")]
    public void FailedHintRetainsAcceptedSourceIdentitiesAndGeometry(string Case)
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below(Case == "peer" ? "missing" : "A", Case == "overflow" ? int.MaxValue : Case == "occupied" ? 4 : 1);
        if (Case == "cycle") { Node(Model, "A").PlacementHint = Below("B", 1); }
        var Update = Prepare(Service, 1, Model);
        Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Update.FailureReason, Is.Not.Empty);
        Assert.That(Service.TryCommit(Update, out _), Is.False);
        var After = Service.CopySnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(After.IsStale, Is.True);
            Assert.That(After.SourceText, Is.EqualTo(Before.SourceText));
            Assert.That(After.SourceRevision, Is.EqualTo(Before.SourceRevision));
            Assert.That(After.Identities, Is.SameAs(Before.Identities));
            Assert.That(Geometry(After.Layout), Is.EqualTo(Geometry(Before.Layout)));
        });
        Commit(Service, Prepare(Service, 2, Parsed(Service)));
        Assert.That(Service.CopySnapshot().IsStale, Is.False);
    }

    [Test]
    public void ReconnectAndPortChangeUseStableIdentityAndOneLocalClosure()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Model.Edges[0].TargetId = "C";
        var Update = Prepare(Service, 1, Model);
        Commit(Service, Update);
        Assert.That(Update.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:1" }));
        Model = Parsed(Service);
        Model.Edges[0].RoutingHint = new() { Leave = LayoutHintDirection.East, Enter = LayoutHintDirection.East };
        Commit(Service, Prepare(Service, 2, Model));
        var After = Service.CopySnapshot();
        Assert.That(After.Layout.EdgeRoutes[0].EdgeId, Is.EqualTo("relationship:1"));
        Assert.That(After.Layout.EdgeRoutes[0].TargetId, Is.EqualTo("C"));
        Assert.That(After.Layout.EdgeRoutes[0].ExitSide, Is.EqualTo("right"));
        Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
        Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
    }

    [Test]
    public void MixedHintAndAddedRelationshipPublishTogether()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 3);
        Model.EdgesMutable.Add(new() { Id = "bc", SourceId = "B", TargetId = "C", MarkerEnd = EdgeEndpointMarker.Point, CreationIndex = 2 });
        var Update = Prepare(Service, 1, Model);
        Assert.That(Service.CopySnapshot().Model.Edges, Has.Count.EqualTo(2));
        Commit(Service, Update);
        var After = Service.CopySnapshot();
        Assert.That(After.Model.Edges, Has.Count.EqualTo(3));
        Assert.That(After.Layout.EdgeRoutes.Select(Route => Route.EdgeId), Is.EquivalentTo(After.Identities.Bindings.Select(Binding => Binding.Id)));
        Assert.That(Update.RebuiltRouteIds, Does.Contain("relationship:3"));
        Assert.That(Region(Service, "B").StartRow, Is.EqualTo(4));
    }

    [Test]
    public void TypedHintMatchesTheEquivalentExplicitCellMove()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Direct = new DiagramLayoutSession("direct", 0, Before.Model, Before.Layout);
        var Explicit = Direct.PrepareMove(0, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(4, 0, 1, 1) });
        Assert.That(Direct.TryCommit(Explicit, out var Reason), Is.True, Reason);
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 3);
        var Typed = Prepare(Service, 1, Model);
        Commit(Service, Typed);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Direct.CopyLayout())));
        Assert.That(Typed.AffectedRouteIds, Is.EqualTo(Explicit.AffectedRouteIds));
        Assert.That(Typed.RebuiltRouteIds, Is.EqualTo(Explicit.RebuiltRouteIds));
    }

    [Test]
    public void MovedObstacleRebuildsNonincidentRoute()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Node(Model, "C").PlacementHint = Below("A", 1);
        var Update = Prepare(Service, 1, Model);
        Commit(Service, Update);
        Assert.That(Update.AffectedRouteIds, Does.Contain("relationship:1"));
        Assert.That(Update.RebuiltRouteIds, Is.EqualTo(Update.AffectedRouteIds));
    }

    [Test]
    public void BudgetFailureCannotBecomeRebuildOrPublishAnyPartOfMixedEdit()
    {
        var (Service, _, _) = Fixture();
        var Before = Service.CopySnapshot();
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 3);
        Node(Model, "Y").PlacementHint = Below("X", 3);
        Service.ObserveSourceRevision(1);
        var Update = Service.Prepare("document", 1, "typed text remains in host", Model, Service.Options, RouteLimit: 1);
        Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Update.FailureReason, Does.Contain("route limit"));
        Assert.That(Service.TryCommit(Update, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Before.Layout)));
        Assert.That(Service.CopySnapshot().Identities, Is.SameAs(Before.Identities));
    }

    [Test]
    public void ErrorDiagnosticsAndAmbiguousReconciliationRetainAcceptedState()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Model.DiagnosticsMutable.Add(new(MermaidDiagnosticSeverity.Error, "test.invalid", "invalid source", null, null));
        Assert.That(Prepare(Service, 1, Model).Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Model = Parsed(Service);
        Model.EdgesMutable.Add(new() { Id = "another-ab", SourceId = "A", TargetId = "B", MarkerEnd = EdgeEndpointMarker.Point });
        var Update = Prepare(Service, 2, Model);
        Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Update.FailureReason, Does.Contain("Ambiguous"));
        Assert.That(Service.CopySnapshot().SourceRevision, Is.Zero);
        Assert.That(Service.CopySnapshot().Identities.Bindings, Has.Count.EqualTo(2));
    }

    [Test]
    public void SourceCandidatesOwnTheirModelAndDiscardedIdentities()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Model.EdgesMutable.Add(new() { Id = "bc", SourceId = "B", TargetId = "C", MarkerEnd = EdgeEndpointMarker.Point });
        var Discarded = Prepare(Service, 1, Model);
        Assert.That(Discarded.FailureReason, Is.Null);
        // This never enters the accepted identity lineage.
        var Accepted = Prepare(Service, 2, Model);
        Model.Nodes[0].InlineStyle.Fill = "later caller mutation";
        Model.EdgesMutable.Clear();
        Commit(Service, Accepted);
        Assert.That(Service.CopySnapshot().Identities.Bindings.Last().Id, Is.EqualTo("relationship:3"));
        Assert.That(Service.CopySnapshot().Model.Nodes[0].InlineStyle.Fill, Is.EqualTo("blue"));
        Assert.That(Service.CopySnapshot().Model.Edges, Has.Count.EqualTo(3));
        Assert.That(Service.TryCommit(Discarded, out _), Is.False);
    }

    [Test]
    public void InvalidIntermediateTextAndStaleWorkersCannotReplaceNewerSource()
    {
        var (Service, _, _) = Fixture();
        var Older = Prepare(Service, 1, Parsed(Service));
        Service.ObserveSourceRevision(2); // parsing failed; there is intentionally no successor model
        Assert.That(Service.TryCommit(Older, out _), Is.False);
        Assert.That(Service.CopySnapshot().IsStale, Is.True);
        var Newer = Prepare(Service, 3, Parsed(Service), "valid again");
        var Competing = Service.Prepare("document", 3, "same revision", Parsed(Service), Service.Options);
        Commit(Service, Newer);
        Assert.That(Service.TryCommit(Competing, out _), Is.False);
        Assert.That(Service.TryCommit(Newer, out _), Is.False);
        Assert.That(Service.CopySnapshot().SourceText, Is.EqualTo("valid again"));
    }

    [Test]
    public void InvalidatedInstanceRejectsPendingWorkAndReplacementOwnsItsResults()
    {
        var (Old, Model, Layout) = Fixture();
        var Pending = Prepare(Old, 1, Parsed(Old));
        Old.Invalidate("document/options changed");
        var Replacement = new DiagramSourceUpdateService("document", 1, "replacement", Model, Layout);
        Assert.That(Old.TryCommit(Pending, out string? Reason), Is.False);
        Assert.That(Reason, Does.Contain("invalidated"));
        Assert.That(Old.CopySnapshot().IsStale, Is.True);
        Assert.That(Replacement.TryCommit(Pending, out _), Is.False);
        Assert.That(Old.Prepare("document", 1, "late", Model, Old.Options).FailureReason, Does.Contain("invalidated"));
        Commit(Replacement, Prepare(Replacement, 2, Parsed(Replacement)));
        Assert.That(Replacement.CopySnapshot().SourceText, Is.EqualTo("updated"));
    }

    [TestCase("direction")]
    [TestCase("text-format")]
    [TestCase("shape")]
    [TestCase("font")]
    [TestCase("group")]
    [TestCase("edge-marker")]
    public void UnsupportedChangesRequestExplicitRebuildWithoutExecutingIt(string Change)
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        switch (Change)
        {
            case "direction": Model.Direction = "LR"; break;
            case "text-format": Model.Nodes[0].LabelType = MermaidLabelType.Markdown; break;
            case "shape": Model.Nodes[0].Shape = "circle"; break;
            case "font": Model.Nodes[0].InlineStyle.FontSize = 24; break;
            case "group": Model.GroupsMutable.Add(new() { Id = "G" }); break;
            case "edge-marker": Model.Edges[0].MarkerEnd = EdgeEndpointMarker.Circle; break;
        }
        string Before = Geometry(Service.CopySnapshot().Layout);
        AssertNoPlacement(() =>
        {
            var Update = Prepare(Service, 1, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.RebuildRequired));
            Assert.That(Service.TryCommit(Update, out _), Is.False);
            Assert.That(Update.RebuiltRouteIds, Is.Empty);
        });
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
    }

    [Test]
    public void OptionAndDocumentMismatchesCannotPublish()
    {
        var (Service, _, _) = Fixture();
        Service.ObserveSourceRevision(1);
        Assert.That(Service.Prepare("other", 1, "", Parsed(Service), Service.Options).Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Service.Prepare("document", 1, "", Parsed(Service), Service.Options with { TargetWidthPx = 700 }).Kind,
            Is.EqualTo(DiagramSourceUpdateKind.RebuildRequired));
    }

    [Test]
    public void EnterpriseReparseAndHintUseTheSharedService()
    {
        const string Source = "enterprise\n application-component A\n application-component B\n A -flow-> B";
        DiagramContract Map(string Text) => EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Text).Document!);
        var Model = Map(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Model, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Service = new DiagramSourceUpdateService("document", 0, Source, Model, Layout);
        Commit(Service, Prepare(Service, 1, Map(Source + "\n%% a comment")));
        Model = Map(Source);
        Node(Model, "B").PlacementHint = Below("A", 2);
        Commit(Service, Prepare(Service, 2, Model));
        Assert.That(Service.CopySnapshot().Identities.Bindings[0].Id, Is.EqualTo("relationship:1"));
        Assert.That(Service.CopySnapshot().Model.Nodes.Single(Node => Node.Id == "B").PlacementHint!.Offset, Is.EqualTo(2));
    }

    [Test]
    public void NewPlateauTimelineCannotReuseAnOlderSemanticConstraintSnapshot()
    {
        const string Source = "enterprise\n application-component app\n plateau before\n plateau after";
        var Model = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Model, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Service = new DiagramSourceUpdateService("document", 0, Source, Model, Layout);
        var Updated = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source + "\n after -triggering-> before").Document!);
        var Candidate = Prepare(Service, 1, Updated);
        Assert.That(Candidate.Kind, Is.EqualTo(DiagramSourceUpdateKind.RebuildRequired));
        Assert.That(Candidate.FailureReason, Does.Contain("semantic constraint"));
        Assert.That(Candidate.RebuiltRouteIds, Is.Empty);
        Assert.That(Service.CopySnapshot().Model.Edges, Is.Empty);
    }

    private static (DiagramSourceUpdateService Service, DiagramContract Model, FlowchartLayoutResult Layout) Fixture()
    {
        DiagramLayoutSession Canvas = new("document", 0);
        void Apply(DiagramLayoutSession.Update Update) { Assert.That(Canvas.TryCommit(Update, out var Reason), Is.True, Reason); }
        foreach (var (Id, Row, Column) in new[] { ("A", 0, 0), ("B", 3, 0), ("C", 5, 0), ("X", 0, 8), ("Y", 3, 8) })
        {
            Apply(Canvas.PrepareAddNode(0, new() { Id = Id, Label = Id, Shape = "rect" }, new(Row, Column, 1, 1)));
        }
        Apply(Canvas.PrepareAddRelationship(0, "ab", new() { SourceId = "A", TargetId = "B", MarkerEnd = EdgeEndpointMarker.Point }));
        Apply(Canvas.PrepareAddRelationship(0, "xy", new() { SourceId = "X", TargetId = "Y", MarkerEnd = EdgeEndpointMarker.Point }));
        var Snapshot = Canvas.CopySnapshot();
        var Model = Snapshot.RoutingModel;
        for (int Index = 0; Index < Model.Edges.Count; Index++) { Model.Edges[Index].IsUserDefinedId = false; Model.Edges[Index].CreationIndex = Index; }
        Model.Nodes[0].InlineStyle.Fill = "blue";
        Model.Nodes[0].DeclarationsMutable.Add(new("node", "A[A]", new(0, 4, 2, 1, 2, 5)));
        return (new("document", 0, "initial", Model, Snapshot.Layout), Model, Snapshot.Layout);
    }

    [TestCase(PlacementHintKind.NorthWest, -3, -5)]
    [TestCase(PlacementHintKind.NorthEast, -3, 5)]
    [TestCase(PlacementHintKind.SouthEast, 3, 5)]
    [TestCase(PlacementHintKind.SouthWest, 3, -5)]
    public void DiagonalUpdateMovesBothAxesAndDependentsButKeepsOrthogonalRoutes(PlacementHintKind Kind, int Row, int Column)
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Node(Model, "C").PlacementHint = Below("B", 1);
        Commit(Service, Prepare(Service, 1, Model));
        var Before = Service.CopySnapshot();
        AssertNoPlacement(() =>
        {
            Model = Parsed(Service);
            Node(Model, "B").PlacementHint = new() { Kind = Kind, PeerId = "A", Offset = 2, ColumnOffset = 4 };
            var Update = Prepare(Service, 2, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting));
            Commit(Service, Update);
            Assert.That(Region(Service, "B"), Is.EqualTo(new FlowchartCellRegion(Row, Column, 1, 1)));
            Assert.That(Region(Service, "C"), Is.EqualTo(new FlowchartCellRegion(Row + 2, Column, 1, 1)));
            Assert.That(Update.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:1" }));
            Model = Parsed(Service);
            Assert.That(Node(Model, "B").PlacementHint!.ColumnOffset, Is.EqualTo(4), "detached copies retain both axes");
            Node(Model, "B").PlacementHint!.ColumnOffset = 5;
            Update = Prepare(Service, 3, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting), "changing only the horizontal gap must route locally");
            Commit(Service, Update);
            Assert.That(Region(Service, "B").StartColumn, Is.EqualTo(Column + Math.Sign(Column)));
        });
        var After = Service.CopySnapshot();
        Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
        Assert.That(FlowchartRouteRuleChecker.Check(After.Model, After.Layout), Is.Empty);
        foreach (var Route in After.Layout.EdgeRoutes)
            for (int Index = 1; Index < Route.Points.Count; Index++)
                Assert.That(Math.Abs(Route.Points[Index].X - Route.Points[Index - 1].X) < 0.001
                    || Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y) < 0.001, Is.True, "relationship segments stay orthogonal");
    }

    [Test]
    public void OccupiedDiagonalDestinationRejectsWithoutPublishingOrRebuilding()
    {
        var (Service, _, _) = Fixture();
        var Before = Geometry(Service.CopySnapshot().Layout);
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = new() { Kind = PlacementHintKind.SouthEast, PeerId = "A", Offset = 2, ColumnOffset = 7 };
        var Update = Prepare(Service, 1, Model);
        Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Service.TryCommit(Update, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Before));
        Assert.That(Service.CopySnapshot().SourceRevision, Is.Zero);
    }

    [Test]
    public void AbsoluteAnchorMovesItsHintDependentsAndRetainsIndependentGeometry()
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        Node(Model, "B").PlacementHint = Below("A", 2);
        Node(Model, "C").PlacementHint = Below("B", 1);
        Commit(Service, Prepare(Service, 1, Model));
        var Before = Service.CopySnapshot();
        AssertNoPlacement(() =>
        {
            Model = Parsed(Service);
            Node(Model, "A").PlacementHint = new() { Kind = PlacementHintKind.At, CellRow = -2, CellColumn = 2 };
            var Update = Prepare(Service, 2, Model);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting));
            Commit(Service, Update);
            Assert.That(Region(Service, "A"), Is.EqualTo(new FlowchartCellRegion(-2, 2, 1, 1)));
            Assert.That(Region(Service, "B"), Is.EqualTo(new FlowchartCellRegion(1, 2, 1, 1)));
            Assert.That(Region(Service, "C"), Is.EqualTo(new FlowchartCellRegion(3, 2, 1, 1)));
            Assert.That(Update.RebuiltRouteIds, Is.EqualTo(new[] { "relationship:1" }));
        });
        var After = Service.CopySnapshot();
        Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
        Assert.That(Node(Parsed(Service), "A").PlacementHint!.CellRow, Is.EqualTo(-2));
        Assert.That(Node(Parsed(Service), "A").PlacementHint!.CellColumn, Is.EqualTo(2));
        Model = Parsed(Service);
        Node(Model, "A").PlacementHint!.CellRow = 0;
        Node(Model, "A").PlacementHint!.CellColumn = 8;
        var Rejected = Prepare(Service, 3, Model);
        Assert.That(Rejected.Kind, Is.EqualTo(DiagramSourceUpdateKind.Rejected));
        Assert.That(Service.TryCommit(Rejected, out _), Is.False);
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(After.Layout)));
        Model = Parsed(Service);
        Node(Model, "A").PlacementHint = null;
        Commit(Service, Prepare(Service, 4, Model));
        Assert.That(Region(Service, "A"), Is.EqualTo(new FlowchartCellRegion(0, 0, 1, 1)));
        Assert.That(Region(Service, "B"), Is.EqualTo(new FlowchartCellRegion(3, 0, 1, 1)));
        Assert.That(Geometry(Service.CopySnapshot().Layout), Is.EqualTo(Geometry(Before.Layout)), "undoing a first pin restores its pre-pin geometry");
    }
    private static DiagramContract Parsed(DiagramSourceUpdateService Service)
    {
        var Snapshot = Service.CopySnapshot();
        foreach (var Binding in Snapshot.Identities.Bindings)
        {
            Snapshot.Model.Edges[Binding.ParsedEdgeIndex].Id = Binding.ParserId;
            Snapshot.Model.Edges[Binding.ParsedEdgeIndex].IsUserDefinedId = false;
        }
        return Snapshot.Model;
    }
    private static DiagramSourceUpdateService.Update Prepare(DiagramSourceUpdateService Service, long Revision, DiagramContract Model, string Text = "updated")
    {
        Service.ObserveSourceRevision(Revision);
        return Service.Prepare("document", Revision, Text, Model, Service.Options);
    }
    private static void Commit(DiagramSourceUpdateService Service, DiagramSourceUpdateService.Update Update)
    {
        Assert.That(Update.FailureReason, Is.Null);
        Assert.That(Service.TryCommit(Update, out var Reason), Is.True, Reason);
    }
    private static NodeContract Node(DiagramContract Model, string Id) => Model.Nodes.Single(Node => Node.Id == Id);
    private static PlacementHintContract Below(string Peer, int Offset) => new() { Kind = PlacementHintKind.Below, PeerId = Peer, Offset = Offset };
    private static FlowchartCellRegion Region(DiagramSourceUpdateService Service, string Id)
        => Service.CopySnapshot().Layout.CellNodes!.Single(Node => Node.Id == Id).CellNode!.Region;
    private static string Geometry(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes });
    private static string Component(FlowchartLayoutResult Layout, params string[] Ids) => JsonSerializer.Serialize(new
    {
        Nodes = Layout.NodeBounds.Where(Node => Ids.Contains(Node.NodeId)),
        Routes = Layout.EdgeRoutes.Where(Route => Ids.Contains(Route.SourceId) || Ids.Contains(Route.TargetId))
    });
    private static void AssertNoPlacement(Action Action)
    {
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        try { Action(); Assert.That(Placements, Is.Zero); }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
    }
}
