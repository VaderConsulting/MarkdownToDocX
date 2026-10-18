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
    /// <summary>AppContext compatibility switch used by hosts that explicitly retain the legacy pixel layout.</summary>
    public const string CellRegionsSwitch = "MermaidRecognition.Layout.UseCellRegions";
    /// <summary>Uses authoritative whole-cell regions for closed diagrams. Defaults to enabled; open mode remains unbound.</summary>
    public bool UseCellRegions { get; init; } = !AppContext.TryGetSwitch(CellRegionsSwitch, out bool Enabled) || Enabled;
    /// <summary>Persistent diagnostics for rejected direct writes, retained through candidate rollback and SVG painting.</summary>
    internal List<FlowchartLayoutQualityFailure> RejectedCellPixelWritesMutable { get; } = [];
    /// <summary>Rejected cell-derived geometry writes, naming the repair path and node without crashing a Release conversion.</summary>
    public IReadOnlyList<FlowchartLayoutQualityFailure> RejectedCellPixelWrites => RejectedCellPixelWritesMutable;
    /// <summary>Current named pipeline stage, restored across nested group reflows.</summary>
    internal string? ActiveLayoutStage { get; set; }
    /// <summary>Optional cell-backed leaf candidates spent across grouped and external placement in this diagram.</summary>
    internal int CellLeafCandidateMoves { get; set; }
    /// <summary>Suppresses recursive cell placement while a complete final route model is being scored as a private candidate.</summary>
    internal bool IsEvaluatingCellRouteCandidate { get; set; }
    private static readonly AsyncLocal<FlowchartRenderContext?> Ambient = new();

    internal sealed class RepairSearchBudget
    {
        internal HashSet<string> AttemptedMoves { get; } = new(StringComparer.Ordinal);
        internal int CandidateMoves { get; set; }
        internal int AcceptedMoves { get; set; }
    }

    internal RepairSearchBudget MoveBeforeDetourBudget { get; } = new();


    /// <summary>Exact automatic route results, scoped to a contract and its complete obstacle geometry.</summary>
    internal Dictionary<(DiagramContract Contract, string Geometry), Dictionary<string, List<(double X, double Y)>>> GridRouteCache { get; } = [];

    /// <summary>Completed side assignments indexed by all obstacles and the incoming port allocation.</summary>
    internal Dictionary<(DiagramContract Contract, string Geometry), string[]> PortSideCache { get; } = [];

    /// <summary>Legal terminal candidate families keyed by complete obstacles, shapes, sides and allocated endpoints.</summary>
    internal Dictionary<(DiagramContract Contract, string Geometry), Dictionary<string, List<List<(double X, double Y)>>>> TerminalCandidateCache { get; } = [];

    internal static Action<string, long>? OperationCountObserver { get; set; }

    internal static void ReportOperationCount(string Name, long Count)
    {
        if (Count > 0) OperationCountObserver?.Invoke(Name, Count);
    }

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

    internal List<string> LayoutStageTrace { get; } = [];

    internal List<FlowchartLayoutQualityFailure> LayoutStageViolations { get; } = [];

    internal List<FlowchartSvgLineJump.DrawnJump> Jumps { get; } = [];

    internal bool IsSvgEdgePainting { get; set; }

    internal DiagramContract? CachedRouteContract { get; set; }

    /// <summary>Exact endpoint and obstacle geometry associated with the cached guide family.</summary>
    internal string? CachedRouteGeometry { get; set; }

    internal List<FlowchartResolvedGuide>? CachedRouteGuides { get; set; }

    internal List<(EdgeContract Edge, IReadOnlyList<(double X, double Y)> Points)>? CachedSvgRenderGuides { get; set; }

    internal FlowchartSvgSpatialIndex? CachedLabelSpatialIndex { get; set; }

    internal List<Action<StringBuilder>> DeferredMarkerDraws { get; } = [];

    internal bool FlushingDeferredMarkers { get; set; }

    internal object? ActiveRenderSession { get; set; }

    internal void BeginRender()
    {
        Jumps.Clear();
        MoveBeforeDetourBudget.AttemptedMoves.Clear();
        MoveBeforeDetourBudget.CandidateMoves = 0;
        MoveBeforeDetourBudget.AcceptedMoves = 0;
        GridRouteCache.Clear();
        PortSideCache.Clear();
        TerminalCandidateCache.Clear();
        IsSvgEdgePainting = false;
        CachedRouteContract = null;
        CachedRouteGeometry = null;
        CachedRouteGuides = null;
        CachedSvgRenderGuides = null;
        CachedLabelSpatialIndex = null;
        DeferredMarkerDraws.Clear();
        FlushingDeferredMarkers = false;
        ActiveRenderSession = null;
    }

    private sealed class AmbientScope(FlowchartRenderContext? Previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = Previous;
    }
}
