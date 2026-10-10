namespace MermaidRecognition.Layout;

/// <summary>Dispatches supported native diagram adapters without consuming the legacy fallback.</summary>
public static class NativeDiagramRenderBridge
{
    /// <summary>Returns false when a source needs another renderer; malformed Enterprise input reports its diagnostic.</summary>
    public static bool TryRenderSvg(string Source, out string Svg) =>
        Enterprise.EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out Svg)
        || NativePieRenderBridge.TryRenderSvg(Source, out Svg);
}
