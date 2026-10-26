using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MDConverter;

namespace LayoutCatalogueBench;

internal static class Program
{
    private static readonly Regex SectionPattern = new(
        @"(?ms)^### (?<id>(?:F|A|EIP)\d+)\b.*?\r?\n```mermaid\s*\r?\n(?<source>.*?)\r?\n```",
        RegexOptions.Compiled);

    private static int Main(string[] Args)
    {
        string RepositoryRoot = FindRepositoryRoot();
        string OutputPath = Args.Length > 0 ? Args[0] : Path.Combine(RepositoryRoot, "docs", "bench", "catalogue-latest.jsonl");
        int CapMilliseconds = Args.Length > 1 && int.TryParse(Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ParsedCap)
            ? ParsedCap
            : 90_000;
        string? IdFilter = Args.Length > 2 ? Args[2] : null;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        List<string> MarkdownFiles = new List<string> { "enterprise.md", "flow.md", "EIP.md" };
        using StreamWriter Writer = new StreamWriter(OutputPath, false, Encoding.UTF8);
        long TotalMilliseconds = 0;
        int Rendered = 0;
        int Failed = 0;
        foreach (string MarkdownFile in MarkdownFiles)
        {
            string PathOnDisk = Path.Combine(RepositoryRoot, MarkdownFile);
            if (!File.Exists(PathOnDisk))
            {
                continue;
            }

            string Markdown = File.ReadAllText(PathOnDisk);
            foreach (Match Section in SectionPattern.Matches(Markdown))
            {
                string Id = Section.Groups["id"].Value;
                if (IdFilter != null && !string.Equals(Id, IdFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string Source = Section.Groups["source"].Value;
                Stopwatch Timer = Stopwatch.StartNew();
                bool Ok = false;
                string? Svg = null;
                string? Error = null;
                try
                {
                    Ok = MermaidDiagramExport.TryRenderPreviewSvg(Source, out string RenderedSvg);
                    Svg = RenderedSvg;
                }
                catch (Exception Ex)
                {
                    Error = Ex.GetType().Name + ": " + Ex.Message;
                }

                Timer.Stop();
                long Elapsed = Timer.ElapsedMilliseconds;
                if (Elapsed > CapMilliseconds)
                {
                    Error = "cap-exceeded";
                    Ok = false;
                }

                string Sha = Svg != null && Ok
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Svg)))
                    : string.Empty;
                BenchRecord Record = new BenchRecord
                {
                    Id = Id,
                    SourceFile = MarkdownFile,
                    Ok = Ok,
                    Milliseconds = Elapsed,
                    SvgBytes = Svg?.Length ?? 0,
                    SvgSha256 = Sha,
                    Error = Error
                };
                Writer.WriteLine(JsonSerializer.Serialize(Record));
                TotalMilliseconds += Elapsed;
                if (Ok)
                {
                    Rendered++;
                }
                else
                {
                    Failed++;
                }

                Console.WriteLine($"{Id} {(Ok ? "ok" : "fail")} {Elapsed} ms");
            }
        }

        Console.WriteLine($"Rendered {Rendered}, failed {Failed}, total {TotalMilliseconds} ms");
        Console.WriteLine($"Wrote {OutputPath}");
        return Failed > 0 ? 1 : 0;
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

    private sealed class BenchRecord
    {
        public string Id { get; set; } = string.Empty;

        public string SourceFile { get; set; } = string.Empty;

        public bool Ok { get; set; }

        public long Milliseconds { get; set; }

        public int SvgBytes { get; set; }

        public string SvgSha256 { get; set; } = string.Empty;

        public string? Error { get; set; }
    }
}
