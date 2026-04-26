using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidSharp;
using SkiaSharp;
using Svg.Skia;

namespace SvgAudit;

internal static class Program
{
    private sealed class TestCase
    {
        public required string SourceFilePath { get; init; }
        public required string DirectoryPath { get; init; }
        public required string ClassName { get; init; }
        public required string MethodName { get; init; }
        public required string Input { get; init; }

        public string VerifiedSvgPath
        {
            get
            {
                return Path.Combine(DirectoryPath, $"{ClassName}.{MethodName}.verified.svg");
            }
        }
    }

    private sealed class TestResult
    {
        public required TestCase TestCase { get; init; }
        public required bool VerifiedExists { get; init; }
        public required bool RenderSucceeded { get; init; }
        public required bool ExactMatch { get; init; }
        public required string Notes { get; init; }
    }

    private static int Main(string[] Args)
    {
        if (Args.Length >= 1 && string.Equals(Args[0], "render", StringComparison.OrdinalIgnoreCase))
        {
            return RenderFromStdinToSvg(Args);
        }

        string TestsRoot = Args.Length >= 1
            ? Args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "extern", "Naiad", "src", "Tests"));
        string OutputRoot = Args.Length >= 2
            ? Args[1]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "out", "svg-audit"));

        if (!Directory.Exists(TestsRoot))
        {
            Console.Error.WriteLine("Tests root not found: " + TestsRoot);
            return 2;
        }

        List<string> TestFiles = Directory.GetFiles(TestsRoot, "*Tests.cs", SearchOption.AllDirectories)
            .Where(Path => !Path.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path => Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<TestCase> TestCases = new List<TestCase>();
        foreach (string TestFilePath in TestFiles)
        {
            string Source = File.ReadAllText(TestFilePath);
            TestCases.AddRange(ExtractTestCases(TestFilePath, Source));
        }

        List<TestResult> Results = new List<TestResult>();
        foreach (TestCase TestCase in TestCases)
        {
            Results.Add(Evaluate(TestCase, OutputRoot));
        }

        WriteSummary(TestsRoot, Results);

        int FailCount = Results.Count(Result => Result.VerifiedExists && Result.RenderSucceeded && !Result.ExactMatch);
        int MissingVerifiedCount = Results.Count(Result => !Result.VerifiedExists);
        int RenderFailedCount = Results.Count(Result => !Result.RenderSucceeded);

        if (RenderFailedCount > 0)
        {
            return 3;
        }

        if (FailCount > 0 || MissingVerifiedCount > 0)
        {
            return 1;
        }

        return 0;
    }

    private static int RenderFromStdinToSvg(string[] Args)
    {
        // Usage:
        // dotnet run --project .\src\SvgAudit\SvgAudit.csproj -- render <outputSvgPath> [outputPngPath] [--no-stdout]
        // (Reads Mermaid input from stdin.)
        string OutputSvgPath = Args.Length >= 2 ? Args[1] : Path.Combine(Environment.CurrentDirectory, "rendered.svg");
        string? OutputPngPath = Args.Length >= 3 ? Args[2] : null;
        bool NoStdout = Args.Any(Arg => string.Equals(Arg, "--no-stdout", StringComparison.OrdinalIgnoreCase));
        string Input = Console.In.ReadToEnd();

        if (string.IsNullOrWhiteSpace(Input))
        {
            Console.Error.WriteLine("No Mermaid input provided on stdin.");
            return 2;
        }

        MemoryStream SvgStream = Mermaid.RenderToMemoryStream(Input, OutputSvgPath, RenderOptions.Default);
        SvgStream.Position = 0;
        string Svg;
        using (StreamReader Reader = new StreamReader(SvgStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
        {
            Svg = Reader.ReadToEnd();
        }

        if (!string.IsNullOrWhiteSpace(OutputPngPath))
        {
            TryRenderSvgToPng(Svg, OutputPngPath);
        }

        if (!NoStdout)
        {
            Console.WriteLine(Svg);
        }
        else
        {
            Console.WriteLine("Wrote: " + OutputSvgPath);
            if (!string.IsNullOrWhiteSpace(OutputPngPath))
            {
                Console.WriteLine("Wrote: " + OutputPngPath);
            }
        }
        return 0;
    }

    private static void TryRenderSvgToPng(string SvgXml, string OutputPngPath)
    {
        // Best-effort preview renderer for local inspection.
        // If SVG parsing fails, we still write the SVG and return the stream.
        try
        {
            SKSvg Svg = new SKSvg();
            Svg.FromSvg(SvgXml);

            SKPicture? Picture = Svg.Picture;
            if (Picture is null)
            {
                return;
            }

            SKRect Bounds = Picture.CullRect;
            int Width = (int)Math.Ceiling(Math.Max(1.0, Bounds.Width));
            int Height = (int)Math.Ceiling(Math.Max(1.0, Bounds.Height));

            using (SKBitmap Bitmap = new SKBitmap(Width, Height))
            using (SKCanvas Canvas = new SKCanvas(Bitmap))
            {
                Canvas.Clear(SKColors.White);
                Canvas.DrawPicture(Picture);
                Canvas.Flush();

                using (SKImage Image = SKImage.FromBitmap(Bitmap))
                using (SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    using (FileStream FileStream = new FileStream(OutputPngPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        Data.SaveTo(FileStream);
                        FileStream.Flush();
                    }
                }
            }
        }
        catch
        {
            // Best-effort only.
        }
    }

    private static List<TestCase> ExtractTestCases(string TestFilePath, string Source)
    {
        // We only need enough parsing to map [Test] methods that call VerifySvg(input)
        // and have a `const string input = """ ... """;` declaration.
        //
        // This stays intentionally simple and deterministic to avoid pulling Roslyn in.

        string DirectoryPath = Path.GetDirectoryName(TestFilePath) ?? "";
        string? ClassName = ExtractFirstMatch(Source, @"\bclass\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)\b", "Name");
        if (string.IsNullOrWhiteSpace(ClassName))
        {
            return new List<TestCase>();
        }

        List<TestCase> Results = new List<TestCase>();

        Regex MethodRegex = new Regex(
            @"(?s)\[\s*Test\s*\].*?\bTask\s+(?<Method>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*\)\s*\{(?<Body>.*?)\n\s*\}",
            RegexOptions.Compiled);

        MatchCollection Matches = MethodRegex.Matches(Source);
        foreach (Match Match in Matches)
        {
            string MethodName = Match.Groups["Method"].Value;
            string Body = Match.Groups["Body"].Value;

            if (!Body.Contains("VerifySvg", StringComparison.Ordinal))
            {
                continue;
            }

            string? Input = ExtractConstRawString(Body, "input");
            if (string.IsNullOrWhiteSpace(Input))
            {
                continue;
            }

            Results.Add(new TestCase
            {
                SourceFilePath = TestFilePath,
                DirectoryPath = DirectoryPath,
                ClassName = ClassName,
                MethodName = MethodName,
                Input = Input.Trim()
            });
        }

        return Results;
    }

    private static string? ExtractConstRawString(string Body, string VariableName)
    {
        // Matches:
        // const string input =
        //     """
        //     ...
        //     """;
        Regex RawRegex = new Regex(
            @"(?s)\bconst\s+string\s+" + Regex.Escape(VariableName) + @"\s*=\s*""\""""\s*\r?\n(?<Content>.*?)\r?\n\s*""\""""\s*;",
            RegexOptions.Compiled);

        Match Match = RawRegex.Match(Body);
        if (Match.Success)
        {
            return Match.Groups["Content"].Value;
        }

        // Fallback: const string input = "....";
        Regex NormalRegex = new Regex(
            @"\bconst\s+string\s+" + Regex.Escape(VariableName) + @"\s*=\s*""(?<Content>(?:\\""|[^""])*)""\s*;",
            RegexOptions.Compiled);

        Match = NormalRegex.Match(Body);
        if (Match.Success)
        {
            return Match.Groups["Content"].Value.Replace("\\\"", "\"", StringComparison.Ordinal);
        }

        return null;
    }

    private static TestResult Evaluate(TestCase TestCase, string OutputRoot)
    {
        bool VerifiedExists = File.Exists(TestCase.VerifiedSvgPath);
        if (!VerifiedExists)
        {
            return new TestResult
            {
                TestCase = TestCase,
                VerifiedExists = false,
                RenderSucceeded = true,
                ExactMatch = false,
                Notes = "Missing verified SVG: " + TestCase.VerifiedSvgPath
            };
        }

        string VerifiedSvg = File.ReadAllText(TestCase.VerifiedSvgPath);
        string ActualSvg;
        try
        {
            ActualSvg = Mermaid.Render(TestCase.Input, RenderOptions.Default);
        }
        catch (Exception Ex)
        {
            return new TestResult
            {
                TestCase = TestCase,
                VerifiedExists = true,
                RenderSucceeded = false,
                ExactMatch = false,
                Notes = "Render failed: " + Ex.GetType().Name + ": " + Ex.Message
            };
        }

        string VerifiedNormalised = NormaliseSvg(VerifiedSvg);
        string ActualNormalised = NormaliseSvg(ActualSvg);

        bool ExactMatch = string.Equals(VerifiedNormalised, ActualNormalised, StringComparison.Ordinal);
        string Notes = ExactMatch ? "OK" : SummariseMismatch(VerifiedSvg, ActualSvg);

        TryWriteArtifacts(OutputRoot, TestCase, VerifiedSvg, ActualSvg, Notes);

        return new TestResult
        {
            TestCase = TestCase,
            VerifiedExists = true,
            RenderSucceeded = true,
            ExactMatch = ExactMatch,
            Notes = Notes
        };
    }

    private static void TryWriteArtifacts(string OutputRoot, TestCase TestCase, string VerifiedSvg, string ActualSvg, string Notes)
    {
        try
        {
            Directory.CreateDirectory(OutputRoot);
            string BaseName = TestCase.ClassName + "." + TestCase.MethodName;
            string VerifiedOutPath = Path.Combine(OutputRoot, BaseName + ".verified.svg");
            string ActualOutPath = Path.Combine(OutputRoot, BaseName + ".actual.svg");
            string NotesOutPath = Path.Combine(OutputRoot, BaseName + ".notes.txt");

            File.WriteAllText(VerifiedOutPath, VerifiedSvg, new UTF8Encoding(false));
            File.WriteAllText(ActualOutPath, ActualSvg, new UTF8Encoding(false));
            File.WriteAllText(NotesOutPath, Notes, new UTF8Encoding(false));
        }
        catch
        {
            // Best-effort only; audit still proceeds.
        }
    }

    private static string NormaliseSvg(string Svg)
    {
        // Canonicalisation-lite: parse XML, remove insignificant whitespace, and serialise in a stable way.
        // We deliberately do NOT attempt full C14N; we only need a stable fingerprint for "exact match" checks.
        try
        {
            XDocument Document = XDocument.Parse(Svg, LoadOptions.PreserveWhitespace);
            RemoveStyleElements(Document.Root);
            NormaliseForeignObjects(Document.Root);
            NormaliseTextElements(Document.Root);
            RemoveWhitespaceTextNodes(Document.Root);
            SortAttributes(Document.Root);

            string Serialised = Document.ToString(SaveOptions.DisableFormatting);
            return Serialised;
        }
        catch
        {
            return Regex.Replace(Svg ?? string.Empty, @"\s+", " ").Trim();
        }
    }

    private static void RemoveStyleElements(XElement? Element)
    {
        if (Element == null)
        {
            return;
        }

        List<XElement> StyleElements = Element
            .Descendants()
            .Where(Descendant => string.Equals(Descendant.Name.LocalName, "style", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (XElement StyleElement in StyleElements)
        {
            StyleElement.Remove();
        }
    }

    private static void NormaliseForeignObjects(XElement? Root)
    {
        if (Root == null)
        {
            return;
        }

        List<XElement> ForeignObjects = Root
            .Descendants()
            .Where(Descendant => string.Equals(Descendant.Name.LocalName, "foreignObject", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (XElement ForeignObject in ForeignObjects)
        {
            string TextContent = Regex.Replace(ForeignObject.Value ?? string.Empty, @"\s+", " ").Trim();
            XElement Placeholder = new XElement(ForeignObject.Name.Namespace + "text");
            Placeholder.SetAttributeValue("data-normalised-from", "foreignObject");
            Placeholder.Value = TextContent;
            ForeignObject.ReplaceWith(Placeholder);
        }
    }

    private static void NormaliseTextElements(XElement? Root)
    {
        if (Root == null)
        {
            return;
        }

        List<XElement> TextElements = Root
            .Descendants()
            .Where(Descendant => string.Equals(Descendant.Name.LocalName, "text", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (XElement TextElement in TextElements)
        {
            string TextContent = Regex.Replace(TextElement.Value ?? string.Empty, @"\s+", " ").Trim();
            TextElement.RemoveAttributes();
            TextElement.RemoveNodes();
            TextElement.Value = TextContent;
        }
    }

    private static void RemoveWhitespaceTextNodes(XElement? Element)
    {
        if (Element == null)
        {
            return;
        }

        List<XNode> Nodes = Element.Nodes().ToList();
        foreach (XNode Node in Nodes)
        {
            if (Node is XText Text)
            {
                if (string.IsNullOrWhiteSpace(Text.Value))
                {
                    Text.Remove();
                }
            }
            else if (Node is XElement Child)
            {
                RemoveWhitespaceTextNodes(Child);
            }
        }
    }

    private static void SortAttributes(XElement? Element)
    {
        if (Element == null)
        {
            return;
        }

        List<XAttribute> Attributes = Element.Attributes().OrderBy(Attribute => Attribute.Name.ToString(), StringComparer.Ordinal).ToList();
        Element.RemoveAttributes();
        foreach (XAttribute Attribute in Attributes)
        {
            Element.Add(Attribute);
        }

        foreach (XElement Child in Element.Elements())
        {
            SortAttributes(Child);
        }
    }

    private static string SummariseMismatch(string VerifiedSvg, string ActualSvg)
    {
        // High-signal mismatch hints (text presence, foreignObject, marker usage).
        string VerifiedText = ExtractAllText(VerifiedSvg);
        string ActualText = ExtractAllText(ActualSvg);

        bool VerifiedHasForeignObject = VerifiedSvg.Contains("<foreignObject", StringComparison.OrdinalIgnoreCase);
        bool ActualHasForeignObject = ActualSvg.Contains("<foreignObject", StringComparison.OrdinalIgnoreCase);

        int VerifiedMarkerEndCount = CountOccurrences(VerifiedSvg, "marker-end=");
        int ActualMarkerEndCount = CountOccurrences(ActualSvg, "marker-end=");

        string VerifiedHash = ShortHash(NormaliseSvg(VerifiedSvg));
        string ActualHash = ShortHash(NormaliseSvg(ActualSvg));

        StringBuilder Builder = new StringBuilder();
        Builder.Append("DIFF ");
        Builder.Append("hash ");
        Builder.Append(VerifiedHash);
        Builder.Append(" -> ");
        Builder.Append(ActualHash);

        if (VerifiedHasForeignObject != ActualHasForeignObject)
        {
            Builder.Append(" | foreignObject ");
            Builder.Append(VerifiedHasForeignObject ? "expected" : "not-expected");
            Builder.Append(" vs ");
            Builder.Append(ActualHasForeignObject ? "actual-has" : "actual-no");
        }

        if (VerifiedMarkerEndCount != ActualMarkerEndCount)
        {
            Builder.Append(" | marker-end count ");
            Builder.Append(VerifiedMarkerEndCount);
            Builder.Append(" vs ");
            Builder.Append(ActualMarkerEndCount);
        }

        if (!string.Equals(VerifiedText, ActualText, StringComparison.Ordinal))
        {
            Builder.Append(" | text mismatch");
        }

        return Builder.ToString();
    }

    private static string ExtractAllText(string Svg)
    {
        try
        {
            XDocument Document = XDocument.Parse(Svg);
            string Text = string.Concat(Document.Descendants().Select(Element => Element.Value));
            return Regex.Replace(Text, @"\s+", " ").Trim();
        }
        catch
        {
            return "";
        }
    }

    private static int CountOccurrences(string Value, string Needle)
    {
        if (string.IsNullOrEmpty(Value) || string.IsNullOrEmpty(Needle))
        {
            return 0;
        }

        int Count = 0;
        int Index = 0;
        while (true)
        {
            int Next = Value.IndexOf(Needle, Index, StringComparison.OrdinalIgnoreCase);
            if (Next < 0)
            {
                break;
            }
            Count++;
            Index = Next + Needle.Length;
        }
        return Count;
    }

    private static string ShortHash(string Text)
    {
        byte[] Bytes = Encoding.UTF8.GetBytes(Text ?? string.Empty);
        byte[] Hash = SHA256.HashData(Bytes);
        return Convert.ToHexString(Hash).Substring(0, 12);
    }

    private static string? ExtractFirstMatch(string Text, string Pattern, string GroupName)
    {
        Match Match = Regex.Match(Text, Pattern);
        if (!Match.Success)
        {
            return null;
        }

        return Match.Groups[GroupName].Value;
    }

    private static void WriteSummary(string TestsRoot, List<TestResult> Results)
    {
        int Total = Results.Count;
        int WithVerified = Results.Count(Result => Result.VerifiedExists);
        int MissingVerified = Results.Count(Result => !Result.VerifiedExists);
        int RenderFailed = Results.Count(Result => !Result.RenderSucceeded);
        int ExactMatches = Results.Count(Result => Result.VerifiedExists && Result.RenderSucceeded && Result.ExactMatch);
        int Diffs = Results.Count(Result => Result.VerifiedExists && Result.RenderSucceeded && !Result.ExactMatch);

        Console.WriteLine("SVG AUDIT");
        Console.WriteLine("TestsRoot: " + TestsRoot);
        Console.WriteLine("Total: " + Total);
        Console.WriteLine("WithVerified: " + WithVerified);
        Console.WriteLine("MissingVerified: " + MissingVerified);
        Console.WriteLine("RenderFailed: " + RenderFailed);
        Console.WriteLine("ExactMatches: " + ExactMatches);
        Console.WriteLine("Diffs: " + Diffs);
        Console.WriteLine();

        foreach (TestResult Result in Results
            .Where(Result => !Result.VerifiedExists || !Result.RenderSucceeded || !Result.ExactMatch)
            .OrderBy(Result => Result.TestCase.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(Result => Result.TestCase.MethodName, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(Result.TestCase.ClassName + "." + Result.TestCase.MethodName);
            Console.WriteLine("  Verified: " + (Result.VerifiedExists ? "yes" : "no"));
            Console.WriteLine("  Render: " + (Result.RenderSucceeded ? "ok" : "failed"));
            Console.WriteLine("  ExactMatch: " + (Result.ExactMatch ? "yes" : "no"));
            Console.WriteLine("  Notes: " + Result.Notes);
            Console.WriteLine();
        }
    }
}

