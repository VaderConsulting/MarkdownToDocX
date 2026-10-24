using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.RuleValidation.Tests;

/// <summary>Captures and compares catalogue connector geometry in the renderer's page coordinates.</summary>
internal static class Program
{
    /// <summary>Runs all unchanged catalogue sources and writes a baseline or compares against one.</summary>
    /// <param name="Args">A capture or compare mode and baseline path; the -diagnostics modes also retain ordered final findings. Compare modes accept optional comma-separated scenario ids.</param>
    /// <returns>Zero for a complete capture or an exact match; one for changed scenarios; two for invalid arguments.</returns>
    private static int Main(string[] Args)
    {
        if (Args.Length is not (2 or 3) || Args[0] is not ("--capture" or "--compare" or "--capture-diagnostics" or "--compare-diagnostics")
            || Args.Length == 3 && Args[0] is not ("--compare" or "--compare-diagnostics"))
        {
            Console.Error.WriteLine("Usage: RouteGeometryHarness (--capture|--compare|--capture-diagnostics|--compare-diagnostics) <baseline.jsonl> [scenario-id,...]");
            return 2;
        }
        bool Capture = Args[0] is "--capture" or "--capture-diagnostics";
        bool IncludeDiagnostics = Args[0].EndsWith("-diagnostics", StringComparison.Ordinal);
        var Expected = Capture ? new Dictionary<string, JsonNode>() : File.ReadLines(Args[1])
            .Select(Line => JsonNode.Parse(Line)!).ToDictionary(Row => Row["Id"]!.GetValue<string>());
        var Selected = Args.Length == 3 ? Args[2].Split(',').ToHashSet(StringComparer.Ordinal) : null;
        using var Writer = Capture ? new StreamWriter(Args[1]) { AutoFlush = true } : null;
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        var Changed = new List<string>();
        var Clock = Stopwatch.StartNew();
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Selected != null && !Selected.Contains(Scenario.Id)) continue;
            var CaseClock = Stopwatch.StartNew();
            var Actual = Snapshot(Scenario, IncludeDiagnostics);
            Console.WriteLine($"GEOMETRY_CASE|{Scenario.Id}|seconds={CaseClock.Elapsed.TotalSeconds:0.000}");
            Seen.Add(Scenario.Id);
            if (Capture) Writer!.WriteLine(Actual.ToJsonString());
            else if (!Expected.TryGetValue(Scenario.Id, out var Old)
                || (IncludeDiagnostics ? !string.Equals(Old.ToJsonString(), Actual.ToJsonString(), StringComparison.Ordinal)
                    : !JsonNode.DeepEquals(Old, Actual)))
            {
                Changed.Add(Scenario.Id);
                Console.WriteLine($"GEOMETRY_CHANGED|{Scenario.Id}");
            }
        }
        var Missing = (Selected ?? Expected.Keys.ToHashSet(StringComparer.Ordinal)).Except(Seen).ToArray();
        foreach (string Id in Missing) Console.WriteLine($"GEOMETRY_MISSING|{Id}");
        Console.WriteLine(Capture
            ? $"GEOMETRY_CAPTURED|scenarios={Seen.Count}|seconds={Clock.Elapsed.TotalSeconds:0.000}"
            : $"GEOMETRY_COMPLETE|scenarios={Seen.Count}|changed={Changed.Count}|missing={Missing.Length}|seconds={Clock.Elapsed.TotalSeconds:0.000}");
        return Changed.Count == 0 && Missing.Length == 0 ? 0 : 1;
    }

    /// <summary>Uses the renderer's own frame, retaining exact terminal, bend and guide coordinates without pixel rounding.</summary>
    /// <param name="Scenario">One unchanged catalogue source and its native language mapping.</param>
    /// <param name="IncludeDiagnostics">Whether to include exact ordered diagnostic finding values alongside geometry.</param>
    /// <returns>A deterministic page-coordinate snapshot, including unsuccessful native layouts rather than silently dropping them.</returns>
    private static JsonNode Snapshot(RouteRuleCatalogueAuditTests.Scenario Scenario, bool IncludeDiagnostics)
    {
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        bool Native = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout);
        if (!Native) return JsonSerializer.SerializeToNode(new { Scenario.Id, Native })!;
        if (!FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Layout, out var Frame))
            throw new InvalidOperationException($"No rendering frame for successful layout {Scenario.Id}.");
        // Observe the final guide cache without adding a production API or
        // invoking a second route search that might choose different paths.
        var Guides = (IEnumerable<FlowchartResolvedGuide>?)typeof(FlowchartLayoutResult)
            .GetProperty("ResolvedEdgeGuides", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Layout);
        JsonNode Result = JsonSerializer.SerializeToNode(new
        {
            Scenario.Id, Native,
            // Compare positions on the page, not raw model Y: migration changes
            // its sign while the actual rendered connectors must stay identical.
            Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).OrderBy(Box => Box.NodeId, StringComparer.Ordinal)
                .Select(Box => new { Box.NodeId, Centre = PagePoint(Frame, Box.CenterX, Box.CenterY), Box.Width, Box.Height }),
            Routes = Layout.EdgeRoutes.OrderBy(Route => Route.EdgeId, StringComparer.Ordinal)
                .Select(Route => new { Route.EdgeId, Route.SourceId, Route.TargetId, Route.ExitSide, Route.EntrySide,
                    Points = Route.Points.Select(Point => PagePoint(Frame, Point.X, Point.Y)) }),
            Guides = Guides?.OrderBy(Guide => Guide.Edge.Id, StringComparer.Ordinal)
                .Select(Guide => new { Guide.Edge.Id, Guide.Edge.SourceId, Guide.Edge.TargetId,
                    Points = Guide.Points.Select(Point => PagePoint(Frame, Point.X, Point.Y)) })
        })!;
        if (IncludeDiagnostics)
            Result["Diagnostics"] = JsonSerializer.SerializeToNode(typeof(FlowchartRenderContext)
                .GetProperty("LayoutStageViolations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Layout.RenderContext));
        return Result;
    }

    /// <summary>Preserves both double coordinates in a JSON array instead of serializing unnamed tuple fields.</summary>
    /// <param name="Frame">The exact coordinate transform used by the renderer.</param>
    /// <param name="X">The model point's horizontal coordinate.</param>
    /// <param name="Y">The model point's vertical coordinate in the engine's current convention.</param>
    /// <returns>The point in page coordinates, with no tolerance or rounding.</returns>
    private static double[] PagePoint(FlowchartSvgCoordinateFrame Frame, double X, double Y)
    {
        var Point = Frame.LayoutToSvg(X, Y);
        return [Point.X, Point.Y];
    }
}
