using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LiveEditor.Editor;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

// Run serially in a fresh process: the production observers are process-wide,
// and each distinct source/options pair must enter layout with a cold SVG cache.
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DiagramLayoutBench OUTPUT.jsonl [@complete|SOURCE.mmd|@fixtures] [TARGET_WIDTH_PX]");
    return 1;
}
var Options = args.Length > 2
    ? new DiagramLayoutOptions { EnableWidthConstrainedLayout = true, TargetWidthPx = double.Parse(args[2], CultureInfo.InvariantCulture) }
    : DiagramLayoutOptions.Default;
string Selection = args.Length > 1 ? args[1] : "@fixtures";
IEnumerable<(string Id, string Source)> Cases = Selection switch
{
    "@complete" => [("enterprise-complete", SampleCatalog.Find("enterprise")!.FullSource)],
    "@fixtures" => Fixtures(),
    _ => [(Path.GetFileNameWithoutExtension(Selection), File.ReadAllText(Selection))]
};
string Output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(Output)!);
using var Writer = new StreamWriter(Output, false, new UTF8Encoding(false));
int Failed = 0;
foreach (var Item in Cases)
{
    var Analysis = EnterpriseValidator.Analyze(Item.Source);
    var ScoreContract = EnterpriseFlowchartContractMapper.Build(Analysis.Document);
    object? Model = null;
    var Wraps = new List<object>();
    var Stages = new Dictionary<string, double>(StringComparer.Ordinal);
    string? Failure = null;
    string Svg = string.Empty;
    var PreviousPublished = DiagramGridLayout.PublishedLayoutObserver;
    var PreviousWrap = DiagramGridLayout.BandWrapCandidateObserver;
    var PreviousStage = EnterpriseSvgRenderer.StageTimingObserver;
    DiagramGridLayout.PublishedLayoutObserver = (Contract, Layout) =>
    {
        var Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        string Geometry = JsonSerializer.Serialize(new
        {
            Nodes = Layout.NodeBounds.OrderBy(Node => Node.NodeId, StringComparer.Ordinal),
            Groups = Layout.ClusterBounds.OrderBy(Node => Node.NodeId, StringComparer.Ordinal),
            Routes = Layout.EdgeRoutes.OrderBy(Route => Route.EdgeId, StringComparer.Ordinal)
        });
        Model = new
        {
            Nodes = Layout.NodeBounds.Count, Routes = Layout.EdgeRoutes.Count, ExpectedRoutes = Contract.Edges.Count,
            Score.Bends, Score.Crossings, Score.Overlaps, Score.Disconnections,
            GeometryHash = Hash(Geometry), FindingsHash = Hash(JsonSerializer.Serialize(Findings)),
            Findings = Findings.GroupBy(Finding => Finding.Rule).ToDictionary(Group => Group.Key, Group => Group.Count()),
            HardFindings = Findings.Count(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"),
            Diagnostics = Layout.RenderContext.BandSpaceDiagnostics.ToArray()
        };
    };
    DiagramGridLayout.BandWrapCandidateObserver = (Width, Layout, Cost, Legal) =>
    {
        var Score = FlowchartLayoutScore.Measure(ScoreContract, Layout, false);
        Wraps.Add(new { Columns = Width, Legal, WidthPx = Score.Width, HeightPx = Score.Height, Score.Bends, Score.Crossings });
    };
    EnterpriseSvgRenderer.StageTimingObserver = (Name, Milliseconds) =>
    {
        if (Milliseconds >= 0) Stages[Name] = Stages.GetValueOrDefault(Name) + Milliseconds;
    };
    var Clock = Stopwatch.StartNew();
    try
    {
        if (Analysis.HasErrors) throw new InvalidOperationException(string.Join("; ", Analysis.Errors.Select(Error => Error.Message)));
        Svg = EnterpriseSvgRenderer.Render(Analysis.Document, Options);
        if (Model == null) throw new InvalidOperationException("No published model was observed; run in a fresh process with distinct fixture sources.");
    }
    catch (Exception Error) { Failure = Error.ToString(); Failed++; }
    finally
    {
        Clock.Stop();
        DiagramGridLayout.PublishedLayoutObserver = PreviousPublished;
        DiagramGridLayout.BandWrapCandidateObserver = PreviousWrap;
        EnterpriseSvgRenderer.StageTimingObserver = PreviousStage;
    }
    XElement? Root = Svg.Length == 0 ? null : XDocument.Parse(Svg).Root;
    double WidthPx = Length(Root, "width"), HeightPx = Length(Root, "height");
    var Record = new
    {
        Item.Id, SourceHash = Hash(Item.Source), Options, UseCellRegions = new FlowchartRenderContext().UseCellRegions,
        Milliseconds = Clock.Elapsed.TotalMilliseconds, RenderedWidthPx = WidthPx, RenderedHeightPx = HeightPx,
        Reference12PxTextAt1200PxFitWidth = 12 * Math.Min(1, 1200 / Math.Max(1, WidthPx)),
        SvgHash = Hash(Svg), Model, Wraps, Stages, Failure
    };
    Writer.WriteLine(JsonSerializer.Serialize(Record));
    Writer.Flush();
    if (Svg.Length > 0) File.WriteAllText(Path.Combine(Path.GetDirectoryName(Output)!, Path.GetFileNameWithoutExtension(Output) + "-" + Item.Id + ".svg"), Svg);
    Console.WriteLine($"{Item.Id}: {WidthPx:0} x {HeightPx:0}, {Clock.Elapsed.TotalMilliseconds:0} ms, {(Failure == null ? "ok" : "FAILED")}");
}
Console.WriteLine(Output);
return Failed == 0 ? 0 : 1;

static string Hash(string Value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Value)));
static double Length(XElement? Root, string Name) => double.TryParse(((string?)Root?.Attribute(Name))?.TrimEnd('p', 'x'),
    NumberStyles.Float, CultureInfo.InvariantCulture, out double Value) ? Value : 0;

static IEnumerable<(string Id, string Source)> Fixtures()
{
    yield return ("single-layer-free", "enterprise\n" + string.Join("\n", Enumerable.Range(0, 12).Select(I => " application-component a" + I)));
    yield return ("single-layer-fan", "enterprise\n application-component hub\n"
        + string.Join("\n", Enumerable.Range(0, 8).Select(I => " application-component a" + I + "\n hub -flow-> a" + I)));
    yield return ("cross-layer-fan", "enterprise\n business-actor owner\n"
        + string.Join("\n", Enumerable.Range(0, 8).Select(I => " application-component a" + I + "\n owner -association-> a" + I)));
    yield return ("nested-and-parallel", "enterprise\n group outer \"Outer\" {\n group inner \"Inner\" {\n"
        + " application-component a\n application-component b\n }\n }\n business-actor owner\n"
        + " a -flow-> b \"first\"\n a -association-> b \"second\"\n owner -association-> a");
    yield return ("horizontal-chain", "enterprise\n direction LR\n"
        + string.Join("\n", Enumerable.Range(0, 8).Select(I => " application-component a" + I)) + "\n"
        + string.Join("\n", Enumerable.Range(0, 7).Select(I => " a" + I + " -flow-> a" + (I + 1))));
}
