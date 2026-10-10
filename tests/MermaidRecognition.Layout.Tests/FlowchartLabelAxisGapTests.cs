using System.Globalization;
using System.Xml.Linq;

using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartLabelAxisGapTests
{
    [TestCase("TB", false, "Step 1: validate input", 1)]
    [TestCase("BT", false, "Step 1: validate input", 1)]
    [TestCase("LR", true, "Step 1: validate input", 1)]
    [TestCase("RL", true, "Step 1: validate input", 1)]
    [TestCase("TB", false, "Line 1<br/>Line 2<br/>Line 3", 3)]
    [TestCase("BT", false, "Line 1<br/>Line 2<br/>Line 3", 3)]
    [TestCase("LR", true, "Line 1<br/>Line 2<br/>Line 3", 3)]
    [TestCase("RL", true, "Line 1<br/>Line 2<br/>Line 3", 3)]
    public void Render_StraightLabel_BreakMatchesTextExtentAlongConnector(
        string Direction, bool Horizontal, string Label, int LineCount)
    {
        var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nA[Source] -->|\"{Label}\"| B[Target]");
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        XDocument Svg = XDocument.Parse(FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract));
        XElement[] Lines = Svg.Descendants().Where(Element => Element.Name.LocalName == "line"
            && (string?)Element.Attribute("data-edge-source") == "A"
            && (string?)Element.Attribute("data-edge-target") == "B").ToArray();
        Assert.That(Lines, Has.Length.EqualTo(2), "The stroke must resume on both sides of the label.");
        string Axis = Horizontal ? "x" : "y";
        var Spans = Lines.Select(Line => new[] { Read(Line, Axis + "1"), Read(Line, Axis + "2") })
            .Select(Points => (Start: Points.Min(), End: Points.Max())).OrderBy(Span => Span.Start).ToArray();
        double TextExtent = Horizontal
            ? FlowchartNodeSizeEstimator.MeasureWrappedLabel(Label, Options, Options.EdgeLabelFontSizePx).Width
            : Options.EdgeLabelFontSizePx * (LineCount == 1 ? 1 : LineCount * 1.5);
        double ExpectedGap = TextExtent + 2 * Options.EdgeLabelFontSizePx * 0.512;
        Assert.That(Spans[1].Start - Spans[0].End, Is.EqualTo(ExpectedGap).Within(0.01));
        Assert.That(Spans[0].End - Spans[0].Start, Is.GreaterThanOrEqualTo(10));
        Assert.That(Spans[1].End - Spans[1].Start, Is.GreaterThanOrEqualTo(10));
    }

    [TestCase("TB")]
    [TestCase("BT")]
    public void Layout_VerticalSingleLineLabel_WidthDoesNotInflateRankGap(string Direction)
    {
        double Gap(string Label)
        {
            var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nA[Source] -->|\"{Label}\"| B[Target]");
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);
            var Source = Layout.NodeBounds.Single(Node => Node.NodeId == "A");
            var Target = Layout.NodeBounds.Single(Node => Node.NodeId == "B");
            return Math.Abs(Target.CenterY - Source.CenterY) - (Source.Height + Target.Height) / 2;
        }

        Assert.That(Gap("Step 1: validate input"), Is.EqualTo(Gap("ok")),
            "Labels with the same height need the same vertical space.");
    }

    private static double Read(XElement Element, string Attribute) =>
        double.Parse((string)Element.Attribute(Attribute)!, CultureInfo.InvariantCulture);
}
