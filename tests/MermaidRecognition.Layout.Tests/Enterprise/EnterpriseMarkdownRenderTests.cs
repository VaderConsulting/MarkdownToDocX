using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseMarkdownRenderTests
{
    [Test]
    public void FormattingBoundaries_PreserveSurroundingSpaces()
    {
        var Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg("enterprise\napplication-component first \"`Before **bold** after`\""));
        var Text = Svg.Descendants().Single(Item => Item.Name.LocalName == "text");
        Assert.That(Text.Value, Is.EqualTo("Before bold after"));
        Assert.That((string?)Text.Attribute(XNamespace.Xml + "space"), Is.EqualTo("preserve"));
    }

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
