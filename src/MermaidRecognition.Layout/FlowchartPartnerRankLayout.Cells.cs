using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Plans specialised partner-rank geometry directly on the parent cell grid.</summary>
internal static partial class FlowchartPartnerRankLayout
{
    /// <summary>Plans required port extents and every partner lane together before applying any specialised many-to-many placement.</summary>
    /// <param name="Blocks">Complete detected partner blocks.</param>
    /// <param name="Horizontal">Whether the cross axis is logical rows.</param>
    /// <param name="Align">Whether automatic alignment is allowed.</param>
    /// <returns>Whether the complete exclusive assignment was accepted; failure leaves all cell regions and gaps unchanged.</returns>
    internal static bool PrepareCells(List<Partners> Blocks, bool Horizontal, bool Align = true)
    {
        var Nodes = Blocks.SelectMany(Block => Block.Parents.Concat(Block.Children)).Distinct().ToList();
        if (Nodes.Count == 0) return false;
        var Before = FlowchartCellRepair.Capture(Nodes);
        var Minimums = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        foreach (var Block in Blocks)
        {
            int Needed = DiagramGridMetrics.CeilToGrid(Block.Parents.Length == Block.Children.Length
                ? checked(4 * (Block.Parents.Length - 1) * Block.PortPitch + DiagramGridMetrics.GRID_UNIT_PX)
                : checked(2 * (Math.Max(Block.Parents.Length, Block.Children.Length) - 1) * Block.PortPitch + DiagramGridMetrics.GRID_UNIT_PX));
            foreach (var Node in Block.Parents.Concat(Block.Children))
            {
                var Previous = Minimums.GetValueOrDefault(Node.Id, (Width: (double)Node.Width, Height: (double)Node.Height));
                Minimums[Node.Id] = (Horizontal ? Previous.Width : Math.Max(Previous.Width, Needed),
                    Horizontal ? Math.Max(Previous.Height, Needed) : Previous.Height);
            }
        }
        if (!DiagramGridLayout.TryPlanCellMinimumExtents(Nodes, Minimums, Before.Regions, out var Targets)) return false;
        if (Align)
        foreach (var Block in Blocks)
        {
            var Members = Block.Parents.Concat(Block.Children).ToArray();
            int Pitch = Members.Max(Node => Horizontal ? Targets[Node.Id].RowSpan : Targets[Node.Id].ColumnSpan);
            int Anchor = Horizontal ? Targets[Block.Parents[0].Id].StartRow : Targets[Block.Parents[0].Id].StartColumn;
            var Longer = Block.Children.Length > Block.Parents.Length ? Block.Children : Block.Parents;
            for (int Index = 0; Index < Longer.Length; Index++) Set(Longer[Index], checked(Anchor + Index * Pitch));
            foreach (var Pair in Block.AlignedChildren)
            {
                var Reference = Block.Children.Length > Block.Parents.Length ? Block.Children[Pair.Value] : Block.Parents[Pair.Key];
                var Moving = Block.Children.Length > Block.Parents.Length ? Block.Parents[Pair.Key] : Block.Children[Pair.Value];
                Set(Moving, Horizontal ? Targets[Reference.Id].StartRow : Targets[Reference.Id].StartColumn);
            }
        }
        return FlowchartCellRepair.TryAssign(Nodes, Targets.Where(Pair => Nodes.Any(Node => Node.Id == Pair.Key))
            .ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal));

        // Only the private full plan changes. All extents and both ranks must
        // pass ownership and hint checks together before anything is published.
        void Set(DiagramGridLayout.GridNode Node, int Cross)
        {
            var Region = Targets[Node.Id];
            Targets[Node.Id] = new(Horizontal ? Cross : Region.StartRow, Horizontal ? Region.StartColumn : Cross, Region.RowSpan, Region.ColumnSpan);
        }
    }
}
