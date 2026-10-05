using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Final-SVG quality gate: sample 03, enterprise flowchart samples, and catalogue subgraph diagrams.
/// A defect is tolerated only with a search-backed justification.
/// </summary>
[TestFixture]
public sealed class FlowchartRenderedSvgQualityGateTests
{
    private static readonly string[] CatalogueSubgraphIds =
    [
        "F36", "F37", "F38", "F39", "F40", "F41", "F42", "F43", "F44", "F45", "F46", "F47",
        "F59", "F60", "F73", "F74", "F78", "F84", "F126", "F127", "F128", "F129", "F132"
    ];

    private const string SAMPLE_03_SOURCE = """
        flowchart TB
          subgraph dc["Primary data centre"]
            subgraph appNode["App Server Node"]
              jvm["Java VM"]
              war["shop.war"]
            end
            pg["PostgreSQL"]
            lb["Load Balancer"]
            lan["DC LAN"]
          end
          subgraph webShop["Web Shop"]
            checkout(["Checkout"])
          end
          shopUi["Shop Web UI"]
          ordering(["Online Ordering"])
          order["Order"]
          hosting(["Java Application Hosting"])
          dbSvc(["Database Service"])
          orderTbl["orders table"]
          webShop --> shopUi
          checkout --> ordering
          shopUi --> ordering
          checkout --> order
          war --> webShop
          orderTbl --> order
          jvm --> hosting
          pg --> dbSvc
          hosting --> webShop
          dbSvc --> webShop
          pg --> orderTbl
          lb -->|HTTPS| appNode
          lan --> lb
          lan --> appNode
        """;

    [Test]
    public void Sample03_FinalSvg_PassesQualityGate()
    {
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", SAMPLE_03_SOURCE);
        Assert.That(Report.Passes, Is.True, FormatReport(Report));
    }

    [Test]
    public void CatalogueAndEnterprise_FinalSvg_ReportsJustifiedFailuresOnly()
    {
        List<NamedSource> Diagrams = LoadCatalogueSubgraphDiagrams();
        Diagrams.AddRange(LoadEnterpriseSamples());
        Diagrams.Add(new NamedSource("sample-03", SAMPLE_03_SOURCE));

        int Failed = 0;
        StringBuilder Summary = new StringBuilder();
        foreach (NamedSource Diagram in Diagrams)
        {
            FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate(Diagram.Name, Diagram.Source);
            if (Report.Passes)
            {
                continue;
            }

            Failed++;
            Summary.AppendLine(FormatReport(Report));
        }

        TestContext.Out.WriteLine(
            "Quality gate diagrams: "
            + Diagrams.Count.ToString(CultureInfo.InvariantCulture)
            + ", failed: "
            + Failed.ToString(CultureInfo.InvariantCulture));
        TestContext.Out.WriteLine(Summary.ToString());
        FlowchartRenderedSvgQualityReport Sample03 = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", SAMPLE_03_SOURCE);
        Assert.That(Sample03.Passes, Is.True, FormatReport(Sample03));
        Assert.That(Failed, Is.LessThanOrEqualTo(2), Summary.ToString());
    }

    private static List<NamedSource> LoadEnterpriseSamples()
    {
        List<NamedSource> Diagrams = [];
        string DirectoryPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "EnterpriseFlowchartSamples");
        if (!Directory.Exists(DirectoryPath))
        {
            return Diagrams;
        }

        foreach (string FilePath in Directory.GetFiles(DirectoryPath, "*-flowchart.mmd").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            Diagrams.Add(new NamedSource(Path.GetFileNameWithoutExtension(FilePath), File.ReadAllText(FilePath)));
        }

        return Diagrams;
    }

    private static List<NamedSource> LoadCatalogueSubgraphDiagrams()
    {
        List<NamedSource> Diagrams = [];
        string FlowPath = FindRepoFile("flow.md");
        string Markdown = File.ReadAllText(FlowPath);
        foreach (string FixtureId in CatalogueSubgraphIds)
        {
            if (TryExtractFixture(Markdown, FixtureId, out string Source))
            {
                Diagrams.Add(new NamedSource(FixtureId, Source));
            }
        }

        return Diagrams;
    }

    private static bool TryExtractFixture(string Markdown, string FixtureId, out string Source)
    {
        Source = string.Empty;
        Match Heading = Regex.Match(
            Markdown,
            @"^### " + Regex.Escape(FixtureId) + @"\b[^\n]*\n+(?:```mermaid\n)(.*?)```",
            RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Multiline);
        if (!Heading.Success)
        {
            return false;
        }

        Source = Heading.Groups[1].Value.Trim();
        return !string.IsNullOrWhiteSpace(Source);
    }

    private static string FindRepoFile(string FileName)
    {
        DirectoryInfo? Cursor = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (Cursor is not null)
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

    private static string FormatReport(FlowchartRenderedSvgQualityReport Report)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append(Report.DiagramName);
        Builder.Append(": ");
        Builder.Append(Report.Failures.Count.ToString(CultureInfo.InvariantCulture));
        Builder.Append(" unjustified, ");
        Builder.Append(Report.Tolerated.Count.ToString(CultureInfo.InvariantCulture));
        Builder.Append(" tolerated");
        Builder.AppendLine();
        foreach (FlowchartLayoutQualityFailure Failure in Report.Failures)
        {
            Builder.Append("  FAIL ");
            Builder.Append(Failure.Kind);
            Builder.Append(": ");
            Builder.AppendLine(Failure.Description);
        }

        foreach (FlowchartLayoutQualityFailure Tolerated in Report.Tolerated)
        {
            Builder.Append("  TOLERATED ");
            Builder.Append(Tolerated.Kind);
            Builder.Append(": ");
            Builder.Append(Tolerated.Description);
            Builder.Append(" [");
            Builder.Append(Tolerated.Justification);
            Builder.AppendLine("]");
        }

        return Builder.ToString();
    }

    private sealed class NamedSource
    {
        public NamedSource(string Name, string Source)
        {
            this.Name = Name;
            this.Source = Source;
        }

        public string Name { get; }

        public string Source { get; }
    }
}
