using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidSharp;
using SkiaSharp;
using Svg.Skia;

namespace MermaidPngProbe;

internal static class Program
{
    private const float RASTER_SCALE = 4.0f;

    private static int Main(string[] Args)
    {
        if (Args.Length < 2)
        {
            Console.Error.WriteLine("Usage: MermaidPngProbe <markdownPath> <sectionId> [outputDirectory]");
            Console.Error.WriteLine("Example: MermaidPngProbe ..\\..\\flow.md F01 ..\\..\\out\\f01-png");
            return 2;
        }

        string MarkdownPath = Path.GetFullPath(Args[0]);
        string SectionId = Args[1].Trim();
        string OutputDirectory = Args.Length >= 3
            ? Path.GetFullPath(Args[2])
            : Path.Combine(Path.GetDirectoryName(MarkdownPath) ?? ".", SectionId.ToLowerInvariant() + "-png");

        if (!File.Exists(MarkdownPath))
        {
            Console.Error.WriteLine("Markdown file not found: " + MarkdownPath);
            return 2;
        }

        string Markdown = File.ReadAllText(MarkdownPath);
        List<MermaidSourceBlock> Blocks = ExtractSectionMermaidBlocks(Markdown, SectionId);
        if (Blocks.Count == 0)
        {
            Console.Error.WriteLine("No Mermaid blocks found in section: " + SectionId);
            return 2;
        }

        Directory.CreateDirectory(OutputDirectory);

        MethodInfo? ExpandMethod = typeof(MDConverter.Pipeline.DocxWriter).GetMethod(
            "ExpandSvgFlowchartMarkersForRasterization",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? RasterizeMethod = typeof(MDConverter.Pipeline.DocxWriter).GetMethod(
            "TryRasterizeSvgToPng",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (ExpandMethod == null || RasterizeMethod == null)
        {
            Console.Error.WriteLine("Could not resolve DocxWriter raster helpers via reflection.");
            return 3;
        }

        StringBuilder Report = new StringBuilder();
        Report.AppendLine(CultureInfo.InvariantCulture, $"# PNG probe — section {SectionId}");
        Report.AppendLine();
        Report.AppendLine("| Block | Diagram | Links under nodes (raw SVG) | Links raised (expanded) | Raw PNG | Pipeline PNG |");
        Report.AppendLine("| --- | --- | --- | --- | --- | --- |");

        for (int Index = 0; Index < Blocks.Count; Index++)
        {
            MermaidSourceBlock Block = Blocks[Index];
            string Prefix = string.Create(CultureInfo.InvariantCulture, $"{Index:D2}");
            string Svg = Mermaid.Render(Block.Source, RenderOptions.Default);
            string RawSvgPath = Path.Combine(OutputDirectory, Prefix + ".naiad.svg");
            File.WriteAllText(RawSvgPath, Svg, new UTF8Encoding(false));

            string ExpandedSvg = (string)ExpandMethod.Invoke(null, new object[] { Svg })!;
            string ExpandedSvgPath = Path.Combine(OutputDirectory, Prefix + ".expanded.svg");
            File.WriteAllText(ExpandedSvgPath, ExpandedSvg, new UTF8Encoding(false));

            PaintOrderAnalysis RawAnalysis = AnalyzeFlowchartPaintOrder(Svg);
            PaintOrderAnalysis ExpandedAnalysis = AnalyzeFlowchartPaintOrder(ExpandedSvg);

            string RawPngPath = Path.Combine(OutputDirectory, Prefix + ".raw.png");
            string PipelinePngPath = Path.Combine(OutputDirectory, Prefix + ".pipeline.png");

            bool RawPngOk = TryRasterizeSvgFile(Svg, RawPngPath, out string? RawError);
            byte[]? PipelinePngBytes = (byte[]?)RasterizeMethod.Invoke(null, new object[] { ExpandedSvg });
            bool PipelinePngOk = PipelinePngBytes != null && PipelinePngBytes.Length > 0;
            if (PipelinePngOk && PipelinePngBytes != null)
            {
                File.WriteAllBytes(PipelinePngPath, PipelinePngBytes);
            }

            Report.AppendLine(CultureInfo.InvariantCulture,
                $"| {Prefix} | {Block.DiagramType} | {FormatPaintIssue(RawAnalysis)} | {FormatPaintIssue(ExpandedAnalysis)} | {(RawPngOk ? "ok" : RawError ?? "fail")} | {(PipelinePngOk ? "ok" : "fail")} |");

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{Prefix} {Block.DiagramType}: raw={FormatPaintIssue(RawAnalysis)} expanded={FormatPaintIssue(ExpandedAnalysis)}"));
        }

        Report.AppendLine();
        Report.AppendLine("## Notes");
        Report.AppendLine();
        Report.AppendLine("- **Raw SVG** is Naiad output (edges before node fills).");
        Report.AppendLine("- **Expanded SVG** applies `ExpandSvgFlowchartMarkersForRasterization` (inline arrowheads, target attach, z-order raise).");
        Report.AppendLine("- **Pipeline PNG** uses the same raster path as DocxWriter (`TryRasterizeSvgToPng` on expanded SVG).");
        Report.AppendLine("- **Raw PNG** rasterises Naiad SVG without marker expansion (Skia does not draw `marker-end` reliably).");
        Report.AppendLine();
        Report.AppendLine("Flowchart repositioning is only required when diagrams use `flowchart-link` + `pointEnd` markers and are rasterised to PNG.");

        string ReportPath = Path.Combine(OutputDirectory, "report.md");
        File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
        Console.WriteLine("Wrote: " + OutputDirectory);
        Console.WriteLine("Report: " + ReportPath);
        return 0;
    }

    private static string FormatPaintIssue(PaintOrderAnalysis Analysis)
    {
        if (Analysis.FlowchartLinkCount == 0)
        {
            return "n/a (no flowchart links)";
        }

        if (!Analysis.HasLinksBeforeLastNodeGeometry)
        {
            return "ok";
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"issue ({Analysis.FlowchartLinkCount} link(s) before node geometry)");
    }

    private static List<MermaidSourceBlock> ExtractSectionMermaidBlocks(string Markdown, string SectionId)
    {
        string Normalised = Markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        string SectionPattern = @"(?ms)^###\s*" + Regex.Escape(SectionId) + @"\b[^\n]*\n(.*?)(?=^###\s|\z)";
        Match SectionMatch = Regex.Match(Normalised, SectionPattern);
        if (!SectionMatch.Success)
        {
            return new List<MermaidSourceBlock>();
        }

        string SectionBody = SectionMatch.Groups[1].Value;
        Regex BlockRegex = new Regex(@"(?s)```mermaid\s*\n(?<Body>.*?)\n```", RegexOptions.CultureInvariant);
        MatchCollection Matches = BlockRegex.Matches(SectionBody);
        List<MermaidSourceBlock> Blocks = new List<MermaidSourceBlock>();
        for (int Index = 0; Index < Matches.Count; Index++)
        {
            string Source = Matches[Index].Groups["Body"].Value.Trim();
            Blocks.Add(new MermaidSourceBlock(Source, DetectDiagramType(Source)));
        }

        return Blocks;
    }

    private static string DetectDiagramType(string Source)
    {
        string FirstLine = Source.Split('\n')[0].Trim();
        int SpaceIndex = FirstLine.IndexOf(' ');
        if (SpaceIndex > 0)
        {
            return FirstLine.Substring(0, SpaceIndex);
        }

        return FirstLine;
    }

    private static PaintOrderAnalysis AnalyzeFlowchartPaintOrder(string Svg)
    {
        PaintOrderAnalysis Analysis = new PaintOrderAnalysis();
        try
        {
            XDocument Document = XDocument.Parse(Svg);
            XElement? Root = Document.Root;
            if (Root == null)
            {
                return Analysis;
            }

            int LastNodeShapeIndex = -1;
            List<int> LinkIndices = new List<int>();
            int ChildIndex = 0;
            foreach (XElement Child in Root.Elements())
            {
                string? ClassValue = Child.Attribute("class")?.Value ?? string.Empty;
                if (ClassValue.Contains("node", StringComparison.OrdinalIgnoreCase)
                    && !ClassValue.Contains("nodeLabel", StringComparison.OrdinalIgnoreCase))
                {
                    LastNodeShapeIndex = ChildIndex;
                }

                if (ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase))
                {
                    LinkIndices.Add(ChildIndex);
                    Analysis.FlowchartLinkCount++;
                }

                ChildIndex++;
            }

            if (LastNodeShapeIndex < 0 || LinkIndices.Count == 0)
            {
                return Analysis;
            }

            for (int Index = 0; Index < LinkIndices.Count; Index++)
            {
                if (LinkIndices[Index] < LastNodeShapeIndex)
                {
                    Analysis.HasLinksBeforeLastNodeGeometry = true;
                    break;
                }
            }
        }
        catch
        {
        }

        return Analysis;
    }

    private static bool IsNodeGeometryElement(XElement Element)
    {
        string LocalName = Element.Name.LocalName;
        if (!string.Equals(LocalName, "path", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "line", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "rect", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "circle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "ellipse", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LocalName, "polygon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? ClassValue = Element.Attribute("class")?.Value ?? string.Empty;
        if (ClassValue.Contains("flowchart-link", StringComparison.OrdinalIgnoreCase)
            || ClassValue.Contains("nodeLabel", StringComparison.OrdinalIgnoreCase)
            || ClassValue.Contains("marker", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool TryRasterizeSvgFile(string Svg, string OutputPngPath, out string? Error)
    {
        Error = null;
        try
        {
            SKSvg SvgDocument = new SKSvg();
            SvgDocument.FromSvg(Svg);
            SKPicture? Picture = SvgDocument.Picture;
            if (Picture == null)
            {
                Error = "no picture";
                return false;
            }

            SKRect DrawBounds = Picture.CullRect;
            if (DrawBounds.Width <= 0 || DrawBounds.Height <= 0)
            {
                DrawBounds.Inflate(16, 16);
            }

            int Width = (int)Math.Ceiling(Math.Max(1.0, DrawBounds.Width * RASTER_SCALE));
            int Height = (int)Math.Ceiling(Math.Max(1.0, DrawBounds.Height * RASTER_SCALE));
            using SKBitmap Bitmap = new SKBitmap(Width, Height);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);
            Canvas.Scale(RASTER_SCALE);
            Canvas.Translate(-DrawBounds.Left, -DrawBounds.Top);
            Canvas.DrawPicture(Picture);
            Canvas.Flush();

            using SKImage Image = SKImage.FromBitmap(Bitmap);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPngPath) ?? ".");
            File.WriteAllBytes(OutputPngPath, Data.ToArray());
            return true;
        }
        catch (Exception Ex)
        {
            Error = Ex.Message;
            return false;
        }
    }

    private sealed class MermaidSourceBlock
    {
        public MermaidSourceBlock(string Source, string DiagramType)
        {
            this.Source = Source;
            this.DiagramType = DiagramType;
        }

        public string Source { get; }
        public string DiagramType { get; }
    }

    private sealed class PaintOrderAnalysis
    {
        public int FlowchartLinkCount { get; set; }
        public bool HasLinksBeforeLastNodeGeometry { get; set; }
    }
}
