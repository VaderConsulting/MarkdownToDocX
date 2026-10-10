using System.Text.RegularExpressions;

using MDConverter;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;

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

if (string.Equals(Command, "dump-gate", StringComparison.OrdinalIgnoreCase))
{
    string Mermaid = args.Length > 1 ? File.ReadAllText(args[1]) : throw new InvalidOperationException("dump-gate requires a .mmd path.");
    DumpGate(Path.GetFileName(args[1]), Mermaid);
    return;
}

if (string.Equals(Command, "dump-svg-gate", StringComparison.OrdinalIgnoreCase))
{
    string Mermaid = args.Length > 1 ? File.ReadAllText(args[1]) : throw new InvalidOperationException("dump-svg-gate requires a .mmd path.");
    string Svg = args.Length > 2 ? File.ReadAllText(args[2]) : throw new InvalidOperationException("dump-svg-gate requires an .svg path.");
    DumpSvgGate(Path.GetFileName(args[1]), Mermaid, Svg);
    return;
}

Console.WriteLine("Usage: FlowchartLayoutFixRender rasterize <dir> | render-flow <dir> | render-mmd <in> <out> | refresh-after [dir] | dump-layout <mmd> | dump-gate <mmd> | dump-svg-gate <mmd> <svg>");

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

static void DumpGate(string Name, string Mermaid)
{
    if (!NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Mermaid, out string Svg) || string.IsNullOrWhiteSpace(Svg))
    {
        Console.WriteLine("RENDER FAILED");
        return;
    }

    DumpSvgGate(Name, Mermaid, Svg);
    DumpEdgePaths(Svg);
}

static void DumpSvgGate(string Name, string Mermaid, string Svg)
{
    FlowchartRenderedSvgQualityReport Picture = FlowchartRenderedSvgQualityGate.EvaluatePicture(Name + "-picture", Mermaid, Svg);
    FlowchartRenderedSvgQualityReport Live = FlowchartRenderedSvgQualityGate.Evaluate(Name, Mermaid);
    Console.WriteLine("PICTURE failures=" + Picture.Failures.Count + " tolerated=" + Picture.Tolerated.Count);
    foreach (FlowchartLayoutQualityFailure Failure in Picture.Failures)
    {
        Console.WriteLine("  FAIL " + Failure.Kind + " " + Failure.Description);
    }

    foreach (FlowchartLayoutQualityFailure Failure in Picture.Tolerated)
    {
        Console.WriteLine("  TOL " + Failure.Kind + " " + Failure.Description + " :: " + Failure.Justification);
    }

    Console.WriteLine("LIVE failures=" + Live.Failures.Count + " tolerated=" + Live.Tolerated.Count);
    foreach (FlowchartLayoutQualityFailure Failure in Live.Failures)
    {
        Console.WriteLine("  FAIL " + Failure.Kind + " " + Failure.Description);
    }

    foreach (FlowchartLayoutQualityFailure Failure in Live.Tolerated)
    {
        Console.WriteLine("  TOL " + Failure.Kind + " " + Failure.Description + " :: " + Failure.Justification);
    }

    Console.WriteLine("JUMPS " + FlowchartSvgLineJump.LastJumps.Count);
    foreach (FlowchartSvgLineJump.DrawnJump Jump in FlowchartSvgLineJump.LastJumps)
    {
        Console.WriteLine(
            "  JUMP "
            + Jump.HopperSource
            + "->"
            + Jump.HopperTarget
            + " over "
            + Jump.CrossedSource
            + "->"
            + Jump.CrossedTarget
            + " at "
            + Jump.X.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + ","
            + Jump.Y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + " :: "
            + Jump.Reason);
    }
}

static void DumpEdgePaths(string Svg)
{
    foreach (System.Text.RegularExpressions.Match Match in System.Text.RegularExpressions.Regex.Matches(
        Svg,
        "<path[^>]* d=\"([^\"]+)\"[^>]* data-edge-source=\"([^\"]+)\" data-edge-target=\"([^\"]+)\"[^>]*>"))
    {
        Console.WriteLine(Match.Groups[2].Value + "->" + Match.Groups[3].Value + " " + Match.Groups[1].Value);
    }

    foreach (System.Text.RegularExpressions.Match Match in System.Text.RegularExpressions.Regex.Matches(
        Svg,
        "<path[^>]* data-edge-source=\"([^\"]+)\" data-edge-target=\"([^\"]+)\"[^>]* d=\"([^\"]+)\"[^>]*>"))
    {
        Console.WriteLine(Match.Groups[1].Value + "->" + Match.Groups[2].Value + " " + Match.Groups[3].Value);
    }

    foreach (System.Text.RegularExpressions.Match Match in System.Text.RegularExpressions.Regex.Matches(
        Svg,
        "<line[^>]* x1=\"([^\"]+)\" y1=\"([^\"]+)\" x2=\"([^\"]+)\" y2=\"([^\"]+)\"[^>]* data-edge-source=\"([^\"]+)\" data-edge-target=\"([^\"]+)\"[^>]*>"))
    {
        Console.WriteLine(
            Match.Groups[5].Value
            + "->"
            + Match.Groups[6].Value
            + " LINE "
            + Match.Groups[1].Value
            + ","
            + Match.Groups[2].Value
            + " "
            + Match.Groups[3].Value
            + ","
            + Match.Groups[4].Value);
    }
}

static void RenderOne(string Stem, string Mermaid, string OutputDirectory)
{
    try
    {
        if (!(MermaidRecognition.Layout.Enterprise.EnterpriseRenderBridge.TryRenderEnterpriseSvg(Mermaid, out string Svg)
            || NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Mermaid, out Svg)) || string.IsNullOrWhiteSpace(Svg))
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
