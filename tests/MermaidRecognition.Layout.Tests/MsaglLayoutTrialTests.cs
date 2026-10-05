using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Side-by-side MSAGL trial: the official engine must produce a picture, and the gate counts defects per engine.
/// </summary>
[TestFixture]
public sealed class MsaglLayoutTrialTests
{
    [Test]
    public void OfficialMsaglEngines_RenderTheWebShopDiagram()
    {
        string Source = File.ReadAllText(FindEnterpriseSample("03-flowchart.mmd"));
        string Rectilinear = DiagramLayoutEngine.RenderToSvg(
            MermaidRecognition.Core.MermaidRecognizer.Parse(Source),
            DiagramLayoutProfile.FlowchartMsaglRectilinear());
        string Spline = DiagramLayoutEngine.RenderToSvg(
            MermaidRecognition.Core.MermaidRecognizer.Parse(Source),
            DiagramLayoutProfile.FlowchartMsaglSpline());
        Assert.That(Rectilinear, Is.Not.Empty, "MSAGL rectilinear produced no SVG.");
        Assert.That(Spline, Is.Not.Empty, "MSAGL spline produced no SVG.");
        Assert.That(Rectilinear, Does.Contain("data-edge-source"));
        Assert.That(Spline, Does.Contain("data-edge-source"));
    }

    [Test]
    public void ThreeEngines_ReportGateCountsForTheFullSet()
    {
        List<NamedSource> Diagrams = LoadGateSet();
        StringBuilder Summary = new StringBuilder();
        int CustomFailed = CountEngineFailures(Diagrams, DiagramLayoutProfile.FlowchartCustom(), "custom", Summary);
        int RectilinearFailed = CountEngineFailures(
            Diagrams,
            DiagramLayoutProfile.FlowchartMsaglRectilinear(),
            "msagl-rectilinear",
            Summary);
        int SplineFailed = CountEngineFailures(
            Diagrams,
            DiagramLayoutProfile.FlowchartMsaglSpline(),
            "msagl-spline",
            Summary);
        TestContext.Out.WriteLine(Summary.ToString());
        TestContext.Out.WriteLine(
            "Gate diagrams: "
            + Diagrams.Count.ToString(CultureInfo.InvariantCulture)
            + " custom-failed="
            + CustomFailed.ToString(CultureInfo.InvariantCulture)
            + " rectilinear-failed="
            + RectilinearFailed.ToString(CultureInfo.InvariantCulture)
            + " spline-failed="
            + SplineFailed.ToString(CultureInfo.InvariantCulture));
        Assert.That(Diagrams.Count, Is.GreaterThanOrEqualTo(30));
    }

    private static int CountEngineFailures(
        IReadOnlyList<NamedSource> Diagrams,
        DiagramLayoutProfile Profile,
        string EngineName,
        StringBuilder Summary)
    {
        int Failed = 0;
        int Defects = 0;
        foreach (NamedSource Diagram in Diagrams)
        {
            FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluateWithEngine(
                Diagram.Name,
                Diagram.Source,
                Profile);
            Defects += Report.Failures.Count;
            if (Report.Passes)
            {
                continue;
            }

            Failed++;
            Summary.Append(EngineName);
            Summary.Append(' ');
            Summary.Append(Diagram.Name);
            Summary.Append(": ");
            Summary.Append(Report.Failures.Count.ToString(CultureInfo.InvariantCulture));
            Summary.AppendLine(" defects");
            foreach (FlowchartLayoutQualityFailure Failure in Report.Failures)
            {
                Summary.Append("  ");
                Summary.Append(Failure.Kind);
                Summary.Append(": ");
                Summary.AppendLine(Failure.Description);
            }
        }

        Summary.Append(EngineName);
        Summary.Append(" failed diagrams=");
        Summary.Append(Failed.ToString(CultureInfo.InvariantCulture));
        Summary.Append(" of ");
        Summary.Append(Diagrams.Count.ToString(CultureInfo.InvariantCulture));
        Summary.Append(" total defects=");
        Summary.AppendLine(Defects.ToString(CultureInfo.InvariantCulture));
        return Failed;
    }

    private static List<NamedSource> LoadGateSet()
    {
        List<NamedSource> Diagrams = [];
        string FlowPath = FindRepoFile("flow.md");
        string Markdown = File.ReadAllText(FlowPath);
        string[] CatalogueIds =
        [
            "F36", "F37", "F38", "F39", "F40", "F41", "F42", "F43", "F44", "F45", "F46", "F47",
            "F59", "F60", "F73", "F74", "F78", "F84", "F126", "F127", "F128", "F129", "F132"
        ];
        foreach (string FixtureId in CatalogueIds)
        {
            Match Heading = Regex.Match(
                Markdown,
                @"^### " + Regex.Escape(FixtureId) + @"\b[^\n]*\n+(?:```mermaid\n)(.*?)```",
                RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Multiline);
            if (Heading.Success)
            {
                Diagrams.Add(new NamedSource(FixtureId, Heading.Groups[1].Value.Trim()));
            }
        }

        string SampleDirectory = Path.Combine(FindRepoRoot(), "tests", "MermaidRecognition.Layout.Tests", "EnterpriseFlowchartSamples");
        foreach (string FilePath in Directory.GetFiles(SampleDirectory, "*-flowchart.mmd").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            Diagrams.Add(new NamedSource(Path.GetFileNameWithoutExtension(FilePath), File.ReadAllText(FilePath)));
        }

        return Diagrams;
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
