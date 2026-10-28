using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class EnterpriseRetainedDrawingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RetainedGroupAndCompoundRoutesAreDrawnWithoutEndpointRewrites(bool Compound)
    {
        string Source = "enterprise\n" + (Compound ? "application-component" : "group") + " parent\n"
            + "application-component child in parent\nparent -association-> child\n";
        var Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.HasErrors, Is.False);
        using var Scope = FlowchartRenderContext.Activate(new() { LayoutOptions = new() { EnableIncrementalRouting = true } });
        var Initial = EnterpriseLayoutEngine.Layout(Analysis.Document).RoutingContext!;
        Assert.That(Initial.Layout.EdgeRoutes.Count, Is.EqualTo(1));
        var Drawing = EnterpriseLayoutEngine.FromGeometry(Analysis.Document, Initial);
        var Edges = EnterpriseFlowchartEdgeRouter.Route(Analysis.Document, Drawing.Nodes, Initial, PreserveGeometry: true);
        Assert.That(Edges.Single().SvgPoints, Is.EqualTo(Initial.Layout.EdgeRoutes.Single().Points
            .Select(Point => Initial.Frame.LayoutToSvg(Point.X, Point.Y))));
    }
    [Test]
    public void LabelPlacementCannotMoveSuppliedRelationshipPorts()
    {
        const string Source = "enterprise\napplication-component a\napplication-component b\napplication-component c\napplication-component d\n"
            + "a -flow-> b \"labelword123\"\nc -flow-> d\n";
        var Document = EnterpriseValidator.Analyze(Source).Document;
        var Model = EnterpriseFlowchartContractMapper.Build(Document);
        var Layout = new FlowchartLayoutResult();
        foreach (var (Id, X, Y) in new[] { ("a", 0d, 0d), ("b", 300d, 0d), ("c", 150d, 300d), ("d", 150d, -300d) })
            Layout.NodeBoundsMutable.Add(new() { NodeId = Id, CenterX = X, CenterY = Y, Width = 150, Height = 90 });
        foreach (var Edge in Model.Edges)
        {
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId, TargetId = Edge.TargetId,
                ExitSide = Edge.SourceId == "a" ? "right" : "bottom", EntrySide = Edge.SourceId == "a" ? "left" : "top" };
            var Points = Edge.SourceId == "a" ? new[] { (75d, 0d), (225d, 0d) } : new[] { (150d, 255d), (150d, -255d) };
            foreach (var (X, Y) in Points) Route.Points.Add(new() { X = X, Y = Y });
            Layout.EdgeRoutesMutable.Add(Route);
        }
        Assert.That(EnterpriseFlowchartEdgeRouter.TryBuildRetainedContext(Model, Layout,
            new() { MinLeft = -75, MaxTop = 345, DiagramPaddingPx = 20 }, out var Geometry), Is.True);
        var Drawing = EnterpriseLayoutEngine.FromGeometry(Document, Geometry);
        var Routed = EnterpriseFlowchartEdgeRouter.Route(Document, Drawing.Nodes, Geometry, PreserveGeometry: true);
        foreach (var Route in Layout.EdgeRoutes)
            Assert.That(Routed.Single(Edge => Edge.Relationship.SourceId == Route.SourceId).SvgPoints,
                Is.EqualTo(Route.Points.Select(Point => Geometry.Frame.LayoutToSvg(Point.X, Point.Y))));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingRetainedGeometryCannotFallBackToRoutingDuringDrawing(bool MissingModelEdge)
    {
        var Document = EnterpriseValidator.Analyze("enterprise\napplication-component a\napplication-component b\na -flow-> b\n").Document;
        var Model = EnterpriseFlowchartContractMapper.Build(Document);
        if (MissingModelEdge) Model.EdgesMutable.Clear();
        var Layout = new FlowchartLayoutResult();
        Layout.NodeBoundsMutable.Add(new() { NodeId = "a", CenterX = 0, CenterY = 0, Width = 150, Height = 90 });
        Layout.NodeBoundsMutable.Add(new() { NodeId = "b", CenterX = 0, CenterY = -300, Width = 150, Height = 90 });
        Assert.That(EnterpriseFlowchartEdgeRouter.TryBuildRetainedContext(Model, Layout, new() { DiagramPaddingPx = 20 }, out var Geometry), Is.True);
        Assert.That(() => EnterpriseSvgRenderer.RenderRetained(Document, Geometry), Throws.InvalidOperationException.With.Message.Contains("authoritative"));
    }
    [Test]
    public void CandidateDrawingDoesNotEnterInitialLayoutOrPublishTheCandidate()
    {
        const string Source = "enterprise\napplication-component a \"A\"\napplication-component b \"B\" below a +2\na -flow-> b\n";
        var Options = new DiagramLayoutOptions { EnableIncrementalRouting = true };
        using var Scope = FlowchartRenderContext.Activate(new() { LayoutOptions = Options });
        var Initial = EnterpriseLayoutEngine.Layout(EnterpriseValidator.Analyze(Source).Document).RoutingContext!;
        Assert.That(Initial.Layout.RenderContext.UseCellRegions, Is.True);
        Assert.That(Initial.Layout.CellNodes, Is.Not.Null, "Initial cell geometry must survive the Enterprise drawing boundary.");
        var Service = new DiagramSourceUpdateService("document", 1, Source, Initial.Contract, Initial.Layout);
        string Changed = Source.Replace("below a +2", "below a +3");
        var Document = EnterpriseValidator.Analyze(Changed).Document;
        Service.ObserveSourceRevision(2);
        var Stages = new List<string>();
        var Previous = EnterpriseFlowchartEdgeRouter.StageTimingObserver;
        EnterpriseFlowchartEdgeRouter.StageTimingObserver = (Stage, Time) => { if (Time < 0) Stages.Add(Stage); };
        try
        {
            var Update = Service.Prepare("document", 2, Changed, EnterpriseFlowchartContractMapper.Build(Document), Options);
            Assert.That(Update.Kind, Is.EqualTo(DiagramSourceUpdateKind.LocalRouting));
            var Snapshot = Service.CopyCandidateSnapshot(Update);
            Assert.That(EnterpriseFlowchartEdgeRouter.TryBuildRetainedContext(Snapshot.Model, Snapshot.Layout, Initial.Frame, out var Context), Is.True);
            string Svg = EnterpriseSvgRenderer.RenderRetained(Document, Context);
            Assert.That(Svg, Does.Contain("data-id=\"b\""));
            Assert.That(Stages, Does.Not.Contain("enterprise-shared-flowchart-layout"));
            Assert.That(Stages, Does.Not.Contain("enterprise-contract-map"));
            Assert.That(Service.CopySnapshot().SourceRevision, Is.EqualTo(1), "drawing does not accept the edit");
            Assert.That(Snapshot.SourceRevision, Is.EqualTo(2));
            Assert.That(Snapshot.Layout.ResolvedEdgeGuides!.All(Guide => Snapshot.Model.Edges.Contains(Guide.Edge)), Is.True);
            Assert.That(Service.TryCommit(Update, out var Reason), Is.True, Reason);
            Assert.That(Service.CopySnapshot().SourceText, Is.EqualTo(Changed));
        }
        finally { EnterpriseFlowchartEdgeRouter.StageTimingObserver = Previous; }
    }
}
