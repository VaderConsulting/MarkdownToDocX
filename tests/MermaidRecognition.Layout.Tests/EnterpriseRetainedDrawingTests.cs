using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class EnterpriseRetainedDrawingTests
{
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
