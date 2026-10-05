using System.Text.RegularExpressions;

using MDConverter;
using MermaidRecognition.Layout;

string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string Command = args.Length > 0 ? args[0] : "help";
if (string.Equals(Command, "rasterize", StringComparison.OrdinalIgnoreCase))
{
    string DirectoryPath = args.Length > 1 ? args[1] : Path.Combine(RepoRoot, "docs", "flowchart", "layout-fixes", "before");
    RasterizeDirectory(DirectoryPath);
    return;
}

if (string.Equals(Command, "render-flow", StringComparison.OrdinalIgnoreCase))
{
    string OutputDirectory = args.Length > 1 ? args[1] : Path.Combine(RepoRoot, "docs", "flowchart", "layout-fixes", "after");
    RenderFlowMarkdown(RepoRoot, OutputDirectory);
    return;
}

if (string.Equals(Command, "render-mmd", StringComparison.OrdinalIgnoreCase))
{
    string InputDirectory = args.Length > 1 ? args[1] : throw new InvalidOperationException("render-mmd requires an input directory.");
    string OutputDirectory = args.Length > 2 ? args[2] : InputDirectory;
    RenderMermaidFiles(InputDirectory, OutputDirectory);
    return;
}

if (string.Equals(Command, "refresh-after", StringComparison.OrdinalIgnoreCase))
{
    string AfterDirectory = args.Length > 1
        ? args[1]
        : Path.Combine(RepoRoot, "docs", "flowchart", "layout-fixes", "after");
    RefreshAfterDocs(RepoRoot, AfterDirectory);
    return;
}

if (string.Equals(Command, "dump-layout", StringComparison.OrdinalIgnoreCase))
{
    string Mermaid = args.Length > 1 ? File.ReadAllText(args[1]) : throw new InvalidOperationException("dump-layout requires a .mmd path.");
    DumpLayout(Mermaid);
    return;
}

Console.WriteLine("Usage: FlowchartLayoutFixRender rasterize <dir> | render-flow <dir> | render-mmd <in> <out> | refresh-after [dir] | dump-layout <mmd>");

static void RasterizeDirectory(string DirectoryPath)
{
    Directory.CreateDirectory(DirectoryPath);
    foreach (string SvgPath in Directory.GetFiles(DirectoryPath, "*.svg").OrderBy(Path.GetFileName, StringComparer.Ordinal))
    {
        string Svg = File.ReadAllText(SvgPath);
        byte[]? Png = MermaidDiagramExport.TryRasterizeSvg(Svg);
        string PngPath = Path.ChangeExtension(SvgPath, ".png");
        if (Png is null || Png.Length == 0)
        {
            Console.WriteLine("FAIL " + Path.GetFileName(SvgPath));
            continue;
        }

        File.WriteAllBytes(PngPath, Png);
        Console.WriteLine("PNG " + Path.GetFileName(PngPath));
    }
}

static void RenderFlowMarkdown(string RepoRoot, string OutputDirectory)
{
    Directory.CreateDirectory(OutputDirectory);
    string FlowMarkdown = File.ReadAllText(Path.Combine(RepoRoot, "flow.md"));
    foreach (Match HeadingMatch in Regex.Matches(FlowMarkdown, @"^#+ \**(F\d+[A-Za-z]?)\b", RegexOptions.Multiline))
    {
        string FixtureId = HeadingMatch.Groups[1].Value;
        Match BlockMatch = Regex.Match(FlowMarkdown.Substring(HeadingMatch.Index), @"```mermaid\r?\n(.*?)```", RegexOptions.Singleline);
        if (!BlockMatch.Success)
        {
            Console.WriteLine("SKIP " + FixtureId);
            continue;
        }

        RenderOne(FixtureId, BlockMatch.Groups[1].Value, OutputDirectory);
    }
}

static void RenderMermaidFiles(string InputDirectory, string OutputDirectory)
{
    Directory.CreateDirectory(OutputDirectory);
    foreach (string MermaidPath in Directory.GetFiles(InputDirectory, "*.mmd").OrderBy(Path.GetFileName, StringComparer.Ordinal))
    {
        string Stem = Path.GetFileNameWithoutExtension(MermaidPath);
        RenderOne(Stem, File.ReadAllText(MermaidPath), OutputDirectory);
    }
}

static void RefreshAfterDocs(string RepoRoot, string AfterDirectory)
{
    Directory.CreateDirectory(AfterDirectory);
    RenderFlowMarkdown(RepoRoot, AfterDirectory);
    string SamplesDirectory = Path.Combine(RepoRoot, "tests", "MermaidRecognition.Layout.Tests", "EnterpriseFlowchartSamples");
    if (Directory.Exists(SamplesDirectory))
    {
        foreach (string MermaidPath in Directory.GetFiles(SamplesDirectory, "*-flowchart.mmd").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string Number = Path.GetFileNameWithoutExtension(MermaidPath).Replace("-flowchart", string.Empty, StringComparison.Ordinal);
            RenderOne("enterprise-" + Number + "-flowchart", File.ReadAllText(MermaidPath), AfterDirectory);
        }
    }

    RasterizeDirectory(AfterDirectory);
}

static void DumpLayout(string Mermaid)
{
    MermaidRecognition.Contract.DiagramContract Contract = MermaidRecognition.Core.MermaidRecognizer.Parse(Mermaid);
    if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
    {
        Console.WriteLine("LAYOUT FAILED");
        return;
    }

    foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
    {
        Console.WriteLine(
            Bounds.NodeId
            + " cx="
            + Bounds.CenterX.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " cy="
            + Bounds.CenterY.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " w="
            + Bounds.Width.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " h="
            + Bounds.Height.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    }
}

static void RenderOne(string Stem, string Mermaid, string OutputDirectory)
{
    try
    {
        if (!NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Mermaid, out string Svg) || string.IsNullOrWhiteSpace(Svg))
        {
            Console.WriteLine("SKIP " + Stem);
            return;
        }

        File.WriteAllText(Path.Combine(OutputDirectory, Stem + ".svg"), Svg);
        byte[]? Png = MermaidDiagramExport.TryRasterizeSvg(Svg);
        if (Png is not null && Png.Length > 0)
        {
            File.WriteAllBytes(Path.Combine(OutputDirectory, Stem + ".png"), Png);
            Console.WriteLine("OK " + Stem);
        }
        else
        {
            Console.WriteLine("SVG " + Stem);
        }
    }
    catch (Exception Exception)
    {
        Console.WriteLine("FAIL " + Stem + " " + Exception.Message);
    }
}
