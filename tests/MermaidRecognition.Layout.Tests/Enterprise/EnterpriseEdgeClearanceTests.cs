using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseEdgeClearanceTests
{
    [TestCase("01-application-layer.mmd")]
    [TestCase("02-technology-layer.mmd")]
    [TestCase("03-cross-layer-webshop.mmd")]
    [TestCase("06-business-layer.mmd")]
    [TestCase("07-motivation.mmd")]
    [TestCase("08-strategy.mmd")]
    [TestCase("09-physical.mmd")]
    [TestCase("10-implementation-migration.mmd")]
    [TestCase("11-grouping-location-junction.mmd")]
    [TestCase("12-motivation-to-technology.mmd")]
    public void ValidSamples_EnterpriseGeometryMatchesEquivalentFlowchart(string FileName)
    {
        string Source = ReadSample(FileName);
        IReadOnlyList<string> Mismatches = EnterpriseParity.FindGeometryMismatches(Source, 2.0);
        Assert.That(Mismatches, Is.Empty, FileName + ":" + Environment.NewLine + string.Join(Environment.NewLine, Mismatches));
    }

    [Test]
    public void SimpleGraph_EnterpriseAndFlowchartShareTheSameConnectorPath()
    {
        string EnterpriseSource = """
            enterprise
              application-component top "Top"
              application-component mid "Mid"
              application-component bot "Bottom"
              top -flow-> mid
              mid -flow-> bot
              top -association-> bot
            """;
        IReadOnlyList<string> Mismatches = EnterpriseParity.FindGeometryMismatches(EnterpriseSource, 2.0);
        Assert.That(Mismatches, Is.Empty, string.Join(Environment.NewLine, Mismatches));

        string FlowchartSource = EnterpriseParity.WriteFlowchartSource(EnterpriseSource);
        Assert.That(FlowchartSource, Does.StartWith("flowchart TB"));
        Assert.That(FlowchartSource, Does.Contain("top[\"Top\"]"));
        Assert.That(FlowchartSource, Does.Contain("top --> mid"));
        Assert.That(FlowchartSource, Does.Contain("top --> bot"));

        string EnterpriseSvg = EnterpriseRenderBridge.RenderEnterpriseSvg(EnterpriseSource);
        XDocument Document = XDocument.Parse(EnterpriseSvg);
        XElement? Skip = Document.Descendants()
            .FirstOrDefault(Element =>
                (string?)Element.Attribute("data-source") == "top"
                && (string?)Element.Attribute("data-target") == "bot");
        Assert.That(Skip, Is.Not.Null);
        XElement? Path = Skip!.Elements().FirstOrDefault(Element => Element.Name.LocalName == "path");
        Assert.That(Path, Is.Not.Null);
        Assert.That((string?)Path!.Attribute("d"), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Sample11_JunctionsSitOnTheirEdgeEnds()
    {
        string Source = ReadSample("11-grouping-location-junction.mmd");
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(Source);
        XDocument Document = XDocument.Parse(Svg);
        XElement Both = Document.Descendants().First(Element => (string?)Element.Attribute("data-id") == "both");
        XElement Either = Document.Descendants().First(Element => (string?)Element.Attribute("data-id") == "either");
        XElement BothCircle = Both.Descendants().First(Element => Element.Name.LocalName == "circle");
        XElement StartBoth = Document.Descendants().First(Element =>
            (string?)Element.Attribute("data-source") == "start"
            && (string?)Element.Attribute("data-target") == "both");
        XElement? StartBothPath = StartBoth.Elements().FirstOrDefault(Element => Element.Name.LocalName == "path");
        Assert.That(StartBothPath, Is.Not.Null);
        double JunctionX = double.Parse((string)BothCircle.Attribute("cx")!, CultureInfo.InvariantCulture);
        double JunctionY = double.Parse((string)BothCircle.Attribute("cy")!, CultureInfo.InvariantCulture);
        string PathData = (string)StartBothPath!.Attribute("d")!;
        (double EndX, double EndY) = LastPathPoint(PathData);
        Assert.That(EndY, Is.EqualTo(JunctionY).Within(2.0));
        Assert.That(Math.Abs(EndX - JunctionX), Is.LessThanOrEqualTo(14.0));
        Assert.That(JunctionY, Is.GreaterThan(40));
        Assert.That((string?)Either.Attribute("data-id"), Is.EqualTo("either"));
    }

    [Test]
    public void Sample12_DeclaredDirectionsAreForwardInTheEquivalentFlowchart()
    {
        string Source = ReadSample("12-motivation-to-technology.mmd");
        string FlowchartSource = EnterpriseParity.WriteFlowchartSource(Source);
        Assert.That(FlowchartSource, Does.Contain("board --> raiseRev"));
        Assert.That(FlowchartSource, Does.Contain("takeOrder --> selling"));
        Assert.That(FlowchartSource, Does.Contain("selling --> webShop"));
        Assert.That(FlowchartSource, Does.Contain("appNode --> hosting"));
        Assert.That(FlowchartSource, Does.Not.Contain("-.->"));

        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(Source);
        XDocument Document = XDocument.Parse(Svg);
        double TakeOrderTop = ElementTop(Document, "takeOrder");
        double SellingTop = ElementTop(Document, "selling");
        double WebShopTop = ElementTop(Document, "webShop");
        double AppNodeTop = ElementTop(Document, "appNode");
        double HostingTop = ElementTop(Document, "hosting");
        Assert.That(TakeOrderTop, Is.LessThan(SellingTop - 10), "Take Order must rank above Selling Service");
        Assert.That(SellingTop, Is.LessThan(WebShopTop - 10), "Selling Service must rank above Web Shop");
        Assert.That(AppNodeTop, Is.LessThan(HostingTop - 10), "App Server Node must rank above Hosting");
    }

    private static (double X, double Y) LastPathPoint(string PathData)
    {
        MatchCollection Pairs = Regex.Matches(
            PathData,
            @"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)");
        Assert.That(Pairs.Count, Is.GreaterThan(0));
        Match Last = Pairs[Pairs.Count - 1];
        return (
            double.Parse(Last.Groups[1].Value, CultureInfo.InvariantCulture),
            double.Parse(Last.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private static double ElementTop(XDocument Document, string Id)
    {
        XElement Group = Document.Descendants().First(Element => (string?)Element.Attribute("data-id") == Id);
        XElement? Rect = Group.Descendants().FirstOrDefault(Element => Element.Name.LocalName == "rect");
        if (Rect != null)
        {
            return double.Parse((string)Rect.Attribute("y")!, CultureInfo.InvariantCulture);
        }

        XElement Circle = Group.Descendants().First(Element => Element.Name.LocalName == "circle");
        double Cy = double.Parse((string)Circle.Attribute("cy")!, CultureInfo.InvariantCulture);
        double Radius = double.Parse((string)Circle.Attribute("r")!, CultureInfo.InvariantCulture);
        return Cy - Radius;
    }

    private static string ReadSample(string FileName)
    {
        DirectoryInfo? DirectoryCursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (DirectoryCursor != null)
        {
            string Candidate = Path.Combine(DirectoryCursor.FullName, "docs", "enterprise", "samples", FileName);
            if (File.Exists(Candidate))
            {
                return File.ReadAllText(Candidate);
            }

            DirectoryCursor = DirectoryCursor.Parent;
        }

        string Copied = Path.Combine(AppContext.BaseDirectory, "EnterpriseSamples", FileName);
        if (File.Exists(Copied))
        {
            return File.ReadAllText(Copied);
        }

        throw new FileNotFoundException("Sample not found: " + FileName);
    }
}
