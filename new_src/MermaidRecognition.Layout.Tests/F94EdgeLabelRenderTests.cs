using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F94EdgeLabelRenderTests
{
    [Test]
    public void RenderToSvg_F94_MultiLineEdgeLabel_IsVerticallyCentredOnConnector()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A[A] -->|"Line 1<br/>Line 2<br/>Line 3"| B[B]
            """);

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Match HorizontalSpineLine = Regex.Matches(
                Svg,
                @"<line x1=""([^""]+)"" y1=""([^""]+)"" x2=""([^""]+)"" y2=""([^""]+)""")
            .Cast<Match>()
            .Where(Match =>
            {
                double X1 = double.Parse(Match.Groups[1].Value, CultureInfo.InvariantCulture);
                double Y1 = double.Parse(Match.Groups[2].Value, CultureInfo.InvariantCulture);
                double X2 = double.Parse(Match.Groups[3].Value, CultureInfo.InvariantCulture);
                double Y2 = double.Parse(Match.Groups[4].Value, CultureInfo.InvariantCulture);
                return Math.Abs(Y1 - Y2) < 0.5 && Math.Abs(X2 - X1) > 20.0;
            })
            .First();

        double SpineMidSvgY =
            (double.Parse(HorizontalSpineLine.Groups[2].Value, CultureInfo.InvariantCulture)
            + double.Parse(HorizontalSpineLine.Groups[4].Value, CultureInfo.InvariantCulture)) / 2.0;

        Match LabelText = Regex.Match(
            Svg,
            @"<text y=""([^""]+)""[^>]*dominant-baseline=""middle""[^>]*>(.*?)</text>",
            RegexOptions.Singleline);
        Assert.That(LabelText.Success, Is.True, Svg);

        double FirstLineSvgY = double.Parse(LabelText.Groups[1].Value, CultureInfo.InvariantCulture);
        int LineCount = Regex.Matches(LabelText.Groups[2].Value, "<tspan", RegexOptions.IgnoreCase).Count;
        double LineHeightPx = Options.EdgeLabelFontSizePx * 1.5;
        double LabelCentreSvgY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(
            FirstLineSvgY,
            LineCount,
            Options.EdgeLabelFontSizePx,
            1.5,
            UseMiddleDominantBaseline: true);
        double MiddleLineCentreSvgY = FirstLineSvgY + LineHeightPx;

        TestContext.WriteLine(
            $"Spine Y: {SpineMidSvgY}, Label centre Y: {LabelCentreSvgY}, Middle line centre Y: {MiddleLineCentreSvgY}, First line Y: {FirstLineSvgY}");
        Assert.That(LabelCentreSvgY, Is.EqualTo(SpineMidSvgY).Within(2.0));
        Assert.That(MiddleLineCentreSvgY, Is.EqualTo(SpineMidSvgY).Within(2.0));
    }
}
