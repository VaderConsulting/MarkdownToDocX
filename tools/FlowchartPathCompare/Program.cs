using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;

namespace FlowchartPathCompare;

internal static class Program
{
    private sealed class ScenarioBlock
    {
        public required string ScenarioId { get; init; }

        public required string ScenarioTitle { get; init; }

        public required string Source { get; init; }
    }

    private sealed class CompareRow
    {
        public required string ScenarioId { get; init; }

        public required string ScenarioTitle { get; init; }

        public bool LegacyOk { get; init; }

        public bool RecognitionOk { get; init; }

        public string? LegacyError { get; init; }

        public string? RecognitionError { get; init; }

        public string? LegacySvgFile { get; init; }

        public string? RecognitionSvgFile { get; init; }
    }

    private static int Main(string[] Args)
    {
        if (Args.Length < 2)
        {
            Console.Error.WriteLine("Usage: FlowchartPathCompare <flow.md> <outputDirectory> [--max=<n>]");
            return 1;
        }

        string MarkdownPath = Path.GetFullPath(Args[0]);
        string OutputDirectory = Path.GetFullPath(Args[1]);
        int? MaxBlocks = null;

        for (int I = 2; I < Args.Length; I++)
        {
            if (Args[I].StartsWith("--max=", StringComparison.OrdinalIgnoreCase))
            {
                string Value = Args[I]["--max=".Length..];
                if (int.TryParse(Value, out int Parsed) && Parsed > 0)
                {
                    MaxBlocks = Parsed;
                }
            }
        }

        if (!File.Exists(MarkdownPath))
        {
            Console.Error.WriteLine("File not found: " + MarkdownPath);
            return 1;
        }

        string Markdown = File.ReadAllText(MarkdownPath);
        List<ScenarioBlock> Blocks = ExtractFlowchartBlocks(Markdown);
        if (MaxBlocks.HasValue)
        {
            Blocks = Blocks.Take(MaxBlocks.Value).ToList();
        }

        Directory.CreateDirectory(OutputDirectory);
        List<CompareRow> Rows = new List<CompareRow>();
        RenderOptions Options = RenderOptions.Default;

        foreach (ScenarioBlock Block in Blocks)
        {
            string BaseName = SanitiseFileName(Block.ScenarioId);
            string LegacyPath = Path.Combine(OutputDirectory, BaseName + "-legacy.svg");
            string RecognitionPath = Path.Combine(OutputDirectory, BaseName + "-recognition.svg");

            bool LegacyOk = false;
            string? LegacyError = null;
            try
            {
                string LegacySvg = Mermaid.Render(Block.Source, Options);
                File.WriteAllText(LegacyPath, LegacySvg, Encoding.UTF8);
                LegacyOk = true;
            }
            catch (Exception Ex)
            {
                LegacyError = Ex.Message;
            }

            bool RecognitionOk = false;
            string? RecognitionError = null;
            try
            {
                if (FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(Block.Source, Options, out string RecognitionSvg))
                {
                    File.WriteAllText(RecognitionPath, RecognitionSvg, Encoding.UTF8);
                    RecognitionOk = true;
                }
                else
                {
                    RecognitionError = "Not detected as flowchart or render returned false.";
                }
            }
            catch (Exception Ex)
            {
                RecognitionError = Ex.Message;
            }

            Rows.Add(new CompareRow
            {
                ScenarioId = Block.ScenarioId,
                ScenarioTitle = Block.ScenarioTitle,
                LegacyOk = LegacyOk,
                RecognitionOk = RecognitionOk,
                LegacyError = LegacyError,
                RecognitionError = RecognitionError,
                LegacySvgFile = LegacyOk ? BaseName + "-legacy.svg" : null,
                RecognitionSvgFile = RecognitionOk ? BaseName + "-recognition.svg" : null,
            });

            Console.WriteLine(
                Block.ScenarioId
                + "  legacy="
                + (LegacyOk ? "OK" : "FAIL")
                + "  recognition="
                + (RecognitionOk ? "OK" : "FAIL"));
        }

        WriteReport(OutputDirectory, MarkdownPath, Rows);
        WriteIndexHtml(OutputDirectory, Rows);

        int LegacyPass = Rows.Count(Row => Row.LegacyOk);
        int RecognitionPass = Rows.Count(Row => Row.RecognitionOk);
        Console.WriteLine();
        Console.WriteLine("Legacy:      " + LegacyPass + "/" + Rows.Count);
        Console.WriteLine("Recognition: " + RecognitionPass + "/" + Rows.Count);
        Console.WriteLine("Open: " + Path.Combine(OutputDirectory, "index.html"));
        return RecognitionPass == Rows.Count ? 0 : 2;
    }

    static List<ScenarioBlock> ExtractFlowchartBlocks(string Markdown)
    {
        List<(int Position, string ScenarioId, string ScenarioTitle)> Headers = ParseScenarioHeaders(Markdown);
        Regex Regex = new Regex(@"(?s)```mermaid\s*\r?\n(?<Body>.*?)(\r?\n```)", RegexOptions.IgnoreCase);
        List<ScenarioBlock> Blocks = new List<ScenarioBlock>();
        int BlockIndex = 0;

        foreach (Match Match in Regex.Matches(Markdown))
        {
            string Body = Match.Groups["Body"].Value.Trim();
            if (!FlowchartRecognitionRenderBridge.IsFlowchartSource(Body))
            {
                BlockIndex++;
                continue;
            }

            (string ScenarioId, string ScenarioTitle) = ResolveScenario(BlockIndex, Headers);
            Blocks.Add(new ScenarioBlock
            {
                ScenarioId = ScenarioId,
                ScenarioTitle = ScenarioTitle,
                Source = Body,
            });
            BlockIndex++;
        }

        return Blocks;
    }

    static List<(int Position, string ScenarioId, string ScenarioTitle)> ParseScenarioHeaders(string Markdown)
    {
        List<(int, string, string)> Headers = new List<(int, string, string)>();
        Regex HeaderRegex = new Regex(
            @"^###\s+(?<Id>F\d{2}[A-Z]?)\s+(?<Title>.+)$",
            RegexOptions.Multiline);
        foreach (Match Match in HeaderRegex.Matches(Markdown))
        {
            Headers.Add((Match.Index, Match.Groups["Id"].Value, Match.Groups["Title"].Value.Trim()));
        }

        return Headers;
    }

    static (string ScenarioId, string ScenarioTitle) ResolveScenario(
        int BlockIndex,
        List<(int Position, string ScenarioId, string ScenarioTitle)> Headers)
    {
        if (BlockIndex < Headers.Count)
        {
            return (Headers[BlockIndex].ScenarioId, Headers[BlockIndex].ScenarioTitle);
        }

        if (Headers.Count > 0)
        {
            (int _, string Id, string Title) = Headers[Headers.Count - 1];
            return (Id, Title);
        }

        return ("BLOCK-" + (BlockIndex + 1).ToString("D2"), string.Empty);
    }

    static string SanitiseFileName(string ScenarioId)
    {
        return ScenarioId.Replace(':', '-').Replace('/', '-');
    }

    static void WriteReport(string OutputDirectory, string MarkdownPath, List<CompareRow> Rows)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.AppendLine("# Flowchart render path comparison");
        Builder.AppendLine();
        Builder.AppendLine("Source: `" + MarkdownPath + "`");
        Builder.AppendLine();
        Builder.AppendLine("| Scenario | Title | Legacy (Naiad parser) | Recognition (`MermaidRecognizer` + adapter) |");
        Builder.AppendLine("| --- | --- | --- | --- |");

        foreach (CompareRow Row in Rows)
        {
            Builder.AppendLine(
                "| "
                + Row.ScenarioId
                + " | "
                + Row.ScenarioTitle.Replace("|", "\\|")
                + " | "
                + (Row.LegacyOk ? "OK" : "FAIL: " + Row.LegacyError)
                + " | "
                + (Row.RecognitionOk ? "OK" : "FAIL: " + Row.RecognitionError)
                + " |");
        }

        File.WriteAllText(Path.Combine(OutputDirectory, "report.md"), Builder.ToString(), Encoding.UTF8);
    }

    static void WriteIndexHtml(string OutputDirectory, List<CompareRow> Rows)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        Builder.AppendLine("<title>flow.md — legacy vs recognition</title>");
        Builder.AppendLine("<style>");
        Builder.AppendLine("body{font-family:Segoe UI,sans-serif;margin:1rem;background:#f5f5f5;}");
        Builder.AppendLine("h1{font-size:1.25rem;} .row{display:flex;gap:1rem;margin-bottom:2rem;align-items:flex-start;}");
        Builder.AppendLine(".panel{flex:1;background:#fff;border:1px solid #ccc;padding:.5rem;}");
        Builder.AppendLine(".panel h2{font-size:.9rem;margin:0 0 .5rem;} img{max-width:100%;height:auto;border:1px solid #eee;}");
        Builder.AppendLine(".fail{color:#a00;font-size:.85rem;} .ok{color:#060;}");
        Builder.AppendLine("</style></head><body>");
        Builder.AppendLine("<h1>flow.md — Legacy Naiad parser vs MermaidRecognition adapter</h1>");
        Builder.AppendLine("<p>Left: <code>Mermaid.Render</code> (legacy <code>FlowchartParser</code>). ");
        Builder.AppendLine("Right: <code>UseFlowchartRecognitionParser</code> path.</p>");

        foreach (CompareRow Row in Rows)
        {
            Builder.AppendLine("<section class=\"row\"><div style=\"width:7rem\">");
            Builder.AppendLine("<strong>" + Row.ScenarioId + "</strong><br/><small>" + System.Net.WebUtility.HtmlEncode(Row.ScenarioTitle) + "</small></div>");
            Builder.AppendLine("<div class=\"panel\"><h2>Legacy</h2>");
            if (Row.LegacyOk && Row.LegacySvgFile is not null)
            {
                Builder.AppendLine("<p class=\"ok\">OK</p><img src=\"" + Row.LegacySvgFile + "\" alt=\"legacy\"/>");
            }
            else
            {
                Builder.AppendLine("<p class=\"fail\">" + System.Net.WebUtility.HtmlEncode(Row.LegacyError ?? "failed") + "</p>");
            }

            Builder.AppendLine("</div><div class=\"panel\"><h2>Recognition</h2>");
            if (Row.RecognitionOk && Row.RecognitionSvgFile is not null)
            {
                Builder.AppendLine("<p class=\"ok\">OK</p><img src=\"" + Row.RecognitionSvgFile + "\" alt=\"recognition\"/>");
            }
            else
            {
                Builder.AppendLine("<p class=\"fail\">" + System.Net.WebUtility.HtmlEncode(Row.RecognitionError ?? "failed") + "</p>");
            }

            Builder.AppendLine("</div></section>");
        }

        Builder.AppendLine("</body></html>");
        File.WriteAllText(Path.Combine(OutputDirectory, "index.html"), Builder.ToString(), Encoding.UTF8);
    }
}
