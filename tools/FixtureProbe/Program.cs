using System.Text.RegularExpressions;
using MDConverter;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string OutputDirectory = Path.Combine(RepoRoot, "out", "FixtureProbe", "png");
int OutIndex = Array.IndexOf(args, "--out");
if (OutIndex >= 0 && OutIndex + 1 < args.Length)
{
    OutputDirectory = args[OutIndex + 1];
}

Directory.CreateDirectory(OutputDirectory);
bool PngOnly = args.Contains("--png");
bool SvgOnly = args.Contains("--svgonly");

int MarkdownIndex = Array.IndexOf(args, "--md");
if (MarkdownIndex >= 0 && MarkdownIndex + 1 < args.Length)
{
    RenderMarkdownSections(Path.GetFullPath(args[MarkdownIndex + 1]), [.. args.Skip(MarkdownIndex + 2).Where(Arg => !Arg.StartsWith("--", StringComparison.Ordinal) && Arg != OutputDirectory)], OutputDirectory, SvgOnly);
    return;
}

string FlowMarkdown = File.ReadAllText(Path.Combine(RepoRoot, "flow.md"));
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

    DumpLayout(Mermaid);
}

static void DumpLayout(string Mermaid)
{
    Console.WriteLine(Mermaid.Trim());
    DiagramContract Contract = MermaidRecognizer.Parse(Mermaid);
    if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
    {
        Console.WriteLine("layout failed");
        return;
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
        Console.WriteLine($"  {Edge.SourceId}->{Edge.TargetId} {Kind} [{SemanticClass}] minlen={Edge.MinLength}: {Joined}");
    }
}

static void RenderMarkdownSections(string MarkdownPath, List<string> SectionIds, string OutputDirectory, bool SvgOnly)
{
    string Markdown = File.ReadAllText(MarkdownPath);
    foreach (string SectionId in SectionIds)
    {
        Match HeadingMatch = Regex.Match(Markdown, $@"^## {Regex.Escape(SectionId)}\.[^\n]*$", RegexOptions.Multiline);
        if (!HeadingMatch.Success)
        {
            Console.WriteLine($"=== {SectionId}: heading not found");
            continue;
        }

        int SectionStart = HeadingMatch.Index + HeadingMatch.Length;
        Match NextHeading = Regex.Match(Markdown.Substring(SectionStart), @"^## ", RegexOptions.Multiline);
        string Section = NextHeading.Success ? Markdown.Substring(SectionStart, NextHeading.Index) : Markdown.Substring(SectionStart);
        int BlockIndex = 0;
        foreach (Match BlockMatch in Regex.Matches(Section, @"^(?:```|:::)mermaid\r?\n(.*?)^(?:```|:::)\s*$", RegexOptions.Singleline | RegexOptions.Multiline))
        {
            string Mermaid = BlockMatch.Groups[1].Value;
            string FileStem = $"{SectionId}_{BlockIndex}";
            BlockIndex++;
            string FirstLine = Mermaid.TrimStart().Split('\n')[0].Trim();
            Console.WriteLine($"=== {FileStem} {FirstLine}");
            try
            {
                if (NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Mermaid, out string Svg))
                {
                    File.WriteAllText(Path.Combine(OutputDirectory, $"{FileStem}.svg"), Svg);
                }

                if (SvgOnly)
                {
                    continue;
                }

                byte[]? Png = MermaidDiagramExport.TryRenderPng(Mermaid);
                if (Png != null)
                {
                    File.WriteAllBytes(Path.Combine(OutputDirectory, $"{FileStem}.png"), Png);
                }

                if (FirstLine.StartsWith("flowchart", StringComparison.Ordinal) && !Mermaid.Contains("subgraph", StringComparison.Ordinal))
                {
                    DumpLayout(Mermaid);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine($"=== {FileStem}: render failed {Ex}");
            }
        }
    }
}
