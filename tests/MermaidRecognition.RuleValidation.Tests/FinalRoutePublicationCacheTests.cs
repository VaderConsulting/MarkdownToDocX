using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Prevents late accepted route geometry from leaving stale painter guides or label collision geometry.</summary>
[TestFixture, NonParallelizable]
public sealed class FinalRoutePublicationCacheTests
{
    /// <summary>A real accepted F74 interior repair republishes exact model routes and invalidates both painter caches.</summary>
    [Test]
    public void AcceptedInteriorRepairInvalidatesPainterCaches()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "F74");
        DiagramContract Contract = MermaidRecognizer.Parse(Scenario.Source);
        HashSet<FlowchartRenderContext> Injected = [];
        Action<string, FlowchartLayoutResult>? Previous = DiagramGridLayout.LayoutStageObserver;
        DiagramGridLayout.LayoutStageObserver = (Stage, Layout) =>
        {
            Previous?.Invoke(Stage, Layout);
            if (Stage != "bend-removing-port-reassignment-4.12")
            {
                return;
            }

            Layout.RenderContext.CachedSvgRenderGuides = [];
            Layout.RenderContext.CachedLabelSpatialIndex = new();
            Layout.RenderContext.CachedLabelSpatialIndex.AddPoint(-10000, -10000);
            Injected.Add(Layout.RenderContext);
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            Assert.That(Injected, Does.Contain(Layout.RenderContext));
            Assert.That(Layout.RenderContext.CachedSvgRenderGuides, Is.Null);
            Assert.That(Layout.RenderContext.CachedLabelSpatialIndex, Is.Null);
            foreach (FlowchartResolvedGuide Guide in Layout.ResolvedEdgeGuides!)
            {
                FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Line => Line.EdgeId == Guide.Edge.Id);
                Assert.That(Guide.Points, Is.EqualTo(Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()));
            }
        }
        finally { DiagramGridLayout.LayoutStageObserver = Previous; }
    }
}
