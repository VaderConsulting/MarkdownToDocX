using System.Text.RegularExpressions;
using System.Xml.Linq;

using MermaidRecognition.Contract;
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
        DiagramContract Contract = MermaidRecognizer.Parse($"""
            flowchart {Direction}
            A[Request] -->{EdgeLabel} B[Approved]
            A -->{EdgeLabel} C[Rejected]
            """);
        XDocument Svg = XDocument.Parse(FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract));
        foreach (string Target in new[] { "B", "C" })
        {
            string[] Paths = [.. Svg.Descendants()
                .Where(Element => Element.Name.LocalName == "path"
                    && (string?)Element.Attribute("data-edge-source") == "A"
                    && (string?)Element.Attribute("data-edge-target") == Target)
                .Select(Element => (string)Element.Attribute("d")!)];
            foreach (string Path in Paths)
            {
                OrthogonalRouteTestHelper.AssertEveryBendRounded(Path);
                int CubicCount = Regex.Matches(Path, "C").Count;
                if (Regex.Matches(Path, "L").Count > 1)
                {
                    Assert.That(CubicCount, Is.GreaterThan(0),
                        "A bent connector must be rounded even when its label cannot fit a gap.");
                }
            }
        }
    }

    [Test]
    public void Render_ForcedElbow_ExercisesRoundedCorner()
    {
        // An explicit orthogonal elbow prevents a straight fan from making this
        // rounding regression vacuous when placement improves.
        string Path = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(
            [(0d, 0d), (120d, 0d), (120d, 120d)], 20);
        OrthogonalRouteTestHelper.AssertOrthogonalPathWithRoundedCorners(Path);
        Assert.That(Path, Does.Contain("C"));
    }
}
