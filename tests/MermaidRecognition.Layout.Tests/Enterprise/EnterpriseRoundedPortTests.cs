using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseRoundedPortTests
{
    [TestCase("TB", false, 8)]
    [TestCase("TB", true, 8)]
    [TestCase("TB", false, 16)]
    [TestCase("TB", true, 16)]
    [TestCase("LR", false, 8)]
    [TestCase("LR", true, 8)]
    [TestCase("LR", false, 16)]
    [TestCase("LR", true, 16)]
    public void CrowdedRoundedBoxPorts_MeetTheDrawnFlatSides(string Direction, bool Incoming, int Count)
    {
        var Source = new StringBuilder($"enterprise\n direction {Direction}\n application-service hub \"Shared service\"\n");
        for (int Index = 0; Index < Count; Index++)
            Source.AppendLine($"application-component n{Index} \"Peer {Index}\"");
        for (int Index = 0; Index < Count; Index++)
            Source.AppendLine(Incoming ? $"n{Index} -association-> hub" : $"hub -association-> n{Index}");
        XDocument Svg = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source.ToString()));
        XElement Box = Svg.Descendants().Single(Element => (string?)Element.Attribute("data-id") == "hub")
            .Elements().First(Element => Element.Name.LocalName == "rect");
        double Left = Number(Box, "x"), Top = Number(Box, "y");
        double Right = Left + Number(Box, "width"), Bottom = Top + Number(Box, "height");
        double Radius = Number(Box, "rx");
        var Ports = new List<(double X, double Y)>();
        foreach (XElement Edge in Svg.Descendants().Where(Element => Element.Attribute("data-source") != null))
        {
            string Path = (string)Edge.Elements().First(Element => Element.Name.LocalName == "path").Attribute("d")!;
            double[] Points = Regex.Matches(Path, @"-?\d+(?:\.\d+)?")
                .Select(Match => double.Parse(Match.Value, CultureInfo.InvariantCulture)).ToArray();
            double X = Incoming ? Points[^2] : Points[0], Y = Incoming ? Points[^1] : Points[1];
            double NextX = Incoming ? Points[^4] : Points[2], NextY = Incoming ? Points[^3] : Points[3];
            bool Side = (Math.Abs(X - Left) < 0.01 || Math.Abs(X - Right) < 0.01)
                && Y >= Top + Radius && Y <= Bottom - Radius;
            bool HorizontalSide = (Math.Abs(Y - Top) < 0.01 || Math.Abs(Y - Bottom) < 0.01)
                && X >= Left + Radius && X <= Right - Radius;
            Assert.That(Side || HorizontalSide, Is.True, "Ports must meet a flat border, clear of rounded corners.");
            Assert.That(Side ? Math.Abs(NextY - Y) : Math.Abs(NextX - X), Is.LessThan(0.01), "The terminal run must be normal to the border.");
            Assert.That(Ports.All(Port => Math.Abs(Port.X - X) + Math.Abs(Port.Y - Y) >= 6), Is.True, "Separate lines need distinct ports.");
            Ports.Add((X, Y));
        }
        Assert.That(Ports, Has.Count.EqualTo(Count));
    }

    private static double Number(XElement Element, string Name) => double.Parse((string)Element.Attribute(Name)!, CultureInfo.InvariantCulture);
}
