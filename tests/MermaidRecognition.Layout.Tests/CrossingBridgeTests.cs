using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Core;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CrossingBridgeTests
{
    private static string Stroke(string Path, string Source, string Target) =>
        $"<path d=\"{Path}\" stroke=\"black\" fill=\"none\" data-edge-source=\"{Source}\" data-edge-target=\"{Target}\"/>";

    [TestCase(20)]
    [TestCase(500)]
    public void InteriorCrossingNearLongRunEnd_StillHasBridge(int Length)
    {
        string Svg = "<svg>" + Stroke($"M0,0 L{Length},0", "a", "b")
            + Stroke("M10,-40 L10,40", "c", "d") + "</svg>";
        string Result = FlowchartSvgLineJump.Apply(Svg);
        Assert.That(FlowchartSvgLineJump.LastJumps, Has.Count.EqualTo(1));
        Assert.That(Result, Does.Contain("A6,6"));
    }

    [Test]
    public void Bridge_PreservesRoundedCornersAndMarkerAttributes()
    {
        const string Curve = "C100,0 120,0 120,20";
        string Svg = "<svg>" + Stroke("M50,-30 L50,30", "a", "b")
            + Stroke("M0,0 L100,0 " + Curve + " L120,80", "c", "d")
                .Replace("stroke=\"black\"", "marker-end=\"url(#end)\" stroke=\"black\"") + "</svg>";
        string Result = FlowchartSvgLineJump.Apply(Svg);
        Assert.That(Result, Does.Contain(Curve));
        Assert.That(Result, Does.Contain("marker-end=\"url(#end)\""));
        Assert.That(Result, Does.Contain("A6,6"));
    }

    [Test]
    public void LabelBreak_IsNotMistakenForCrossing()
    {
        string Svg = "<svg>" + Stroke("M0,0 L40,0 M60,0 L100,0", "a", "b")
            + Stroke("M50,-40 L50,40", "c", "d") + "</svg>";
        Assert.That(FlowchartSvgLineJump.Apply(Svg), Is.EqualTo(Svg));
        Assert.That(FlowchartSvgLineJump.LastJumps, Is.Empty);
    }

    [Test]
    public void SharedEndpoint_IsAJunctionWithoutBridge()
    {
        string Svg = "<svg>" + Stroke("M0,0 L50,0", "a", "b")
            + Stroke("M50,0 L50,40", "b", "c") + "</svg>";
        Assert.That(FlowchartSvgLineJump.Apply(Svg), Is.EqualTo(Svg));
    }

    [Test]
    public void CloselySpacedCrossings_EachHaveSeparateBridge()
    {
        string Svg = "<svg>" + Stroke("M40,-40 L40,40", "a", "b")
            + Stroke("M46,-40 L46,40", "c", "d")
            + Stroke("M0,0 L100,0", "e", "f") + "</svg>";
        string Result = FlowchartSvgLineJump.Apply(Svg);
        Assert.That(Regex.Matches(Result, "A2.4,2.4").Count, Is.EqualTo(2));
        Assert.That(FlowchartSvgLineJump.LastJumps, Has.Count.EqualTo(2));
    }

    [TestCase("TB", false)]
    [TestCase("LR", false)]
    [TestCase("BT", false)]
    [TestCase("RL", false)]
    [TestCase("TB", true)]
    public void PartnerRankRenderer_BridgesUnavoidableCrossings(string Direction, bool Enterprise)
    {
        string Source = Enterprise
            ? "enterprise\napplication-component a\napplication-component b\napplication-component c\napplication-component d\na -flow-> c\na -flow-> d\nb -flow-> c\nb -flow-> d"
            : $"flowchart {Direction}\na --> c\na --> d\nb --> c\nb --> d";
        string Svg = Enterprise ? EnterpriseRenderBridge.RenderEnterpriseSvg(Source)
            : FlowchartSugiyamaSvgRenderer.RenderToSvg(MermaidRecognizer.Parse(Source));
        Assert.That(FlowchartSvgLineJump.LastJumps, Is.Not.Empty);
        XDocument Document = XDocument.Parse(Svg);
        Assert.That(Document.Descendants().Where(Element => Element.Attribute("data-edge-source") != null)
            .Any(Element => ((string?)Element.Attribute("d"))?.Contains('A') == true), Is.True);
    }
}
