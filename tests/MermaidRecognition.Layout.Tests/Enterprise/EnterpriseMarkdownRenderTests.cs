using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseMarkdownRenderTests
{
    [Test]
    public void MarkdownLabels_RenderBoldItalicAndExplicitSourceBreaks()
    {
        var Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg("enterprise\napplication-component first \"`**Bold**\n*Italic*`\"\napplication-service second\nfirst -realization-> second \"`**Link**`\""));
        Assert.That(Svg.Descendants().Any(Item => (string?)Item.Attribute("font-weight") == "bold" && Item.Value == "Bold"), Is.True);
        Assert.That(Svg.Descendants().Any(Item => (string?)Item.Attribute("font-style") == "italic" && Item.Value == "Italic"), Is.True);
        Assert.That(Svg.Descendants().Any(Item => (string?)Item.Attribute("font-weight") == "bold" && Item.Value == "Link"), Is.True);
        Assert.That(string.Join("", Svg.Descendants().Where(Item => Item.Name.LocalName == "text").Select(Item => Item.Value)), Does.Not.Contain("*"));
    }
}
