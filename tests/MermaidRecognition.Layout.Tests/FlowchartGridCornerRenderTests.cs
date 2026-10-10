using System.Text.RegularExpressions;
using System.Xml.Linq;

using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartGridCornerRenderTests
{
    [TestCase("LR", "approve")]
    [TestCase("RL", "approve")]
    [TestCase("TB", "approve")]
    [TestCase("BT", "approve")]
    [TestCase("LR", "")]
    [TestCase("RL", "")]
    [TestCase("TB", "")]
    [TestCase("BT", "")]
    [TestCase("LR", "x")]
    [TestCase("RL", "x")]
    [TestCase("TB", "x")]
    [TestCase("BT", "x")]
    public void Render_FanOut_RoundsEveryBendWithOrWithoutLabelGap(string Direction, string Label)
    {
        string EdgeLabel = Label.Length == 0 ? "" : $"|{Label}|";
        var Contract = MermaidRecognizer.Parse($"""
            flowchart {Direction}
            A[Request] -->{EdgeLabel} B[Approved]
            A -->{EdgeLabel} C[Rejected]
            """);
        XDocument Svg = XDocument.Parse(FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract));
        int RoundedCornerCount = 0;
        foreach (string Target in new[] { "B", "C" })
        {
            string[] Paths = Svg.Descendants()
                .Where(Element => Element.Name.LocalName == "path"
                    && (string?)Element.Attribute("data-edge-source") == "A"
                    && (string?)Element.Attribute("data-edge-target") == Target)
                .Select(Element => (string)Element.Attribute("d")!)
                .ToArray();
            foreach (string Path in Paths)
            {
                OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(Path);
                int CubicCount = Regex.Matches(Path, "C").Count;
                RoundedCornerCount += CubicCount;
                if (Regex.Matches(Path, "L").Count > 1)
                {
                    Assert.That(CubicCount, Is.GreaterThan(0),
                        "A bent connector must be rounded even when its label cannot fit a gap.");
                }
            }
        }
        Assert.That(RoundedCornerCount, Is.GreaterThan(0), "Expected at least one rounded branch.");
    }
}
