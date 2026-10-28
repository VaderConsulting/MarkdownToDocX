using MermaidRecognition.Contract;
using MermaidRecognition.Diagnostics;

namespace MermaidRecognition.Layout;

/// <summary>Dispatches supported native diagram adapters without consuming the legacy fallback.</summary>
public static class NativeDiagramRenderBridge
{
    /// <summary>Returns false when a source needs another renderer; malformed Enterprise input reports its diagnostic.</summary>
    public static bool TryRenderSvg(string Source, out string Svg)
        => TryRenderSvg(Source, out Svg, null);

    public static bool TryRenderSvg(string Source, out string Svg, MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout)
        => TryRenderSvg(Source, out Svg, DefaultLayout, null);

    public static bool TryRenderSvg(string Source, out string Svg,
        MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout, string? DefaultLegend)
    {
        if (Enterprise.EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out Svg, DefaultLayout, DefaultLegend)
            || NativePieRenderBridge.TryRenderSvg(Source, out Svg))
        {
            return true;
        }

        if ((DefaultLayout.HasValue || !string.IsNullOrWhiteSpace(DefaultLegend)
                || System.Text.RegularExpressions.Regex.IsMatch(Source, @"(?im)^\s*legend\s*:"))
            && NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Source, out Svg, DefaultLayout, DefaultLegend))
        {
            return true;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(Source, @"^\s*(?:%%[^\r\n]*[\r\n]\s*)*pie\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            DiagramContract Contract = MermaidRecognition.Core.MermaidRecognizer.Parse(Source);
            foreach (MermaidDiagnostic Diagnostic in Contract.Diagnostics)
            {
                if (Diagnostic.Code == "recognition.pie.unclassified-line")
                {
                    throw new System.InvalidOperationException("Pie diagram contains an unsupported statement: " + Diagnostic.Message);
                }
            }

            int Sections = 0;
            foreach (SourceStatementContract Statement in Contract.SourceStatements)
            {
                if (Statement.LineTypeId == "pie.section")
                {
                    Sections++;
                }
            }

            if (Sections != Contract.Nodes.Count)
            {
                throw new System.InvalidOperationException("Pie diagram contains an invalid section value.");
            }
        }
        return false;
    }
}
