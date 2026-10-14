using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Parses, validates, and renders native enterprise diagrams to SVG.
/// </summary>
public static class EnterpriseRenderBridge
{
    internal static Action<string, double>? StageTimingObserver { get; set; }
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
        => TryRenderEnterpriseSvg(Source, out Svg, DefaultLayout, null);

    public static bool TryRenderEnterpriseSvg(string? Source, out string Svg,
        MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout, string? DefaultLegend)
    {
        Svg = string.Empty;
        if (!IsEnterpriseSource(Source))
        {
            return false;
        }

        StageTimingObserver?.Invoke("enterprise-analysis", -1);
        Stopwatch AnalysisTimer = Stopwatch.StartNew();
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source ?? string.Empty);
        AnalysisTimer.Stop();
        StageTimingObserver?.Invoke("enterprise-analysis", AnalysisTimer.Elapsed.TotalMilliseconds);
        if (DefaultLayout.HasValue && !Analysis.Document.Configuration.ContainsKey("layout"))
            Analysis.Document.LayoutMode = DefaultLayout.Value;
        if (!string.IsNullOrWhiteSpace(DefaultLegend) && !Analysis.Document.Configuration.ContainsKey("legend"))
            Analysis.Document.Configuration["legend"] = DefaultLegend;
        if (Analysis.HasErrors)
        {
            throw new InvalidOperationException(FormatErrors(Analysis));
        }

        Svg = EnterpriseNotationLegendRenderer.ApplyToDiagram(
            Analysis.Document, EnterpriseSvgRenderer.Render(Analysis.Document));
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

    public static bool TryGetLegendTableEntries(string? Source, string? DefaultLegend, out IReadOnlyList<string> Entries)
    {
        Entries = Array.Empty<string>();
        if (!IsEnterpriseSource(Source)) return false;
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source ?? string.Empty);
        if (!Analysis.Document.Configuration.ContainsKey("legend") && !string.IsNullOrWhiteSpace(DefaultLegend))
            Analysis.Document.Configuration["legend"] = DefaultLegend;
        if (Analysis.HasErrors || !Analysis.Document.Configuration.TryGetValue("legend", out string? Setting)
            || !string.Equals(Setting?.Trim(), "table", StringComparison.OrdinalIgnoreCase)) return false;
        Entries = EnterpriseNotationLegendRenderer.EntryNames(Analysis.Document);
        return true;
    }

    private static string FormatErrors(EnterpriseAnalysisResult Analysis)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append("Enterprise diagram could not be rendered.");
        foreach (EnterpriseDiagnostic Diagnostic in Analysis.Errors)
        {
            Builder.Append('\n');
            Builder.Append(Diagnostic.Message);
        }

        return Builder.ToString();
    }
}
