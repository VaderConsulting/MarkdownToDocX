using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseOutlineAndLabelTests
{
    [TestCase("+")]
    [TestCase("-")]
    public void ShortInfluenceLabel_HasCharacterClearanceWithoutAnHtmlBox(string Strength)
    {
        string Source = $"enterprise\n direction LR\n assessment a \"Input\"\n goal b \"Outcome\"\n a -influence({Strength})-> b";
        XDocument Document = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        XElement Edge = Document.Descendants().Single(Element => Element.Attribute("data-source") != null);
        string Path = (string)Edge.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!;
        string[] Fragments = Path.Split('M', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(Fragments, Has.Length.EqualTo(2));
        double[] Before = Coordinates(Fragments[0]), After = Coordinates(Fragments[1]);
        double Gap = After[0] - Before[^2];
        XElement Text = Edge.Elements().Single(Element => Element.Name.LocalName == "text");
        double FontSize = Number(Text, "font-size");
        Assert.That(Gap, Is.InRange(FontSize, 2 * FontSize), "A single glyph needs its width and one character of clearance on either side.");
        Assert.That(Number(Text, "x"), Is.EqualTo((After[0] + Before[^2]) / 2).Within(0.01));
    }

    [TestCase("TB", false)]
    [TestCase("LR", false)]
    [TestCase("TB", true)]
    [TestCase("LR", true)]
    public void MultipleRelationships_MeetTheDrawnEllipseOutline(string Direction, bool Outgoing)
    {
        string Relationships = Outgoing ? "c -association-> a\n c -association-> b" : "a -association-> c\n b -association-> c";
        string Source = $"enterprise\n direction {Direction}\n goal a \"First\"\n goal b \"Second\"\n value c \"Shared value\"\n {Relationships}";
        XDocument Document = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        XElement Ellipse = Document.Descendants().Single(Element => Element.Name.LocalName == "ellipse");
        double Cx = Number(Ellipse, "cx"), Cy = Number(Ellipse, "cy");
        double Rx = Number(Ellipse, "rx"), Ry = Number(Ellipse, "ry");
        Assert.That(Rx, Is.GreaterThan(Ry));
        foreach (XElement Edge in Document.Descendants().Where(Element => Element.Attribute("data-source") != null))
        {
            string Path = (string)Edge.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!;
            double[] Points = Coordinates(Path);
            double X = Outgoing ? Points[0] : Points[^2], Y = Outgoing ? Points[1] : Points[^1];
            double Outline = Math.Pow((X - Cx) / Rx, 2) + Math.Pow((Y - Cy) / Ry, 2);
            Assert.That(Outline, Is.EqualTo(1).Within(0.06), "Every terminal must meet the visible ellipse rather than an inscribed circle.");
        }
    }

    private static double Number(XElement Element, string Name) => double.Parse((string)Element.Attribute(Name)!, CultureInfo.InvariantCulture);
    private static double[] Coordinates(string Path) => Regex.Matches(Path, @"-?\d+(?:\.\d+)?")
        .Select(Match => double.Parse(Match.Value, CultureInfo.InvariantCulture)).ToArray();
}
