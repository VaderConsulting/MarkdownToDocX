using MermaidRecognition.NaiadAdapter;
using MermaidSharp;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F82TrapezoidViewBoxTests
{
    const string F82_PAIR_SOURCE = """
        flowchart TD
        Top[/Wide top/] --> Bottom[\Wide bottom/]
        """;

    [Test]
    public void TryRenderFlowchartSvg_F82Pair_ViewBoxIncludesSlantedShapeOutset()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            F82_PAIR_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.That(Rendered, Is.True);

        System.Text.RegularExpressions.Match ViewBoxMatch = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"viewBox=""([^""]+)""");
        Assert.That(ViewBoxMatch.Success, Is.True);

        string[] Parts = ViewBoxMatch.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(Parts, Has.Length.EqualTo(4));
        double ViewBoxWidth = double.Parse(Parts[2], System.Globalization.CultureInfo.InvariantCulture);

        System.Text.RegularExpressions.Match PathMatch = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"<path\b[^>]*\bd=""([^""]+)""[^>]*\bclass=""node""");
        Assert.That(PathMatch.Success, Is.True);

        string PathData = PathMatch.Groups[1].Value;
        double MaxX = PathData
            .Split(new[] { 'M', 'L' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Part => Part.Split(',')[0].Trim())
            .Where(Value => double.TryParse(Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(Value => double.Parse(Value, System.Globalization.CultureInfo.InvariantCulture))
            .Max();

        double ViewBoxMinX = double.Parse(Parts[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(
            ViewBoxMinX + ViewBoxWidth,
            Is.GreaterThan(MaxX + 1.0),
            "viewBox width must include slanted node geometry beyond the dagre box");
    }
}
