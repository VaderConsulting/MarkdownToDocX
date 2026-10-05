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

if (string.Equals(Command, "trial-msagl", StringComparison.OrdinalIgnoreCase))
{
    string OutputDirectory = args.Length > 1
        ? args[1]
        : Path.Combine(RepoRoot, "docs", "flowchart", "msagl-trial");
    RenderAdoptedMsaglTrial(RepoRoot, OutputDirectory);
    return;
}

Console.WriteLine("Usage: FlowchartLayoutFixRender rasterize <dir> | render-flow <dir> | render-mmd <in> <out> | refresh-after [dir] | dump-layout <mmd> | trial-msagl [dir]");

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

static void RenderAdoptedMsaglTrial(string RepoRoot, string OutputDirectory)
{
    Dictionary<string, string> Diagrams = LoadTrialDiagrams(RepoRoot);
    string CustomDirectory = Path.Combine(OutputDirectory, "custom");
    string RectilinearDirectory = Path.Combine(OutputDirectory, "msagl-rectilinear");
    string SplineDirectory = Path.Combine(OutputDirectory, "msagl-spline");
    Directory.CreateDirectory(CustomDirectory);
    Directory.CreateDirectory(RectilinearDirectory);
    Directory.CreateDirectory(SplineDirectory);

    foreach (KeyValuePair<string, string> Diagram in Diagrams.OrderBy(Pair => Pair.Key, StringComparer.Ordinal))
    {
        RenderOne(Diagram.Key, Diagram.Value, CustomDirectory);
        RenderAdoptedOne(Diagram.Key, Diagram.Value, RectilinearDirectory, AdoptedMsaglRoutingProfile.SharedRectilinear);
        RenderAdoptedOne(Diagram.Key, Diagram.Value, SplineDirectory, AdoptedMsaglRoutingProfile.SharedSpline);
    }

    WriteTrialGateCounts(OutputDirectory, Diagrams);
}

static Dictionary<string, string> LoadTrialDiagrams(string RepoRoot)
{
    Dictionary<string, string> Diagrams = new Dictionary<string, string>(StringComparer.Ordinal);
    string[] CatalogueIds =
    [
        "F36", "F37", "F38", "F39", "F40", "F41", "F42", "F43", "F44", "F45", "F46", "F47",
        "F59", "F60", "F73", "F74", "F78", "F84", "F126", "F127", "F128", "F129", "F132"
    ];
    string FlowMarkdown = File.ReadAllText(Path.Combine(RepoRoot, "flow.md"));
    foreach (string FixtureId in CatalogueIds)
    {
        Match HeadingMatch = Regex.Match(
            FlowMarkdown,
            @"^### " + Regex.Escape(FixtureId) + @"\b[^\n]*\n+(?:```mermaid\r?\n)(.*?)```",
            RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Multiline);
        if (HeadingMatch.Success)
        {
            Diagrams[FixtureId] = HeadingMatch.Groups[1].Value.Trim();
        }
    }

    string SamplesDirectory = Path.Combine(RepoRoot, "tests", "MermaidRecognition.Layout.Tests", "EnterpriseFlowchartSamples");
    if (Directory.Exists(SamplesDirectory))
    {
        foreach (string MermaidPath in Directory.GetFiles(SamplesDirectory, "*-flowchart.mmd").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string Number = Path.GetFileNameWithoutExtension(MermaidPath).Replace("-flowchart", string.Empty, StringComparison.Ordinal);
            string Source = File.ReadAllText(MermaidPath);
            Diagrams["enterprise-" + Number + "-flowchart"] = Source;
            if (string.Equals(Number, "03", StringComparison.Ordinal))
            {
                Diagrams["enterprise-03-enterprise"] = Source;
            }
        }
    }

    return Diagrams;
}

static void RenderAdoptedOne(string Stem, string Mermaid, string OutputDirectory, AdoptedMsaglRoutingProfile Profile)
{
    try
    {
        if (!AdoptedMsaglRenderBridge.TryRenderSvg(Mermaid, Profile, out string Svg) || string.IsNullOrWhiteSpace(Svg))
        {
            Console.WriteLine("SKIP " + Profile.Style + " " + Stem);
            return;
        }

        File.WriteAllText(Path.Combine(OutputDirectory, Stem + ".svg"), Svg);
        byte[]? Png = MermaidDiagramExport.TryRasterizeSvg(Svg);
        if (Png is not null && Png.Length > 0)
        {
            File.WriteAllBytes(Path.Combine(OutputDirectory, Stem + ".png"), Png);
            Console.WriteLine("OK " + Profile.Style + " " + Stem);
        }
        else
        {
            Console.WriteLine("SVG " + Profile.Style + " " + Stem);
        }
    }
    catch (Exception Exception)
    {
        Console.WriteLine("FAIL " + Profile.Style + " " + Stem + " " + Exception.Message);
    }
}

static void WriteTrialGateCounts(string OutputDirectory, Dictionary<string, string> Diagrams)
{
    string[] Engines = ["custom", "msagl-rectilinear", "msagl-spline"];
    System.Text.StringBuilder Builder = new System.Text.StringBuilder();
    Builder.AppendLine("# Adopted MSAGL routing trial — gate counts");
    Builder.AppendLine();
    Builder.AppendLine("Counts come from `EvaluatePicture` on the stored SVG. Justifications are not applied.");
    Builder.AppendLine();
    foreach (string Engine in Engines)
    {
        string EngineDirectory = Path.Combine(OutputDirectory, Engine);
        int TotalFailures = 0;
        int DiagramsCounted = 0;
        Builder.AppendLine("## " + Engine);
        Builder.AppendLine();
        foreach (KeyValuePair<string, string> Diagram in Diagrams.OrderBy(Pair => Pair.Key, StringComparer.Ordinal))
        {
            string SvgPath = Path.Combine(EngineDirectory, Diagram.Key + ".svg");
            if (!File.Exists(SvgPath))
            {
                Builder.AppendLine("- " + Diagram.Key + ": missing SVG");
                continue;
            }

            string Svg = File.ReadAllText(SvgPath);
            FlowchartRenderedSvgQualityReport Report = FlowchartRenderedSvgQualityGate.EvaluatePicture(
                Engine + "/" + Diagram.Key,
                Diagram.Value,
                Svg);
            TotalFailures += Report.Failures.Count;
            DiagramsCounted++;
            Builder.Append("- ");
            Builder.Append(Diagram.Key);
            Builder.Append(": ");
            Builder.Append(Report.Failures.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Builder.AppendLine(" failures");
        }

        Builder.AppendLine();
        Builder.Append("Total: ");
        Builder.Append(TotalFailures.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Builder.Append(" failures across ");
        Builder.Append(DiagramsCounted.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Builder.AppendLine(" diagrams.");
        Builder.AppendLine();
    }

    File.WriteAllText(Path.Combine(OutputDirectory, "gate-counts.md"), Builder.ToString());
    Console.WriteLine(Builder.ToString());
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
