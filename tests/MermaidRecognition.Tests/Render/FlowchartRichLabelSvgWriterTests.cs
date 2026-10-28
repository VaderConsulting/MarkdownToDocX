using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

[TestFixture]
public sealed class FlowchartRichLabelSvgWriterTests
{
    [Test]
    public void ContainsIntentionalRichMarkup_DetectsHtmlAndMarkdown()
    {
        Assert.That(
            FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup("<b>Bold</b> <i>Italic</i>"),
            Is.True);
        Assert.That(
            FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup("Use **bold** text"),
            Is.True);
    }

    [Test]
    public void ContainsIntentionalRichMarkup_IgnoresBareDecodedSymbols()
    {
        Assert.That(
            FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup("Symbols: star* underscore_ backtick`"),
            Is.False);
        Assert.That(
            FlowchartRichLabelSvgWriter.ContainsIntentionalRichMarkup("Comparison: a< b && c> d"),
            Is.False);
    }

    [Test]
    public void GetMultilineBlockStartX_UnevenLines_UsesWidestLineForCentreing()
    {
        List<string> Lines =
        [
            "- Item 1",
            "- Item 22",
            "- Item 333"
        ];

        const double CentreX = 100.0;
        double BlockStartX = FlowchartRichLabelSvgWriter.GetMultilineBlockStartX(CentreX, Lines, 16.0);
        double MaxWidthPx = FlowchartRichLabelSvgWriter.EstimateStyledLabelMaxWidthPx(
            "- Item 1<br/>- Item 22<br/>- Item 333",
            16.0);

        Assert.That(BlockStartX, Is.EqualTo(CentreX - (MaxWidthPx / 2.0)).Within(0.01));
    }

    [Test]
    public void IsMarkdownListLabel_DetectsBulletLines()
    {
        Assert.That(
            FlowchartRichLabelSvgWriter.IsMarkdownListLabel("- Item 1\n- Item 2\n- Item 3"),
            Is.True);
        Assert.That(
            FlowchartRichLabelSvgWriter.IsMarkdownListLabel("Item 1\nItem 2"),
            Is.False);
    }

    [Test]
    public void StripRichMarkupToPlainText_TableLabel_ProducesRowText()
    {
        Assert.That(
            FlowchartRichLabelSvgWriter.StripRichMarkupToPlainText(
                "<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>"),
            Is.EqualTo("A  B\nC  D"));
    }

    [Test]
    public void AppendCentredLabel_RichHtmlLine_UsesStartAnchorOffsetFromCentre()
    {
        const double CentreX = 200.0;
        StringBuilder Builder = new StringBuilder();
        FlowchartRichLabelSvgWriter.AppendCentredLabel(
            Builder,
            "<b>Bold</b> <i>Italic</i> <span style='color:red'>Red</span>",
            CentreX,
            50.0,
            16.0,
            "#333",
            "trebuchet ms, verdana, arial, sans-serif",
            "normal",
            1.5);

        string Svg = Builder.ToString();
        Assert.That(Svg, Does.Contain("text-anchor=\"start\""));
        Assert.That(Svg, Does.Not.Contain("text-anchor=\"middle\""));

        Match TspanMatch = Regex.Match(Svg, @"<tspan x=""([-\d.]+)"" dy=""0"">");
        Assert.That(TspanMatch.Success, Is.True);

        double LineStartX = double.Parse(TspanMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double LineWidthPx = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(
            "<b>Bold</b> <i>Italic</i> <span style='color:red'>Red</span>",
            16.0);
        double ExpectedStartX = CentreX - (LineWidthPx / 2.0);
        Assert.That(LineStartX, Is.EqualTo(ExpectedStartX).Within(0.01));
    }

    [Test]
    public void ComputeFirstBaselineY_ThreeLineBlock_CentresBlockOnSpine()
    {
        const double SpineY = 43.0;
        double FirstBaselineY = FlowchartRichLabelSvgWriter.ComputeFirstBaselineY(SpineY, 3, 12.0, 1.5);
        double BlockCentreY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(FirstBaselineY, 3, 12.0, 1.5);
        double FirstLineCentreY = FirstBaselineY - FlowchartRichLabelSvgWriter.ComputeBaselineOffsetPx(12.0);

        Assert.That(FirstBaselineY, Is.EqualTo(25.0 + (12.0 * FlowchartRichLabelSvgWriter.TEXT_BASELINE_OFFSET_EM)).Within(0.01));
        Assert.That(BlockCentreY, Is.EqualTo(SpineY).Within(0.01));
        Assert.That(FirstLineCentreY + 18.0, Is.EqualTo(SpineY).Within(0.01));
    }

    [Test]
    public void AppendCentredLabel_SingleLine_WritesExplicitBaselineWithoutDominantBaseline()
    {
        StringBuilder Builder = new StringBuilder();
        FlowchartRichLabelSvgWriter.AppendCentredLabel(Builder, "<b>Bold</b>", 100.0, 50.0, 16.0, "#333", "trebuchet ms", "normal", 1.5);
        string Svg = Builder.ToString();
        Match TextMatch = Regex.Match(Svg, @"<text y=""([^""]+)""");

        Assert.That(TextMatch.Success, Is.True, Svg);
        Assert.That(double.Parse(TextMatch.Groups[1].Value, CultureInfo.InvariantCulture), Is.EqualTo(50.0 + (16.0 * FlowchartRichLabelSvgWriter.TEXT_BASELINE_OFFSET_EM)).Within(0.01));
        Assert.That(Svg, Does.Not.Contain("dominant-baseline"));
    }
}
