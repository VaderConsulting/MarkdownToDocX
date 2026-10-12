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
    private sealed record ScenarioResult(string Id, int Lines, IReadOnlyList<UnnecessaryBendViolation> Violations);

    [Test]
    public void EveryCatalogueDiagram_DoesNotExceedBendBaseline()
    {
        string Root = FindRepositoryRoot();
        List<ScenarioResult> Results = [];
        AddCatalogue(Results, Path.Combine(Root, "flow.md"), Enterprise: false);
        AddCatalogue(Results, Path.Combine(Root, "enterprise.md"), Enterprise: true);
        AddCatalogue(Results, Path.Combine(Root, "EIP.md"), Enterprise: false);

        string BaselinePath = Path.Combine(Root, "tests", "MermaidRecognition.RuleValidation.Tests", "UnnecessaryBendBaseline.txt");
        if (!File.Exists(BaselinePath))
        {
            WriteBaseline(BaselinePath, Results);
            PrintTable(Results, new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));
            Assert.Pass("Initial unnecessary-bend baseline written.");
        }

        Dictionary<string, HashSet<string>> Baseline = ReadBaseline(BaselinePath);
        PrintTable(Results, Baseline);
        List<string> Regressions = [];
        foreach (ScenarioResult Result in Results)
        {
            Baseline.TryGetValue(Result.Id, out HashSet<string>? Allowed);
            Allowed ??= [];
            foreach (UnnecessaryBendViolation Violation in Result.Violations)
            {
                string Key = Violation.Check + ":" + Violation.LineId;
                if (!Allowed.Contains(Key)) Regressions.Add(Result.Id + " " + Key);
            }
        }
        Assert.That(Regressions, Is.Empty, "The unnecessary-bend baseline may only shrink.");
    }

    private static void AddCatalogue(List<ScenarioResult> Results, string PathValue, bool Enterprise)
    {
        if (!File.Exists(PathValue)) return;
        string Markdown = File.ReadAllText(PathValue);
        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> Diagrams = MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);
        Dictionary<string, int> Occurrences = new(StringComparer.Ordinal);
        foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram Diagram in Diagrams)
        {
            string Id = Enterprise ? ResolveEnterpriseId(Markdown, Diagram) : Diagram.DiagramId;
            if (!Occurrences.TryAdd(Id, 1)) Id += "." + (++Occurrences[Id]).ToString(CultureInfo.InvariantCulture);
            DiagramContract Contract = Enterprise
                ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Diagram.Source).Document)
                : MermaidRecognizer.Parse(Diagram.Source);
            if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
            {
                Results.Add(new ScenarioResult(Id, 0, []));
                continue;
            }
            Results.Add(new ScenarioResult(Id, Layout.EdgeRoutes.Count,
                UnnecessaryBendChecker.Check(Id, Contract, Layout)));
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

    private static void WriteBaseline(string PathValue, IReadOnlyList<ScenarioResult> Results)
    {
        List<string> Lines = ["# scenario\tcount\tcheck:line-id..."];
        foreach (ScenarioResult Result in Results.OrderBy(Result => Result.Id, StringComparer.Ordinal))
        {
            IEnumerable<string> Keys = Result.Violations.Select(Violation => Violation.Check + ":" + Violation.LineId)
                .OrderBy(Value => Value, StringComparer.Ordinal);
            Lines.Add(Result.Id + "\t" + Result.Violations.Count.ToString(CultureInfo.InvariantCulture)
                + "\t" + string.Join("\t", Keys));
        }
        File.WriteAllLines(PathValue, Lines);
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
