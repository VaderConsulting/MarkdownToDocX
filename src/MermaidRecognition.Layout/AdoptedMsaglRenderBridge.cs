using System;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout;

/// <summary>
/// Trial render path that draws flowchart and enterprise contracts from adopted MSAGL routes.
/// </summary>
public static class AdoptedMsaglRenderBridge
{
    /// <summary>
    /// Parses Mermaid and renders with the adopted router in <paramref name="Profile"/>.
    /// </summary>
    /// <param name="Source">Mermaid source.</param>
    /// <param name="Profile">Router options. Flowchart and enterprise share the same profiles.</param>
    /// <param name="Svg">Rendered SVG when this method returns true.</param>
    /// <returns>True when layout and routing produced SVG.</returns>
    public static bool TryRenderSvg(string Source, AdoptedMsaglRoutingProfile Profile, out string Svg)
    {
        Svg = string.Empty;
        if (string.IsNullOrWhiteSpace(Source) || Profile == null)
        {
            return false;
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (!string.Equals(Contract.Kind, "flowchart", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Contract.Kind, "graph", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string Rendered = AdoptedMsaglSvgRenderer.RenderToSvg(Contract, Profile);
        if (string.IsNullOrEmpty(Rendered))
        {
            return false;
        }

        Svg = Rendered;
        return true;
    }
}
