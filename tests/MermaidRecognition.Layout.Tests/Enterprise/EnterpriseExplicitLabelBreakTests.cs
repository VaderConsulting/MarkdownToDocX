using System.Globalization;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseExplicitLabelBreakTests
{
    [TestCase("<br>")]
    [TestCase("<br/>")]
    [TestCase("<BR />")]
    [TestCase("\\n")]
    public void ExplicitBreaks_RenderSeparateLinesInGroupsNodesAndRelationships(string Separator)
    {
        string Label = "Alpha" + Separator + "Beta";
        string Source = $"enterprise\n group g \"{Label}\"\n application-component a \"{Label}\" in g\n application-component b \"Target\"\n a -flow-> b \"{Label}\"";
        XDocument Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        foreach (string Id in new[] { "g", "a" })
        {
            XElement Node = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == Id);
            Assert.That(Node.Elements().Where(Element => Element.Name.LocalName == "text").Select(Element => Element.Value), Is.EqualTo(new[] { "Alpha", "Beta" }));
        }
        XElement Edge = Svg.Descendants().Single(Element => Element.Attribute("data-source") != null);
        Assert.That(Edge.Descendants().Where(Element => Element.Name.LocalName == "tspan").Select(Element => Element.Value), Is.EqualTo(new[] { "Alpha", "Beta" }));
        Assert.That(Svg.ToString(), Does.Not.Contain("&lt;br"));
    }

    [Test]
    public void JunctionCaption_UsesSeparateLinesAndKeepsItsSymbolSmall()
    {
        const string Source = "enterprise\n or-junction a \"First<br>Second\"";
        var Document = EnterpriseValidator.Analyze(Source).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Plain = EnterpriseLayoutEngine.Layout(EnterpriseValidator.Analyze("enterprise\n or-junction a \"First\"").Document);
        Assert.That(Layout.Nodes["a"].Height, Is.EqualTo(Plain.Nodes["a"].Height), "External caption lines must not inflate the symbol.");
        var Svg = XDocument.Parse(EnterpriseSvgRenderer.Render(Document));
        var Node = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == "a");
        Assert.That(Node.Elements().Where(Element => Element.Name.LocalName == "text").Select(Element => Element.Value), Is.EqualTo(new[] { "First", "Second" }));
    }

    [Test]
    public void MultilineGroupTitleAndNode_ReserveSpaceBeforeMemberContent()
    {
        string Source = "enterprise\n group g \"One<br>Two<br>Three<br>Four\"\n application-component a \"First<br>Second<br>Third<br>Fourth<br>Fifth\" in g";
        var Document = EnterpriseValidator.Analyze(Source).Document;
        var Layout = EnterpriseLayoutEngine.Layout(Document);
        var Group = Layout.Nodes["g"];
        var Member = Layout.Nodes["a"];
        Assert.That(Member.Height, Is.GreaterThanOrEqualTo(5 * 15 + 26));
        Assert.That(Group.AbsoluteY + 18 + 3 * 15, Is.LessThan(Member.AbsoluteY), "The multiline tab must clear its member.");
        XDocument Svg = XDocument.Parse(EnterpriseSvgRenderer.Render(Document));
        XElement Tab = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == "g");
        XElement Body = Tab.Elements().Single(Element => Element.Name.LocalName == "rect");
        double BodyTop = double.Parse((string)Body.Attribute("y")!, CultureInfo.InvariantCulture);
        double LastBaseline = Tab.Elements().Where(Element => Element.Name.LocalName == "text")
            .Max(Element => double.Parse((string)Element.Attribute("y")!, CultureInfo.InvariantCulture));
        Assert.That(LastBaseline, Is.LessThan(BodyTop));
    }
}
