using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartEdgeLabelGapTests
{
    const string FormatSource = """
        flowchart TD
            M[Inbound] --> F{Format v1 / v2 / v3?}
            F -->|v1| T1[Translator v1]
            F -->|v2| T2[Translator v2]
            F -->|unknown| I[Invalid / quarantine]
        """;

    [Test]
    public void Render_FormatV1Diagram_BreaksStrokeAroundEdgeLabels()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(FormatSource);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        Assert.That(Svg, Does.Not.Contain("fill=\"#ffffff\""),
            "Rule B13 breaks the stroke instead of masking with a white rectangle.");

        foreach (string Label in new[] { "v1", "v2", "unknown" })
        {
            AssertLabelHasStrokeGap(Svg, Label);
        }
    }

    internal static void AssertLabelHasStrokeGap(string Svg, string Label) =>
        AssertLabelHasStrokeGap(Svg, Label, FlowchartRenderOptions.FromContract(MermaidRecognizer.Parse(FormatSource)));

    internal static void AssertLabelHasStrokeGap(string Svg, string Label, FlowchartRenderOptions Options)
    {
        Match LabelMatch = Regex.Match(
            Svg,
            $@"<text[^>]*>\s*<tspan[^>]*>{Regex.Escape(Label)}</tspan>",
            RegexOptions.CultureInvariant);
        if (!LabelMatch.Success)
        {
            LabelMatch = Regex.Match(
                Svg,
                $@"<text[^>]*>.*?<tspan[^>]*>[^<]*{Regex.Escape(Label)}",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        Assert.That(LabelMatch.Success, Is.True, $"Could not find label '{Label}' in SVG.");

        Match TextMatch = Regex.Match(LabelMatch.Value, @"<text([^>]*)>", RegexOptions.CultureInvariant);
        string TextAttributes = TextMatch.Success ? TextMatch.Groups[1].Value : LabelMatch.Value;
        Match LabelXMatch = Regex.Match(TextAttributes, @"\bx=""([\d.]+)""", RegexOptions.CultureInvariant);
        if (!LabelXMatch.Success)
        {
            LabelXMatch = Regex.Match(LabelMatch.Value, @"<tspan[^>]*\bx=""([\d.]+)""", RegexOptions.CultureInvariant);
        }

        Assert.That(LabelXMatch.Success, Is.True, $"Could not locate label x for '{Label}'.");
        double LabelX = double.Parse(LabelXMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double LabelY = double.Parse(
            Regex.Match(TextAttributes, @"\by=""([\d.]+)""").Groups[1].Value,
            CultureInfo.InvariantCulture);
        int LineCount = Regex.Matches(LabelMatch.Value, "<tspan", RegexOptions.IgnoreCase).Count;
        if (LineCount < 1)
        {
            LineCount = 1;
        }

        FlowchartLabelDimensions Dimensions = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
            Label,
            Options,
            Options.EdgeLabelFontSizePx);
        double HalfGap = FlowchartEdgeLabelGapHelper.ComputeHalfGapWidthPx(
            Dimensions,
            Options.EdgeLabelFontSizePx);
        double LabelCentreY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(
            LabelY,
            LineCount,
            Options.EdgeLabelFontSizePx,
            1.5);

        AssertLabelHasStrokeGapAt(Svg, LabelX, LabelCentreY, HalfGap, Label);
    }

    internal static void AssertLabelHasStrokeGapAt(
        string Svg,
        double LabelX,
        double LabelCentreY,
        double HalfGap,
        string LabelName)
    {
        Assert.That(HasStrokeGapBesideLabel(Svg, LabelX, LabelCentreY, HalfGap), Is.True,
            $"Expected a stroke gap on each side of label '{LabelName}'.");
    }

    static bool HasStrokeGapBesideLabel(string Svg, double LabelX, double LabelCentreY, double HalfGap)
    {
        const double TolerancePx = 0.75;
        foreach (Match LineMatch in Regex.Matches(Svg, "<line[^>]+/>", RegexOptions.CultureInvariant))
        {
            string Line = LineMatch.Value;
            if (!TryParseLineAttribute(Line, "x1", out double X1)
                || !TryParseLineAttribute(Line, "y1", out double Y1)
                || !TryParseLineAttribute(Line, "x2", out double X2)
                || !TryParseLineAttribute(Line, "y2", out double Y2))
            {
                continue;
            }

            bool Horizontal = Math.Abs(Y1 - Y2) <= TolerancePx
                && Math.Abs(Y1 - LabelCentreY) <= TolerancePx;
            if (Horizontal
                && Math.Abs(X2 - (LabelX - HalfGap)) <= TolerancePx
                && X2 < LabelX)
            {
                return true;
            }

            bool Vertical = Math.Abs(X1 - X2) <= TolerancePx
                && Math.Abs(X1 - LabelX) <= TolerancePx;
            if (Vertical
                && Math.Abs(Y2 - (LabelCentreY - HalfGap)) <= TolerancePx
                && Y2 < LabelCentreY)
            {
                return true;
            }
        }

        return false;
    }

    static bool TryParseLineAttribute(string Line, string Attribute, out double Value)
    {
        Match Match = Regex.Match(
            Line,
            $@"{Attribute}=""([\d.]+)""",
            RegexOptions.CultureInvariant);
        if (!Match.Success)
        {
            Value = 0.0;
            return false;
        }

        return double.TryParse(Match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
    }
}
