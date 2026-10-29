using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiveEditor.Azure;
using LiveEditor.Editor;
using MDConverter;

namespace LayoutCatalogueBench;

internal static class Program
{
    private const string LIVEEDITOR_ENTERPRISE_COMPLETE_FLAG = "@liveeditor-enterprise-complete";

    private const string LANDGATE_ENTERPRISE_IMPORT_FLAG = "@landgate-enterprise-import";

    private static readonly Regex SectionPattern = new(
        @"(?ms)^### (?<id>(?:F|A|EIP)\d+)\b.*?\r?\n```mermaid\s*\r?\n(?<source>.*?)\r?\n```",
        RegexOptions.Compiled);

    private static int Main(string[] Args)
    {
        if (Args.Length == 2 && Args[0] == "@landgate-enterprise-source")
        {
            string Csv = Path.Combine(FindRepositoryRoot(), "in", "Landgate.csv");
            if (!AzureResourceGraphInterop.TryImportFile(Csv, AzureResourceGraphImportOptions.CreateEnterpriseDefaults(), out var Source, out var Notes, out var Failure))
            { Console.Error.WriteLine(Failure); return 1; }
            string Output = Path.GetFullPath(Args[1]);
            Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
            File.WriteAllText(Output, Source, new UTF8Encoding(false));
            Console.WriteLine(Output);
            if (Notes.Length > 0) Console.WriteLine(Notes);
            return 0;
        }
        if (Args.Length > 0 && string.Equals(Args[0], LIVEEDITOR_ENTERPRISE_COMPLETE_FLAG, StringComparison.OrdinalIgnoreCase))
        {
            string[] Rest = Args.Length > 1 ? Args[1..] : Array.Empty<string>();
            return RunLiveEditorEnterpriseComplete(Rest);
        }

        if (Args.Length > 0 && string.Equals(Args[0], LANDGATE_ENTERPRISE_IMPORT_FLAG, StringComparison.OrdinalIgnoreCase))
        {
            string[] Rest = Args.Length > 1 ? Args[1..] : Array.Empty<string>();
            return RunLandgateEnterpriseImport(Rest);
        }

        string RepositoryRoot = FindRepositoryRoot();
        string OutputPath = Args.Length > 0 ? Args[0] : Path.Combine(RepositoryRoot, "docs", "bench", "catalogue-latest.jsonl");
        int CapMilliseconds = Args.Length > 1 && int.TryParse(Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ParsedCap)
            ? ParsedCap
            : 90_000;
        string? IdFilter = Args.Length > 2 ? Args[2] : null;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        List<string> MarkdownFiles = ["enterprise.md", "flow.md", "EIP.md"];
        using StreamWriter Writer = new StreamWriter(OutputPath, false, Encoding.UTF8);
        long TotalMilliseconds = 0;
        long TotalLayoutMilliseconds = 0;
        int Rendered = 0;
        int Failed = 0;
        int Skipped = 0;
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
                if (IdFilter == null && CatalogueBenchExclusions.TryGetSkipReason(Id, Source, out string SkipReason))
                {
                    Skipped++;
                    Console.WriteLine($"{Id} skip {SkipReason}");
                    continue;
                }

                BenchRecord Record = RenderOne(Id, MarkdownFile, Source, CapMilliseconds);
                Writer.WriteLine(JsonSerializer.Serialize(Record));
                TotalMilliseconds += Record.Milliseconds;
                TotalLayoutMilliseconds += Record.LayoutMilliseconds;
                if (Record.Ok)
                {
                    Rendered++;
                }
                else
                {
                    Failed++;
                }

                Console.WriteLine($"{Id} {(Record.Ok ? "ok" : "fail")} {Record.Milliseconds.ToString()} ms");
            }
        }

        Console.WriteLine($"Rendered {Rendered}, failed {Failed}, skipped {Skipped}, total {TotalMilliseconds} ms, layout {TotalLayoutMilliseconds} ms");
        Console.WriteLine($"Wrote {OutputPath}");
        return Failed > 0 ? 1 : 0;
    }

    private static int RunLiveEditorEnterpriseComplete(string[] Args)
    {
        string RepositoryRoot = FindRepositoryRoot();
        DiagramSampleGroup? Group = SampleCatalog.Find("enterprise");
        if (Group == null)
        {
            Console.Error.WriteLine("Enterprise sample group not found in SampleCatalog.");
            return 1;
        }

        string OutputPath = Args.Length > 0
            ? Args[0]
            : Path.Combine(RepositoryRoot, "docs", "bench", "liveeditor-enterprise-complete.jsonl");
        int CapMilliseconds = Args.Length > 1 && int.TryParse(Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ParsedCap)
            ? ParsedCap
            : 90_000;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        string SourceFile = "LiveEditor/Samples/Enterprise/" + Group.CompleteTitle;
        string StagesPath = Path.Combine(Path.GetDirectoryName(OutputPath)!, Path.GetFileNameWithoutExtension(OutputPath) + "-stages.json");
        BenchRecord Record = RenderOne("LiveEditor-enterprise-complete", SourceFile, Group.FullSource, CapMilliseconds, StagesPath);
        using StreamWriter Writer = new StreamWriter(OutputPath, false, Encoding.UTF8);
        Writer.WriteLine(JsonSerializer.Serialize(Record));
        Console.WriteLine($"{Record.Id} {(Record.Ok ? "ok" : "fail")} render {Record.Milliseconds.ToString()} ms layout {Record.LayoutMilliseconds.ToString()} ms");
        Console.WriteLine($"Menu: Samples -> Enterprise -> {Group.CompleteTitle} (title \"All options\" in source)");
        Console.WriteLine($"Wrote {OutputPath}");
        Console.WriteLine($"Stages: {StagesPath}");
        return Record.Ok ? 0 : 1;
    }

    private static int RunLandgateEnterpriseImport(string[] Args)
    {
        string RepositoryRoot = FindRepositoryRoot();
        string CsvPath = Path.Combine(RepositoryRoot, "in", "Landgate.csv");
        if (!File.Exists(CsvPath))
        {
            Console.Error.WriteLine($"Missing {CsvPath}");
            return 1;
        }

        AzureResourceGraphImportOptions Options = AzureResourceGraphImportOptions.CreateEnterpriseDefaults();
        if (!AzureResourceGraphInterop.TryImportFile(CsvPath, Options, out string MermaidSource, out string Warnings, out string ErrorMessage))
        {
            Console.Error.WriteLine(ErrorMessage);
            return 1;
        }

        string OutputPath = Args.Length > 0
            ? Args[0]
            : Path.Combine(RepositoryRoot, "docs", "bench", "landgate-enterprise-import.jsonl");
        int CapMilliseconds = Args.Length > 1 && int.TryParse(Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ParsedCap)
            ? ParsedCap
            : 600_000;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        string StagesPath = Path.Combine(Path.GetDirectoryName(OutputPath)!, Path.GetFileNameWithoutExtension(OutputPath) + "-stages.json");
        BenchRecord Record = RenderOne("Landgate-enterprise-import", "in/Landgate.csv (enterprise defaults)", MermaidSource, CapMilliseconds, StagesPath);
        using StreamWriter Writer = new StreamWriter(OutputPath, false, Encoding.UTF8);
        Writer.WriteLine(JsonSerializer.Serialize(Record));
        Console.WriteLine($"{Record.Id} {(Record.Ok ? "ok" : "fail")} render {Record.Milliseconds.ToString()} ms layout {Record.LayoutMilliseconds.ToString()} ms");
        if (!string.IsNullOrWhiteSpace(Warnings))
        {
            Console.WriteLine($"Import notes: {Warnings}");
        }

        Console.WriteLine($"Wrote {OutputPath}");
        Console.WriteLine($"Stages: {StagesPath}");
        return Record.Ok ? 0 : 1;
    }

    private static BenchRecord RenderOne(string Id, string SourceFile, string Source, int CapMilliseconds, string? StagesJsonPath = null)
    {
        Stopwatch Timer = Stopwatch.StartNew();
        bool Ok = false;
        string? Svg = null;
        string? Error = null;
        long LayoutMs = 0;
        try
        {
            using (CatalogueLayoutTiming LayoutTiming = new CatalogueLayoutTiming())
            {
                Ok = MermaidDiagramExport.TryRenderPreviewSvg(Source, out string RenderedSvg);
                Svg = RenderedSvg;
                LayoutMs = LayoutTiming.LayoutMilliseconds;
                if (!string.IsNullOrWhiteSpace(StagesJsonPath))
                {
                    LayoutTiming.WriteStagesJson(StagesJsonPath);
                }
            }
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
        return new BenchRecord
        {
            Id = Id,
            SourceFile = SourceFile,
            Ok = Ok,
            Milliseconds = Elapsed,
            LayoutMilliseconds = LayoutMs,
            SvgBytes = Svg?.Length ?? 0,
            SvgSha256 = Sha,
            Error = Error
        };
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

        public long LayoutMilliseconds { get; set; }

        public int SvgBytes { get; set; }

        public string SvgSha256 { get; set; } = string.Empty;

        public string? Error { get; set; }
    }
}
