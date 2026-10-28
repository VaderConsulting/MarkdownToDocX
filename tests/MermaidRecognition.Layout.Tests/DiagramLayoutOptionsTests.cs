using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class DiagramLayoutOptionsTests
{
    [Test]
    public void DisabledOptionsPreserveExistingSvgAndIgnoreInactiveWidth()
    {
        const string Source = "enterprise\n business-actor owner\n application-component app\n owner -association-> app";
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        string Before = EnterpriseSvgRenderer.Render(Document);
        string Explicit = EnterpriseSvgRenderer.Render(EnterpriseValidator.Analyze(Source).Document,
            new() { TargetWidthPx = 600 });
        Assert.That(Explicit, Is.EqualTo(Before));
        Assert.That(new DiagramLayoutOptions { TargetWidthPx = 600 }.GeometryCacheKey, Is.Empty);
    }

    [Test]
    public void GeometryCachesSeparateFlagsAndActiveWidths()
    {
        DiagramLayoutOptions[] Options = [.. Enumerable.Range(0, 8).Select(Mask => new DiagramLayoutOptions
        {
            EnableIncrementalRouting = (Mask & 1) != 0,
            EnableRouteAwareConstruction = (Mask & 2) != 0,
            EnableWidthConstrainedLayout = (Mask & 4) != 0
        })];
        Assert.That(Options.Select(Item => Item.GeometryCacheKey).Distinct().Count(), Is.EqualTo(8));
        DiagramLayoutOptions Width = Options[4];
        Assert.That((Width with { TargetWidthPx = 600 }).GeometryCacheKey, Is.Not.EqualTo(Width.GeometryCacheKey));
    }

    [Test]
    public void ExplicitRenderPropagatesOptionsAndRestoresTheCallerOnFailure()
    {
        FlowchartRenderContext Caller = new FlowchartRenderContext();
        using IDisposable Scope = FlowchartRenderContext.Activate(Caller);
        DiagramLayoutOptions Options = new DiagramLayoutOptions { EnableWidthConstrainedLayout = true, TargetWidthPx = 700 };
        Assert.Throws<ArgumentNullException>(() => EnterpriseSvgRenderer.Render(null!, Options));
        Assert.That(FlowchartRenderContext.Current, Is.SameAs(Caller));
        EnterpriseDocument Document = EnterpriseValidator.Analyze("enterprise\n business-actor actor\n application-component app\n actor -association-> app").Document;
        Dictionary<string, EnterpriseLayoutNode> Nodes = [];
        using (FlowchartRenderContext.Activate(new() { LayoutOptions = Options }))
        {
            Assert.That(EnterpriseFlowchartEdgeRouter.TryBuildRoutingContext(Document, Nodes, out EnterpriseFlowchartRoutingContext? Context), Is.True);
            Assert.That(Context.Layout.RenderContext.LayoutOptions, Is.SameAs(Options));
        }
        Assert.That(FlowchartRenderContext.Current, Is.SameAs(Caller));
    }

    [TestCase(0), TestCase(-1), TestCase(double.NaN), TestCase(double.PositiveInfinity)]
    public void InvalidWidthIsRejected(double Width)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DiagramLayoutOptions { TargetWidthPx = Width });
}
