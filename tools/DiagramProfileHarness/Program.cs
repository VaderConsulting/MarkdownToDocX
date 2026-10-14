using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using MDConverter;
using MDConverter.Options;
using MDConverter.Pipeline;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

if (args.Length == 2 && args[0] == "--worker")
{
    var d = JsonSerializer.Deserialize<Diagram>(File.ReadAllText(args[1]))!;
    var stages = new Dictionary<string, double>(StringComparer.Ordinal);
    string stagePath = args[1] + ".stages.json";
    void Record(string name, double ms)
    {
        stages[name] = ms < 0 ? -1 : stages.GetValueOrDefault(name) < 0
            ? ms : stages.GetValueOrDefault(name) + ms;
        File.WriteAllText(stagePath, JsonSerializer.Serialize(stages));
    }
    foreach (var (type, propertyName) in new (Type, string)[]
    {
        (typeof(DiagramGridLayout), "LayoutStageTimingObserver"),
        (typeof(MarkdownToDocxConverter), "ConversionStageTimingObserver"),
        (typeof(DocxWriter), "ConversionStageTimingObserver"),
        (typeof(EnterpriseRenderBridge), "StageTimingObserver"),
        (typeof(EnterpriseSvgRenderer), "StageTimingObserver"),
        (typeof(FlowchartSvgCoordinateFrameResolver), "StageTimingObserver"),
        (typeof(EnterpriseSvgRenderer).Assembly.GetType(
            "MermaidRecognition.Layout.Enterprise.EnterpriseFlowchartEdgeRouter", throwOnError: true)!,
            "StageTimingObserver")
    })
        type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, new Action<string, double>(Record));
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
    Console.WriteLine(JsonSerializer.Serialize(new Result(d.File, d.Index, d.Name, timer.Elapsed.TotalMilliseconds,
        success, error, stages)));
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

string outDir = Path.Combine(Path.GetTempPath(), "md2docx-diagram-profile-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outDir);
string dll = Assembly.GetExecutingAssembly().Location;
string dotnet = Environment.ProcessPath!;
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
        var startInfo = new ProcessStartInfo(dotnet, $"\"{dll}\" --worker \"{input}\"")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = root
        };
        using var process = Process.Start(startInfo)!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(true);
            string stagePath = input + ".stages.json";
            Dictionary<string, double> partialStages = File.Exists(stagePath)
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(stagePath)) ?? [] : [];
            string active = string.Join(",", partialStages.Where(p => p.Value < 0).Select(p => p.Key));
            results.Add(new Result(diagram.File, diagram.Index, diagram.Name, 60_000, false,
                "TIMEOUT after 60 seconds" + (active.Length == 0 ? "" : " during " + active), partialStages));
        }
        else
        {
            string output = outputTask.GetAwaiter().GetResult();
            string stderr = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                results.Add(JsonSerializer.Deserialize<Result>(output.Trim().Split('\n').Last())!);
            else
                results.Add(new Result(diagram.File, diagram.Index, diagram.Name, 0, false,
                    (stderr + output).Trim(), []));
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
    string Error, Dictionary<string, double> Stages);
