using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Sizes external gaps from declared connection demand before publishing an initial band placement.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>One connection's reservation through a free strip; cross coordinates are doubled cell indices, not pixel positions.</summary>
    /// <param name="Columns">Whether this strip separates columns.</param>
    /// <param name="After">Cell index immediately before the strip.</param>
    /// <param name="First">First cross-axis coordinate occupied by the reservation.</param>
    /// <param name="Last">Last cross-axis coordinate occupied by the reservation.</param>
    /// <param name="Edge">Declared connection index, distinguishing repeated links.</param>
    private sealed record InitialTrackDemand(bool Columns, int After, int First, int Last, int Edge);

    /// <summary>Plans initial gap widths from fixed endpoint cells and the unrelated cells that block their direct columns or rows.</summary>
    /// <param name="Contract">Declared relationships and group endpoints.</param>
    /// <param name="Targets">Complete private placement; no node region is modified.</param>
    /// <param name="Grid">Grid supplying immutable configuration and the unpublished starting gaps.</param>
    /// <param name="Context">Owning diagnostic context, if present.</param>
    /// <param name="InitialState">Optional unpublished initial gaps already selected with this placement.</param>
    /// <returns>All measured whole-grid gap increases, to publish atomically with the initial regions.</returns>
    internal static IReadOnlyList<FlowchartCellGapGrowth> PlanInitialRoutingCapacity(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartCellRegion> Targets, FlowchartLogicalGrid Grid, FlowchartRenderContext? Context,
        FlowchartLogicalGrid.Snapshot? InitialState = null)
    {
        var Endpoints = new Dictionary<string, FlowchartCellRegion>(Targets, StringComparer.Ordinal);
        foreach (var Group in Contract.Groups)
        {
            var Members = Targets.Where(Pair => DiagramGridLayout.IsInsideGroup(Contract, Pair.Key, Group.Id)).Select(Pair => Pair.Value).ToArray();
            if (Members.Length == 0) continue;
            int Row = Members.Min(Region => Region.StartRow), Column = Members.Min(Region => Region.StartColumn);
            Endpoints[Group.Id] = new(Row, Column, Members.Max(Region => Region.EndRow) - Row, Members.Max(Region => Region.EndColumn) - Column);
        }
        var Demands = new List<InitialTrackDemand>();
        for (int Index = 0; Index < Contract.Edges.Count; Index++)
        {
            var Edge = Contract.Edges[Index];
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                || Edge.SourceId == Edge.TargetId || !Endpoints.TryGetValue(Edge.SourceId, out var Source)
                || !Endpoints.TryGetValue(Edge.TargetId, out var Target)) continue;
            bool Vertical = Source.EndRow <= Target.StartRow || Target.EndRow <= Source.StartRow;
            if (!Vertical && Source.EndColumn > Target.StartColumn && Target.EndColumn > Source.StartColumn) continue;
            AddInitialConnectionDemand(Contract, Targets, Demands, Edge, Source, Target, Vertical, Index);
        }
        var Before = InitialState ?? Grid.Capture();
        var Growth = new List<FlowchartCellGapGrowth>();
        foreach (var Strip in AssignOpenInitialGaps(Demands, Targets, Context).GroupBy(Item => (Item.Columns, Item.After)))
        {
            int Count = Strip.SelectMany(Item => new[] { Item.First, Item.Last }).Distinct()
                .Select(Position => Strip.Where(Item => Item.First <= Position && Item.Last >= Position).Select(Item => Item.Edge).Distinct().Count())
                .DefaultIfEmpty(0).Max();
            // One connector does not need parallel tracks; its existing normal
            // terminal clearance remains owned by ordinary route construction.
            if (Count == 0) continue;
            int Reserved = InitialReservedTrackCount(Count);
            double Required = DiagramGridMetrics.GapWidthForTracks(Reserved);
            double Available = Strip.Key.Columns ? Before.ColumnGaps.GetValueOrDefault(Strip.Key.After, Grid.Configuration.ColumnGap)
                : Before.RowGaps.GetValueOrDefault(Strip.Key.After, Grid.Configuration.RowGap);
            ReportCapacity(Context, $"INITIAL_CORRIDOR_DEMAND|columns={Strip.Key.Columns}|after={Strip.Key.After}|lines={Count}|reserved={Reserved}|available={Available:R}");
            if (Required <= Available) continue;
            Growth.Add(new(Strip.Key.Columns, Strip.Key.After, Required - Available, DiagramGridMetrics.GRID_UNIT_PX));
            string Stage = Context?.DeferInitialBandCapacity == true ? "INITIAL_CORRIDOR_FORECAST" : "INITIAL_CORRIDOR_CAPACITY";
            ReportCapacity(Context, $"{Stage}|columns={Strip.Key.Columns}|after={Strip.Key.After}|lines={Count}|reserved={Reserved}|before={Available:R}|required={Required:R}|afterWidth={Available + Math.Ceiling((Required - Available) / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX:R}");
        }
        return Growth;
    }

    /// <summary>Reserves configured headroom for a used gap, including at least one additional track; empty gaps need no artificial traffic.</summary>
    /// <param name="Forecast">Nonnegative forecast number of simultaneously overlapping lines.</param>
    /// <returns>Rounded-up forecast plus headroom, or zero for an unused gap.</returns>
    internal static int InitialReservedTrackCount(int Forecast)
    {
        if (Forecast < 0) throw new ArgumentOutOfRangeException(nameof(Forecast));
        if (Forecast == 0)
        {
            return 0;
        }

        if (Forecast == 1)
        {
            return 1;
        }

        return checked(Math.Max(Forecast + 1,
            (int)Math.Ceiling(Forecast * (1 + FlowchartLayoutRules.CorridorCapacityBuffer))));
    }

    /// <summary>Assigns each forecast strip to the nearest globally open gap, never growing a gap inside another node.</summary>
    /// <param name="Demands">Unpublished declarations projected into strips.</param>
    /// <param name="Targets">Disjoint fixed regions; the configured positive external gaps connect their free surroundings.</param>
    /// <param name="Context">Owning diagnostics, if present.</param>
    /// <returns>Finite interior reservations; exterior destinations are reported as unbounded free corridors.</returns>
    private static IReadOnlyList<InitialTrackDemand> AssignOpenInitialGaps(IReadOnlyList<InitialTrackDemand> Demands,
        IReadOnlyDictionary<string, FlowchartCellRegion> Targets, FlowchartRenderContext? Context)
    {
        var Result = new List<InitialTrackDemand>();
        foreach (bool Columns in new[] { true, false })
        {
            var Axis = new InitialCapacityAxis(Columns);
            int First = Targets.Values.Min(Axis.CrossStart), Last = Targets.Values.Max(Axis.CrossEnd) - 1;
            var Open = Enumerable.Range(First - 1, Last - First + 2).Where(After => !Targets.Values.Any(Region =>
                Axis.CrossStart(Region) <= After && Axis.CrossEnd(Region) > After + 1)).ToArray();
            // Every internal candidate is a full free strip. Positive external
            // gaps between the disjoint bodies connect it to the free exterior;
            // forecast reassignment may approach it around bodies, never through
            // the occupied internal cells of a wide or tall node.
            foreach (var Demand in Demands.Where(Item => Item.Columns == Columns))
            {
                int After = Open.OrderBy(Index => Math.Abs((long)Index - Demand.After))
                    .ThenBy(Index => Index < First || Index >= Last).ThenBy(Index => Index).First();
                bool Exterior = After < First || After >= Last;
                if (After != Demand.After || Exterior)
                    ReportCapacity(Context, $"INITIAL_CORRIDOR_REDIRECT|columns={Columns}|from={Demand.After}|to={After}|edge={Demand.Edge}|exterior={Exterior}");
                if (!Exterior) Result.Add(Demand with { After = After });
            }
        }
        return Result;
    }

    /// <summary>Maps one declaration into approach and trunk reservations without allocating ports or routing a line.</summary>
    /// <param name="Contract">Declared containment.</param>
    /// <param name="Targets">Fixed complete cell ownership.</param>
    /// <param name="Demands">Private reservation list receiving this connection.</param>
    /// <param name="Edge">Declared link, including frame endpoints.</param>
    /// <param name="Source">Source cell envelope.</param>
    /// <param name="Target">Target cell envelope.</param>
    /// <param name="Vertical">Whether bands or separated rows define the main axis.</param>
    /// <param name="Index">Distinct declaration index, including repeated relationships.</param>
    private static void AddInitialConnectionDemand(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartCellRegion> Targets, List<InitialTrackDemand> Demands,
        EdgeContract Edge, FlowchartCellRegion Source, FlowchartCellRegion Target, bool Vertical, int Index)
    {
            var Axis = new InitialCapacityAxis(Vertical);
            if (Axis.MainStart(Source) > Axis.MainStart(Target)) (Source, Target) = (Target, Source);
            bool SourceClear = InitialEndpointColumnIsClear(Contract, Targets, Edge, Axis, Source, Target, Axis.CrossStart(Source));
            bool TargetClear = InitialEndpointColumnIsClear(Contract, Targets, Edge, Axis, Source, Target, Axis.CrossStart(Target));
            if (Axis.CrossStart(Source) == Axis.CrossStart(Target) && SourceClear) return;
            int Track;
            if (SourceClear) Track = checked(2 * Axis.CrossStart(Source));
            else if (TargetClear) Track = checked(2 * Axis.CrossStart(Target));
            else
            {
                int Left = Targets.Values.Min(Axis.CrossStart) - 1, Right = Targets.Values.Max(Axis.CrossEnd) - 1;
                var Free = Enumerable.Range(Left, Right - Left + 1).Where(After => !Targets.Values.Any(Region =>
                    Axis.CrossStart(Region) <= After && Axis.CrossEnd(Region) > After + 1));
                int After = Free.OrderBy(Value => Math.Abs((long)Value - Axis.CrossStart(Source)) + Math.Abs((long)Value - Axis.CrossStart(Target)))
                    .ThenBy(Value => Value < Targets.Values.Min(Axis.CrossStart) || Value >= Right)
                    .ThenBy(Value => Value).First(); // Prefer an equally near interior strip over an exterior detour.
                Track = checked(2 * After + 1);
                if (After >= Targets.Values.Min(Axis.CrossStart) && After < Targets.Values.Max(Axis.CrossEnd) - 1)
                    Demands.Add(new(Vertical, After, Axis.MainStart(Source) * 2, Axis.MainEnd(Target) * 2, Index));
            }
            AddInitialApproachDemand(Demands, !Vertical, Axis.MainEnd(Source) - 1, Axis.CrossStart(Source) * 2, Track, Index);
            AddInitialApproachDemand(Demands, !Vertical, Axis.MainStart(Target) - 1, Axis.CrossStart(Target) * 2, Track, Index);
    }

    /// <summary>Tests a whole endpoint column against unrelated cell bodies between the endpoints, including intervening ranks.</summary>
    /// <param name="Contract">Declared frame ancestry.</param>
    /// <param name="Targets">Fixed regions.</param>
    /// <param name="Edge">Source and target identities excluded from unrelated obstacles.</param>
    /// <param name="Axis">Main/cross cell interpretation.</param>
    /// <param name="Source">Earlier main-axis endpoint envelope.</param>
    /// <param name="Target">Later main-axis endpoint envelope.</param>
    /// <param name="Column">Cross-axis cell index being tested.</param>
    /// <returns>Whether a normal endpoint run can continue without entering another node's cells.</returns>
    private static bool InitialEndpointColumnIsClear(DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartCellRegion> Targets, EdgeContract Edge, InitialCapacityAxis Axis,
        FlowchartCellRegion Source, FlowchartCellRegion Target, int Column)
        => !Targets.Any(Pair => Pair.Key != Edge.SourceId && Pair.Key != Edge.TargetId
            && !DiagramGridLayout.IsInsideGroup(Contract, Pair.Key, Edge.SourceId)
            && !DiagramGridLayout.IsInsideGroup(Contract, Pair.Key, Edge.TargetId)
            && Axis.MainStart(Pair.Value) < Axis.MainEnd(Target) && Axis.MainEnd(Pair.Value) > Axis.MainStart(Source)
            && Axis.CrossStart(Pair.Value) <= Column && Axis.CrossEnd(Pair.Value) > Column);

    /// <summary>Adds a perpendicular approach only when the selected trunk differs from the endpoint's own cell coordinate.</summary>
    /// <param name="Demands">Private connection reservations.</param>
    /// <param name="Columns">Whether the approach strip separates columns.</param>
    /// <param name="After">Cell boundary adjacent to the endpoint.</param>
    /// <param name="Endpoint">Doubled endpoint cell coordinate.</param>
    /// <param name="Track">Doubled trunk coordinate.</param>
    /// <param name="Index">Distinct declared connection.</param>
    private static void AddInitialApproachDemand(List<InitialTrackDemand> Demands, bool Columns,
        int After, int Endpoint, int Track, int Index)
    {
        if (Endpoint != Track) Demands.Add(new(Columns, After, Math.Min(Endpoint, Track), Math.Max(Endpoint, Track), Index));
    }

    /// <summary>Shares one demand calculation between column gaps and rank gaps without changing page coordinates.</summary>
    /// <param name="Vertical">Whether the main cell axis is the row axis.</param>
    private sealed record InitialCapacityAxis(bool Vertical)
    {
        /// <summary>Returns the first cell on the main axis.</summary>
        /// <param name="Region">Fixed occupied rectangle.</param>
        /// <returns>Starting row for vertical demand, otherwise starting column.</returns>
        internal int MainStart(FlowchartCellRegion Region) => Vertical ? Region.StartRow : Region.StartColumn;
        /// <summary>Returns the exclusive last cell boundary on the main axis.</summary>
        /// <param name="Region">Fixed occupied rectangle.</param>
        /// <returns>End row for vertical demand, otherwise end column.</returns>
        internal int MainEnd(FlowchartCellRegion Region) => Vertical ? Region.EndRow : Region.EndColumn;
        /// <summary>Returns the first cell on the perpendicular axis.</summary>
        /// <param name="Region">Fixed occupied rectangle.</param>
        /// <returns>Starting column for vertical demand, otherwise starting row.</returns>
        internal int CrossStart(FlowchartCellRegion Region) => Vertical ? Region.StartColumn : Region.StartRow;
        /// <summary>Returns the exclusive last cell boundary on the perpendicular axis.</summary>
        /// <param name="Region">Fixed occupied rectangle.</param>
        /// <returns>End column for vertical demand, otherwise end row.</returns>
        internal int CrossEnd(FlowchartCellRegion Region) => Vertical ? Region.EndColumn : Region.EndRow;
    }
}
