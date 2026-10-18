using System;
using System.Collections.Generic;
using System.Linq;

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
    internal static void Bind(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, int Width, int Height,
        bool Horizontal, FlowchartRenderContext Context)
    {
        var Config = FlowchartLogicalGridConfiguration.FromRules();
        int Columns = Math.Max(1, (int)Math.Ceiling((Width + Config.ColumnGap) / (Config.CellWidth + Config.ColumnGap)));
        int Rows = Math.Max(1, (int)Math.Ceiling((Height + Config.RowGap) / (Config.CellHeight + Config.RowGap)));
        int ColumnPitch = Math.Max(Rows, Columns);
        var Grid = Create(Config, Nodes.Select(Node => new FlowchartCellPlacementSeed(
            Node.Id, Node.Rank, Node.Column, Node.IsDiamond ? 2 * ColumnPitch : Rows,
            Node.IsDiamond ? ColumnPitch : Columns)).ToArray(), Horizontal);
        foreach (var Node in Nodes) Node.BindCells(Grid, Context);
    }

    /// <summary>Assigns all existing cell-bound nodes from their current logical ranks and columns in one repair transaction.</summary>
    /// <param name="Nodes">Whole diagram; fixed hints retain their declared reservations.</param>
    /// <param name="Horizontal">Whether ranks advance along columns.</param>
    /// <returns>Whether the complete exclusive plan was published, with no pixel writes or partial application.</returns>
    internal static bool TryAssign(IReadOnlyList<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        var Original = FlowchartCellRepair.Capture(Nodes);
        var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        foreach (var Node in Nodes.Where(Node => Node.PlacementHintLocked)) Targets.Add(Node.Id, Original.Regions[Node.Id]);
        int RowPitch = Original.Regions.Values.Max(Region => Region.RowSpan);
        int ColumnPitch = Original.Regions.Values.Max(Region => Region.ColumnSpan);
        foreach (var Node in Nodes.Where(Node => !Node.PlacementHintLocked).OrderBy(Node => Node.Rank)
            .ThenBy(Node => Node.Column).ThenBy(Node => Node.Id, StringComparer.Ordinal))
        {
            var Before = Original.Regions[Node.Id];
            int Row = checked((Horizontal ? Node.Column : Node.Rank) * RowPitch);
            int Column = checked((Horizontal ? Node.Rank : Node.Column) * ColumnPitch);
            var Region = new FlowchartCellRegion(Row, Column, Before.RowSpan, Before.ColumnSpan);
            while (Targets.Values.Any(Other => Other.Overlaps(Region)))
            {
                if (Horizontal) Row = checked(Row + RowPitch); else Column = checked(Column + ColumnPitch);
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
    /// <returns>A fully reserved grid with one exclusive contiguous region per node.</returns>
    /// <exception cref="ArgumentException">Node identities repeat or a declared identity is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A measured span is not positive.</exception>
    internal static FlowchartLogicalGrid Create(FlowchartLogicalGridConfiguration Configuration,
        IReadOnlyList<FlowchartCellPlacementSeed> Inputs, bool Horizontal)
    {
        ArgumentNullException.ThrowIfNull(Inputs);
        var Grid = new FlowchartLogicalGrid(Configuration);
        var Ordered = Inputs.OrderBy(Input => Input.Rank).ThenBy(Input => Input.Column)
            .ThenBy(Input => Input.Id, StringComparer.Ordinal).ToArray();
        var Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
        foreach (var Input in Ordered)
        {
            ArgumentException.ThrowIfNullOrEmpty(Input.Id);
            _ = new FlowchartCellRegion(0, 0, Input.RowSpan, Input.ColumnSpan);
        }
        int RowPitch = Ordered.Select(Input => Input.RowSpan).DefaultIfEmpty(1).Max();
        int ColumnPitch = Ordered.Select(Input => Input.ColumnSpan).DefaultIfEmpty(1).Max();
        foreach (var Input in Ordered)
        {
            int Row = checked((Horizontal ? Input.Column : Input.Rank) * RowPitch);
            int Column = checked((Horizontal ? Input.Rank : Input.Column) * ColumnPitch);
            var Region = new FlowchartCellRegion(Row, Column, Input.RowSpan, Input.ColumnSpan);
            while (Targets.Values.Any(Existing => Existing.Overlaps(Region)))
            {
                // Only logical coordinates change. This ordering seed neither
                // fits frames nor interprets hints; those are subsequent stages.
                if (Horizontal) Row = checked(Row + RowPitch);
                else Column = checked(Column + ColumnPitch);
                Region = new(Row, Column, Input.RowSpan, Input.ColumnSpan);
            }
            Targets.Add(Input.Id, Region);
        }
        // The new grid remains private until all complete target rectangles
        // have been built and validated; no production node is bound here.
        foreach (var Target in Targets)
            if (!Grid.TryPlace(Target.Key, Target.Value))
                throw new InvalidOperationException("The complete initial cell plan contains shared occupancy.");
        return Grid;
    }
}
