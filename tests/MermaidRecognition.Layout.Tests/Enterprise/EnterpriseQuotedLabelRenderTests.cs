using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseQuotedLabelRenderTests
{
    [Test]
    public void QuotedCommentsAndEntities_RenderAsSafeLiteralText()
    {
        const string Source = """
            enterprise
              title A title %% ignored
              group g "Frame #amp; review"
              application-component a "The \"quoted\" 20%% #lt;v2#gt;" in g
              data-object b "#169; #128578;"
              a -access(write)-> b "20%% #quot;daily#quot;" %% ignored
            """;
        XDocument Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        XElement Node = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == "a");
        Assert.That(string.Join(" ", Node.Elements().Where(Element => Element.Name.LocalName == "text").Select(Element => Element.Value)), Is.EqualTo("The \"quoted\" 20%% <v2>"));
        XElement Group = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == "g");
        Assert.That(Group.Elements().Single(Element => Element.Name.LocalName == "text").Value, Is.EqualTo("Frame & review"));
        XElement Target = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == "b");
        Assert.That(Target.Elements().Single(Element => Element.Name.LocalName == "text").Value, Is.EqualTo("© 🙂"));
        XElement Edge = Svg.Descendants().Single(Element => Element.Attribute("data-source") != null);
        Assert.That(string.Join(" ", Edge.Descendants().Where(Element => Element.Name.LocalName == "tspan").Select(Element => Element.Value)), Is.EqualTo("20%% \"daily\""));
        Assert.That(Svg.Descendants().Any(Element => Element.Name.LocalName == "v2"), Is.False, "Decoded angle brackets must stay text, not SVG markup.");
        Assert.That(Svg.Root!.Elements().Single(Element => Element.Name.LocalName == "text").Value, Is.EqualTo("A title"));
    }
}
