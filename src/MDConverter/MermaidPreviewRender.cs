using System;
using System.Text;
using MDConverter.Pipeline;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace MDConverter;

/// <summary>
/// Renders diagrams for LiveEditor preview with explicit failure reasons.
/// </summary>
public static class MermaidPreviewRender
{
    /// <summary>Rasterises an already accepted drawing with the preview's size budget.</summary>
    public static byte[]? RasterizeAcceptedSvg(string Svg, out string? PreviewAdvisory)
        => DocxWriter.TryRasterizeSvgToPreviewPng(Svg, out PreviewAdvisory);

    /// <summary>
    /// Renders SVG and a preview PNG using the same pipeline as the live preview.
    /// </summary>
    public static bool TryRender(string MermaidSource, out string Svg, out byte[]? Png, out string FailureMessage)
    {
        return TryRender(MermaidSource, out Svg, out Png, out FailureMessage, out _);
    }

    /// <summary>
    /// Renders SVG and a preview PNG using the same pipeline as the live preview.
    /// </summary>
    /// <param name="MermaidSource"></param>
    /// <param name="Svg"></param>
    /// <param name="Png"></param>
    /// <param name="FailureMessage"></param>
    /// <param name="PreviewAdvisory">Non-fatal notice when the preview was scaled to fit.</param>
    public static bool TryRender(string MermaidSource, out string Svg, out byte[]? Png, out string FailureMessage, out string? PreviewAdvisory)
    {
        Svg = string.Empty;
        Png = null;
        FailureMessage = string.Empty;
        PreviewAdvisory = null;
        if (string.IsNullOrWhiteSpace(MermaidSource))
        {
            FailureMessage = "Diagram source is empty.";
            return false;
        }

        try
        {
            if (!DocxWriter.TryRenderDiagramSvg(MermaidSource, out Svg) || string.IsNullOrWhiteSpace(Svg))
            {
                FailureMessage = DescribeSvgFailure(MermaidSource);
                Svg = string.Empty;
                return false;
            }
        }
        catch (Exception Ex)
        {
            FailureMessage = string.IsNullOrWhiteSpace(Ex.Message) ? "Diagram rendering failed." : Ex.Message;
            Svg = string.Empty;
            return false;
        }

        Png = DocxWriter.TryRasterizeSvgToPreviewPng(Svg, out PreviewAdvisory);
        if (Png == null || Png.Length == 0)
        {
            FailureMessage = DescribeRasterFailure(Svg);
            Svg = string.Empty;
            Png = null;
            PreviewAdvisory = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Renders preview SVG and PNG from an already analysed enterprise document (one parse per preview).
    /// </summary>
    public static bool TryRenderAnalyzed(
        EnterpriseAnalysisResult Analysis,
        out string Svg,
        out byte[]? Png,
        out string FailureMessage,
        out string? PreviewAdvisory)
    {
        Svg = string.Empty;
        Png = null;
        FailureMessage = string.Empty;
        PreviewAdvisory = null;
        if (Analysis.HasErrors)
        {
            StringBuilder Builder = new StringBuilder();
            Builder.Append("Enterprise diagram could not be rendered.");
            foreach (EnterpriseDiagnostic Diagnostic in Analysis.Errors)
            {
                Builder.Append(' ');
                Builder.Append(Diagnostic.Message);
            }

            FailureMessage = Builder.ToString();
            return false;
        }

        try
        {
            if (!EnterpriseRenderBridge.TryRenderEnterpriseDocument(Analysis.Document, out Svg) || string.IsNullOrWhiteSpace(Svg))
            {
                FailureMessage = "Enterprise layout did not produce SVG (diagram may be too large or complex for the preview).";
                Svg = string.Empty;
                return false;
            }
        }
        catch (Exception Ex)
        {
            FailureMessage = string.IsNullOrWhiteSpace(Ex.Message) ? "Diagram rendering failed." : Ex.Message;
            Svg = string.Empty;
            return false;
        }

        Png = DocxWriter.TryRasterizeSvgToPreviewPng(Svg, out PreviewAdvisory);
        if (Png == null || Png.Length == 0)
        {
            FailureMessage = DescribeRasterFailure(Svg);
            Svg = string.Empty;
            Png = null;
            PreviewAdvisory = null;
            return false;
        }

        return true;
    }

    private static string DescribeSvgFailure(string MermaidSource)
    {
        if (EnterpriseParser.IsEnterpriseSource(MermaidSource))
        {
            EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(MermaidSource);
            if (Analysis.HasErrors)
            {
                StringBuilder Builder = new StringBuilder();
                Builder.Append("Enterprise diagram could not be rendered.");
                foreach (EnterpriseDiagnostic Diagnostic in Analysis.Errors)
                {
                    Builder.Append(' ');
                    Builder.Append(Diagnostic.Message);
                }

                return Builder.ToString();
            }

            if (!NativeDiagramRenderBridge.TryRenderSvg(MermaidSource, out _))
            {
                return "Enterprise layout did not produce SVG (diagram may be too large or complex for the preview).";
            }
        }

        return "Diagram SVG could not be generated for this source.";
    }

    private static string DescribeRasterFailure(string Svg)
    {
        if (TryReadSvgPixelSize(Svg, out int Width, out int Height))
        {
            long Pixels = (long)Width * (long)Height;
            if (Pixels > 16_000_000)
            {
                return "Import and layout finished, but the preview could not be rasterised at a safe size ("
                    + Width.ToString()
                    + "×"
                    + Height.ToString()
                    + " layout units). Your Mermaid source in the editor is complete. Try fewer resource categories, or export to Word for the full diagram.";
            }

            return $"Preview image could not be created from SVG ({Width.ToString()}×{Height.ToString()} pixels).";
        }

        return "Preview image could not be created from the rendered SVG.";
    }

    private static bool TryReadSvgPixelSize(string Svg, out int Width, out int Height)
    {
        Width = 0;
        Height = 0;
        if (string.IsNullOrWhiteSpace(Svg))
        {
            return false;
        }

        int WidthIndex = Svg.IndexOf("width=\"", StringComparison.OrdinalIgnoreCase);
        int HeightIndex = Svg.IndexOf("height=\"", StringComparison.OrdinalIgnoreCase);
        if (WidthIndex < 0 || HeightIndex < 0)
        {
            return false;
        }

        if (!TryReadSvgLength(Svg, WidthIndex + 7, out double WidthValue) || !TryReadSvgLength(Svg, HeightIndex + 8, out double HeightValue))
        {
            return false;
        }

        Width = (int)Math.Ceiling(Math.Max(1.0, WidthValue));
        Height = (int)Math.Ceiling(Math.Max(1.0, HeightValue));
        return true;
    }

    private static bool TryReadSvgLength(string Svg, int StartIndex, out double Value)
    {
        Value = 0;
        if (StartIndex < 0 || StartIndex >= Svg.Length)
        {
            return false;
        }

        int EndIndex = Svg.IndexOf('"', StartIndex);
        if (EndIndex < 0)
        {
            return false;
        }

        string Token = Svg.Substring(StartIndex, EndIndex - StartIndex).Trim();
        if (Token.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            Token = Token.Substring(0, Token.Length - 2);
        }

        return double.TryParse(Token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Value);
    }
}
