using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseTitleBoundsTests
{
    [TestCase("A deliberately long title with \"quotes\", <br>, ampersands & Unicode – Ünïcödé ✓")]
    [TestCase("WWWW WWWW WWWW WWWW WWWW WWWW WWWW WWWW")]
    [TestCase("業務構成の概要と詳細情報についての長い見出しと説明文")]
    public void WideLiteralTitle_ExpandsCanvasAndCentresUnchangedDiagram(string Title)
    {
        const string Body = " application-component a \"Input\"\n application-component b \"Output\"\n a -flow-> b";
        XDocument Plain = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg("enterprise\n" + Body));
        XDocument Titled = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg("enterprise\n title " + Title + "\n" + Body));
        double PlainWidth = Number(Plain.Root!, "width"), Width = Number(Titled.Root!, "width");
        Assert.That(Width, Is.GreaterThan(PlainWidth + 100), "A title wider than the diagram must enlarge the viewport.");
        XElement Text = Titled.Root!.Elements().Single(Element => Element.Name.LocalName == "text");
        Assert.That(Text.Value, Is.EqualTo(Title), "The title is literal text, including special characters.");
        Assert.That(Number(Text, "x"), Is.EqualTo(Width / 2).Within(0.01));
        XElement Content = Titled.Root.Elements().Single(Element => Element.Name.LocalName == "g");
        double[] Translation = [.. Regex.Matches((string)Content.Attribute("transform")!, @"-?\d+(?:\.\d+)?").Select(Match => double.Parse(Match.Value, CultureInfo.InvariantCulture))];
        Assert.That(Translation[0], Is.EqualTo((Width - PlainWidth) / 2).Within(0.01));
        Assert.That(Translation[1], Is.GreaterThan(Number(Text, "y")));
        IEnumerable<string> PlainElements = Plain.Descendants().Where(Element => Element.Attribute("data-id") != null).Select(Element => Element.ToString());
        IEnumerable<string> TitledElements = Titled.Descendants().Where(Element => Element.Attribute("data-id") != null).Select(Element => Element.ToString());
        Assert.That(TitledElements, Is.EqualTo(PlainElements), "Title space must not distort node geometry.");
    }

    private static double Number(XElement Element, string Name) => double.Parse((string)Element.Attribute(Name)!, CultureInfo.InvariantCulture);
}
