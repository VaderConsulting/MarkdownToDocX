using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Mutable state owned by one flowchart layout and render. The ambient accessor
/// is scoped to that context while legacy renderer helpers are migrated.
/// </summary>
public sealed class FlowchartRenderContext
{
    private static readonly AsyncLocal<FlowchartRenderContext?> Ambient = new();

    /// <summary>Context associated with the current render or direct layout call.</summary>
    public static FlowchartRenderContext Current => Ambient.Value ??= new FlowchartRenderContext();

    /// <summary>Temporarily activates this state for legacy helpers during a render.</summary>
    internal static IDisposable Activate(FlowchartRenderContext Context)
    {
        FlowchartRenderContext? Previous = Ambient.Value;
        Ambient.Value = Context;
        return new AmbientScope(Previous);
    }

    internal List<FlowchartLayoutQualityFailure> Justifications { get; } = [];

    internal List<FlowchartSvgLineJump.DrawnJump> Jumps { get; } = [];

    internal DiagramContract? CachedRouteContract { get; set; }

    internal List<FlowchartResolvedGuide>? CachedRouteGuides { get; set; }

    internal List<Action<StringBuilder>> DeferredMarkerDraws { get; } = [];

    internal bool FlushingDeferredMarkers { get; set; }

    internal object? ActiveRenderSession { get; set; }

    internal void BeginRender()
    {
        Jumps.Clear();
        CachedRouteContract = null;
        CachedRouteGuides = null;
        DeferredMarkerDraws.Clear();
        FlushingDeferredMarkers = false;
        ActiveRenderSession = null;
    }

    private sealed class AmbientScope(FlowchartRenderContext? Previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = Previous;
    }
}
