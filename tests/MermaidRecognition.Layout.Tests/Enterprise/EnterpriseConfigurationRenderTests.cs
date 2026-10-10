using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseConfigurationRenderTests
{
    [Test]
    public void SupportedTypography_ScalesContentAndKeepsMeasuredGeometry()
    {
        string Source = "enterprise\napplication-component first";
        var Plain = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source)).Root!;
        var Configured = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(
            "%%{init: {\"themeVariables\":{\"fontSize\":\"24px\",\"fontFamily\":\"Verdana\",\"background\":\"#eeeeee\"},\"unknown\":true}}%%\n" + Source)).Root!;
        Assert.That((double)Configured.Attribute("width")!, Is.EqualTo((double)Plain.Attribute("width")! * 2));
        Assert.That((string?)Configured.Attribute("viewBox"), Is.EqualTo((string?)Plain.Attribute("viewBox")));
        Assert.That((string?)Configured.Attribute("font-family"), Is.EqualTo("Verdana"));
        Assert.That((string?)Configured.Elements().First(Element => Element.Name.LocalName == "rect").Attribute("fill"), Is.EqualTo("#eeeeee"));
    }
}
