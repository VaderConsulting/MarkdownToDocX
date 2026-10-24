using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using MDConverter;
using MDConverter.Options;
using MDConverter.Pipeline;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

if (args.Length == 3 && args[0] == "--docx")
{
    string InputPath = Path.GetFullPath(args[1]);
    string OutputPath = Path.GetFullPath(args[2]);
    MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(new MarkdownToDocxOptions());
    Converter.ConvertFile(InputPath, OutputPath);
    Console.WriteLine("DOCX_WRITTEN|" + OutputPath);
    return;
}

if (args.Length == 3 && args[0] == "--profile-documents")
{
    string documentRoot = Path.GetFullPath(args[1]);
    using var writer = new StreamWriter(args[2], append: false) { AutoFlush = true };
    foreach (string file in new[] { "flow.md", "enterprise.md", "EIP.md" })
    {
        var progress = new DocumentDiagramTimer(file, writer);
        var clock = Stopwatch.StartNew();
        _ = new MarkdownToDocxConverter(new MarkdownToDocxOptions(), progress)
            .ConvertText(File.ReadAllText(Path.Combine(documentRoot, file)), documentRoot);
        progress.Report(100, "Complete");
        Console.WriteLine($"DOCUMENT_DONE|{file}|{clock.Elapsed.TotalSeconds:0.000}s");
    }
    return;
}

if (args.Length == 2 && args[0] == "--worker")
{
    var d = JsonSerializer.Deserialize<Diagram>(File.ReadAllText(args[1]))!;
    var stages = new Dictionary<string, double>(StringComparer.Ordinal);
    var counts = new Dictionary<string, long>(StringComparer.Ordinal);
    string stagePath = args[1] + ".stages.json";
    string countPath = args[1] + ".counts.json";
    void Record(string name, double ms)
    {
        stages[name] = ms < 0 ? -1 : stages.GetValueOrDefault(name) < 0
            ? ms : stages.GetValueOrDefault(name) + ms;
        File.WriteAllText(stagePath, JsonSerializer.Serialize(stages));
    }
    int countWrites = 0;
    void RecordCount(string name, long count)
    {
        counts[name] = counts.GetValueOrDefault(name) + count;
        if (++countWrites % 128 == 0)
            File.WriteAllText(countPath, JsonSerializer.Serialize(counts));
    }
    foreach (var (type, propertyName) in new (Type, string)[]
    {
        (typeof(DiagramGridLayout), "LayoutStageTimingObserver"),
        (typeof(MarkdownToDocxConverter), "ConversionStageTimingObserver"),
        (typeof(DocxWriter), "ConversionStageTimingObserver"),
        (typeof(EnterpriseRenderBridge), "StageTimingObserver"),
        (typeof(EnterpriseSvgRenderer), "StageTimingObserver"),
        (typeof(FlowchartSvgCoordinateFrameResolver), "StageTimingObserver"),
        (typeof(FlowchartSugiyamaSvgRenderer), "StageTimingObserver"),
        (typeof(EnterpriseSvgRenderer).Assembly.GetType(
            "MermaidRecognition.Layout.Enterprise.EnterpriseFlowchartEdgeRouter", throwOnError: true)!,
            "StageTimingObserver")
    })
        type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, new Action<string, double>(Record));
    typeof(FlowchartRenderContext).GetProperty("OperationCountObserver", BindingFlags.Static | BindingFlags.NonPublic)!
        .SetValue(null, new Action<string, long>(RecordCount));
    string? svgSha256 = null;
    Action<string> captureSvg = svg => svgSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg)));
    typeof(FlowchartSugiyamaSvgRenderer).GetProperty("RenderedSvgObserver", BindingFlags.Static | BindingFlags.NonPublic)!
        .SetValue(null, captureSvg);
    typeof(EnterpriseSvgRenderer).GetProperty("RenderedSvgObserver", BindingFlags.Static | BindingFlags.NonPublic)!
        .SetValue(null, captureSvg);
    bool success = false;
    string error = "";
    var timer = Stopwatch.StartNew();
    try
    {
        var markdown = $"```mermaid\n{d.Source}\n```";
        _ = new MarkdownToDocxConverter(new MarkdownToDocxOptions()).ConvertText(markdown, Environment.CurrentDirectory);
        success = true;
    }
    catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message.Replace('\n', ' '); }
    timer.Stop();
    File.WriteAllText(countPath, JsonSerializer.Serialize(counts));
    Console.WriteLine(JsonSerializer.Serialize(new Result(d.File, d.Index, d.Name, timer.Elapsed.TotalMilliseconds,
        success, error, stages, counts, svgSha256)));
    return;
}

if ((args.Length != 2 && args.Length != 3) || args[0] != "--profile")
{
    Console.Error.WriteLine("Usage: DiagramProfileHarness --profile <repo-root> [results.jsonl]");
    Environment.ExitCode = 2;
    return;
}

string root = Path.GetFullPath(args[1]);
string[] files = ["enterprise.md", "flow.md", "EIP.md"];
var diagrams = new List<Diagram>();
foreach (var file in files)
{
    var markdown = File.ReadAllText(Path.Combine(root, file));
    var pattern = file == "EIP.md"
        ? @"(?ms)^:::mermaid\s*\r?\n(?<body>.*?)^:::\s*$"
        : @"(?ms)^```(?:mermaid|enterprise)\s*\r?\n(?<body>.*?)^```\s*$";
    var matches = Regex.Matches(markdown, pattern);
    for (int i = 0; i < matches.Count; i++)
    {
        int start = matches[i].Index;
        string prefix = markdown[..start];
        var heading = Regex.Matches(prefix, @"(?m)^#{1,4}\s+(.+?)\s*$").Cast<Match>().LastOrDefault();
        string name = heading?.Groups[1].Value.Trim() ?? $"Diagram {i + 1}";
        diagrams.Add(new Diagram(file, i + 1, name, matches[i].Groups["body"].Value.Trim()));
    }
}

string? only = Environment.GetEnvironmentVariable("MD2DOCX_PROFILE_ONLY");
if (!string.IsNullOrWhiteSpace(only))
{
    string[] key = only.Split(':', 2);
    if (key.Length != 2 || !key[1].Split(',').All(value => int.TryParse(value, out _)))
        throw new ArgumentException("MD2DOCX_PROFILE_ONLY must be <file>:<one-based-index>[,<index>...].");
    var selected = key[1].Split(',').Select(int.Parse).ToHashSet();
    diagrams = diagrams.Where(d => string.Equals(d.File, key[0], StringComparison.OrdinalIgnoreCase)
        && selected.Contains(d.Index)).ToList();
}

string outDir = Path.Combine(Path.GetTempPath(), "md2docx-diagram-profile-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outDir);
string dll = Assembly.GetExecutingAssembly().Location;
// The harness may itself run through its apphost. Launch the managed worker
// with dotnet explicitly so the assembly path is not mistaken for an argument.
string dotnet = "dotnet";
string resultsPath = args.Length == 3 ? Path.GetFullPath(args[2])
    : Path.Combine(root, "diagram-profile-results.jsonl");
var results = File.Exists(resultsPath)
    ? File.ReadLines(resultsPath).Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => JsonSerializer.Deserialize<Result>(line)!).ToList()
    : new List<Result>();
var completed = results.Select(r => (r.File, r.Index)).ToHashSet();
try
{
    foreach (var diagram in diagrams)
    {
        if (completed.Contains((diagram.File, diagram.Index))) continue;
        string input = Path.Combine(outDir, $"{diagram.File}-{diagram.Index:D4}.json");
        File.WriteAllText(input, JsonSerializer.Serialize(diagram));
        var startInfo = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = root
        };
        startInfo.ArgumentList.Add(dll);
        startInfo.ArgumentList.Add("--worker");
        startInfo.ArgumentList.Add(input);
        using var process = Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(true);
            string stagePath = input + ".stages.json";
            Dictionary<string, double> partialStages = File.Exists(stagePath)
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(stagePath)) ?? [] : [];
            Dictionary<string, long> partialCounts = File.Exists(input + ".counts.json")
                ? JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(input + ".counts.json")) ?? [] : [];
            string active = string.Join(",", partialStages.Where(p => p.Value < 0).Select(p => p.Key));
            results.Add(new Result(diagram.File, diagram.Index, diagram.Name, 60_000, false,
                "TIMEOUT after 60 seconds" + (active.Length == 0 ? "" : " during " + active), partialStages, partialCounts, null));
        }
        else
        {
            string output = outputTask.GetAwaiter().GetResult();
            string stderr = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                results.Add(JsonSerializer.Deserialize<Result>(output.Trim().Split('\n').Last())!);
            else
                results.Add(new Result(diagram.File, diagram.Index, diagram.Name, 0, false,
                    (stderr + output).Trim(), [], [], null));
        }
        File.AppendAllText(resultsPath, JsonSerializer.Serialize(results[^1]) + Environment.NewLine);
        if (results.Count % 10 == 0 || !results[^1].Success)
            Console.WriteLine($"{results.Count}/{diagrams.Count} {results[^1].File} #{results[^1].Index} "
                + $"{results[^1].Milliseconds:0} ms {results[^1].Name} {results[^1].Error}");
    }
}
finally { Directory.Delete(outDir, true); }

Console.WriteLine("SUMMARY " + JsonSerializer.Serialize(new
{
    Count = results.Count,
    TotalMs = results.Sum(r => r.Milliseconds),
    Failed = results.Count(r => !r.Success),
    Slowest = results.OrderByDescending(r => r.Milliseconds).Take(20).ToArray(),
    StageTotals = results.SelectMany(r => r.Stages).GroupBy(p => p.Key)
        .ToDictionary(g => g.Key, g => g.Sum(p => p.Value))
}));

record Diagram(string File, int Index, string Name, string Source);
record Result(string File, int Index, string Name, double Milliseconds, bool Success,
    string Error, Dictionary<string, double> Stages, Dictionary<string, long> Counts, string? SvgSha256);

/// <summary>Measures each diagram block inside a real whole-document conversion without process-start overhead.</summary>
/// <param name="File">The source catalogue recorded with each diagram timing.</param>
/// <param name="Writer">The caller-owned JSON-lines output; the timer does not close it.</param>
sealed class DocumentDiagramTimer(string File, TextWriter Writer) : IConversionProgressReporter
{
    private readonly Stopwatch _clock = new();
    private string? _diagram;
    private int _index;

    /// <summary>Closes the preceding diagram timing and starts the next diagram when its write begins.</summary>
    /// <param name="PercentComplete">The converter's current progress percentage.</param>
    /// <param name="Message">The block description, including its enclosing source heading.</param>
    public void Report(int PercentComplete, string Message)
    {
        if (_diagram != null)
        {
            _clock.Stop();
            Writer.WriteLine(JsonSerializer.Serialize(new { File, Index = _index, Heading = _diagram,
                Milliseconds = _clock.Elapsed.TotalMilliseconds }));
            _diagram = null;
        }
        if (!Message.StartsWith("Writing Mermaid diagram", StringComparison.Ordinal)) return;
        _index++;
        _diagram = Message;
        _clock.Restart();
    }
}
