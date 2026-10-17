using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans specialised partner-rank geometry directly on the parent cell grid.</summary>
internal static partial class FlowchartPartnerRankLayout
{
    /// <summary>Repairs measured partner channel shortfalls by growing shared external gaps atomically, never translating derived pixels.</summary>
    /// <param name="Contract">Group ownership used to measure and rebuild frames.</param>
    /// <param name="Nodes">Complete diagram, including every gap-affected member.</param>
    /// <param name="Frames">Derived cache refreshed only after acceptance.</param>
    /// <param name="Blocks">Prepared many-to-many channel demands.</param>
    /// <param name="Horizontal">Whether channels advance across columns.</param>
    /// <returns>Whether the complete gap plan was accepted.</returns>
    internal static bool GrowCellGaps(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        List<DiagramGridLayout.GridFrame> Frames, List<Partners> Blocks, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Nodes);
        var Required = new Dictionary<int, double>();
        foreach (var Block in Blocks)
        {
            var (Start, End, _) = Limits(Contract, Frames, Block, Horizontal);
            int Tracks = checked(Block.Parents.Length * Block.Children.Length - Block.AlignedChildren.Count);
            int Extra = DiagramGridMetrics.CeilToGrid(2 * TERMINAL_RESERVE + (Tracks - 1) * CORNER_TRACK_PITCH - (End - Start));
            if (Extra <= 0) continue;
            var Parents = Block.Parents.Select(Node => Before.Regions[Node.Id]).ToArray();
            var Children = Block.Children.Select(Node => Before.Regions[Node.Id]).ToArray();
            bool ParentsFirst = Parents.Min(Region => Horizontal ? Region.StartColumn : Region.StartRow)
                <= Children.Min(Region => Horizontal ? Region.StartColumn : Region.StartRow);
            var Earlier = ParentsFirst ? Parents : Children; var Later = ParentsFirst ? Children : Parents;
            int FirstGap = Earlier.Max(Region => Horizontal ? Region.EndColumn : Region.EndRow) - 1;
            int LastGap = Later.Min(Region => Horizontal ? Region.StartColumn : Region.StartRow) - 1;
            int? After = null;
            for (int Gap = FirstGap; Gap <= LastGap; Gap++)
            {
                if (Before.Regions.Values.Any(Region => Horizontal
                    ? Region.StartColumn <= Gap && Region.EndColumn > Gap + 1
                    : Region.StartRow <= Gap && Region.EndRow > Gap + 1)) continue;
                After = Gap; break;
            }
            // A span across every channel boundary needs another complete
            // placement candidate; never enlarge that node as a side effect.
            if (!After.HasValue) return false;
            Required[After.Value] = Math.Max(Required.GetValueOrDefault(After.Value), Extra);
        }
        if (Required.Count == 0) return false;
        var Growth = Required.Select(Pair => new FlowchartCellGapGrowth(Horizontal, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX)).ToArray();
        var Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(), Growth);
        if (Plan?.TryApply() != true) return false;
        var OriginalFrames = Frames.ToArray();
        try { DiagramGridLayout.ReplaceCellFrames(Contract, Nodes, Frames); }
        catch
        {
            Plan.Rollback(); Frames.Clear(); Frames.AddRange(OriginalFrames); throw;
        }
        return true;
    }

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
