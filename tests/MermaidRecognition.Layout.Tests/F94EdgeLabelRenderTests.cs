using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
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

        SvgStraightConnector HorizontalSpineLine = SvgStraightConnectorReader.Read(Svg)
            .First(Connector => Math.Abs(Connector.Y1 - Connector.Y2) < 0.5 && Math.Abs(Connector.X2 - Connector.X1) > 20.0);

        double SpineMidSvgY = (HorizontalSpineLine.Y1 + HorizontalSpineLine.Y2) / 2.0;

        Match LabelText = Regex.Match(
            Svg,
            @"<text y=""([^""]+)""[^>]*>(.*?)</text>",
            RegexOptions.Singleline);
        Assert.That(LabelText.Success, Is.True, Svg);

        double FirstLineSvgY = double.Parse(LabelText.Groups[1].Value, CultureInfo.InvariantCulture);
        int LineCount = Regex.Matches(LabelText.Groups[2].Value, "<tspan", RegexOptions.IgnoreCase).Count;
        double LineHeightPx = Options.EdgeLabelFontSizePx * 1.5;
        double LabelCentreSvgY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(FirstLineSvgY, LineCount, Options.EdgeLabelFontSizePx, 1.5);
        double FirstLineCentreSvgY = FirstLineSvgY - FlowchartRichLabelSvgWriter.ComputeBaselineOffsetPx(Options.EdgeLabelFontSizePx);
        double MiddleLineCentreSvgY = FirstLineCentreSvgY + LineHeightPx;
        double LabelHalfHeight = (LineCount * LineHeightPx) / 2.0;

        TestContext.WriteLine(
            $"Spine Y: {SpineMidSvgY}, Label centre Y: {LabelCentreSvgY}, Middle line centre Y: {MiddleLineCentreSvgY}, First line Y: {FirstLineSvgY}");
        Assert.That(LineCount, Is.EqualTo(3));
        Assert.That(MiddleLineCentreSvgY, Is.EqualTo(LabelCentreSvgY).Within(0.5), "The middle line should sit at the centre of the label block.");
        Assert.That(LabelCentreSvgY, Is.EqualTo(SpineMidSvgY).Within(1.0),
            "Rule B13: multiline edge labels sit centred on the connector.");
        FlowchartLabelDimensions LabelDimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            "Line 1<br/>Line 2<br/>Line 3",
            Options,
            Options.EdgeLabelFontSizePx);
        double HalfGap = FlowchartEdgeLabelGapHelper.ComputeHalfGapWidthPx(
            LabelDimensions,
            Options.EdgeLabelFontSizePx);
        Match FirstTspan = Regex.Match(LabelText.Value, @"<tspan x=""([^""]+)""", RegexOptions.CultureInvariant);
        double LabelCentreSvgX = double.Parse(FirstTspan.Groups[1].Value, CultureInfo.InvariantCulture)
            + (LabelDimensions.Width / 2.0);
        FlowchartEdgeLabelGapTests.AssertLabelHasStrokeGapAt(
            Svg,
            LabelCentreSvgX,
            LabelCentreSvgY,
            HalfGap,
            "Line 1");
    }
}
