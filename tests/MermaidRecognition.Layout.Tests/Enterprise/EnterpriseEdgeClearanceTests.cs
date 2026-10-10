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

    [TestCase("TB", "and-junction")]
    [TestCase("LR", "and-junction")]
    [TestCase("TB", "or-junction")]
    [TestCase("LR", "or-junction")]
    public void JunctionIncomingConnector_EndsOnItsCircularOutline(string Direction, string Kind)
    {
        string Source = $"enterprise\n direction {Direction}\n business-event a \"Input\"\n {Kind} b \"Join\"\n business-event c \"Output\"\n a -association-> b\n b -association-> c";
        XDocument Document = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        XElement Junction = Document.Descendants().First(Element => (string?)Element.Attribute("data-id") == "b");
        XElement Circle = Junction.Descendants().First(Element => Element.Name.LocalName == "circle");
        XElement Edge = Document.Descendants().First(Element => (string?)Element.Attribute("data-source") == "a"
            && (string?)Element.Attribute("data-target") == "b");
        XElement Path = Edge.Elements().First(Element => Element.Name.LocalName == "path");
        (double X, double Y) End = LastPathPoint((string)Path.Attribute("d")!);
        double Cx = double.Parse((string)Circle.Attribute("cx")!, CultureInfo.InvariantCulture);
        double Cy = double.Parse((string)Circle.Attribute("cy")!, CultureInfo.InvariantCulture);
        double Radius = double.Parse((string)Circle.Attribute("r")!, CultureInfo.InvariantCulture);
        Assert.That(Math.Sqrt(Math.Pow(End.X - Cx, 2) + Math.Pow(End.Y - Cy, 2)),
            Is.EqualTo(Radius).Within(2), "The terminal must meet the drawn circle, whichever side routing selects.");
    }

    [Test]
    public void CrossLayerChain_PreservesDeclaredRelationshipDirection()
    {
        string Source = """
            enterprise
              business-process a "Process"
              business-service b "Service"
              application-component c "Application"
              a -realization-> b
              b -association-> c
            """;
        string FlowchartSource = EnterpriseParity.WriteFlowchartSource(Source);
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        XDocument Document = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(Source));
        foreach (EnterpriseRelationship Relationship in Analysis.Document.Relationships)
        {
            Assert.That(FlowchartSource, Does.Contain(Relationship.SourceId + " --> " + Relationship.TargetId));
            Assert.That(ElementTop(Document, Relationship.SourceId),
                Is.LessThan(ElementTop(Document, Relationship.TargetId) - 10));
        }
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
