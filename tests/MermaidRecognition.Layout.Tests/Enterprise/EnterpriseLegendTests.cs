using System.Text.RegularExpressions;
using System.Xml.Linq;

using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseLegendTests
{
    [TestCase(170)]
    [TestCase(171)]
    [TestCase(172)]
    [TestCase(173)]
    [TestCase(174)]
    [TestCase(175)]
    [TestCase(177)]
    [TestCase(179)]
    public void PositionedLegend_RendersOutsideMainDiagram(int Number)
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(ReadScenario(Number));
        XDocument Xml = XDocument.Parse(Svg);
        XElement Legend = Xml.Descendants().Single(Element => (string?)Element.Attribute("class") == "diagram-legend");
        Assert.That(Legend.Descendants().Any(Element => Element.Name.LocalName == "text" && Element.Value == "Legend"), Is.True);
    }

    [TestCase(176)]
    [TestCase(178)]
    public void NonImageLegendModes_DoNotDrawLegend(int Number)
    {
        string Svg = EnterpriseRenderBridge.RenderEnterpriseSvg(ReadScenario(Number));
        Assert.That(Svg, Does.Not.Contain("diagram-legend"));
    }

    [Test]
    public void Legend_DoesNotChangeMainElementCoordinates()
    {
        string WithLegend = ReadScenario(170);
        string WithoutLegend = Regex.Replace(WithLegend, @"(?ms)^---\s*.*?^---\s*", string.Empty);
        XDocument On = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(WithLegend));
        XDocument Off = XDocument.Parse(EnterpriseRenderBridge.RenderEnterpriseSvg(WithoutLegend));
        string[] OnElements = [.. On.Descendants().Where(Element => Element.Attribute("data-id") != null).Select(Element => Element.ToString(SaveOptions.DisableFormatting))];
        string[] OffElements = [.. Off.Descendants().Where(Element => Element.Attribute("data-id") != null).Select(Element => Element.ToString(SaveOptions.DisableFormatting))];
        Assert.That(OnElements, Is.EqualTo(OffElements));
    }

    [Test]
    public void FlowchartLegend_UsesSharedFrontmatterSetting()
    {
        string Source = "---\nconfig:\n  legend: right\n---\nflowchart TB\na[Start] --> b(Work)";
        Assert.That(NativeDiagramRenderBridge.TryRenderSvg(Source, out string Svg), Is.True);
        Assert.That(Svg, Does.Contain("diagram-legend"));
        Assert.That(Svg, Does.Contain("Rectangle"));
        Assert.That(Svg, Does.Contain("Rounded rectangle"));
        Assert.That(Svg, Does.Contain("Relationship"));
    }

    private static string ReadScenario(int Number)
    {
        string Markdown = File.ReadAllText(FindRepositoryFile("enterprise.md"));
        Match Section = Regex.Match(Markdown, $@"(?ms)^### A{Number}\b(?<body>.*?)(?=^### A\d+\b|\z)");
        Match Fence = Regex.Match(Section.Groups["body"].Value, @"(?ms)\x60\x60\x60mermaid\s*\r?\n(?<source>.*?)\r?\n\x60\x60\x60");
        return Fence.Groups["source"].Value;
    }

    private static string FindRepositoryFile(string FileName)
    {
        DirectoryInfo? Cursor = new(AppContext.BaseDirectory);
        while (Cursor != null)
        {
            string Candidate = Path.Combine(Cursor.FullName, FileName);
            if (File.Exists(Candidate))
            {
                return Candidate;
            }

            Cursor = Cursor.Parent;
        }
        throw new FileNotFoundException(FileName);
    }
}
