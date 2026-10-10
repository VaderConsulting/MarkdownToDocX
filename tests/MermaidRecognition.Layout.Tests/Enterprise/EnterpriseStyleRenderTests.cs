using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseStyleRenderTests
{
    [Test]
    public void Styles_ChangePaintWhilePreservingIconsAndEndMarks()
    {
        string Source = "enterprise\napplication-component first\napplication-service second\nfirst -realization-> second 'Label'";
        var Plain = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        var Styled = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source + "\nclassDef default fill:#abcdef,stroke:red\nlinkStyle 0 stroke:blue,stroke-width:3px,stroke-dasharray:5\\,5,opacity:0.6,color:green"));
        var Element = Styled.Descendants().First(Item => (string?)Item.Attribute("data-id") == "first");
        Assert.That((string?)Element.Elements().First().Attribute("fill"), Is.EqualTo("#abcdef"));
        var Icon = Element.Descendants().First(Item => (string?)Item.Attribute("class") == "notation-icon");
        var OriginalIcon = Plain.Descendants().First(Item => (string?)Item.Attribute("class") == "notation-icon");
        Assert.That(Icon.ToString(), Is.EqualTo(OriginalIcon.ToString()));
        var Link = Styled.Descendants().First(Item => Item.Attribute("data-source") != null);
        var Path = Link.Elements().First();
        Assert.That((string?)Path.Attribute("stroke"), Is.EqualTo("blue"));
        Assert.That((string?)Path.Attribute("stroke-dasharray"), Is.EqualTo("5,5"));
        Assert.That((string?)Path.Attribute("opacity"), Is.EqualTo("0.6"));
        Assert.That((string?)Link.Descendants().First(Item => Item.Name.LocalName == "text").Attribute("fill"), Is.EqualTo("green"));
        Assert.That(Link.Elements().Skip(1).Where(Item => Item.Name.LocalName != "text").Select(Item => Item.ToString()),
            Is.EqualTo(Plain.Descendants().First(Item => Item.Attribute("data-source") != null).Elements().Skip(1).Where(Item => Item.Name.LocalName != "text").Select(Item => Item.ToString())));
    }
}
