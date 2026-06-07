using System.Text.RegularExpressions;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class InvisibleEdgeRenderTests
{
    [Test]
    public void TryRenderFlowchartSvg_InvisibleEdge_DoesNotRenderVisibleStroke()
    {
        string Source = """
            flowchart LR
            A[A] --> B[B]
            B ~~~ C[C]
            """;

        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            Source,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);

        MatchCollection Paths = Regex.Matches(Svg, @"<path[^>]*class=""flowchart-link""[^>]*>", RegexOptions.IgnoreCase);
        Assert.That(Paths.Count, Is.GreaterThanOrEqualTo(2));

        bool FoundInvisibleStroke = false;
        for (int Index = 0; Index < Paths.Count; Index++)
        {
            string PathTag = Paths[Index].Value;
            if (PathTag.Contains("stroke=\"none\"", StringComparison.OrdinalIgnoreCase)
                || PathTag.Contains("stroke-width=\"0\"", StringComparison.OrdinalIgnoreCase))
            {
                FoundInvisibleStroke = true;
                break;
            }
        }

        Assert.That(FoundInvisibleStroke, Is.True, "Expected at least one rendered edge path to have no visible stroke for `~~~`.");
    }
}

