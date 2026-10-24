using System;
using MDConverter.Pipeline;
using MermaidSharp;

namespace MDConverter;

/// <summary>
/// Renders Mermaid source to SVG or PNG using the same pipeline as DOCX embedding.
/// </summary>
public static class MermaidDiagramExport
{
    /// <summary>
    /// Renders Mermaid diagram source to an SVG document string.
    /// </summary>
    public static string RenderSvg(string MermaidSource)
    {
        return RenderSvg(MermaidSource, null);
    }

    /// <summary>
    /// Renders Mermaid diagram source to an SVG document string with optional progress reporting.
    /// </summary>
    public static string RenderSvg(string MermaidSource, IConversionProgressReporter? ProgressReporter)
    {
        if (string.IsNullOrWhiteSpace(MermaidSource))
        {
            throw new ArgumentException("Mermaid source is required.", nameof(MermaidSource));
        }

        ProgressReporter?.Report(25, "Rendering diagram SVG");
        string Svg = Mermaid.Render(MermaidSource, RenderOptions.Default);
        ProgressReporter?.Report(100, "Diagram SVG complete");
        return Svg;
    }

    /// <summary>
    /// Rasterises an SVG document string to PNG bytes using the DOCX diagram pipeline.
    /// </summary>
    public static byte[]? TryRasterizeSvg(string Svg) => DocxWriter.TryRasterizeSvgToPng(Svg);

    /// <summary>
    /// Renders the SVG that the preview raster is drawn from.
    /// </summary>
    /// <param name="MermaidSource">Diagram source.</param>
    /// <param name="Svg">Rendered SVG when the method returns true.</param>
    /// <returns>True when an SVG document was produced.</returns>
    public static bool TryRenderPreviewSvg(string MermaidSource, out string Svg)
    {
        if (string.IsNullOrWhiteSpace(MermaidSource))
        {
            Svg = string.Empty;
            return false;
        }

        return DocxWriter.TryRenderDiagramSvg(MermaidSource, out Svg);
    }

    /// <summary>
    /// Renders Mermaid diagram source to PNG bytes (flowchart markers inlined for Skia).
    /// </summary>
    public static byte[]? TryRenderPng(string MermaidSource)
    {
        return TryRenderPng(MermaidSource, null);
    }

    /// <summary>
    /// Renders Mermaid diagram source to PNG bytes with optional progress reporting.
    /// </summary>
    public static byte[]? TryRenderPng(string MermaidSource, IConversionProgressReporter? ProgressReporter)
    {
        ProgressReporter?.Report(10, "Rendering diagram");
        byte[]? PngBytes = DocxWriter.TryRasterizeMermaidSourceToPng(MermaidSource);
        if (PngBytes != null && PngBytes.Length > 0)
        {
            ProgressReporter?.Report(100, "Diagram PNG complete");
        }

        return PngBytes;
    }
}
