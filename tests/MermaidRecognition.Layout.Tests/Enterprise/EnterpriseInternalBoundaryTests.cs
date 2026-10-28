using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseInternalBoundaryTests
{
    [TestCase("TB", false)]
    [TestCase("TB", true)]
    [TestCase("LR", false)]
    [TestCase("LR", true)]
    public void VisibleParentChildRelationship_StaysInTheFreeContainerCorridor(string Direction, bool ParentSource)
    {
        string Relation = ParentSource ? "p -flow-> b" : "a -flow-> p";
        string Source = $$"""
            enterprise
              direction {{Direction}}
              application-component p "Container"
              application-function a "First" in p
              application-function b "Second" in p
              p -assignment-> a
              p -assignment-> b
              {{Relation}} "A caption"
              a -triggering-> b
            """;
        XDocument Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        XElement Rect(string Id) => Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == Id)
            .Elements().First(Element => Element.Name.LocalName == "rect");
        XElement Parent = Rect("p"), Child = Rect(ParentSource ? "b" : "a");
        XElement Edge = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-source") == (ParentSource ? "p" : "a")
            && (string?)Element.Attribute("data-target") == (ParentSource ? "b" : "p"));
        string Path = (string)Edge.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!;
        Assert.That(Path.Split('M', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(2), "The line must break around its caption.");
        double[] Points = Coordinates(Path);
        double ChildBottom = Number(Child, "y") + Number(Child, "height");
        double ParentBottom = Number(Parent, "y") + Number(Parent, "height");
        double ChildX = ParentSource ? Points[^2] : Points[0];
        double ChildY = ParentSource ? Points[^1] : Points[1];
        double ParentX = ParentSource ? Points[0] : Points[^2];
        Assert.That(ParentSource ? Points[1] : Points[^1], Is.EqualTo(ParentBottom).Within(0.01));
        bool BottomPort = Math.Abs(ChildY - ChildBottom) < 0.01;
        Assert.That(BottomPort
            ? Math.Abs(ChildX - Number(Child, "x") - (Number(Child, "width") / 2)) < 0.01
            : Math.Abs(ChildY - Number(Child, "y") - (Number(Child, "height") / 2)) < 0.01
                && (Math.Abs(ChildX - Number(Child, "x")) < 0.01
                    || Math.Abs(ChildX - Number(Child, "x") - Number(Child, "width")) < 0.01), Is.True);
        Assert.That(ParentX, Is.InRange(Number(Parent, "x") + 10, Number(Parent, "x") + Number(Parent, "width") - 10));
        XElement Caption = Edge.Elements().Single(Element => Element.Name.LocalName == "text");
        double Y = Number(Caption, "y") - 4;
        double X = Number(Caption, "x");
        if (BottomPort)
        {
            Assert.That(Y - 6, Is.GreaterThan(ChildBottom + 6));
        }
        else
        {
            Assert.That(Math.Abs(X - ChildX), Is.GreaterThan(30));
        }

        Assert.That(Y + 6, Is.LessThan(ParentBottom - 6));
        XElement Sibling = Rect(ParentSource ? "a" : "b");
        Assert.That(X < Number(Sibling, "x") - 6 || X > Number(Sibling, "x") + Number(Sibling, "width") + 6
            || ChildBottom > Number(Sibling, "y") + Number(Sibling, "height") + 6, Is.True,
            "The internal line must clear the sibling body.");
    }

    private static double Number(XElement Element, string Name) => double.Parse((string)Element.Attribute(Name)!, CultureInfo.InvariantCulture);
    private static double[] Coordinates(string Path) => [.. Regex.Matches(Path, @"-?\d+(?:\.\d+)?").Select(Match => double.Parse(Match.Value, CultureInfo.InvariantCulture))];
}
