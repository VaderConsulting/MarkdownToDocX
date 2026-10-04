using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class NativeFlowchartSvgRendererTests
{
    [Test]
    public void RenderToSvg_Simple_Fixture_Produces_Valid_Svg()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(NativeRenderCapabilities.CanRender(Contract), Is.True);

        string Svg = NativeFlowchartSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.StartWith("<svg"));
        Assert.That(Svg, Does.Contain("</svg>"));
        Assert.That(Svg, Does.Contain("<rect"));
        Assert.That(Svg, Does.Contain("<line"));
    }
}
