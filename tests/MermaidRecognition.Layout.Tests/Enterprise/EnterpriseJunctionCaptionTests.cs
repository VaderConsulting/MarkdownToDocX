using System.Globalization;
using System.Xml.Linq;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseJunctionCaptionTests
{
    [TestCase("TB", "or-junction")]
    [TestCase("LR", "or-junction")]
    [TestCase("TB", "and-junction")]
    [TestCase("LR", "and-junction")]
    public void SplitAndJoinCaptions_ClearRenderedConnectorsAndStayInsideCanvas(string Direction, string Kind)
    {
        string Source = $"""
            enterprise
              direction {Direction}
              business-process a "Input"
              {Kind} b "Choose a route"
              business-process c "First route"
              business-process d "Second route"
              {Kind} e "Join the routes"
              business-process f "Output"
              a -flow-> b
              b -flow-> c
              b -flow-> d
              c -flow-> e
              d -flow-> e
              e -flow-> f
            """;
        XDocument Document = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        var Captions = Document.Descendants().Where(Element => Element.Name.LocalName == "g"
            && ((string?)Element.Attribute("class"))?.Contains("junction", StringComparison.Ordinal) == true)
            .SelectMany(Element => Element.Elements().Where(Child => Child.Name.LocalName == "text")).ToList();
        Assert.That(Captions, Has.Count.EqualTo(2));
        var Paths = Document.Descendants().Where(Element => Element.Attribute("data-source") != null)
            .SelectMany(Element => Element.Elements().Where(Child => Child.Name.LocalName == "path")).ToList();
        foreach (var Caption in Captions)
        {
            double Left = Number(Caption, "x"), Bottom = Number(Caption, "y");
            double Font = Number(Caption, "font-size");
            double Right = Left + FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Caption.Value, Font);
            double Top = Bottom - Font;
            Assert.That(Left, Is.GreaterThanOrEqualTo(0));
            Assert.That(Top, Is.GreaterThanOrEqualTo(0));
            Assert.That(Right, Is.LessThan(Number(Document.Root!, "width")));
            Assert.That(Bottom, Is.LessThan(Number(Document.Root!, "height")));
            foreach (var Path in Paths)
            {
                Assert.That(FlowchartSvgPathGeometryHelper.TryFlattenPathData((string)Path.Attribute("d")!, out var Points), Is.True);
                foreach (var Point in Points)
                {
                    double Dx = Math.Max(0, Math.Max(Left - Point.X, Point.X - Right));
                    double Dy = Math.Max(0, Math.Max(Top - Point.Y, Point.Y - Bottom));
                    Assert.That(Math.Sqrt(Dx * Dx + Dy * Dy), Is.GreaterThanOrEqualTo(4),
                        "Junction captions must remain legible beside every rendered connector and arrowhead.");
                }
            }
        }
    }

    private static double Number(XElement Element, string Attribute) =>
        double.Parse((string)Element.Attribute(Attribute)!, CultureInfo.InvariantCulture);
}
