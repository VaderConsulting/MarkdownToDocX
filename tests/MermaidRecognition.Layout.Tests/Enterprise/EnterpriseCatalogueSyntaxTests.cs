using System.Text.RegularExpressions;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseCatalogueSyntaxTests
{
    private static readonly int[] ValidSyntaxScenarios =
        [100, 101, 118, 119, 121, 122, 123, 124, 125, 126, 127, 128];

    private static readonly (int Number, int Errors)[] InvalidScenarios =
    [
        (78, 1), (79, 4), (80, 2), (81, 2), (82, 3), (83, 2), (84, 1),
        (86, 2), (87, 1), (88, 4), (89, 2), (90, 4), (91, 4), (92, 3),
        (93, 1), (94, 3), (102, 3)
    ];

    private static readonly int[] PlacementHintScenarios =
        [154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164];

    [TestCaseSource(nameof(PlacementHintScenarios))]
    public void PlacementHintScenario_ParsesWithoutErrors(int Number)
    {
        string Source = ReadScenario(Number);
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty,
            $"A{Number}: {string.Join("; ", Analysis.Errors.Select(Item => Item.Message))}");
        Assert.That(EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out string Svg), Is.True);
        Assert.That(Svg, Does.StartWith("<svg"));
    }

    [Test]
    public void NoteAndPlainConnection_RenderAsDrawingItemsWithHints()
    {
        const string Source = """
            enterprise
            application-component shop "Web Shop"
            note explanation "Read this note" right-of shop
            explanation --- shop [leave W, enter E]
            """;

        Assert.That(EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out string Svg), Is.True);
        Assert.That(Svg, Does.Contain("class=\"note\" data-id=\"explanation\""));
        Assert.That(Svg, Does.Contain("class=\"plain-connection\""));
        Assert.That(Svg, Does.Not.Contain("class=\"relationship association\" data-source=\"explanation\""));
    }

    [TestCaseSource(nameof(ValidSyntaxScenarios))]
    public void SyntaxGapScenario_RendersFromItsCatalogueSource(int Number)
    {
        string Source = ReadScenario(Number);
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty,
            $"A{Number}: {string.Join("; ", Analysis.Errors.Select(Item => Item.Message))}");
        Assert.That(EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out string Svg), Is.True);
        Assert.That(Svg, Does.StartWith("<svg"));
    }

    [Test]
    public void UndeclaredStyleTargets_ProduceTwoWarningsAndStillRender()
    {
        string Source = ReadScenario(129);
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.Errors, Is.Empty);
        Assert.That(Analysis.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.EqualTo(2));
        Assert.That(EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out string Svg), Is.True);
        Assert.That(Svg, Does.StartWith("<svg"));
    }

    [Test]
    public void OutOfRangeLinkStyle_ProducesItsDeclaredError()
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(ReadScenario(130));
        Assert.That(Analysis.Errors, Has.Count.EqualTo(1));
        Assert.That(Analysis.Errors[0].LineNumber, Is.EqualTo(6));
    }

    [TestCaseSource(nameof(InvalidScenarios))]
    public void DeliberatelyInvalidScenario_KeepsItsDeclaredErrorCount((int Number, int Errors) Scenario)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(ReadScenario(Scenario.Number));
        Assert.That(Analysis.Errors, Has.Count.EqualTo(Scenario.Errors), $"A{Scenario.Number}");
    }

    private static string ReadScenario(int Number)
    {
        string Markdown = File.ReadAllText(FindRepositoryFile("enterprise.md"));
        Match Section = Regex.Match(Markdown,
            $@"(?ms)^### A{Number}\b(?<body>.*?)(?=^### A\d+\b|\z)");
        Assert.That(Section.Success, Is.True, $"A{Number} heading is missing.");
        Match Fence = Regex.Match(Section.Groups["body"].Value,
            @"(?ms)```mermaid\s*\r?\n(?<source>.*?)\r?\n```");
        Assert.That(Fence.Success, Is.True, $"A{Number} Mermaid fence is missing.");
        return Fence.Groups["source"].Value;
    }

    private static string FindRepositoryFile(string FileName)
    {
        DirectoryInfo? Cursor = new(AppContext.BaseDirectory);
        while (Cursor != null)
        {
            string Candidate = Path.Combine(Cursor.FullName, FileName);
            if (File.Exists(Candidate)) return Candidate;
            Cursor = Cursor.Parent;
        }
        throw new FileNotFoundException(FileName);
    }
}
