using System.Xml.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
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
    public void ValidSamples_EnterprisePathsMatchFlowchartRouting(string FileName)
    {
        string Source = ReadSample(FileName);
        IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(Source);
        Assert.That(Violations, Is.Empty, FileName + ":" + Environment.NewLine + string.Join(Environment.NewLine, Violations));
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
        IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(EnterpriseSource);
        Assert.That(Violations, Is.Empty, string.Join(Environment.NewLine, Violations));

        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(EnterpriseSource);
        Assert.That(Analysis.HasErrors, Is.False);
        DiagramContract FlowchartContract = EnterpriseFlowchartContractMapper.Build(Analysis.Document);
        string FlowchartSvg = FlowchartSugiyamaSvgRenderer.RenderToSvg(FlowchartContract);
        string EnterpriseSvg = EnterpriseRenderBridge.RenderEnterpriseSvg(EnterpriseSource);

        List<string> EnterprisePaths = ExtractEnterpriseEdgePaths(EnterpriseSvg);
        List<string> FlowchartPaths = ExtractFlowchartEdgePaths(FlowchartSvg);
        Assert.That(EnterprisePaths, Is.EquivalentTo(FlowchartPaths));
    }

    private static List<string> ExtractEnterpriseEdgePaths(string Svg)
    {
        XDocument Document = XDocument.Parse(Svg);
        List<string> Paths = new List<string>();
        foreach (XElement Group in Document.Descendants())
        {
            if (Group.Name.LocalName != "g")
            {
                continue;
            }

            string? ClassName = (string?)Group.Attribute("class");
            if (string.IsNullOrEmpty(ClassName) || !ClassName.StartsWith("relationship", StringComparison.Ordinal))
            {
                continue;
            }

            XElement? Path = Group.Elements().FirstOrDefault(Element => Element.Name.LocalName == "path");
            string? PathData = Path == null ? null : (string?)Path.Attribute("d");
            if (!string.IsNullOrEmpty(PathData))
            {
                Paths.Add(PathData);
            }
        }

        return Paths;
    }

    private static List<string> ExtractFlowchartEdgePaths(string Svg)
    {
        XDocument Document = XDocument.Parse(Svg);
        List<string> Paths = new List<string>();
        foreach (XElement Element in Document.Descendants())
        {
            if (Element.Name.LocalName == "path")
            {
                string? Fill = (string?)Element.Attribute("fill");
                string? PathData = (string?)Element.Attribute("d");
                if (string.Equals(Fill, "none", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(PathData))
                {
                    Paths.Add(PathData);
                }
            }
            else if (Element.Name.LocalName == "line")
            {
                string? X1 = (string?)Element.Attribute("x1");
                string? Y1 = (string?)Element.Attribute("y1");
                string? X2 = (string?)Element.Attribute("x2");
                string? Y2 = (string?)Element.Attribute("y2");
                if (!string.IsNullOrEmpty(X1)
                    && !string.IsNullOrEmpty(Y1)
                    && !string.IsNullOrEmpty(X2)
                    && !string.IsNullOrEmpty(Y2))
                {
                    Paths.Add("M" + X1 + "," + Y1 + "L" + X2 + "," + Y2);
                }
            }
        }

        return Paths;
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
