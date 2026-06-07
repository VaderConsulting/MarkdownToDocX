using System;
using MermaidRecognition;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Renders flowcharts via MermaidRecognition contract and adopted Sugiyama layout.
/// </summary>
public static class NativeFlowchartRenderBridge
{
    /// <summary>
    /// Parses with <see cref="MermaidRecognizer"/> and renders native Sugiyama SVG for flowcharts.
    /// </summary>
    /// <param name="Source">Mermaid source.</param>
    /// <param name="Svg">Rendered SVG when this method returns true.</param>
    /// <returns>True when native Sugiyama render succeeded.</returns>
    public static bool TryRenderFlowchartSvg(string Source, out string Svg)
    {
        Svg = string.Empty;
        if (string.IsNullOrWhiteSpace(Source))
        {
            return false;
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (!string.Equals(Contract.Kind, "flowchart", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Contract.Kind, "graph", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string Rendered = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);
        if (string.IsNullOrEmpty(Rendered))
        {
            return false;
        }

        Svg = Rendered;
        return true;
    }
}
