using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Mutable data owned by one flowchart render. The ambient accessor keeps the
/// existing public diagnostic APIs source-compatible while isolating concurrent
/// renders by asynchronous execution context.
/// </summary>
public sealed class FlowchartRenderContext
{
    private static readonly AsyncLocal<FlowchartRenderContext?> Ambient = new();

    /// <summary>Context associated with the current render or direct layout call.</summary>
    public static FlowchartRenderContext Current => Ambient.Value ??= new FlowchartRenderContext();

    internal List<FlowchartLayoutQualityFailure> Justifications { get; } = [];

    internal List<FlowchartSvgLineJump.DrawnJump> Jumps { get; } = [];

    internal DiagramContract? CachedRouteContract { get; set; }

    internal List<FlowchartResolvedGuide>? CachedRouteGuides { get; set; }

    internal List<Action<StringBuilder>> DeferredMarkerDraws { get; } = [];

    internal bool FlushingDeferredMarkers { get; set; }

    internal object? ActiveRenderSession { get; set; }

    internal void BeginRender()
    {
        Justifications.Clear();
        Jumps.Clear();
        CachedRouteContract = null;
        CachedRouteGuides = null;
        DeferredMarkerDraws.Clear();
        FlushingDeferredMarkers = false;
        ActiveRenderSession = null;
    }
}
