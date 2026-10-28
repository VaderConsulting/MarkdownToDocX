using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>One logical placement input with previously measured whole-cell spans and no drawing coordinates.</summary>
/// <param name="Id">Declared node identity.</param>
/// <param name="Rank">Logical main-axis rank supplied by ranking.</param>
/// <param name="Column">Logical cross-axis column supplied by ordering.</param>
/// <param name="RowSpan">Measured positive row span, including mandatory geometry.</param>
/// <param name="ColumnSpan">Measured positive column span, including mandatory geometry.</param>
internal sealed record FlowchartCellPlacementSeed(string Id, int Rank, int Column, int RowSpan, int ColumnSpan);

/// <summary>Starts initial placement from declared rank/column inputs with fixed diagram cells and measured common spans.</summary>
internal static class FlowchartCellInitialPlacement
{
    /// <summary>Binds the measured common-span model before hints and automatic placement, without enabling independent text spans.</summary>
    /// <param name="Nodes">Prepared logical ranks and columns, before any repair runs.</param>
    /// <param name="Width">Measured common rectangular width.</param>
    /// <param name="Height">Measured common rectangular height.</param>
    /// <param name="Horizontal">Whether ranks advance across columns.</param>
    /// <param name="Context">Owning render context and rejected-write diagnostic sink.</param>
    /// <param name="Contract">Optional declarations whose absolute cell pins reserve space before automatic placement.</param>
    internal static void Bind(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, int Width, int Height,
        bool Horizontal, FlowchartRenderContext Context, DiagramContract? Contract = null)
    {
        FlowchartLogicalGridConfiguration Config = FlowchartLogicalGridConfiguration.FromRules();
        int Columns = Math.Max(1, (int)Math.Ceiling((Width + Config.ColumnGap) / (Config.CellWidth + Config.ColumnGap)));
        int Rows = Math.Max(1, (int)Math.Ceiling((Height + Config.RowGap) / (Config.CellHeight + Config.RowGap)));
        int ColumnPitch = Math.Max(Rows, Columns);
        var Pins = Contract?.Nodes.Where(Node => Node.PlacementHint is { IsActive: true, Kind: PlacementHintKind.At })
            .ToDictionary(Node => Node.Id, Node => (Node.PlacementHint!.CellRow, Node.PlacementHint.CellColumn), StringComparer.Ordinal);
        FlowchartLogicalGrid Grid = Create(Config, Nodes.Select(Node => new FlowchartCellPlacementSeed(
            Node.Id, Node.Rank, Node.Column, Node.IsDiamond ? 2 * ColumnPitch : Rows,
            Node.IsDiamond ? ColumnPitch : Columns)).ToArray(), Horizontal, Pins);
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.BindCells(Grid, Context);
            if (Pins?.ContainsKey(Node.Id) == true) Node.PlacementHintLocked = true;
        }
    }

    /// <summary>Assigns all existing cell-bound nodes from their current logical ranks and columns in one repair transaction.</summary>
    /// <param name="Nodes">Whole diagram; fixed hints retain their declared reservations.</param>
    /// <param name="Horizontal">Whether ranks advance along columns.</param>
    /// <returns>Whether the complete exclusive plan was published, with no pixel writes or partial application.</returns>
    internal static bool TryAssign(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        FlowchartLogicalGrid.Snapshot Original = FlowchartCellRepair.Capture(Nodes);
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode? Node in Nodes.Where(Node => Node.PlacementHintLocked))
        {
            Targets.Add(Node.Id, Original.Regions[Node.Id]);
        }

        int RowPitch = Original.Regions.Values.Max(Region => Region.RowSpan);
        int ColumnPitch = Original.Regions.Values.Max(Region => Region.ColumnSpan);
        foreach (DiagramGridLayout.GridNode? Node in Nodes.Where(Node => !Node.PlacementHintLocked).OrderBy(Node => Node.Rank)
            .ThenBy(Node => Node.Column).ThenBy(Node => Node.Id, StringComparer.Ordinal))
        {
            FlowchartCellRegion Before = Original.Regions[Node.Id];
            int Row = checked((Horizontal ? Node.Column : Node.Rank) * RowPitch);
            int Column = checked((Horizontal ? Node.Rank : Node.Column) * ColumnPitch);
            FlowchartCellRegion Region = new FlowchartCellRegion(Row, Column, Before.RowSpan, Before.ColumnSpan);
            while (Targets.Values.Any(Other => Other.Overlaps(Region)))
            {
                if (Horizontal)
                {
                    Row = checked(Row + RowPitch);
                }
                else
                {
                    Column = checked(Column + ColumnPitch);
                }

                Region = new(Row, Column, Before.RowSpan, Before.ColumnSpan);
            }
            Targets.Add(Node.Id, Region);
        }
        return FlowchartCellRepair.TryAssign(Nodes, Targets);
    }

    /// <summary>Builds a complete deterministic ownership plan before exposing any node or derived drawing geometry.</summary>
    /// <param name="Configuration">Fixed diagram base cells and initial gaps.</param>
    /// <param name="Inputs">Ranked node identities and already measured spans; independent text-span selection is not enabled here.</param>
    /// <param name="Horizontal">Whether ranks advance across columns rather than down rows.</param>
    /// <param name="Pins">Optional explicitly reserved logical row/column origins.</param>
    /// <returns>A fully reserved grid with one exclusive contiguous region per node.</returns>
    /// <exception cref="ArgumentException">Node identities repeat or a declared identity is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A measured span is not positive.</exception>
    internal static FlowchartLogicalGrid Create(FlowchartLogicalGridConfiguration Configuration,
        IReadOnlyList<FlowchartCellPlacementSeed> Inputs, bool Horizontal,
        IReadOnlyDictionary<string, (int Row, int Column)>? Pins = null)
    {
        ArgumentNullException.ThrowIfNull(Inputs);
        FlowchartLogicalGrid Grid = new FlowchartLogicalGrid(Configuration);
        FlowchartCellPlacementSeed[] Ordered = [.. Inputs.OrderBy(Input => Input.Rank).ThenBy(Input => Input.Column).ThenBy(Input => Input.Id, StringComparer.Ordinal)];
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        foreach (FlowchartCellPlacementSeed? Input in Ordered)
        {
            ArgumentException.ThrowIfNullOrEmpty(Input.Id);
            _ = new FlowchartCellRegion(0, 0, Input.RowSpan, Input.ColumnSpan);
        }
        if (Ordered.Select(Input => Input.Id).Distinct(StringComparer.Ordinal).Count() != Ordered.Length)
            throw new ArgumentException("Initial placement node identities must be unique.", nameof(Inputs));
        foreach (var Input in Ordered.Where(Input => Pins?.ContainsKey(Input.Id) == true))
        {
            var Pin = Pins![Input.Id];
            var Region = new FlowchartCellRegion(Pin.Row, Pin.Column, Input.RowSpan, Input.ColumnSpan);
            if (Targets.Values.Any(Other => Other.Overlaps(Region)))
                throw new InvalidOperationException("Absolute cell positions overlap.");
            Targets.Add(Input.Id, Region);
        }
        int RowPitch = Ordered.Select(Input => Input.RowSpan).DefaultIfEmpty(1).Max();
        int ColumnPitch = Ordered.Select(Input => Input.ColumnSpan).DefaultIfEmpty(1).Max();
        foreach (FlowchartCellPlacementSeed? Input in Ordered)
        {
            if (Targets.ContainsKey(Input.Id)) continue;
            int Row = checked((Horizontal ? Input.Column : Input.Rank) * RowPitch);
            int Column = checked((Horizontal ? Input.Rank : Input.Column) * ColumnPitch);
            FlowchartCellRegion Region = new FlowchartCellRegion(Row, Column, Input.RowSpan, Input.ColumnSpan);
            while (Targets.Values.Any(Existing => Existing.Overlaps(Region)))
            {
                // Only logical coordinates change. This ordering seed neither
                // fits frames nor interprets hints; those are subsequent stages.
                if (Horizontal)
                {
                    Row = checked(Row + RowPitch);
                }
                else
                {
                    Column = checked(Column + ColumnPitch);
                }

                Region = new(Row, Column, Input.RowSpan, Input.ColumnSpan);
            }
            Targets.Add(Input.Id, Region);
        }
        // The new grid remains private until all complete target rectangles
        // have been built and validated; no production node is bound here.
        foreach (KeyValuePair<string, FlowchartCellRegion> Target in Targets)
        {
            if (!Grid.TryPlace(Target.Key, Target.Value))
            {
                throw new InvalidOperationException("The complete initial cell plan contains shared occupancy.");
            }
        }

        return Grid;
    }
}
