using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class F93MarkdownListRenderTests
{
    const string F93_SOURCE = """
        flowchart LR
        A["- Item 1<br/>- Item 2<br/>- Item 3"] --> B[Next]
        """;

    [Test]
    public void RenderToSvg_F93_EmitsThreeLeftAlignedListLines()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F93_SOURCE);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        MatchCollection LineTspans = Regex.Matches(Svg, @"<tspan x=""([-\d.]+)""[^>]*>([^<]+)</tspan>");
        List<Match> ListLineTspans = LineTspans
            .Cast<Match>()
            .Where(Match => Match.Groups[2].Value.StartsWith("- Item", StringComparison.Ordinal))
            .ToList();

        Assert.That(ListLineTspans, Has.Count.EqualTo(3));
        Assert.That(ListLineTspans[0].Groups[2].Value, Is.EqualTo("- Item 1"));
        Assert.That(ListLineTspans[1].Groups[2].Value, Is.EqualTo("- Item 2"));
        Assert.That(ListLineTspans[2].Groups[2].Value, Is.EqualTo("- Item 3"));

        double FirstLineX = double.Parse(ListLineTspans[0].Groups[1].Value, CultureInfo.InvariantCulture);
        foreach (Match ListLine in ListLineTspans)
        {
            double LineX = double.Parse(ListLine.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(LineX, Is.EqualTo(FirstLineX).Within(0.01));
        }
    }

    [Test]
    public void TryLayout_F93_ListNode_IsSquare()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F93_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutNodeBounds ListNode = Layout.NodeBounds.First(Bounds => Bounds.NodeId == "A");
        Assert.That(ListNode.Width, Is.EqualTo(ListNode.Height).Within(0.5));
    }

    [Test]
    public void RenderToSvg_F93_UnevenListLines_ShareCommonLeftEdge()
    {
        DiagramContract Contract = MermaidRecognizer.Parse("""
            flowchart LR
            A["- Item 1<br/>- Item 22<br/>- Item 333"] --> B[Next]
            """);

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        MatchCollection LineTspans = Regex.Matches(Svg, @"<tspan x=""([-\d.]+)""[^>]*>- Item");
        Assert.That(LineTspans, Has.Count.EqualTo(3));

        double FirstLineX = double.Parse(LineTspans[0].Groups[1].Value, CultureInfo.InvariantCulture);
        foreach (Match ListLine in LineTspans)
        {
            double LineX = double.Parse(ListLine.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(LineX, Is.EqualTo(FirstLineX).Within(0.01));
        }
    }
}
