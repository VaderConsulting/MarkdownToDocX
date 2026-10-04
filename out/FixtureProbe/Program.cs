using System.Text.RegularExpressions;
using MDConverter;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string FlowMarkdown = File.ReadAllText(Path.Combine(RepoRoot, "flow.md"));
string OutputDirectory = Path.Combine(RepoRoot, "out", "FixtureProbe", "png");
int OutIndex = Array.IndexOf(args, "--out");
if (OutIndex >= 0 && OutIndex + 1 < args.Length)
{
    OutputDirectory = args[OutIndex + 1];
}

Directory.CreateDirectory(OutputDirectory);
bool PngOnly = args.Contains("--png");
bool SvgOnly = args.Contains("--svgonly");
List<string> FixtureIds = [];
if (args.Contains("--all"))
{
    foreach (Match HeadingMatch in Regex.Matches(FlowMarkdown, @"^#+ \**(F\d+[A-Za-z]?)\b", RegexOptions.Multiline))
    {
        string Id = HeadingMatch.Groups[1].Value;
        if (!FixtureIds.Contains(Id))
        {
            FixtureIds.Add(Id);
        }
    }
}
else
{
    for (int ArgIndex = 0; ArgIndex < args.Length; ArgIndex++)
    {
        if (args[ArgIndex] == "--out")
        {
            ArgIndex++;
            continue;
        }

        if (!args[ArgIndex].StartsWith("--", StringComparison.Ordinal))
        {
            FixtureIds.Add(args[ArgIndex]);
        }
    }
}

foreach (string FixtureId in FixtureIds)
{
    Match HeadingMatch = Regex.Match(FlowMarkdown, $@"^#+ \**{Regex.Escape(FixtureId)}\b[^\n]*$", RegexOptions.Multiline);
    if (!HeadingMatch.Success)
    {
        Console.WriteLine($"=== {FixtureId}: heading not found");
        continue;
    }

    Match BlockMatch = Regex.Match(FlowMarkdown.Substring(HeadingMatch.Index), @"```mermaid\r?\n(.*?)```", RegexOptions.Singleline);
    string Mermaid = BlockMatch.Groups[1].Value;
    Console.WriteLine($"=== {FixtureId}");

    try
    {
        if (NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Mermaid, out string Svg))
        {
            File.WriteAllText(Path.Combine(OutputDirectory, $"{FixtureId}.svg"), Svg);
        }

        if (SvgOnly)
        {
            continue;
        }

        byte[]? Png = MermaidDiagramExport.TryRenderPng(Mermaid);
        if (Png != null)
        {
            File.WriteAllBytes(Path.Combine(OutputDirectory, $"{FixtureId}.png"), Png);
        }
    }
    catch (Exception Ex)
    {
        Console.WriteLine($"=== {FixtureId}: render failed {Ex}");
        continue;
    }

    if (PngOnly)
    {
        continue;
    }

    Console.WriteLine(Mermaid.Trim());
    DiagramContract Contract = MermaidRecognizer.Parse(Mermaid);
    if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
    {
        Console.WriteLine("layout failed");
        continue;
    }

    FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
    Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds.ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
    Dictionary<string, NodeContract> NodeById = Contract.Nodes.ToDictionary(Node => Node.Id, Node => Node);
    foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
    {
        string Shape = NodeById.TryGetValue(Bounds.NodeId, out NodeContract? Node) ? Node.Shape ?? "" : "";
        Console.WriteLine($"  {Bounds.NodeId} {Shape} c=({Bounds.CenterX:F1},{Bounds.CenterY:F1}) w={Bounds.Width:F1} h={Bounds.Height:F1}");
    }

    foreach (EdgeContract Edge in Contract.Edges)
    {
        FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(Edge, Contract, BoundsById, NodeById, Direction, out List<(double X, double Y)> Points, out FlowchartEdgeRouteKind Kind);
        string Joined = string.Join(" ", Points.Select(Point => $"({Point.X:F1},{Point.Y:F1})"));
        FlowchartSpineGraphHelper.TryBuildContext(Contract, BoundsById, Direction, FlowchartRenderOptions.FromContract(Contract), out FlowchartSpineLayoutContext? SpineContext);
        FlowchartEdgeSemanticRouteClass SemanticClass = FlowchartEdgeRouteClassifier.Classify(Edge, Contract, BoundsById, NodeById, Direction, SpineContext);
        Console.WriteLine($"  {Edge.SourceId}->{Edge.TargetId} {Kind} [{SemanticClass}]: {Joined}");
    }
}
