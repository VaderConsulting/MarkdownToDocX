using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.RuleValidation;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture]
public sealed class UnnecessaryBendCatalogueTests
{
    private sealed record ScenarioResult(string Id, int Lines, bool LayoutSucceeded, IReadOnlyList<UnnecessaryBendViolation> Violations);
    public sealed record CatalogueScenario(string Id, string Source, bool Enterprise);

    public static IEnumerable<TestCaseData> CatalogueScenarios()
    {
        string Root = FindRepositoryRoot();
        foreach (CatalogueScenario Scenario in ReadCatalogue(Root))
            yield return new TestCaseData(Scenario).SetName($"CatalogueScenario_{Scenario.Id}_DoesNotExceedBendBaseline");
    }

    [TestCaseSource(nameof(CatalogueScenarios))]
    [CancelAfter(30000)]
    public void CatalogueScenario_DoesNotExceedBendBaseline(CatalogueScenario Scenario)
    {
        string Root = FindRepositoryRoot();
        string BaselinePath = Path.Combine(Root, "tests", "MermaidRecognition.RuleValidation.Tests", "UnnecessaryBendBaseline.txt");
        Assert.That(File.Exists(BaselinePath), Is.True, "The checked-in unnecessary-bend baseline is required and must not be recreated by the test.");
        Dictionary<string, HashSet<string>> Baseline = ReadBaseline(BaselinePath);
        bool HasBaseline = Baseline.TryGetValue(Scenario.Id, out HashSet<string>? Allowed);
        Allowed ??= [];

        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document)
            : MermaidRecognizer.Parse(Scenario.Source);
        bool LayoutSucceeded = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout);
        Assert.That(LayoutSucceeded, Is.True, $"Scenario {Scenario.Id} did not lay out; it cannot count as a zero-bend scenario.");

        IReadOnlyList<UnnecessaryBendViolation> Violations = UnnecessaryBendChecker.Check(Scenario.Id, Contract, Layout);
        List<string> Regressions = [];
        foreach (UnnecessaryBendViolation Violation in Violations)
        {
            string Key = Violation.Check + ":" + Violation.LineId;
            if (!Allowed.Contains(Key)) Regressions.Add(Key);
        }
        TestContext.Out.WriteLine($"{Scenario.Id}: {Layout.EdgeRoutes.Count} routes, {Violations.Count} bend findings, {Regressions.Count} new findings.");
        Assert.That(HasBaseline, Is.True, $"Scenario {Scenario.Id} was laid out and scored with {Violations.Count} findings, but is missing from the checked-in bend baseline.");
        Assert.That(Regressions, Is.Empty, $"The unnecessary-bend baseline for {Scenario.Id} may only shrink.");
    }

    [Test]
    public void BendBaselineMustCoverEveryCatalogueScenario()
    {
        string Root = FindRepositoryRoot();
        string BaselinePath = Path.Combine(Root, "tests", "MermaidRecognition.RuleValidation.Tests", "UnnecessaryBendBaseline.txt");
        Assert.That(File.Exists(BaselinePath), Is.True, "The checked-in unnecessary-bend baseline is required and must not be recreated by the test.");
        CatalogueScenario[] Scenarios = ReadCatalogue(Root).ToArray();
        Dictionary<string, HashSet<string>> Baseline = ReadBaseline(BaselinePath);
        ScenarioResult[] Results = Scenarios.Select(Scenario => new ScenarioResult(Scenario.Id, 0, true, [])).ToArray();
        Assert.That(Scenarios, Is.Not.Empty, "At least one catalogue diagram must be extracted and scored.");
        AssertBaselineCoversExactlyTheScoredScenarios(Results, Baseline);
    }

    [Test]
    public void FailedLayoutCannotBeCountedAsAZeroBendScenario()
    {
        ScenarioResult[] Results = [new ScenarioResult("F-test", 0, false, [])];

        Assert.Throws<AssertionException>(() => AssertEveryScenarioLaidOut(Results));
    }

    [Test]
    public void BendBaselineMustCoverEveryScoredScenario()
    {
        ScenarioResult[] Results = [new ScenarioResult("F-test", 2, true, [])];
        Dictionary<string, HashSet<string>> Baseline = new(StringComparer.Ordinal);

        Assert.Throws<AssertionException>(() => AssertBaselineCoversExactlyTheScoredScenarios(Results, Baseline));
    }

    private static void AssertAtLeastOneScenarioWasScored(IReadOnlyList<ScenarioResult> Results)
    {
        Assert.That(Results, Is.Not.Empty, "At least one catalogue diagram must be extracted and scored.");
    }

    private static void AssertEveryScenarioLaidOut(IReadOnlyList<ScenarioResult> Results)
    {
        Assert.That(Results.Where(Result => !Result.LayoutSucceeded).Select(Result => Result.Id), Is.Empty,
            "A failed layout must fail the bend check instead of being treated as a zero-bend scenario.");
    }

    private static void AssertBaselineCoversExactlyTheScoredScenarios(
        IReadOnlyList<ScenarioResult> Results,
        IReadOnlyDictionary<string, HashSet<string>> Baseline)
    {
        HashSet<string> ScoredScenarioIds = Results.Select(Result => Result.Id).ToHashSet(StringComparer.Ordinal);
        string[] Missing = ScoredScenarioIds.Except(Baseline.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] Unknown = Baseline.Keys.Except(ScoredScenarioIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.That(Missing, Is.Empty,
            "The bend baseline must include every scored scenario. Missing: " + string.Join(", ", Missing));
        Assert.That(Unknown, Is.Empty,
            "The bend baseline must not include scenarios absent from the catalogues. Unknown: " + string.Join(", ", Unknown));
    }

    private static IEnumerable<CatalogueScenario> ReadCatalogue(string Root)
    {
        foreach ((string FileName, bool Enterprise) in new[]
        {
            ("flow.md", false),
            ("enterprise.md", true),
            ("EIP.md", false)
        })
        {
            string PathValue = Path.Combine(Root, FileName);
            if (!File.Exists(PathValue))
            {
                throw new FileNotFoundException("A source catalogue required for bend scoring is missing.", PathValue);
            }
            string Markdown = File.ReadAllText(PathValue);
            List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> Diagrams = MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);
            Dictionary<string, int> Occurrences = new(StringComparer.Ordinal);
            foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram Diagram in Diagrams)
            {
                string Id = Enterprise ? ResolveEnterpriseId(Markdown, Diagram) : Diagram.DiagramId;
                if (Occurrences.TryGetValue(Id, out int Count))
                {
                    Count++;
                    Occurrences[Id] = Count;
                    Id += "." + Count.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    Occurrences.Add(Id, 1);
                }
                yield return new CatalogueScenario(Id, Diagram.Source, Enterprise);
            }
        }
    }

    private static string ResolveEnterpriseId(
        string Markdown,
        MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram Diagram)
    {
        int FenceOrdinal = Diagram.Index + 1;
        MatchCollection Fences = Regex.Matches(Markdown, "(?m)^```mermaid\\s*$", RegexOptions.CultureInvariant);
        int FenceIndex = FenceOrdinal <= Fences.Count ? Fences[FenceOrdinal - 1].Index : Markdown.Length;
        MatchCollection Headings = Regex.Matches(Markdown.Substring(0, FenceIndex), @"(?m)^###\s+(A\d+)\b", RegexOptions.CultureInvariant);
        return Headings.Count == 0 ? "EIP" + FenceOrdinal.ToString(CultureInfo.InvariantCulture) : Headings[^1].Groups[1].Value;
    }

    private static Dictionary<string, HashSet<string>> ReadBaseline(string PathValue)
    {
        Dictionary<string, HashSet<string>> Result = new(StringComparer.Ordinal);
        foreach (string Line in File.ReadLines(PathValue))
        {
            if (string.IsNullOrWhiteSpace(Line) || Line.StartsWith('#')) continue;
            string[] Parts = Line.Split('\t');
            Result[Parts[0]] = Parts.Skip(2).Where(Value => Value.Length > 0).ToHashSet(StringComparer.Ordinal);
        }
        return Result;
    }

    private static void PrintTable(IReadOnlyList<ScenarioResult> Results, IReadOnlyDictionary<string, HashSet<string>> Baseline)
    {
        TestContext.Out.WriteLine("| scenario | lines | a | b | c | d | e | f | g | h | i | j | baseline |");
        TestContext.Out.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (ScenarioResult Result in Results)
        {
            Baseline.TryGetValue(Result.Id, out HashSet<string>? Allowed);
            int Count(char Check) => Result.Violations.Count(Violation => Violation.Check == Check);
            TestContext.Out.WriteLine($"| {Result.Id} | {Result.Lines} | {Count('a')} | {Count('b')} | {Count('c')} | {Count('d')} | {Count('e')} | {Count('f')} | {Count('g')} | {Count('h')} | {Count('i')} | {Count('j')} | {Allowed?.Count ?? 0} |");
        }
        int Total(char Check) => Results.Sum(Result => Result.Violations.Count(Violation => Violation.Check == Check));
        TestContext.Out.WriteLine($"Totals: a={Total('a')}, b={Total('b')}, c={Total('c')}, d={Total('d')}, e={Total('e')}, "
            + $"f={Total('f')}, g={Total('g')}, h={Total('h')}, i={Total('i')}, j={Total('j')}");
        foreach (ScenarioResult Result in Results.Where(Result => Result.Violations.Count > 0))
        foreach (UnnecessaryBendViolation Violation in Result.Violations)
            TestContext.Out.WriteLine($"{Result.Id} {Violation.LineId} check {Violation.Check}: "
                + $"{Violation.RoutedCorners} corners, length {Violation.RoutedLength.ToString("0.#", CultureInfo.InvariantCulture)}; better "
                + string.Join(" ", Violation.BetterRoute.Select(Point =>
                    $"({Point.X.ToString("0.#", CultureInfo.InvariantCulture)},{Point.Y.ToString("0.#", CultureInfo.InvariantCulture)})")));
        TestContext.Out.WriteLine("Top 10: " + string.Join(", ", Results.OrderByDescending(Result => Result.Violations.Count)
            .ThenBy(Result => Result.Id, StringComparer.Ordinal).Take(10)
            .Select(Result => Result.Id + "=" + Result.Violations.Count.ToString(CultureInfo.InvariantCulture))));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? Cursor = new(AppContext.BaseDirectory); Cursor != null; Cursor = Cursor.Parent)
            if (File.Exists(Path.Combine(Cursor.FullName, "flow.md"))
                && File.Exists(Path.Combine(Cursor.FullName, "docs", "currentstatus.md"))) return Cursor.FullName;
        throw new DirectoryNotFoundException("Repository root containing flow.md was not found.");
    }
}
