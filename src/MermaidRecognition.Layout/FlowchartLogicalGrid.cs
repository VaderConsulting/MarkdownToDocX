using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Fixed configuration for one diagram; labels and port measurements cannot mutate it.</summary>
/// <param name="CellWidth">Configured base cell width.</param>
/// <param name="CellHeight">Configured base cell height.</param>
/// <param name="ColumnGap">Initial gap between columns.</param>
/// <param name="RowGap">Initial gap between rows.</param>
/// <param name="TextPadding">Configured minimum text inset.</param>
internal sealed record FlowchartLogicalGridConfiguration(double CellWidth, double CellHeight,
    double ColumnGap, double RowGap, double TextPadding)
{
    /// <summary>Reads diagram-independent defaults from the authoritative rule parameters.</summary>
    /// <returns>Fixed base dimensions, initial gaps and text padding.</returns>
    internal static FlowchartLogicalGridConfiguration FromRules() => new(
        FlowchartLayoutRules.BaseCellWidth, FlowchartLayoutRules.BaseCellHeight,
        FlowchartLayoutRules.BaseColumnGap, FlowchartLayoutRules.BaseRowGap,
        FlowchartLayoutRules.NodeTextPadding);
}

/// <summary>A contiguous occupied cell rectangle; occupancy never depends on the drawn node centre.</summary>
internal sealed record FlowchartCellRegion
{
    /// <summary>Creates a whole-cell rectangle, rejecting zero, negative or overflowing spans.</summary>
    /// <param name="StartRow">First occupied logical row.</param>
    /// <param name="StartColumn">First occupied logical column.</param>
    /// <param name="RowSpan">Positive number of contiguous rows.</param>
    /// <param name="ColumnSpan">Positive number of contiguous columns.</param>
    internal FlowchartCellRegion(int StartRow, int StartColumn, int RowSpan, int ColumnSpan)
    {
        if (RowSpan <= 0) throw new ArgumentOutOfRangeException(nameof(RowSpan));
        if (ColumnSpan <= 0) throw new ArgumentOutOfRangeException(nameof(ColumnSpan));
        this.StartRow = StartRow; this.StartColumn = StartColumn;
        this.RowSpan = RowSpan; this.ColumnSpan = ColumnSpan;
        EndRow = checked(StartRow + RowSpan); EndColumn = checked(StartColumn + ColumnSpan);
    }
    /// <summary>First occupied row.</summary>
    internal int StartRow { get; }
    /// <summary>First occupied column.</summary>
    internal int StartColumn { get; }
    /// <summary>Number of occupied rows.</summary>
    internal int RowSpan { get; }
    /// <summary>Number of occupied columns.</summary>
    internal int ColumnSpan { get; }
    /// <summary>Exclusive row bound.</summary>
    internal int EndRow { get; }
    /// <summary>Exclusive column bound.</summary>
    internal int EndColumn { get; }
    /// <summary>Tests cell ownership, including reserved empty corners of nonrectangular shapes.</summary>
    /// <param name="Other">Another occupied region.</param>
    /// <returns>Whether at least one cell belongs to both rectangles.</returns>
    internal bool Overlaps(FlowchartCellRegion Other) => StartRow < Other.EndRow && Other.StartRow < EndRow
        && StartColumn < Other.EndColumn && Other.StartColumn < EndColumn;
}

/// <summary>Owns derived occupancy and grown gaps separately from immutable base-cell configuration.</summary>
internal sealed class FlowchartLogicalGrid
{
    private readonly Dictionary<int, double> ColumnGaps = [];
    private readonly Dictionary<int, double> RowGaps = [];
    private readonly Dictionary<string, FlowchartCellRegion> Regions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<FlowchartCellRegion, bool>> Requirements = new(StringComparer.Ordinal);

    /// <summary>Creates an empty grid with finite positive cells and nonnegative gaps and padding.</summary>
    /// <param name="Configuration">Fixed per-diagram parameters, established before measuring labels.</param>
    internal FlowchartLogicalGrid(FlowchartLogicalGridConfiguration Configuration)
    {
        if (!double.IsFinite(Configuration.CellWidth) || Configuration.CellWidth <= 0
            || !double.IsFinite(Configuration.CellHeight) || Configuration.CellHeight <= 0
            || !double.IsFinite(Configuration.ColumnGap) || Configuration.ColumnGap < 0
            || !double.IsFinite(Configuration.RowGap) || Configuration.RowGap < 0
            || !double.IsFinite(Configuration.TextPadding) || Configuration.TextPadding < 0)
            throw new ArgumentOutOfRangeException(nameof(Configuration));
        this.Configuration = Configuration;
    }
    /// <summary>Fixed settings; changing occupancy or gaps cannot change these values.</summary>
    internal FlowchartLogicalGridConfiguration Configuration { get; }

    /// <summary>Places or moves a node atomically when all its reserved cells are free.</summary>
    /// <param name="NodeId">Node whose previous region may be replaced.</param>
    /// <param name="Region">Complete proposed occupied rectangle.</param>
    /// <returns>False without mutation on a cell conflict.</returns>
    internal bool TryPlace(string NodeId, FlowchartCellRegion Region)
    {
        ArgumentException.ThrowIfNullOrEmpty(NodeId);
        ArgumentNullException.ThrowIfNull(Region);
        if (Requirements.TryGetValue(NodeId, out var Requirement) && !Requirement(Region)) return false;
        if (Regions.Any(Pair => Pair.Key != NodeId && Pair.Value.Overlaps(Region))) return false;
        Regions[NodeId] = Region;
        return true;
    }

    /// <summary>Validates a complete repair assignment before changing any occupied cell.</summary>
    /// <param name="Changes">Existing node ids and their proposed complete rectangular regions.</param>
    /// <returns>False without mutation if a node is missing or any final regions share a cell.</returns>
    internal bool TryApply(IReadOnlyDictionary<string, FlowchartCellRegion> Changes)
    {
        ArgumentNullException.ThrowIfNull(Changes);
        var Candidate = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal);
        foreach (var Change in Changes)
        {
            ArgumentNullException.ThrowIfNull(Change.Value);
            if (!Candidate.ContainsKey(Change.Key)) return false;
            if (Requirements.TryGetValue(Change.Key, out var Requirement) && !Requirement(Change.Value)) return false;
            Candidate[Change.Key] = Change.Value;
        }
        var Ordered = Candidate.Values.ToArray();
        for (int First = 0; First < Ordered.Length; First++)
        for (int Second = 0; Second < First; Second++)
            if (Ordered[First].Overlaps(Ordered[Second])) return false;
        // A swap or group translation is legal as one assignment even when applying
        // its members separately would temporarily occupy another member's old cells.
        foreach (var Change in Changes) Regions[Change.Key] = Change.Value;
        return true;
    }

    /// <summary>Registers mandatory renderer geometry without allowing a repair to create an undersized reservation.</summary>
    /// <param name="NodeId">Existing reserved node.</param>
    /// <param name="Requirement">Predicate evaluated against complete proposed regions.</param>
    internal void Require(string NodeId, Func<FlowchartCellRegion, bool> Requirement)
    {
        ArgumentNullException.ThrowIfNull(Requirement);
        if (!Requirement(Regions[NodeId])) throw new ArgumentException("The current reservation cannot contain the node's mandatory geometry.", nameof(Requirement));
        Requirements.Add(NodeId, Requirement);
    }

    /// <summary>Moves a complete node or linked group by integer row and column deltas.</summary>
    /// <param name="NodeIds">Nodes participating in one atomic repair option.</param>
    /// <param name="Rows">Signed whole-row displacement in page-down coordinates.</param>
    /// <param name="Columns">Signed whole-column displacement.</param>
    /// <returns>Whether the complete assignment is free of shared occupancy.</returns>
    internal bool TryMove(IReadOnlyCollection<string> NodeIds, int Rows, int Columns)
    {
        ArgumentNullException.ThrowIfNull(NodeIds);
        var Changes = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        foreach (string Id in NodeIds)
        {
            if (!Regions.TryGetValue(Id, out var Region)) return false;
            Changes[Id] = new(checked(Region.StartRow + Rows), checked(Region.StartColumn + Columns),
                Region.RowSpan, Region.ColumnSpan);
        }
        return TryApply(Changes);
    }

    /// <summary>Changes an existing node's whole-cell spans while keeping its declared start cell.</summary>
    /// <param name="NodeId">Node whose measured capacity or geometry requires a different span.</param>
    /// <param name="Rows">Positive new row span.</param>
    /// <param name="Columns">Positive new column span.</param>
    /// <returns>False without mutation when the proposed reservation occupies another node's cells.</returns>
    internal bool TryResize(string NodeId, int Rows, int Columns)
    {
        if (!Regions.TryGetValue(NodeId, out var Region)) return false;
        return TryApply(new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal)
        {
            [NodeId] = new(Region.StartRow, Region.StartColumn, Rows, Columns)
        });
    }

    /// <summary>Captures cell ownership and grown gaps together for exact candidate rollback.</summary>
    /// <returns>An immutable snapshot tied to this diagram's grid.</returns>
    internal Snapshot Capture() => new(this, Regions, ColumnGaps, RowGaps);

    /// <summary>Restores the complete authoritative state without reconstructing cells from pixel geometry.</summary>
    /// <param name="State">A snapshot previously captured from this grid.</param>
    internal void Restore(Snapshot State)
    {
        ArgumentNullException.ThrowIfNull(State);
        if (!ReferenceEquals(State.Owner, this)) throw new ArgumentException("The snapshot belongs to another grid.", nameof(State));
        Regions.Clear();
        foreach (var Pair in State.Regions) Regions.Add(Pair.Key, Pair.Value);
        ColumnGaps.Clear();
        foreach (var Pair in State.ColumnGaps) ColumnGaps.Add(Pair.Key, Pair.Value);
        RowGaps.Clear();
        foreach (var Pair in State.RowGaps) RowGaps.Add(Pair.Key, Pair.Value);
    }

    /// <summary>Immutable complete repair state, including space inserted between logical cells.</summary>
    internal sealed class Snapshot
    {
        /// <summary>Copies the complete cell state so later repairs cannot modify the saved option.</summary>
        /// <param name="Owner">Grid that may restore this snapshot.</param>
        /// <param name="Regions">Current node cell rectangles.</param>
        /// <param name="ColumnGaps">Current grown column gaps.</param>
        /// <param name="RowGaps">Current grown row gaps.</param>
        internal Snapshot(FlowchartLogicalGrid Owner, IDictionary<string, FlowchartCellRegion> Regions,
            IDictionary<int, double> ColumnGaps, IDictionary<int, double> RowGaps)
        {
            this.Owner = Owner;
            this.Regions = new ReadOnlyDictionary<string, FlowchartCellRegion>(new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal));
            this.ColumnGaps = new ReadOnlyDictionary<int, double>(new Dictionary<int, double>(ColumnGaps));
            this.RowGaps = new ReadOnlyDictionary<int, double>(new Dictionary<int, double>(RowGaps));
        }
        /// <summary>Owning diagram; snapshots cannot be restored into a different grid.</summary>
        internal FlowchartLogicalGrid Owner { get; }
        /// <summary>Exact immutable occupied rectangles.</summary>
        internal IReadOnlyDictionary<string, FlowchartCellRegion> Regions { get; }
        /// <summary>Exact immutable grown column gaps.</summary>
        internal IReadOnlyDictionary<int, double> ColumnGaps { get; }
        /// <summary>Exact immutable grown row gaps.</summary>
        internal IReadOnlyDictionary<int, double> RowGaps { get; }
    }

    /// <summary>Returns a node's declared region, independently of drawing geometry.</summary>
    /// <param name="NodeId">Node owning the cells.</param>
    /// <returns>The exact occupied rectangle.</returns>
    internal FlowchartCellRegion RegionOf(string NodeId) => Regions[NodeId];

    /// <summary>Increases one measured routing gap by whole configured grid steps.</summary>
    /// <param name="Horizontal">True for a gap between columns; false between rows.</param>
    /// <param name="After">Logical row or column preceding the gap.</param>
    /// <param name="Shortfall">Measured additional clearance, never a neighbour count.</param>
    /// <param name="GridStep">Configured drawing step used to round the shortfall upwards.</param>
    internal void GrowGap(bool Horizontal, int After, double Shortfall, double GridStep)
    {
        if (!double.IsFinite(Shortfall) || Shortfall < 0) throw new ArgumentOutOfRangeException(nameof(Shortfall));
        if (!double.IsFinite(GridStep) || GridStep <= 0) throw new ArgumentOutOfRangeException(nameof(GridStep));
        if (Shortfall == 0) return;
        var Gaps = Horizontal ? ColumnGaps : RowGaps;
        double Initial = Horizontal ? Configuration.ColumnGap : Configuration.RowGap;
        double Updated = Gaps.GetValueOrDefault(After, Initial) + Math.Ceiling(Shortfall / GridStep) * GridStep;
        if (!double.IsFinite(Updated)) throw new ArgumentOutOfRangeException(nameof(Shortfall));
        Gaps[After] = Updated;
    }

    /// <summary>Measures the continuous visible rectangle, including every grown internal gap it spans.</summary>
    /// <param name="Region">Occupied whole-cell region.</param>
    /// <returns>Width and height in page pixels, with no external gap included.</returns>
    internal (double Width, double Height) Measure(FlowchartCellRegion Region) => (
        Extent(Region.StartColumn, Region.ColumnSpan, Configuration.CellWidth, Configuration.ColumnGap, ColumnGaps),
        Extent(Region.StartRow, Region.RowSpan, Configuration.CellHeight, Configuration.RowGap, RowGaps));

    /// <summary>Maps a logical region to its page-down origin without inferring cells from centres.</summary>
    /// <param name="Region">Occupied rectangle.</param>
    /// <returns>Top-left page position relative to the grid origin.</returns>
    internal (double X, double Y) Origin(FlowchartCellRegion Region) => (
        Position(Region.StartColumn, Configuration.CellWidth, Configuration.ColumnGap, ColumnGaps),
        Position(Region.StartRow, Configuration.CellHeight, Configuration.RowGap, RowGaps));

    /// <summary>Measures only internal gaps, using sparse corrections so large spans do not require cell enumeration.</summary>
    /// <param name="Start">First occupied index.</param>
    /// <param name="Span">Number of occupied cells.</param>
    /// <param name="Cell">Fixed cell extent.</param>
    /// <param name="Gap">Initial gap.</param>
    /// <param name="Grown">Sparse current gap extents.</param>
    /// <returns>The continuous region extent.</returns>
    private static double Extent(int Start, int Span, double Cell, double Gap, Dictionary<int, double> Grown) =>
        Span * Cell + (Span - 1.0) * Gap
        + Grown.Where(Pair => Pair.Key >= Start && Pair.Key < (long)Start + Span - 1).Sum(Pair => Pair.Value - Gap);

    /// <summary>Accumulates grown gaps between the origin and an index, including negative logical coordinates.</summary>
    /// <param name="Index">Logical row or column.</param>
    /// <param name="Cell">Fixed cell extent.</param>
    /// <param name="Gap">Initial gap.</param>
    /// <param name="Grown">Sparse current gap extents.</param>
    /// <returns>The physical start coordinate.</returns>
    private static double Position(int Index, double Cell, double Gap, Dictionary<int, double> Grown) =>
        Index * (Cell + Gap) + (Index >= 0
            ? Grown.Where(Pair => Pair.Key >= 0 && Pair.Key < Index).Sum(Pair => Pair.Value - Gap)
            : -Grown.Where(Pair => Pair.Key >= Index && Pair.Key < 0).Sum(Pair => Pair.Value - Gap));
}
