using System.Text.RegularExpressions;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class CatalogueDiagramSvgRenderTests
{
    public static IEnumerable<TestCaseData> CatalogueFiles()
    {
        yield return new TestCaseData("flow.md", false);
        yield return new TestCaseData("enterprise.md", true);
    }

    [TestCaseSource(nameof(CatalogueFiles))]
    [Explicit("Full catalogue SVG gate; run in CI nightly.")]
    public void EveryScenario_RendersNonEmptySvg(string FileName, bool Enterprise)
    {
        string Root = FindRepositoryRoot();
        string Markdown = File.ReadAllText(Path.Combine(Root, FileName));
        List<string> Failures = [];
        foreach (Match Section in Regex.Matches(Markdown, @"(?ms)^### (?<id>(?:F|A|EIP)\d+)\b.*?\r?\n```mermaid\s*\r?\n(?<source>.*?)\r?\n```"))
        {
            string Id = Section.Groups["id"].Value;
            string Source = Section.Groups["source"].Value;
            try
            {
                bool Rendered = NativeDiagramRenderBridge.TryRenderSvg(Source, out string Svg);
                if (!Rendered || string.IsNullOrWhiteSpace(Svg) || Svg.Length < 32)
                {
                    Failures.Add(Id + ": empty or missing SVG");
                }
            }
            catch (Exception Ex)
            {
                Failures.Add(Id + ": " + Ex.GetType().Name + " " + Ex.Message);
            }
        }

        Assert.That(Failures, Is.Empty, string.Join(Environment.NewLine, Failures));
    }

    [Test]
    public void A163_RendersSvg()
    {
        string Source = ReadScenario("A163");
        Assert.That(NativeDiagramRenderBridge.TryRenderSvg(Source, out string Svg), Is.True);
        Assert.That(Svg.Length, Is.GreaterThan(100));
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
