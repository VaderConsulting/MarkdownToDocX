using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;

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
        => TryRenderFlowchartSvg(Source, out Svg, null);

    public static bool TryRenderFlowchartSvg(string Source, out string Svg, MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout)
        => TryRenderFlowchartSvg(Source, out Svg, DefaultLayout, null);

    public static bool TryRenderFlowchartSvg(string Source, out string Svg,
        MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout, string? DefaultLegend)
    {
        Svg = string.Empty;
        if (string.IsNullOrWhiteSpace(Source))
        {
            return false;
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (DefaultLayout.HasValue && !Contract.Configuration.RawDirectiveValues.ContainsKey("layout"))
            Contract.LayoutMode = DefaultLayout.Value;
        if (!string.IsNullOrWhiteSpace(DefaultLegend) && !Contract.Configuration.RawDirectiveValues.ContainsKey("legend"))
            Contract.Configuration.RawDirectiveValuesMutable["legend"] = DefaultLegend;
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

        Svg = FlowchartLegendRenderer.Apply(Contract, Rendered);
        return true;
    }

    public static bool TryGetLegendTableEntries(string Source, string? DefaultLegend, out IReadOnlyList<string> Entries)
    {
        Entries = Array.Empty<string>();
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (!string.IsNullOrWhiteSpace(DefaultLegend) && !Contract.Configuration.RawDirectiveValues.ContainsKey("legend"))
            Contract.Configuration.RawDirectiveValuesMutable["legend"] = DefaultLegend;
        if (!Contract.Configuration.RawDirectiveValues.TryGetValue("legend", out string? Setting)
            || !string.Equals(Setting?.Trim(), "table", StringComparison.OrdinalIgnoreCase)) return false;
        Entries = FlowchartLegendRenderer.EntryNames(Contract);
        return Contract.Kind is "flowchart" or "graph";
    }
}
