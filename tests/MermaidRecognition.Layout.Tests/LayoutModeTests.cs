using MermaidRecognition.Config;
using MermaidRecognition.Core;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class LayoutModeTests
{
    [TestCase("grid", false)]
    [TestCase("open", false)]
    [TestCase("grid", true)]
    [TestCase("open", true)]
    public void BothModes_BridgeCrossingsWithoutChangingConnectorRules(string Mode, bool IsEnterprise)
    {
        string Source = IsEnterprise
            ? "enterprise\napplication-component first\napplication-component second\napplication-component third\napplication-component fourth\nfirst -flow-> third\nfirst -flow-> fourth\nsecond -flow-> third\nsecond -flow-> fourth"
            : "flowchart TB\nfirst --> third\nfirst --> fourth\nsecond --> third\nsecond --> fourth";
        string Svg = IsEnterprise ? EnterpriseRenderBridge.RenderEnterpriseSvg(WithMode(Source, Mode))
            : FlowchartSugiyamaSvgRenderer.RenderToSvg(MermaidRecognizer.Parse(WithMode(Source, Mode)));
        Assert.That(FlowchartSvgLineJump.LastJumps, Is.Not.Empty);
        Assert.That(Svg, Does.Contain("A"));
        Assert.That(Svg, Does.Contain("C"));
    }

    private const string Flow = "flowchart TB\nfirst[Input] --> second[Output]\nfirst --> third[Alternative]\nsecond --> fourth[Finish]\nthird --> fourth";
    private const string Enterprise = "enterprise\napplication-component first 'Input'\napplication-component second 'Output'\napplication-component third 'Alternative'\napplication-component fourth 'Finish'\nfirst -flow-> second\nfirst -flow-> third\nsecond -flow-> fourth\nthird -flow-> fourth";

    [TestCase("grid")]
    [TestCase("open")]
    public void BothModes_KeepOrthogonalRoutesAndNonoverlappingBoxes(string Mode)
    {
        var Contract = MermaidRecognizer.Parse(WithMode(Flow, Mode));
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        foreach (var Route in Layout.EdgeRoutes)
            for (int Index = 1; Index < Route.Points.Count; Index++)
                Assert.That(Math.Abs(Route.Points[Index].X - Route.Points[Index - 1].X) < 0.01
                    || Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y) < 0.01, Is.True);
        foreach (var First in Layout.NodeBounds)
        foreach (var Second in Layout.NodeBounds.Where(Item => string.CompareOrdinal(Item.NodeId, First.NodeId) > 0))
            Assert.That(Math.Abs(First.CenterX - Second.CenterX) >= (First.Width + Second.Width) / 2
                || Math.Abs(First.CenterY - Second.CenterY) >= (First.Height + Second.Height) / 2, Is.True);
        Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(WithMode(Flow, Mode), out var Svg), Is.True);
        Assert.That(Svg, Does.Contain("<svg"));
        Assert.That(EnterpriseRenderBridge.TryRenderEnterpriseSvg(WithMode(Enterprise, Mode), out var EnterpriseSvg), Is.True);
        Assert.That(EnterpriseSvg, Does.Contain("<svg"));
    }

    [Test]
    public void OpenNestedFrames_StayInsideTheViewport()
    {
        string Source = "enterprise\ngroup outer 'Outer'\ngroup inner 'Inner' in outer\napplication-component first in outer\napplication-component second in inner\napplication-component third in inner\nfirst -flow-> second\nthird -flow-> second";
        var Svg = System.Xml.Linq.XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(WithMode(Source, "open")));
        var Root = Svg.Root!;
        double Width = double.Parse(Root.Attribute("width")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        double Height = double.Parse(Root.Attribute("height")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var Rect in Root.Descendants().Where(Item => Item.Name.LocalName == "rect"
            && Item.Parent?.Attribute("class")?.Value == "group"))
        {
            double Value(string Name) => double.Parse(Rect.Attribute(Name)!.Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(Value("x"), Is.GreaterThanOrEqualTo(0));
            Assert.That(Value("y"), Is.GreaterThanOrEqualTo(18));
            Assert.That(Value("x") + Value("width"), Is.LessThanOrEqualTo(Width));
            Assert.That(Value("y") + Value("height"), Is.LessThanOrEqualTo(Height));
        }
    }

    [Test]
    public void ExplicitGrid_RendersExactlyLikeDefault()
    {
        NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Flow, out var Default);
        NativeFlowchartRenderBridge.TryRenderFlowchartSvg(WithMode(Flow, "grid"), out var Explicit);
        Assert.That(Explicit, Is.EqualTo(Default));
        Assert.That(EnterpriseRenderBridge.RenderEnterpriseSvg(WithMode(Enterprise, "grid")),
            Is.EqualTo(EnterpriseRenderBridge.RenderEnterpriseSvg(Enterprise)));
    }

    [Test]
    public void RunDefault_IsOverriddenByDiagramSetting()
    {
        NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Flow, out var RunDefault, DiagramLayoutMode.Open);
        NativeFlowchartRenderBridge.TryRenderFlowchartSvg(WithMode(Flow, "open"), out var ExplicitOpen);
        Assert.That(RunDefault, Is.EqualTo(ExplicitOpen));
        NativeFlowchartRenderBridge.TryRenderFlowchartSvg(WithMode(Flow, "grid"), out var ExplicitGrid, DiagramLayoutMode.Open);
        NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Flow, out var DefaultGrid);
        Assert.That(ExplicitGrid, Is.EqualTo(DefaultGrid));
        EnterpriseRenderBridge.TryRenderEnterpriseSvg(Enterprise, out var EnterpriseDefault, DiagramLayoutMode.Open);
        Assert.That(EnterpriseDefault, Is.EqualTo(EnterpriseRenderBridge.RenderEnterpriseSvg(WithMode(Enterprise, "open"))));
    }

    private static string WithMode(string Source, string Mode) => $"---\nconfig:\n  layout: {Mode}\n---\n{Source}";
}
