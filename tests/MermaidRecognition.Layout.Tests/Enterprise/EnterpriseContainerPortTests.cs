using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseContainerPortTests
{
    [TestCase("Lower")]
    [TestCase("Lower container")]
    public void AlignedContainers_WithAnInternalForwardFlow_UseAStraightSeparateReturn(string LowerTitle)
    {
        string Source = $$"""
            enterprise
              group g "Upper"
              group h "{{LowerTitle}}"
              application-component a "Producer" in g
              application-function b "Work" in a
              application-component c "Consumer" in h
              application-function d "Record" in c
              application-service e "Input"
              data-object f "Output"
              a -assignment-> b
              c -assignment-> d
              b -flow-> d "transfer"
              e -serving-> b
              d -access(write)-> f
              c -serving-> a
            """;
        XDocument Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        var Edges = Svg.Descendants().Where(Element => Element.Attribute("data-source") != null).ToList();
        XElement Return = Edges.Single(Element => (string?)Element.Attribute("data-source") == "c");
        string Path = (string)Return.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!;
        Assert.That(Path, Does.Not.Contain("C"), "Free facing ports should not introduce a backstep.");
        double[] Points = Coordinates(Path);
        Assert.That(Points, Has.Length.EqualTo(4));
        Assert.That(Points[0], Is.EqualTo(Points[2]).Within(0.01));
        XElement Rect(string Id) => Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == Id)
            .Elements().First(Element => Element.Name.LocalName == "rect");
        Assert.That(Points[1], Is.EqualTo(Number(Rect("c"), "y")).Within(0.01));
        Assert.That(Points[3], Is.EqualTo(Number(Rect("a"), "y") + Number(Rect("a"), "height")).Within(0.01));
        XElement Forward = Edges.Single(Element => (string?)Element.Attribute("data-source") == "b");
        double[] ForwardPoints = Coordinates((string)Forward.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!);
        AssertOnGrid(Rect("c"), Points[0], Points[1]);
        AssertOnGrid(Rect("b"), ForwardPoints[0], ForwardPoints[1]);
    }

    /// <summary>
    /// A straight run may share an aligned grid point with another line. The point still has to be one of the
    /// fixed centre-out points on the side it uses.
    /// </summary>
    private static void AssertOnGrid(XElement Node, double X, double Y)
    {
        double Left = Number(Node, "x");
        double Top = Number(Node, "y");
        double Width = Number(Node, "width");
        double Height = Number(Node, "height");
        double Right = Left + Width;
        double Bottom = Top + Height;
        double Offset;
        double Length;
        if (Math.Abs(Y - Top) < 1.0 || Math.Abs(Y - Bottom) < 1.0)
        {
            Offset = X - Left;
            Length = Width;
        }
        else if (Math.Abs(X - Left) < 1.0 || Math.Abs(X - Right) < 1.0)
        {
            Offset = Y - Top;
            Length = Height;
        }
        else
        {
            Assert.Fail($"({X:0.##},{Y:0.##}) is not on the node outline.");
            return;
        }

        double[] Grid = DiagramGridMetrics.SideGridOffsets(Length);
        Assert.That(Grid.Any(Point => Math.Abs(Point - Offset) < 0.5), Is.True,
            $"offset {Offset:0.##} of {Length:0.##}; grid {string.Join(", ", Grid)}");
    }

    private static double Number(XElement Element, string Name) => double.Parse((string)Element.Attribute(Name)!, CultureInfo.InvariantCulture);
    private static double[] Coordinates(string Path) => Regex.Matches(Path, @"-?\d+(?:\.\d+)?")
        .Select(Match => double.Parse(Match.Value, CultureInfo.InvariantCulture)).ToArray();
}
