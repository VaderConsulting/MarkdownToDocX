using System;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Parses, validates, and renders native enterprise diagrams to SVG.
/// </summary>
public static class EnterpriseRenderBridge
{
    /// <summary>
    /// Returns true when the source is an enterprise diagram.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <returns>True when the header is <c>enterprise</c>.</returns>
    public static bool IsEnterpriseSource(string? Source)
    {
        return EnterpriseParser.IsEnterpriseSource(Source);
    }

    /// <summary>
    /// Renders an enterprise diagram. Returns false when the source is not enterprise.
    /// Throws when the source is enterprise but parse or validation fails.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <param name="Svg">Rendered SVG when the method returns true.</param>
    /// <returns>True when render succeeded.</returns>
    public static bool TryRenderEnterpriseSvg(string? Source, out string Svg)
        => TryRenderEnterpriseSvg(Source, out Svg, null);

    public static bool TryRenderEnterpriseSvg(string? Source, out string Svg, MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout)
    {
        Svg = string.Empty;
        if (!IsEnterpriseSource(Source))
        {
            return false;
        }

        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source ?? string.Empty);
        if (DefaultLayout.HasValue && !Analysis.Document.Configuration.ContainsKey("layout"))
            Analysis.Document.LayoutMode = DefaultLayout.Value;
        if (Analysis.HasErrors)
        {
            throw new InvalidOperationException(FormatErrors(Analysis));
        }

        Svg = EnterpriseSvgRenderer.Render(Analysis.Document);
        return !string.IsNullOrEmpty(Svg);
    }

    /// <summary>
    /// Renders an enterprise diagram or throws.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <returns>SVG XML.</returns>
    public static string RenderEnterpriseSvg(string Source)
    {
        if (!TryRenderEnterpriseSvg(Source, out string Svg))
        {
            throw new InvalidOperationException("Source is not an enterprise diagram.");
        }

        return Svg;
    }

    private static string FormatErrors(EnterpriseAnalysisResult Analysis)
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
}
