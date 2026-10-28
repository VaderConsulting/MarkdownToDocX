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
    [TestCase("label")]
    [TestCase("node")]
    [TestCase("removal")]
    [TestCase("font")]
    [TestCase("group")]
    [TestCase("edge-label")]
    public void UnsupportedChangesRequestExplicitRebuildWithoutExecutingIt(string Change)
    {
        var (Service, _, _) = Fixture();
        var Model = Parsed(Service);
        switch (Change)
        {
            case "direction": Model.Direction = "LR"; break;
            case "label": Model.Nodes[0].Label = "larger label"; break;
            case "node": Model.NodesMutable.Add(new() { Id = "new" }); break;
            case "removal": Model.EdgesMutable.RemoveAt(0); break;
            case "font": Model.Nodes[0].InlineStyle.FontSize = 24; break;
            case "group": Model.GroupsMutable.Add(new() { Id = "G" }); break;
            case "edge-label": Model.Edges[0].Label = "new relationship label"; break;
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
