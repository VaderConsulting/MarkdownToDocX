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
    /// <summary>One immutable ownership-and-gap state; publishing or restoring a repair changes this reference once.</summary>
    private Snapshot Current;
    /// <summary>Read-only current column gaps, never mutated independently of ownership.</summary>
    private IReadOnlyDictionary<int, double> ColumnGaps => Current.ColumnGaps;
    /// <summary>Read-only current row gaps, never mutated independently of ownership.</summary>
    private IReadOnlyDictionary<int, double> RowGaps => Current.RowGaps;
    /// <summary>Read-only current node regions, never updated member by member.</summary>
    private IReadOnlyDictionary<string, FlowchartCellRegion> Regions => Current.Regions;
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
        Current = new(this, new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal),
            new Dictionary<int, double>(), new Dictionary<int, double>());
    }
    /// <summary>Fixed settings; changing occupancy or gaps cannot change these values.</summary>
    internal FlowchartLogicalGridConfiguration Configuration { get; }

    /// <summary>Optional semantic convention checked on every complete assignment, including hints and later repairs.</summary>
    internal Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool>? RegionConvention { get; set; }
    /// <summary>Counts proven convention violations before any ownership publication or candidate routing.</summary>
    internal int RegionConventionRejections { get; private set; }

    /// <summary>Checks a complete proposal against immutable semantic membership and records a rejected assignment.</summary>
    /// <param name="Candidate">Complete proposed cell ownership.</param>
    /// <returns>False only for a proven violation of the diagram's semantic convention.</returns>
    internal bool CheckRegionConvention(IReadOnlyDictionary<string, FlowchartCellRegion> Candidate)
    {
        if (RegionConvention?.Invoke(Candidate) != false) return true;
        RegionConventionRejections++;
        return false;
    }

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
        var Candidate = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal) { [NodeId] = Region };
        if (!CheckRegionConvention(Candidate)) return false;
        Current = new(this, Candidate, ColumnGaps, RowGaps);
        return true;
    }

    /// <summary>Validates a complete repair assignment before changing any occupied cell.</summary>
    /// <param name="Changes">Existing node ids and their proposed complete rectangular regions.</param>
    /// <returns>False without mutation if a node is missing or any final regions share a cell.</returns>
    internal bool TryApply(IReadOnlyDictionary<string, FlowchartCellRegion> Changes)
    {
        var Before = Capture();
        return TryPrepare(Changes, [], out var After) && TryCommit(Before, After!);
    }

    /// <summary>Builds and validates the complete target ownership and gaps without touching live geometry.</summary>
    /// <param name="Changes">Complete target rectangles for every moved, swapped or displaced node.</param>
    /// <param name="GapGrowth">All measured gap increases belonging to the same candidate.</param>
    /// <param name="After">Receives the immutable complete candidate only when every check succeeds.</param>
    /// <returns>False with no live mutation for missing nodes, shared cells or mandatory-geometry failures.</returns>
    internal bool TryPrepare(IReadOnlyDictionary<string, FlowchartCellRegion> Changes,
        IReadOnlyList<FlowchartCellGapGrowth> GapGrowth, out Snapshot? After)
    {
        ArgumentNullException.ThrowIfNull(Changes);
        ArgumentNullException.ThrowIfNull(GapGrowth);
        After = null;
        var Candidate = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal);
        foreach (var Change in Changes)
        {
            ArgumentNullException.ThrowIfNull(Change.Value);
            if (!Candidate.ContainsKey(Change.Key)) return false;
            if (Requirements.TryGetValue(Change.Key, out var Requirement) && !Requirement(Change.Value)) return false;
            Candidate[Change.Key] = Change.Value;
        }
        var Ordered = Candidate.Values.ToArray();
        if (!CheckRegionConvention(Candidate)) return false;
        for (int First = 0; First < Ordered.Length; First++)
        for (int Second = 0; Second < First; Second++)
            if (Ordered[First].Overlaps(Ordered[Second])) return false;
        var CandidateColumns = new Dictionary<int, double>(ColumnGaps);
        var CandidateRows = new Dictionary<int, double>(RowGaps);
        foreach (var Growth in GapGrowth)
        {
            if (!double.IsFinite(Growth.Shortfall) || Growth.Shortfall < 0)
                throw new ArgumentOutOfRangeException(nameof(GapGrowth));
            if (!double.IsFinite(Growth.GridStep) || Growth.GridStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(GapGrowth));
            if (Growth.Shortfall == 0) continue;
            var Gaps = Growth.Horizontal ? CandidateColumns : CandidateRows;
            double Initial = Growth.Horizontal ? Configuration.ColumnGap : Configuration.RowGap;
            double Updated = Gaps.GetValueOrDefault(Growth.After, Initial)
                + Math.Ceiling(Growth.Shortfall / Growth.GridStep) * Growth.GridStep;
            if (!double.IsFinite(Updated)) throw new ArgumentOutOfRangeException(nameof(GapGrowth));
            Gaps[Growth.After] = Updated;
        }
        After = Changes.All(Change => Regions[Change.Key] == Change.Value) && GapGrowth.All(Growth => Growth.Shortfall == 0)
            ? Current : new(this, Candidate, CandidateColumns, CandidateRows);
        return true;
    }

    /// <summary>Explains why <see cref="TryPrepare"/> would reject a proposal without mutating live geometry.</summary>
    /// <param name="Changes">Proposed node regions.</param>
    /// <returns>A short machine-readable reason, or null when preparation would succeed.</returns>
    internal string? DescribePrepareFailure(IReadOnlyDictionary<string, FlowchartCellRegion> Changes)
    {
        ArgumentNullException.ThrowIfNull(Changes);
        var Candidate = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal);
        foreach (KeyValuePair<string, FlowchartCellRegion> Change in Changes)
        {
            if (Change.Value == null)
            {
                return "null-region";
            }

            if (!Candidate.ContainsKey(Change.Key))
            {
                return "unknown-node:" + Change.Key;
            }

            if (Requirements.TryGetValue(Change.Key, out Func<FlowchartCellRegion, bool>? Requirement)
                && !Requirement(Change.Value))
            {
                return "mandatory-geometry:" + Change.Key;
            }

            Candidate[Change.Key] = Change.Value;
        }

        if (!CheckRegionConvention(Candidate))
        {
            return "semantic-convention";
        }

        FlowchartCellRegion[] Ordered = Candidate.Values.ToArray();
        for (int First = 0; First < Ordered.Length; First++)
        {
            for (int Second = 0; Second < First; Second++)
            {
                if (Ordered[First].Overlaps(Ordered[Second]))
                {
                    return "cell-overlap";
                }
            }
        }

        return null;
    }

    /// <summary>Publishes all validated target regions and gaps with a single state replacement.</summary>
    /// <param name="Before">Exact state used to prepare the candidate.</param>
    /// <param name="After">Complete validated candidate owned by this grid.</param>
    /// <returns>False without mutation if another accepted repair made the prepared assignment stale.</returns>
    internal bool TryCommit(Snapshot Before, Snapshot After)
    {
        ArgumentNullException.ThrowIfNull(Before);
        ArgumentNullException.ThrowIfNull(After);
        if (!ReferenceEquals(Before.Owner, this) || !ReferenceEquals(After.Owner, this)
            || !ReferenceEquals(Current, Before)) return false;
        Current = After;
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
    internal Snapshot Capture() => Current;

    /// <summary>Restores the complete authoritative state without reconstructing cells from pixel geometry.</summary>
    /// <param name="State">A snapshot previously captured from this grid.</param>
    internal void Restore(Snapshot State)
    {
        ArgumentNullException.ThrowIfNull(State);
        if (!ReferenceEquals(State.Owner, this)) throw new ArgumentException("The snapshot belongs to another grid.", nameof(State));
        Current = State;
    }

    /// <summary>Immutable complete repair state, including space inserted between logical cells.</summary>
    internal sealed class Snapshot
    {
        /// <summary>Copies the complete cell state so later repairs cannot modify the saved option.</summary>
        /// <param name="Owner">Grid that may restore this snapshot.</param>
        /// <param name="Regions">Current node cell rectangles.</param>
        /// <param name="ColumnGaps">Current grown column gaps.</param>
        /// <param name="RowGaps">Current grown row gaps.</param>
        internal Snapshot(FlowchartLogicalGrid Owner, IReadOnlyDictionary<string, FlowchartCellRegion> Regions,
            IReadOnlyDictionary<int, double> ColumnGaps, IReadOnlyDictionary<int, double> RowGaps)
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

        private GapExtraTotals? _ColumnGapTotals;

        private GapExtraTotals? _RowGapTotals;

        private GapExtraTotals ColumnGapTotals =>
            _ColumnGapTotals ??= GapExtraTotals.Create(ColumnGaps, Owner.Configuration.ColumnGap);

        private GapExtraTotals RowGapTotals =>
            _RowGapTotals ??= GapExtraTotals.Create(RowGaps, Owner.Configuration.RowGap);

        internal double ColumnExtent(int Start, int Span) =>
            Span * Owner.Configuration.CellWidth + (Span - 1.0) * Owner.Configuration.ColumnGap
            + ColumnGapTotals.SumBetween(Start, checked(Start + Span - 1));

        internal double RowExtent(int Start, int Span) =>
            Span * Owner.Configuration.CellHeight + (Span - 1.0) * Owner.Configuration.RowGap
            + RowGapTotals.SumBetween(Start, checked(Start + Span - 1));

        internal double ColumnPosition(int Index) =>
            Index * (Owner.Configuration.CellWidth + Owner.Configuration.ColumnGap)
            + (Index >= 0
                ? ColumnGapTotals.SumBetween(0, Index)
                : -ColumnGapTotals.SumBetween(Index, 0));

        internal double RowPosition(int Index) =>
            Index * (Owner.Configuration.CellHeight + Owner.Configuration.RowGap)
            + (Index >= 0
                ? RowGapTotals.SumBetween(0, Index)
                : -RowGapTotals.SumBetween(Index, 0));
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
        var Before = Capture();
        if (TryPrepare(new Dictionary<string, FlowchartCellRegion>(), [new(Horizontal, After, Shortfall, GridStep)], out var Candidate))
            TryCommit(Before, Candidate!);
    }

    /// <summary>Measures the continuous visible rectangle, including every grown internal gap it spans.</summary>
    /// <param name="Region">Occupied whole-cell region.</param>
    /// <param name="State">Optional complete prepared state; projection never publishes it.</param>
    /// <returns>Width and height in page pixels, with no external gap included.</returns>
    internal (double Width, double Height) Measure(FlowchartCellRegion Region, Snapshot? State = null)
    {
        State ??= Current;
        if (!ReferenceEquals(State.Owner, this)) throw new ArgumentException("Candidate geometry must belong to this grid.", nameof(State));
        return (State.ColumnExtent(Region.StartColumn, Region.ColumnSpan),
            State.RowExtent(Region.StartRow, Region.RowSpan));
    }

    /// <summary>Maps a logical region to its page-down origin without inferring cells from centres.</summary>
    /// <param name="Region">Occupied rectangle.</param>
    /// <param name="State">Optional complete prepared state; projection never publishes it.</param>
    /// <returns>Top-left page position relative to the grid origin.</returns>
    internal (double X, double Y) Origin(FlowchartCellRegion Region, Snapshot? State = null)
    {
        State ??= Current;
        if (!ReferenceEquals(State.Owner, this)) throw new ArgumentException("Candidate geometry must belong to this grid.", nameof(State));
        return (State.ColumnPosition(Region.StartColumn), State.RowPosition(Region.StartRow));
    }

    /// <summary>Prefix sums of grown gap corrections for one axis on an immutable snapshot.</summary>
    private sealed class GapExtraTotals
    {
        private readonly int[] _Keys;

        private readonly double[] _Prefix;

        private GapExtraTotals(int[] Keys, double[] Prefix)
        {
            _Keys = Keys;
            _Prefix = Prefix;
        }

        internal static GapExtraTotals Create(IReadOnlyDictionary<int, double> Grown, double BaseGap)
        {
            List<int> Keys = new List<int>();
            List<double> Extras = new List<double>();
            foreach (KeyValuePair<int, double> Pair in Grown)
            {
                double Extra = Pair.Value - BaseGap;
                if (Extra == 0)
                {
                    continue;
                }

                Keys.Add(Pair.Key);
                Extras.Add(Extra);
            }

            int Count = Keys.Count;
            if (Count > 1)
            {
                int[] KeyArray = Keys.ToArray();
                double[] ExtraArray = Extras.ToArray();
                Array.Sort(KeyArray, ExtraArray);
                Keys = new List<int>(KeyArray);
                Extras = new List<double>(ExtraArray);
            }

            double[] Prefix = new double[Count + 1];
            for (int ExtraIndex = 0; ExtraIndex < Count; ExtraIndex++)
            {
                Prefix[ExtraIndex + 1] = Prefix[ExtraIndex] + Extras[ExtraIndex];
            }

            return new GapExtraTotals(Keys.ToArray(), Prefix);
        }

        internal double SumBetween(int FromInclusive, int ToExclusive)
        {
            if (ToExclusive <= FromInclusive || _Keys.Length == 0)
            {
                return 0;
            }

            int Start = LowerBound(FromInclusive);
            int End = LowerBound(ToExclusive);
            return _Prefix[End] - _Prefix[Start];
        }

        private int LowerBound(int Value)
        {
            int Low = 0;
            int High = _Keys.Length;
            while (Low < High)
            {
                int Middle = (Low + High) / 2;
                if (_Keys[Middle] < Value)
                {
                    Low = Middle + 1;
                }
                else
                {
                    High = Middle;
                }
            }

            return Low;
        }
    }
}

/// <summary>One measured gap increase included in a complete atomic repair assignment.</summary>
/// <param name="Horizontal">True for column gaps, false for row gaps.</param>
/// <param name="After">Declared index preceding the gap.</param>
/// <param name="Shortfall">Missing geometric clearance, independent of cell ownership.</param>
/// <param name="GridStep">Existing drawing-grid step used to round the increase.</param>
internal sealed record FlowchartCellGapGrowth(bool Horizontal, int After, double Shortfall, double GridStep);
