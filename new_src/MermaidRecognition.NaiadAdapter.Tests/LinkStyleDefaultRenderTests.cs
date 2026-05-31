using System.Text.RegularExpressions;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class LinkStyleDefaultRenderTests
{
    [Test]
    public void TryRenderFlowchartSvg_LinkStyleDefault_AppliesStrokeAndWidthToEdges()
    {
        string Source = """
            flowchart LR
            A[A] --> B[B] --> C[C]
            linkStyle default stroke:#c60,stroke-width:3px
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);

        // Expect the stroke colour and width to appear on at least one edge path.
        bool HasStroke = Regex.IsMatch(Svg, "stroke=\"#c60\"", RegexOptions.IgnoreCase)
            || Regex.IsMatch(Svg, "stroke=\"#cc6600\"", RegexOptions.IgnoreCase)
            || Regex.IsMatch(Svg, "style=\"[^\"]*stroke:#c60", RegexOptions.IgnoreCase)
            || Regex.IsMatch(Svg, "style=\"[^\"]*stroke:#cc6600", RegexOptions.IgnoreCase);
        bool HasWidth = Regex.IsMatch(Svg, "stroke-width=\"3(\\.0+)?\"", RegexOptions.IgnoreCase)
            || Regex.IsMatch(Svg, "style=\"[^\"]*stroke-width:3(\\.0+)?", RegexOptions.IgnoreCase);

        Assert.That(HasStroke, Is.True, "Expected `linkStyle default stroke:#c60` to affect rendered edge stroke.");
        Assert.That(HasWidth, Is.True, "Expected `linkStyle default stroke-width:3px` to affect rendered edge width.");

        // Arrow markers should be tinted to match the edge stroke colour.
        bool HasTintedMarkerDefinition = Regex.IsMatch(Svg, "marker id=\"mermaid-svg_flowchart-v2-pointEnd--cc6600\"", RegexOptions.IgnoreCase);
        bool UsesTintedMarker = Regex.IsMatch(Svg, "marker-end=\"url\\(#mermaid-svg_flowchart-v2-pointEnd(--margin|Thick)?--cc6600\\)\"", RegexOptions.IgnoreCase);
        bool MarkerUsesInlineStyle = Regex.IsMatch(
            Svg,
            "mermaid-svg_flowchart-v2-pointEnd--cc6600[\\s\\S]*?style=\"[^\"]*fill:#cc6600",
            RegexOptions.IgnoreCase);
        Assert.That(HasTintedMarkerDefinition, Is.True, "Expected a colour-variant arrow marker definition to be emitted.");
        Assert.That(UsesTintedMarker, Is.True, "Expected the edge to reference the colour-variant arrow marker.");
        Assert.That(MarkerUsesInlineStyle, Is.True, "Expected arrow marker path to use inline fill/stroke style (Word parity).");
    }
}

