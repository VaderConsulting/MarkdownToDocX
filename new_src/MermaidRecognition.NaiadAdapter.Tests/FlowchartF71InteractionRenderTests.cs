using MermaidRecognition.NaiadAdapter;
using MermaidSharp;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class FlowchartF71InteractionRenderTests
{
    const string F71_SOURCE = """
        flowchart LR
        Docs[Documentation] --> API[API]
        click Docs href "https://example.com/docs" "Open docs" _blank
        click API call callback() "Tooltip for callback"
        """;

    [Test]
    public void TryRenderFlowchartSvg_F71_DoesNotEmbedInteractionText()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            F71_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Not.Contain("https://example.com/docs"));
        Assert.That(Svg, Does.Not.Contain("Open docs"));
        Assert.That(Svg, Does.Not.Contain("Tooltip for callback"));
    }
}
