using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
[NonParallelizable]
public sealed class DiagramLayoutSessionTests
{
    [Test]
    public void ALoopCannotReserveTheSamePointForBothEnds()
    {
        var Session = TwoComponents();
        string Before = Geometry(Session.CopyLayout());
        var Candidate = Session.PrepareAddRelationship(0, "loop", new() { SourceId = "A", TargetId = "A", RoutingHint = new()
            { Leave = LayoutHintDirection.East, Enter = LayoutHintDirection.East, LeavePoint = 0, EnterPoint = 0 } });
        Assert.That(Candidate.FailureReason, Does.Contain("same exact connection point"));
        Assert.That(Session.TryCommit(Candidate, out _), Is.False);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
    }
    [Test]
    public void RemovingNodeAlsoRemovesItsIncidentRelationshipsAtomically()
    {
        var Session = TwoComponents();
        var Before = Session.CopyLayout();
        var Candidate = Session.PrepareRemoveNode(0, "A");
        Assert.That(Candidate.FailureReason, Is.Null);
        Commit(Session, Candidate);
        Assert.That(Component(Session.CopyLayout(), "X", "Y"), Is.EqualTo(Component(Before, "X", "Y")));
        Assert.That(Session.CopyLayout().NodeBounds.Any(Box => Box.NodeId == "A"), Is.False);
        Assert.That(Session.CopyLayout().EdgeRoutes.Any(Route => Route.SourceId == "A"), Is.False);
        Assert.That(Session.PrepareRemoveNode(0, "missing").FailureReason, Does.Contain("unknown"));
    }
    [Test]
    public void NecessarySiblingRedistributionAndItsUndoRestoreTheOriginalAllocation()
    {
        DiagramLayoutSession Session = new("slot-conflict", 0);
        AddNode(Session, "root", 0, 1); AddNode(Session, "left", 4, 0);
        AddNode(Session, "middle", 4, 1); AddNode(Session, "X", 0, 8); AddNode(Session, "Y", 4, 8);
        AddEdge(Session, "left-line", "root", "left"); AddEdge(Session, "middle-line", "root", "middle"); AddEdge(Session, "xy", "X", "Y");
        Commit(Session, Session.PreparePorts(0, "left-line", new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North }));
        Commit(Session, Session.PreparePorts(0, "middle-line", new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North }));
        string Before = Geometry(Session.CopyLayout());
        var Rejected = Session.PreparePorts(0, "left-line", new() { Leave = LayoutHintDirection.South, LeavePoint = 0,
            Enter = LayoutHintDirection.North, EnterPoint = 0 }, RouteLimit: 1);
        Assert.That(Rejected.FailureReason, Does.Contain("route limit"));
        Assert.That(Session.TryCommit(Rejected, out _), Is.False);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        var Candidate = Session.PreparePorts(0, "left-line", new() { Leave = LayoutHintDirection.South, LeavePoint = 0,
            Enter = LayoutHintDirection.North, EnterPoint = 0 });
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Candidate.RebuiltRouteIds, Does.Contain("middle-line").And.Not.Contain("xy"));
        Commit(Session, Candidate);
        string Moved = Geometry(Session.CopyLayout());
        Commit(Session, Session.PreparePorts(0, "left-line", new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North }));
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        Commit(Session, Session.PreparePorts(0, "left-line", new() { Leave = LayoutHintDirection.South, LeavePoint = 0,
            Enter = LayoutHintDirection.North, EnterPoint = 0 }));
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Moved));
    }
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AFreeExactPointMoveDoesNotRedistributeLegalSiblingPorts(int Step)
    {
        DiagramLayoutSession Session = new("slots", 0);
        AddNode(Session, "root", 0, 1); AddNode(Session, "left", 4, 0);
        AddNode(Session, "middle", 4, 1); AddNode(Session, "right", 4, 2);
        AddEdge(Session, "left-line", "root", "left"); AddEdge(Session, "middle-line", "root", "middle"); AddEdge(Session, "right-line", "root", "right");
        var Before = Session.CopySnapshot();
        var Candidate = Session.PreparePorts(0, "right-line", new() { Leave = LayoutHintDirection.South, LeavePoint = Step,
            Enter = LayoutHintDirection.North, EnterPoint = 0 }, RouteLimit: 1);
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { "right-line" }));
        Commit(Session, Candidate);
        var After = Session.CopySnapshot();
        foreach (string Id in new[] { "left-line", "middle-line" })
            Assert.That(JsonSerializer.Serialize(After.Layout.EdgeRoutes.Single(Route => Route.EdgeId == Id)),
                Is.EqualTo(JsonSerializer.Serialize(Before.Layout.EdgeRoutes.Single(Route => Route.EdgeId == Id))));
        Commit(Session, Session.PreparePorts(0, "right-line", new()));
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Geometry(Before.Layout)));
    }
    [TestCase("add")]
    [TestCase("reconnect")]
    [TestCase("remove")]
    [TestCase("remove-node")]
    public void TimelineTopologyCommandsRequireANewSemanticConstraintSession(string Operation)
    {
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(
            "enterprise\napplication-component app\nplateau before\nplateau after\nbefore -triggering-> after").Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Session = new DiagramLayoutSession("timeline", 0, Contract, Layout);
        string Before = Geometry(Session.CopyLayout());
        var Edge = new EdgeContract { SourceId = "after", TargetId = "before" };
        Edge.MetadataMutable["enterpriseRelationshipKind"] = new RawMetadata("enterpriseRelationshipKind", "Triggering");
        var Candidate = Operation switch
        {
            "add" => Session.PrepareAddRelationship(0, "new", Edge),
            "reconnect" => Session.PrepareReconnect(0, Contract.Edges[0].Id!, "before", "app"),
            "remove-node" => Session.PrepareRemoveNode(0, "before"),
            _ => Session.PrepareRemoveRelationship(0, Contract.Edges[0].Id!)
        };
        Assert.That(Candidate.FailureReason, Does.Contain("semantic constraint"));
        Assert.That(Session.TryCommit(Candidate, out _), Is.False);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
    }
    [Test]
    public void RemovingARelationshipRetainsEverySurvivingTerminalAndVacantSlot()
    {
        var Session = TwoComponents();
        AddNode(Session, "C", 3, 1); AddEdge(Session, "ac", "A", "C");
        var Before = Session.CopySnapshot();
        var Candidate = Session.PrepareRemoveRelationship(0, "ab");
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Candidate.AffectedRouteIds.Concat(Candidate.RebuiltRouteIds), Is.Empty);
        Commit(Session, Candidate);
        var After = Session.CopySnapshot();
        Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
        Assert.That(JsonSerializer.Serialize(After.Layout.EdgeRoutes), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.EdgeRoutes.Where(Route => Route.EdgeId != "ab"))));
        Assert.That(After.Layout.ResolvedEdgeGuides!.Any(Guide => Guide.Edge.Id == "ab"), Is.False);
        Assert.That(FlowchartRouteRuleChecker.Check(After.RoutingModel, After.Layout), Is.Empty);
        string GeometryBefore = Geometry(After.Layout);
        var Unknown = Session.PrepareRemoveRelationship(0, "missing");
        Assert.That(Unknown.FailureReason, Does.Contain("unknown"));
        Assert.That(Session.TryCommit(Unknown, out _), Is.False);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(GeometryBefore));
    }

    [Test]
    public void RemovingACycleEdgeReconsidersOnlySurvivingRoutesWhoseTopologyPolicyChanges()
    {
        DiagramLayoutSession Session = new("cycle", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 3, 0); AddNode(Session, "C", 6, 0);
        AddNode(Session, "X", 0, 8); AddNode(Session, "Y", 3, 8);
        AddEdge(Session, "ab", "A", "B"); AddEdge(Session, "bc", "B", "C"); AddEdge(Session, "ca", "C", "A");
        AddEdge(Session, "xy", "X", "Y");
        var Before = Session.CopySnapshot();
        var Candidate = Session.PrepareRemoveRelationship(0, "ab");
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Candidate.RebuiltRouteIds, Does.Contain("ca").And.Not.Contain("xy"));
        Commit(Session, Candidate);
        var After = Session.CopySnapshot();
        Assert.That(Component(After.Layout, "X", "Y"), Is.EqualTo(Component(Before.Layout, "X", "Y")));
        Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
        Assert.That(FlowchartRouteRuleChecker.Check(After.RoutingModel, After.Layout), Is.Empty);
    }
    [Test]
    public void BuildsCanvasOneItemAtATimeWithoutInitialPlacement()
    {
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        try
        {
            DiagramLayoutSession Session = TwoComponents();
            Assert.Multiple(() =>
            {
                Assert.That(Session.CopyLayout().NodeBounds, Has.Count.EqualTo(4));
                Assert.That(Session.CopyLayout().EdgeRoutes.Select(Route => Route.EdgeId), Is.EquivalentTo(new[] { "ab", "xy" }));
                Assert.That(Placements, Is.Zero);
            });
        }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
    }

    [Test]
    public void MovePreservesIndependentComponentAndRebuildsOnlyDependencies()
    {
        DiagramLayoutSession Session = TwoComponents();
        var Before = Session.CopyLayout();
        var Candidate = Session.PrepareMove(0, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(3, 1, 1, 1) });
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Geometry(Before)), "Preparing must not publish.");
        Commit(Session, Candidate);
        Assert.Multiple(() =>
        {
            Assert.That(Component(Session.CopyLayout(), "X", "Y"), Is.EqualTo(Component(Before, "X", "Y")));
            Assert.That(Candidate.AffectedRouteIds, Is.EqualTo(new[] { "ab" }));
            Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(Candidate.AffectedRouteIds));
        });
    }

    [Test]
    public void MovedObstacleIncludesNonincidentRoute()
    {
        DiagramLayoutSession Session = new("obstacle", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 4, 0); AddNode(Session, "O", 2, 4);
        AddEdge(Session, "ab", "A", "B");
        var Candidate = Session.PrepareMove(0, new Dictionary<string, FlowchartCellRegion> { ["O"] = new(2, 0, 1, 1) });
        Assert.That(Candidate.AffectedRouteIds, Does.Contain("ab"));
        Assert.That(Candidate.FailureReason, Is.Null);
        Commit(Session, Candidate);
    }

    [Test]
    public void AddingAnObstacleReroutesExistingRelationship()
    {
        DiagramLayoutSession Session = new("obstacle", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 4, 0); AddEdge(Session, "ab", "A", "B");
        var Candidate = Session.PrepareAddNode(0, new() { Id = "O" }, new(2, 0, 1, 1));
        Assert.That(Candidate.FailureReason, Is.Null);
        Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { "ab" }));
        Commit(Session, Candidate);
    }

    [Test]
    public void AddingSharedTerminalExpandsClosureAndFreezesOtherComponent()
    {
        DiagramLayoutSession Session = new("shared", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 3, 1);
        AddNode(Session, "X", 0, 6); AddNode(Session, "Y", 3, 6);
        Commit(Session, Session.PrepareAddRelationship(0, "ab", new() { SourceId = "A", TargetId = "B", MarkerEnd = EdgeEndpointMarker.Point,
            RoutingHint = new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North } }));
        AddEdge(Session, "xy", "X", "Y");
        AddNode(Session, "C", 3, -2);
        var Before = Session.CopyLayout();
        var Candidate = Session.PrepareAddRelationship(0, "ac", new() { SourceId = "A", TargetId = "C", MarkerEnd = EdgeEndpointMarker.Point,
            RoutingHint = new() { Leave = LayoutHintDirection.South, LeavePoint = 0, Enter = LayoutHintDirection.North } });
        Assert.That(Candidate.FailureReason, Is.Null);
        Commit(Session, Candidate);
        Assert.Multiple(() =>
        {
            Assert.That(Candidate.AffectedRouteIds, Does.Contain("ac"));
            Assert.That(Candidate.AffectedRouteIds, Does.Contain("ab"));
            Assert.That(Candidate.RebuiltRouteIds, Is.EquivalentTo(Candidate.AffectedRouteIds));
            Assert.That(Component(Session.CopyLayout(), "X", "Y"), Is.EqualTo(Component(Before, "X", "Y")));
        });
    }

    [Test]
    public void ReconnectRetainsEditorIdentityAndNodePositions()
    {
        DiagramLayoutSession Session = TwoComponents(); AddNode(Session, "C", 4, 0);
        var Before = Session.CopyLayout();
        var Candidate = Session.PrepareReconnect(0, "ab", "A", "C");
        Assert.That(Candidate.FailureReason, Is.Null);
        Commit(Session, Candidate);
        var After = Session.CopyLayout();
        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(After.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.NodeBounds)));
            Assert.That(After.EdgeRoutes.Single(Route => Route.EdgeId == "ab").TargetId, Is.EqualTo("C"));
            Assert.That(Component(After, "X", "Y"), Is.EqualTo(Component(Before, "X", "Y")));
        });
    }

    [Test]
    public void PortChangeUsesSameTransactionAndFreezesNodes()
    {
        DiagramLayoutSession Session = TwoComponents();
        var Before = Session.CopyLayout();
        var Candidate = Session.PreparePorts(0, "ab", new() { Leave = LayoutHintDirection.East, Enter = LayoutHintDirection.East });
        Assert.That(Candidate.FailureReason, Is.Null);
        Commit(Session, Candidate);
        var After = Session.CopyLayout();
        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(After.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.NodeBounds)));
            Assert.That(After.EdgeRoutes.Single(Route => Route.EdgeId == "ab").ExitSide, Is.EqualTo("right"));
            Assert.That(After.EdgeRoutes.Single(Route => Route.EdgeId == "ab").EntrySide, Is.EqualTo("right"));
        });
    }

    [Test]
    public void OccupiedMoveRollsBackExactlyAndNextValidRevisionCanCommit()
    {
        DiagramLayoutSession Session = TwoComponents();
        string Before = Geometry(Session.CopyLayout()); long Revision = Session.Revision;
        Session.ObserveSourceRevision(1);
        var Bad = Session.PrepareMove(1, new Dictionary<string, FlowchartCellRegion> { ["A"] = new(3, 0, 1, 1) });
        Assert.Multiple(() =>
        {
            Assert.That(Bad.FailureReason, Is.Not.Null);
            Assert.That(Session.TryCommit(Bad, out _), Is.False);
            Assert.That(Session.Revision, Is.EqualTo(Revision));
            Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        });
        Session.ObserveSourceRevision(2);
        Commit(Session, Session.PrepareMove(2, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(4, 0, 1, 1) }));
    }

    [Test]
    public void InvalidSourceRevisionAndCompetingLayoutCommitRejectOlderCandidates()
    {
        DiagramLayoutSession Session = TwoComponents();
        var Old = Session.PrepareMove(0, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(4, 0, 1, 1) });
        Assert.That(Old.FailureReason, Is.Null);
        Session.ObserveSourceRevision(1); // Text may fail parsing: there is deliberately no replacement model.
        Assert.That(Session.IsStale, Is.True);
        Assert.That(Session.CopySnapshot().SourceRevision, Is.Zero);
        string Before = Geometry(Session.CopyLayout());
        Assert.That(Session.TryCommit(Old, out _), Is.False);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        var First = Session.PrepareMove(1, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(4, 0, 1, 1) });
        var Second = Session.PrepareMove(1, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(5, 0, 1, 1) });
        Commit(Session, First);
        Assert.That(Session.IsStale, Is.False);
        Assert.That(Session.CopySnapshot().SourceRevision, Is.EqualTo(1));
        Assert.That(Session.TryCommit(Second, out _), Is.False);
        Assert.That(Session.TryCommit(First, out _), Is.False, "A candidate cannot be applied twice.");
    }

    [Test]
    public void ForeignSessionAndCallerMutationsCannotChangeCommittedGeometry()
    {
        DiagramLayoutSession Session = TwoComponents();
        string Before = Geometry(Session.CopyLayout());
        var Copy = Session.CopyLayout();
        Copy.EdgeRoutes[0].Points.Clear(); Copy.ResolvedEdgeGuides![0].Points.Clear();
        Copy.CellNodes![0].CellNode!.Grid.TryMove(["A"], 9, 9);
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        var Foreign = TwoComponents().PrepareMove(0, new Dictionary<string, FlowchartCellRegion> { ["B"] = new(4, 0, 1, 1) });
        Assert.That(Session.TryCommit(Foreign, out _), Is.False);
    }

    [Test]
    public void DependencyLimitRejectsWithoutWholeDiagramFallback()
    {
        DiagramLayoutSession Session = new("limit", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 3, 0); AddNode(Session, "C", 3, 2);
        AddEdge(Session, "ab", "A", "B"); AddEdge(Session, "ac", "A", "C");
        string Before = Geometry(Session.CopyLayout());
        var Candidate = Session.PrepareMove(0, new Dictionary<string, FlowchartCellRegion> { ["A"] = new(-1, 0, 1, 1) }, RouteLimit: 1);
        Assert.Multiple(() =>
        {
            Assert.That(Candidate.FailureReason, Does.Contain("limit"));
            Assert.That(Candidate.RebuiltRouteIds, Is.Empty);
            Assert.That(Session.TryCommit(Candidate, out _), Is.False);
            Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
        });
    }

    [Test]
    public void CapturesCompletedLayoutWithoutSharingRoutesHintsOrCells()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("flowchart TB\nA --> B\nX --> Y");
        Assert.That(FlowchartDagrePlacement.TryPlace(Contract, out var Seed), Is.True);
        FlowchartLayoutResult Layout = DiagramGridLayout.Apply(Contract, Seed, new() { UseCellRegions = true });
        DiagramLayoutSession Session = new("import", 0, Contract, Layout);
        string Before = Geometry(Session.CopyLayout());
        Layout.EdgeRoutes[0].Points.Clear(); Contract.EdgesMutable[0].TargetId = "X";
        Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Before));
    }

    [Test]
    public void IllegalRouteCandidatePreservesAcceptedModelGuidesAndRevision()
    {
        DiagramLayoutSession Session = TwoComponents();
        AddNode(Session, "C", 3, 2);
        var Before = Session.CopySnapshot();
        string Guides = JsonSerializer.Serialize(Before.Layout.ResolvedEdgeGuides);
        var Candidate = Session.PrepareAddRelationship(0, "ac", new()
        {
            SourceId = "A", TargetId = "C", MarkerEnd = EdgeEndpointMarker.Point,
            RoutingHint = new() { Leave = LayoutHintDirection.South, Enter = LayoutHintDirection.North }
        });
        Assert.Multiple(() =>
        {
            Assert.That(Candidate.FailureReason, Is.Not.Null);
            Assert.That(Session.TryCommit(Candidate, out _), Is.False);
            Assert.That(Session.Revision, Is.EqualTo(Before.Revision));
            Assert.That(Geometry(Session.CopyLayout()), Is.EqualTo(Geometry(Before.Layout)));
            Assert.That(JsonSerializer.Serialize(Session.CopyLayout().ResolvedEdgeGuides), Is.EqualTo(Guides));
            Assert.That(Session.CopySnapshot().RoutingModel.Edges.Any(Edge => Edge.Id == "ac"), Is.False);
        });
    }

    [Test]
    public void RelationshipIdentitySupportsParallelLinks()
    {
        DiagramLayoutSession Session = new("identity", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 3, 0);
        AddEdge(Session, "first", "A", "B");
        AddEdge(Session, "second", "A", "B");
        var Snapshot = Session.CopySnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(Snapshot.Layout.EdgeRoutes.Select(Route => Route.EdgeId), Is.EquivalentTo(new[] { "first", "second" }));
            Assert.That(FlowchartRouteRuleChecker.Check(Snapshot.RoutingModel, Snapshot.Layout), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SelfLoopConstructionIsLocalAndRetainsIndependentGeometry(bool Labelled)
    {
        DiagramLayoutSession Session = TwoComponents();
        var Before = Session.CopyLayout();
        var Candidate = Session.PrepareAddRelationship(0, "loop", new() { SourceId = "A", TargetId = "A", Label = Labelled ? "retry work" : "" });
        Assert.That(Candidate.FailureReason, Is.Null);
        Commit(Session, Candidate);
        Assert.That(Component(Session.CopyLayout(), "X", "Y"), Is.EqualTo(Component(Before, "X", "Y")));
        var Loop = Session.CopyLayout().EdgeRoutes.Single(Route => Route.EdgeId == "loop");
        Assert.That(Loop.Points.Count, Is.GreaterThanOrEqualTo(4));
        if (Labelled)
        {
            var Options = MermaidRecognition.Render.FlowchartRenderOptions.FromContract(Session.CopySnapshot().RoutingModel);
            var Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel("retry work", Options, Options.EdgeLabelFontSizePx);
            double Horizontal = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(Size, Options.EdgeLabelFontSizePx, true) + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            double Vertical = FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(Size, Options.EdgeLabelFontSizePx, false) + 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            Assert.That(Loop.Points.Zip(Loop.Points.Skip(1)).Any(Pair => Pair.First.Y == Pair.Second.Y
                ? Math.Abs(Pair.First.X - Pair.Second.X) >= Horizontal : Math.Abs(Pair.First.Y - Pair.Second.Y) >= Vertical), Is.True,
                "the loop must reserve measured label space on a straight segment");
        }
        Assert.That(FlowchartRouteRuleChecker.Check(Session.CopySnapshot().RoutingModel, Session.CopyLayout()), Is.Empty);
        Commit(Session, Session.PrepareRemoveRelationship(0, "loop"));
    }

    [Test]
    public void EngineOperationsAreIndependentOfExperimentSwitches([Range(0, 7)] int Mask)
    {
        DiagramLayoutOptions Options = new()
        {
            EnableIncrementalRouting = (Mask & 1) != 0,
            EnableRouteAwareConstruction = (Mask & 2) != 0,
            EnableWidthConstrainedLayout = (Mask & 4) != 0
        };
        DiagramLayoutSession Session = new("flags", 0, Options: Options);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 3, 0); AddEdge(Session, "ab", "A", "B");
        var Snapshot = Session.CopySnapshot();
        Assert.That(Snapshot.Layout.RenderContext.LayoutOptions, Is.EqualTo(Options));
        Assert.That(FlowchartRouteRuleChecker.Check(Snapshot.RoutingModel, Snapshot.Layout), Is.Empty);
        Assert.That(Snapshot.Layout.GraphWidth, Is.GreaterThan(0));
        Assert.That(Snapshot.Layout.GraphHeight, Is.GreaterThan(0));
    }

    [Test]
    public void EnterpriseConstraintSnapshotIsIndependentOfTheOriginalParsedModel()
    {
        var Document = EnterpriseValidator.Analyze("enterprise\n application-component app\n plateau before\n plateau after\n before -triggering-> after").Document!;
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        DiagramLayoutSession Session = new("enterprise", 0, Contract, Layout);
        var OriginalGrid = Layout.CellNodes![0].CellNode!.Grid;
        Assert.That(OriginalGrid.RegionConvention, Is.Not.Null);
        Contract.EdgesMutable.Single(Edge => Edge.SourceId == "before").TargetId = "before";
        Assert.That(OriginalGrid.RegionConvention!(OriginalGrid.Capture().Regions), Is.False, "The original mutable model now violates its convention.");
        var SnapshotGrid = Session.CopyLayout().CellNodes![0].CellNode!.Grid;
        Assert.That(SnapshotGrid.RegionConvention!(SnapshotGrid.Capture().Regions), Is.True);
    }

    [Test]
    public void ReadingAcceptedGeometryPerformsNoRouting()
    {
        DiagramLayoutSession Session = TwoComponents();
        long Searches = 0;
        FlowchartRenderContext.OperationCountObserver = (Name, Count) =>
        {
            if (Name is "automatic-route-cache-misses" or "automatic-route-cache-hits") { Searches += Count; }
        };
        try
        {
            Session.CopySnapshot(); Session.CopyLayout();
            Assert.That(Searches, Is.Zero);
        }
        finally { FlowchartRenderContext.OperationCountObserver = null; }
    }

    private static DiagramLayoutSession TwoComponents()
    {
        DiagramLayoutSession Session = new("two", 0);
        AddNode(Session, "A", 0, 0); AddNode(Session, "B", 3, 0);
        AddNode(Session, "X", 0, 6); AddNode(Session, "Y", 3, 6);
        AddEdge(Session, "ab", "A", "B"); AddEdge(Session, "xy", "X", "Y");
        return Session;
    }

    private static void AddNode(DiagramLayoutSession Session, string Id, int Row, int Column)
        => Commit(Session, Session.PrepareAddNode(0, new() { Id = Id, Label = Id, Shape = "rect" }, new(Row, Column, 1, 1)));
    private static void AddEdge(DiagramLayoutSession Session, string Id, string Source, string Target)
        => Commit(Session, Session.PrepareAddRelationship(0, Id, new() { SourceId = Source, TargetId = Target, MarkerEnd = EdgeEndpointMarker.Point }));
    private static void Commit(DiagramLayoutSession Session, DiagramLayoutSession.Update Update)
    {
        Assert.That(Update.FailureReason, Is.Null);
        Assert.That(Session.TryCommit(Update, out string? Reason), Is.True, Reason);
    }
    private static string Geometry(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes });
    private static string Component(FlowchartLayoutResult Layout, params string[] Ids) => JsonSerializer.Serialize(new
    {
        Nodes = Layout.NodeBounds.Where(Box => Ids.Contains(Box.NodeId)),
        Routes = Layout.EdgeRoutes.Where(Route => Ids.Contains(Route.SourceId) || Ids.Contains(Route.TargetId))
    });
}
