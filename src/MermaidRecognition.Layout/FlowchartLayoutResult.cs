using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Result of native Sugiyama layout for a flowchart contract.
/// </summary>
public sealed class FlowchartLayoutResult
{
    /// <summary>Per-invocation counts distinguishing legacy writes from actual checks on cell-bound nodes.</summary>
    internal FlowchartCellWriteCoverage? CellWriteCoverage { get; set; }

    /// <summary>Authoritative closed-mode nodes retained for ownership audits; null for the legacy and open pipelines.</summary>
    internal IReadOnlyList<DiagramGridLayout.GridNode>? CellNodes { get; set; }

    /// <summary>Mutable routing and rendering state owned by this layout.</summary>
    public FlowchartRenderContext RenderContext { get; internal set; } = new();

    private readonly GeometryTrackingList<FlowchartLayoutNodeBounds> _NodeBounds;

    public IReadOnlyList<FlowchartLayoutNodeBounds> NodeBounds => _NodeBounds;

    internal List<FlowchartLayoutNodeBounds> NodeBoundsMutable => _NodeBounds;

    private double _GraphWidth;

    public double GraphWidth { get => _GraphWidth; init => _GraphWidth = value; }

    private double _GraphHeight;

    public double GraphHeight { get => _GraphHeight; init => _GraphHeight = value; }

    /// <summary>Updates the accepted drawing envelope without translating model geometry.</summary>
    internal void SetGraphSize(double Width, double Height)
    {
        _GraphWidth = Width;
        _GraphHeight = Height;
    }

    private readonly GeometryTrackingList<FlowchartLayoutEdgeRoute> _EdgeRoutes;

    /// <summary>Routes resolved by the layout model for renderers to consume.</summary>
    internal List<FlowchartResolvedGuide>? ResolvedEdgeGuides { get; set; }

    /// <summary>
    /// Dagre edge polylines for the diagram.
    /// </summary>
    public IReadOnlyList<FlowchartLayoutEdgeRoute> EdgeRoutes => _EdgeRoutes;

    internal List<FlowchartLayoutEdgeRoute> EdgeRoutesMutable => _EdgeRoutes;

    private readonly GeometryTrackingList<FlowchartLayoutNodeBounds> _ClusterBounds;

    /// <summary>
    /// Subgraph bounds in the same coordinate system as <see cref="NodeBounds"/>.
    /// </summary>
    public IReadOnlyList<FlowchartLayoutNodeBounds> ClusterBounds => _ClusterBounds;

    internal List<FlowchartLayoutNodeBounds> ClusterBoundsMutable => _ClusterBounds;

    /// <summary>
    /// True when <see cref="DiagramGridLayout"/> rebuilt edge routes on the grid.
    /// </summary>
    public bool UsesGridRoutes { get; internal set; }

    /// <summary>Increments whenever node bounds, cluster bounds, or the route inventory changes.</summary>
    internal int GeometryVersion { get; private set; }

    internal bool SuppressGeometryBump { get; set; }

    private ulong _MemoCheckSignature;

    private bool _MemoCheckValid;

    private FlowchartRouteRuleChecker.Finding[]? _MemoCheckFindings;

    private ulong _MemoMeasureSignature;

    private bool _MemoMeasureValid;

    private bool _MemoMeasureArrangement;

    private FlowchartLayoutScore.Result? _MemoMeasureResult;

    /// <summary>
    /// Creates an empty layout result.
    /// </summary>
    public FlowchartLayoutResult()
    {
        _NodeBounds = new GeometryTrackingList<FlowchartLayoutNodeBounds>(this);
        _EdgeRoutes = new GeometryTrackingList<FlowchartLayoutEdgeRoute>(this);
        _ClusterBounds = new GeometryTrackingList<FlowchartLayoutNodeBounds>(this);
    }

    internal void BumpGeometryVersion()
    {
        if (SuppressGeometryBump)
        {
            return;
        }

        GeometryVersion++;
        _MemoCheckValid = false;
        _MemoCheckFindings = null;
        _MemoMeasureValid = false;
        _MemoMeasureResult = null;
    }

    internal bool TryGetMemoizedCheck(ulong Signature, out IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings)
    {
        if (_MemoCheckValid && _MemoCheckSignature == Signature && _MemoCheckFindings != null)
        {
            Findings = _MemoCheckFindings;
            return true;
        }

        Findings = Array.Empty<FlowchartRouteRuleChecker.Finding>();
        return false;
    }

    internal void StoreMemoizedCheck(ulong Signature, IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings)
    {
        _MemoCheckSignature = Signature;
        _MemoCheckValid = true;
        if (Findings is FlowchartRouteRuleChecker.Finding[] Array)
        {
            _MemoCheckFindings = Array;
            return;
        }

        FlowchartRouteRuleChecker.Finding[] Copy = new FlowchartRouteRuleChecker.Finding[Findings.Count];
        for (int Index = 0; Index < Findings.Count; Index++)
        {
            Copy[Index] = Findings[Index];
        }

        _MemoCheckFindings = Copy;
    }

    internal bool TryGetMemoizedMeasure(ulong Signature, bool IncludeArrangementCheck, out FlowchartLayoutScore.Result Result)
    {
        if (_MemoMeasureValid && _MemoMeasureSignature == Signature && _MemoMeasureArrangement == IncludeArrangementCheck && _MemoMeasureResult != null)
        {
            Result = _MemoMeasureResult;
            return true;
        }

        Result = null!;
        return false;
    }

    internal void StoreMemoizedMeasure(ulong Signature, bool IncludeArrangementCheck, FlowchartLayoutScore.Result Result)
    {
        _MemoMeasureSignature = Signature;
        _MemoMeasureValid = true;
        _MemoMeasureArrangement = IncludeArrangementCheck;
        _MemoMeasureResult = Result;
    }

    /// <summary>
    /// Copies the drawn route for <paramref name="Edge"/> when this layout stored one.
    /// </summary>
    /// <param name="Edge">The edge to look up.</param>
    /// <param name="Points">The route points in layout coordinates.</param>
    /// <returns>True when a stored route matches <paramref name="Edge"/>.</returns>
    public bool TryCopyDrawnRoutePoints(EdgeContract Edge, out List<(double X, double Y)> Points)
    {
        Points = [];
        FlowchartLayoutEdgeRoute? Match = null;
        foreach (FlowchartLayoutEdgeRoute Route in _EdgeRoutes)
        {
            if (!string.IsNullOrEmpty(Edge.Id) && string.Equals(Route.EdgeId, Edge.Id, StringComparison.Ordinal))
            {
                Match = Route;
                break;
            }
        }

        if (Match == null)
        {
            foreach (FlowchartLayoutEdgeRoute Route in _EdgeRoutes)
            {
                if (string.Equals(Route.SourceId, Edge.SourceId, StringComparison.Ordinal)
                    && string.Equals(Route.TargetId, Edge.TargetId, StringComparison.Ordinal))
                {
                    Match = Route;
                    break;
                }
            }
        }

        if (Match == null || Match.Points.Count < 2)
        {
            return false;
        }

        foreach (FlowchartLayoutPoint Point in Match.Points)
        {
            Points.Add((Point.X, Point.Y));
        }

        return true;
    }

    internal sealed class GeometryTrackingList<T> : List<T>
    {
        private readonly FlowchartLayoutResult _Owner;

        internal GeometryTrackingList(FlowchartLayoutResult Owner)
        {
            _Owner = Owner;
        }

        public new void Add(T Item)
        {
            base.Add(Item);
            _Owner.BumpGeometryVersion();
        }

        public new void Clear()
        {
            if (Count == 0)
            {
                return;
            }

            base.Clear();
            _Owner.BumpGeometryVersion();
        }

        public new void AddRange(IEnumerable<T> Collection)
        {
            int Before = Count;
            base.AddRange(Collection);
            if (Count != Before)
            {
                _Owner.BumpGeometryVersion();
            }
        }

        public new void Insert(int Index, T Item)
        {
            base.Insert(Index, Item);
            _Owner.BumpGeometryVersion();
        }

        public new void RemoveAt(int Index)
        {
            base.RemoveAt(Index);
            _Owner.BumpGeometryVersion();
        }

        public new bool Remove(T Item)
        {
            bool Removed = base.Remove(Item);
            if (Removed)
            {
                _Owner.BumpGeometryVersion();
            }

            return Removed;
        }
    }
}
