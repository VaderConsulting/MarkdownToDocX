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
    public void Sample03_FinalSvg_ReportsDefectCounts()
    {
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", SAMPLE_03_SOURCE);
        TestContext.Out.WriteLine(FormatReport(Report));
        TestContext.Out.WriteLine(
            "sample-03 live custom engine failures="
            + Report.Failures.Count.ToString(CultureInfo.InvariantCulture)
            + " tolerated="
            + Report.Tolerated.Count.ToString(CultureInfo.InvariantCulture));
        Assert.That(Report.Failures, Is.Not.Null);
    }

    [Test]
    public void Sample03_EngineIsNoWorseThanOracle()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult EngineLayout), Is.True);
        FlowchartLayoutResult OracleLayout = FlowchartSample03OracleLayout.Build(Contract, EngineLayout.NodeBounds);
        TestContext.Out.WriteLine("Oracle centres:");
        TestContext.Out.WriteLine(FlowchartSample03OracleLayout.FormatCentres(OracleLayout));
        TestContext.Out.WriteLine("Engine centres:");
        TestContext.Out.WriteLine(FlowchartSample03OracleLayout.FormatCentres(EngineLayout));

        FlowchartRenderedSvgQualityReport EngineReport = FlowchartRenderedSvgQualityGate.Evaluate("sample-03", Contract);
        FlowchartRenderedSvgQualityReport OracleReport = FlowchartRenderedSvgQualityGate.Evaluate(
            "sample-03-oracle",
            Contract,
            OracleLayout);
        FlowchartLayoutQualityMetrics EngineMetrics = new FlowchartLayoutQualityMetrics(EngineReport);
        FlowchartLayoutQualityMetrics OracleMetrics = new FlowchartLayoutQualityMetrics(OracleReport);
        TestContext.Out.WriteLine("Engine " + EngineMetrics);
        TestContext.Out.WriteLine("Oracle " + OracleMetrics);
        Assert.That(
            EngineMetrics.IsNoWorseThan(OracleMetrics),
            Is.True,
            "engine " + EngineMetrics + " is worse than oracle " + OracleMetrics
            + Environment.NewLine
            + FormatReport(EngineReport)
            + Environment.NewLine
            + FormatReport(OracleReport));
    }

    [Test]
    public void KnownBadWebShopFlowchartPicture_ReportsEveryVisibleDefect()
    {
        string Svg = File.ReadAllText(FindFixture("enterprise-03-flowchart.known-bad.svg"));
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluateRenderedSvg(
            "sample-03-flowchart-known-bad",
            SAMPLE_03_SOURCE,
            Svg);
        TestContext.Out.WriteLine(FormatReport(Report));
        AssertDetected(Report, "EdgeCrossing", "lan", "appNode", "lb", "appNode");
        AssertDetected(Report, "LabelOwnEdge", "HTTPS");
        AssertDetected(Report, "GroupBorderDiagonal", "war", "webShop", "dc");
        AssertDetected(Report, "GroupBorderDiagonal", "jvm", "hosting", "dc");
        AssertDetected(Report, "GroupBorderDiagonal", "pg", "dbSvc", "dc");
        AssertDetected(Report, "Hook", "lan", "appNode");
    }

    [Test]
    public void KnownBadWebShopEnterprisePicture_ReportsEveryVisibleDefect()
    {
        string Svg = File.ReadAllText(FindFixture("enterprise-03-enterprise.known-bad.svg"));
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluateRenderedSvg(
            "sample-03-enterprise-known-bad",
            SAMPLE_03_SOURCE,
            Svg);
        TestContext.Out.WriteLine(FormatReport(Report));
        AssertDetected(Report, "EdgeCrossing", "dbSvc", "webShop", "checkout", "order");
        AssertDetected(Report, "NodeIntersection", "pg", "dbSvc", "orderTbl");
        AssertDetected(Report, "GroupBorderDiagonal", "war", "webShop", "dc");
        AssertDetected(Report, "GroupBorderDiagonal", "jvm", "hosting", "dc");
        AssertDetected(Report, "GroupInteriorDiagonal", "lb", "appNode", "dc");
    }

    [Test]
    public void CurrentWebShopEnterprisePicture_ReportsEveryListedDefect()
    {
        string Svg = File.ReadAllText(FindFixture("enterprise-03-enterprise.current.svg"));
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-03-enterprise-current",
            SAMPLE_03_SOURCE,
            Svg);
        TestContext.Out.WriteLine(FormatReport(Report));
        AssertDetected(Report, "DanglingArrowhead", "pg", "orderTbl");
        AssertDetected(Report, "DetachedEndpoint", "pg", "orderTbl");
        AssertDetected(Report, "LabelOnBox", "HTTPS", "pg");
        AssertDetected(Report, "ForeignGroupTraversal", "dbSvc", "webShop", "dc");
        AssertDetected(Report, "OverlayingEdges", "lan", "appNode", "lb", "appNode");
    }

    [Test]
    public void CurrentWebShopFlowchartPicture_ReportsEveryListedDefect()
    {
        string Svg = File.ReadAllText(FindFixture("enterprise-03-flowchart.current.svg"));
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-03-flowchart-current",
            SAMPLE_03_SOURCE,
            Svg);
        TestContext.Out.WriteLine(FormatReport(Report));
        AssertDetected(Report, "NodeIntersection", "dbSvc", "webShop", "orderTbl");
        AssertDetected(Report, "EdgeCrossing", "pg", "orderTbl", "lan", "appNode");
        AssertDetected(Report, "EdgeCrossing", "lb", "appNode", "pg", "dbSvc");
        AssertDetected(Report, "GroupBorderDiagonal", "jvm", "hosting");
        AssertDetected(Report, "GroupBorderDiagonal", "war", "webShop");
        AssertDetected(Report, "MixedStyle");
    }

    [Test]
    public void CurrentOrderSuitePicture_ReportsEveryListedDefect()
    {
        string Source = File.ReadAllText(FindEnterpriseSample("01-flowchart.mmd"));
        string Svg = File.ReadAllText(FindFixture("enterprise-01-flowchart.current.svg"));
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-01-current",
            Source,
            Svg);
        TestContext.Out.WriteLine(FormatReport(Report));
        AssertDetected(Report, "RankGap");
        AssertDetected(Report, "LongDiagonal");
        AssertDetected(Report, "LabelOwnEdge", "order details");
        AssertDetected(Report, "DetachedEndpoint", "placeOrder", "order");
        AssertDetected(Report, "DetachedEndpoint", "orderSync", "order");
    }

    [Test]
    public void CurrentSalesTeamPicture_ReportsEveryListedDefect()
    {
        string Source = File.ReadAllText(FindEnterpriseSample("06-flowchart.mmd"));
        string Svg = File.ReadAllText(FindFixture("enterprise-06-flowchart.current.svg"));
        FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-06-current",
            Source,
            Svg);
        TestContext.Out.WriteLine(FormatReport(Report));
        AssertDetected(Report, "OverlayingEdges", "fulfil", "selling", "selling", "seller");
        AssertDetected(Report, "CrampedArrow", "clerk", "seller");
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
            foreach (FlowchartLayoutQualityFailure Tolerated in Report.Tolerated)
            {
                Assert.That(
                    FlowchartLayoutQualityJustification.IsSpecificReason(Tolerated.Justification),
                    Is.True,
                    Diagram.Name + " tolerated a non-specific reason: " + Tolerated.Justification);
            }

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
        TestContext.Out.WriteLine(
            "Gate fail count: "
            + Failed.ToString(CultureInfo.InvariantCulture)
            + " of "
            + Diagrams.Count.ToString(CultureInfo.InvariantCulture));
        Assert.That(Diagrams.Count, Is.GreaterThan(20));
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

    private static void AssertDetected(
        FlowchartRenderedSvgQualityReport Report,
        string Kind,
        params string[] InvolvedIds)
    {
        List<FlowchartLayoutQualityFailure> All = [];
        All.AddRange(Report.Failures);
        All.AddRange(Report.Tolerated);
        foreach (FlowchartLayoutQualityFailure Failure in All)
        {
            if (!string.Equals(Failure.Kind, Kind, StringComparison.Ordinal))
            {
                continue;
            }

            if (Kind == "LabelOwnEdge" || Kind == "LabelOnBox")
            {
                bool AllFound = true;
                foreach (string Token in InvolvedIds)
                {
                    if (!Failure.Description.Contains(Token, StringComparison.Ordinal)
                        && !ContainsId(Failure, Token))
                    {
                        AllFound = false;
                        break;
                    }
                }

                if (AllFound)
                {
                    return;
                }

                continue;
            }

            if (ContainsAllIds(Failure, InvolvedIds))
            {
                return;
            }
        }

        Assert.Fail(
            "The gate did not report "
            + Kind
            + " for "
            + string.Join(", ", InvolvedIds)
            + "."
            + Environment.NewLine
            + FormatReport(Report));
    }

    private static bool ContainsAllIds(FlowchartLayoutQualityFailure Failure, IReadOnlyList<string> InvolvedIds)
    {
        foreach (string Id in InvolvedIds)
        {
            bool Found = Failure.Description.Contains(Id, StringComparison.Ordinal);
            if (!Found)
            {
                foreach (string Involved in Failure.InvolvedIds)
                {
                    if (string.Equals(Involved, Id, StringComparison.Ordinal))
                    {
                        Found = true;
                        break;
                    }
                }
            }

            if (!Found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsId(FlowchartLayoutQualityFailure Failure, string Id)
    {
        foreach (string Involved in Failure.InvolvedIds)
        {
            if (string.Equals(Involved, Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindEnterpriseSample(string FileName)
    {
        string FromOutput = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "EnterpriseFlowchartSamples",
            FileName);
        if (File.Exists(FromOutput))
        {
            return FromOutput;
        }

        return Path.Combine(FindRepoRoot(), "tests", "MermaidRecognition.Layout.Tests", "EnterpriseFlowchartSamples", FileName);
    }

    private static string FindFixture(string FileName)
    {
        string FromOutput = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "QualityGateFixtures",
            FileName);
        if (File.Exists(FromOutput))
        {
            return FromOutput;
        }

        return Path.Combine(FindRepoRoot(), "tests", "MermaidRecognition.Layout.Tests", "QualityGateFixtures", FileName);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? Cursor = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (Cursor is not null)
        {
            if (File.Exists(Path.Combine(Cursor.FullName, "flow.md")))
            {
                return Cursor.FullName;
            }

            Cursor = Cursor.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
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
