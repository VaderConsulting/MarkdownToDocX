using System.IO.Compression;
using MDConverter.Options;

namespace MDConverter.Tests;

[TestClass]
public sealed class CommonMarkSpecFixtureTests
{
    [TestMethod]
    public void CommonMarkSpecExamples_ConvertToValidDocxPackages()
    {
        var Examples = LoadSpecExamples();
        Assert.IsGreaterThan(500, Examples.Count, "Expected the vendored CommonMark spec fixture to contain the official examples.");

        var Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions
        {
            Profile = "strict-commonmark"
        });

        var Failures = new List<string>();
        foreach (var Example in Examples)
        {
            try
            {
                var Bytes = Converter.ConvertText(Example.Markdown, AppContext.BaseDirectory);
                AssertValidDocxPackage(Bytes, Example.Number);
            }
            catch (Exception Ex)
            {
                Failures.Add($"Example {Example.Number} ({Example.Section}): {Ex.GetType().Name}: {Ex.Message}");
                if (Failures.Count >= 20)
                {
                    break;
                }
            }
        }

        if (Failures.Count > 0)
        {
            Assert.Fail("CommonMark spec examples failed DOCX conversion:" + Environment.NewLine + string.Join(Environment.NewLine, Failures));
        }
    }

    private static IReadOnlyList<CommonMarkExample> LoadSpecExamples()
    {
        var SpecPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "commonmark-spec.txt");
        var Lines = File.ReadAllLines(SpecPath);
        var Examples = new List<CommonMarkExample>();
        var Section = string.Empty;
        var ExampleNumber = 0;

        for (var I = 0; I < Lines.Length; I++)
        {
            var Line = Lines[I];
            if (Line.StartsWith("# ", StringComparison.Ordinal))
            {
                Section = Line.Substring(2).Trim();
                continue;
            }

            if (!Line.StartsWith("```````````````````````````````` example", StringComparison.Ordinal))
            {
                continue;
            }

            ExampleNumber++;
            I++;
            var MarkdownLines = new List<string>();
            while (I < Lines.Length && Lines[I] != ".")
            {
                MarkdownLines.Add(DecodeSpecLine(Lines[I]));
                I++;
            }

            while (I < Lines.Length && !Lines[I].StartsWith("````````````````````````````````", StringComparison.Ordinal))
            {
                I++;
            }

            Examples.Add(new CommonMarkExample(ExampleNumber, Section, string.Join(Environment.NewLine, MarkdownLines)));
        }

        return Examples;
    }

    private static string DecodeSpecLine(string Line)
    {
        return Line.Replace('\u2192', '\t');
    }

    private static void AssertValidDocxPackage(byte[] Bytes, int ExampleNumber)
    {
        Assert.IsGreaterThan(0, Bytes.Length, $"Example {ExampleNumber} produced an empty DOCX.");
        using var Stream = new MemoryStream(Bytes);
        using var Archive = new ZipArchive(Stream, ZipArchiveMode.Read);

        Assert.IsNotNull(Archive.GetEntry("[Content_Types].xml"), $"Example {ExampleNumber} missing content types.");
        Assert.IsNotNull(Archive.GetEntry("_rels/.rels"), $"Example {ExampleNumber} missing package relationships.");
        Assert.IsNotNull(Archive.GetEntry("word/document.xml"), $"Example {ExampleNumber} missing document.xml.");
        Assert.IsNotNull(Archive.GetEntry("word/styles.xml"), $"Example {ExampleNumber} missing styles.xml.");
        Assert.IsNotNull(Archive.GetEntry("word/numbering.xml"), $"Example {ExampleNumber} missing numbering.xml.");
    }

    private sealed record CommonMarkExample(int Number, string Section, string Markdown);
}
