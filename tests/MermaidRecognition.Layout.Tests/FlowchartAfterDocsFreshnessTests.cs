using System.Text.RegularExpressions;

using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// After-doc SVGs under docs/flowchart/layout-fixes/after must match a render from the current head.
/// </summary>
[TestFixture]
public sealed class FlowchartAfterDocsFreshnessTests
{
    [Test]
    public void AfterFlowchartSvgs_MatchCurrentHeadRender()
    {
        string AfterDirectory = FindAfterDirectory();
        List<string> Mismatches = [];
        foreach ((string Stem, string Source) in LoadRenderableSources())
        {
            if (!NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Source, out string FreshSvg)
                || string.IsNullOrWhiteSpace(FreshSvg))
            {
                continue;
            }

            string AfterPath = Path.Combine(AfterDirectory, Stem + ".svg");
            if (!File.Exists(AfterPath))
            {
                Mismatches.Add(Stem + " is missing from after/");
                continue;
            }

            string Committed = NormalizeSvg(File.ReadAllText(AfterPath));
            string Fresh = NormalizeSvg(FreshSvg);
            if (!string.Equals(Committed, Fresh, StringComparison.Ordinal))
            {
                Mismatches.Add(Stem + " is stale versus the current head render");
            }
        }

        Assert.That(
            Mismatches,
            Is.Empty,
            "Re-run tools/FlowchartLayoutFixRender refresh-after. Stale after docs:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, Mismatches));
    }

    [Test]
    public void AfterPngs_ExistForEveryAfterSvg()
    {
        string AfterDirectory = FindAfterDirectory();
        List<string> Missing = [];
        foreach (string SvgPath in Directory.GetFiles(AfterDirectory, "*.svg").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string PngPath = Path.ChangeExtension(SvgPath, ".png");
            if (!File.Exists(PngPath) || new FileInfo(PngPath).Length == 0)
            {
                Missing.Add(Path.GetFileName(PngPath));
            }
        }

        Assert.That(
            Missing,
            Is.Empty,
            "Every after SVG must have a PNG rasterised from the current head. Missing: "
            + string.Join(", ", Missing));
    }

    private static IEnumerable<(string Stem, string Source)> LoadRenderableSources()
    {
        string FlowPath = FindRepoFile("flow.md");
        string Markdown = File.ReadAllText(FlowPath);
        foreach (Match HeadingMatch in Regex.Matches(Markdown, @"^#+ \**(F\d+[A-Za-z]?)\b", RegexOptions.Multiline))
        {
            string FixtureId = HeadingMatch.Groups[1].Value;
            Match BlockMatch = Regex.Match(
                Markdown.Substring(HeadingMatch.Index),
                @"```mermaid\r?\n(.*?)```",
                RegexOptions.Singleline);
            if (BlockMatch.Success)
            {
                yield return (FixtureId, BlockMatch.Groups[1].Value);
            }
        }

        string SamplesDirectory = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "EnterpriseFlowchartSamples");
        if (!Directory.Exists(SamplesDirectory))
        {
            yield break;
        }

        foreach (string FilePath in Directory.GetFiles(SamplesDirectory, "*-flowchart.mmd")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string Number = Path.GetFileNameWithoutExtension(FilePath).Replace("-flowchart", string.Empty, StringComparison.Ordinal);
            yield return ("enterprise-" + Number + "-flowchart", File.ReadAllText(FilePath));
        }
    }

    private static string NormalizeSvg(string Svg)
    {
        return Regex.Replace(Svg.Trim().Replace("\r\n", "\n"), @"\s+", " ");
    }

    private static string FindAfterDirectory()
    {
        return Path.Combine(FindRepoRoot(), "docs", "flowchart", "layout-fixes", "after");
    }

    private static string FindRepoFile(string FileName)
    {
        return Path.Combine(FindRepoRoot(), FileName);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? Cursor = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (Cursor is not null)
        {
            if (File.Exists(Path.Combine(Cursor.FullName, "flow.md"))
                && Directory.Exists(Path.Combine(Cursor.FullName, "docs", "flowchart", "layout-fixes", "after")))
            {
                return Cursor.FullName;
            }

            Cursor = Cursor.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test directory.");
    }
}
