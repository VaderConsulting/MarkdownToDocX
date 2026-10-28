using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class EnterpriseWordScaleReadabilityTests
{
    [Test]
    public void EveryEnterpriseDiagram_ReportsWordEmbedScale()
    {
        List<string> Unreadable = [];
        foreach (string Id in ReadEnterpriseIds())
        {
            string Source = ReadScenario(Id);
            DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
            if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult Layout))
            {
                Unreadable.Add(Id + "|layout-failed");
                continue;
            }

            FlowchartDocxEmbedMetrics.EmbedReport Report = FlowchartDocxEmbedMetrics.Measure(Layout);
            TestContext.Out.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"WORD_SCALE|{Id}|scale={Report.WidthScale:0.###}|px={Report.WidthPixels:0}x{Report.HeightPixels:0}|readable={Report.LabelReadable}"));
            if (!Report.LabelReadable || Report.WidthScale < 0.35)
            {
                Unreadable.Add(Id + $"|scale={Report.WidthScale:0.###}");
            }
        }

        TestContext.Out.WriteLine("WORD_SCALE_UNREADABLE|" + string.Join(",", Unreadable));
    }

    [TestCase("F153")]
    public void KeyDiagrams_StayReadableAtWordScale(string Id)
    {
        string FileName = Id.StartsWith('F') ? "flow.md" : "enterprise.md";
        string Markdown = File.ReadAllText(Path.Combine(FindRepositoryRoot(), FileName));
        Match Section = Regex.Match(Markdown, $@"(?ms)^### {Id}\b(?<body>.*?)(?=^### |\z)");
        Match Fence = Regex.Match(Section.Groups["body"].Value, @"(?ms)\x60\x60\x60mermaid\s*\r?\n(?<source>.*?)\r?\n\x60\x60\x60");
        string Source = Fence.Groups["source"].Value;
        DiagramContract Contract = Id.StartsWith('F')
            ? MermaidRecognition.Core.MermaidRecognizer.Parse(Source)
            : EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new FlowchartRenderContext { UseCellRegions = true }, out FlowchartLayoutResult Layout), Is.True, Id);
        FlowchartDocxEmbedMetrics.EmbedReport Report = FlowchartDocxEmbedMetrics.Measure(Layout);
        TestContext.Out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"WORD_SCALE|{Id}|scale={Report.WidthScale:0.###}|px={Report.WidthPixels:0}x{Report.HeightPixels:0}|readable={Report.LabelReadable}"));
        Assert.That(Report.LabelReadable, Is.True, Id + " label scale");
        Assert.That(Report.WidthScale, Is.GreaterThan(0.35), Id + " width scale " + Report.WidthScale.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static IEnumerable<string> ReadEnterpriseIds()
    {
        string Markdown = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "enterprise.md"));
        return Regex.Matches(Markdown, @"^### (A\d+)\b", RegexOptions.Multiline)
            .Select(Match => Match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);
    }

    private static string ReadScenario(string Id)
    {
        string Markdown = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "enterprise.md"));
        Match Section = Regex.Match(Markdown, $@"(?ms)^### {Id}\b(?<body>.*?)(?=^### A\d+\b|\z)");
        Match Fence = Regex.Match(Section.Groups["body"].Value, @"(?ms)\x60\x60\x60mermaid\s*\r?\n(?<source>.*?)\r?\n\x60\x60\x60");
        return Fence.Groups["source"].Value;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? Cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (Cursor != null)
        {
            if (File.Exists(Path.Combine(Cursor.FullName, "enterprise.md")))
            {
                return Cursor.FullName;
            }

            Cursor = Cursor.Parent;
        }

        throw new DirectoryNotFoundException("enterprise.md not found");
    }
}
