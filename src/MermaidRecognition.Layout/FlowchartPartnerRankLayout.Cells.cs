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
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        Dictionary<int, double> Required = [];
        foreach (Partners Block in Blocks)
        {
            (double Start, double End, double _) = Limits(Contract, Frames, Block, Horizontal);
            int Tracks = checked((Block.Parents.Length * Block.Children.Length) - Block.AlignedChildren.Count);
            int Extra = DiagramGridMetrics.CeilToGrid((2 * TERMINAL_RESERVE) + ((Tracks - 1) * CORNER_TRACK_PITCH) - (End - Start));
            if (Extra <= 0)
            {
                continue;
            }

            FlowchartCellRegion[] Parents = [.. Block.Parents.Select(Node => Before.Regions[Node.Id])];
            FlowchartCellRegion[] Children = [.. Block.Children.Select(Node => Before.Regions[Node.Id])];
            bool ParentsFirst = Parents.Min(Region => Horizontal ? Region.StartColumn : Region.StartRow)
                <= Children.Min(Region => Horizontal ? Region.StartColumn : Region.StartRow);
            FlowchartCellRegion[] Earlier = ParentsFirst ? Parents : Children; FlowchartCellRegion[] Later = ParentsFirst ? Children : Parents;
            int FirstGap = Earlier.Max(Region => Horizontal ? Region.EndColumn : Region.EndRow) - 1;
            int LastGap = Later.Min(Region => Horizontal ? Region.StartColumn : Region.StartRow) - 1;
            int? After = null;
            for (int Gap = FirstGap; Gap <= LastGap; Gap++)
            {
                if (Before.Regions.Values.Any(Region => Horizontal
                    ? Region.StartColumn <= Gap && Region.EndColumn > Gap + 1
                    : Region.StartRow <= Gap && Region.EndRow > Gap + 1))
                {
                    continue;
                }

                After = Gap; break;
            }
            // A span across every channel boundary needs another complete
            // placement candidate; never enlarge that node as a side effect.
            if (!After.HasValue)
            {
                return false;
            }

            Required[After.Value] = Math.Max(Required.GetValueOrDefault(After.Value), Extra);
        }
        if (Required.Count == 0)
        {
            return false;
        }

        FlowchartCellGapGrowth[] Growth = [.. Required.Select(Pair => new FlowchartCellGapGrowth(Horizontal, Pair.Key, Pair.Value, DiagramGridMetrics.GRID_UNIT_PX))];
        FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Nodes, new Dictionary<string, FlowchartCellRegion>(), Growth);
        if (Plan?.TryApply() != true)
        {
            return false;
        }

        DiagramGridLayout.GridFrame[] OriginalFrames = [.. Frames];
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
    /// <param name="Align">Legacy alignment option; cell preparation retains only mandatory capacity and overlap clearance.</param>
    /// <returns>Whether the complete exclusive assignment was accepted; failure leaves all cell regions and gaps unchanged.</returns>
    internal static bool PrepareCells(List<Partners> Blocks, bool Horizontal, bool Align = true)
    {
        List<DiagramGridLayout.GridNode> Nodes = [.. Blocks.SelectMany(Block => Block.Parents.Concat(Block.Children)).Distinct()];
        if (Nodes.Count == 0)
        {
            return false;
        }

        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        Dictionary<string, (double Width, double Height)> Minimums = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        foreach (Partners Block in Blocks)
        {
            int Needed = DiagramGridMetrics.CeilToGrid(Block.Parents.Length == Block.Children.Length
                ? checked((4 * (Block.Parents.Length - 1) * Block.PortPitch) + DiagramGridMetrics.GRID_UNIT_PX)
                : checked((2 * (Math.Max(Block.Parents.Length, Block.Children.Length) - 1) * Block.PortPitch) + DiagramGridMetrics.GRID_UNIT_PX));
            foreach (DiagramGridLayout.GridNode? Node in Block.Parents.Concat(Block.Children))
            {
                (double Width, double Height) Previous = Minimums.GetValueOrDefault(Node.Id, (Width: (double)Node.Width, Height: (double)Node.Height));
                Minimums[Node.Id] = (Horizontal ? Previous.Width : Math.Max(Previous.Width, Needed),
                    Horizontal ? Math.Max(Previous.Height, Needed) : Previous.Height);
            }
        }
        if (!DiagramGridLayout.TryPlanCellMinimumExtents(Nodes, Minimums, Before.Regions, out Dictionary<string, FlowchartCellRegion>? Targets))
        {
            return false;
        }

        List<DiagramGridLayout.GridNode> Packed = [];
        foreach (DiagramGridLayout.GridNode? Node in Nodes.OrderBy(Node => Horizontal ? Before.Regions[Node.Id].StartColumn : Before.Regions[Node.Id].StartRow)
            .ThenBy(Node => Horizontal ? Before.Regions[Node.Id].StartRow : Before.Regions[Node.Id].StartColumn))
        {
            FlowchartCellRegion Region = Targets[Node.Id];
            foreach (DiagramGridLayout.GridNode Other in Packed)
            {
                FlowchartCellRegion Obstacle = Targets[Other.Id];
                if (!Region.Overlaps(Obstacle))
                {
                    continue;
                }

                Region = new(Horizontal ? Obstacle.EndRow : Region.StartRow,
                    Horizontal ? Region.StartColumn : Obstacle.EndColumn, Region.RowSpan, Region.ColumnSpan);
            }
            Targets[Node.Id] = Region; Packed.Add(Node);
        }
        // Each rank keeps its own original start. Only capacity-induced
        // overlap moves a later member; no partner is placed on another
        // rank's centre or start line merely to make a straight connector.
        Dictionary<string, FlowchartCellRegion> Changes = Targets.Where(Pair => Nodes.Any(Node => Node.Id == Pair.Key)).ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal);
        FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Nodes, Changes);
        if (Plan == null)
        {
            return false;
        }

        Partners[] Updated = [.. Blocks.Select(Block => Block with { AlignedChildren = Block.AlignedChildren
            .Where(Pair => PortsAlreadyAlign(Block.Parents[Pair.Key], Block.Children[Pair.Value], Plan.After, Horizontal))
            .ToDictionary(Pair => Pair.Key, Pair => Pair.Value) })];
        if (!Plan.TryApply())
        {
            return false;
        }

        for (int Index = 0; Index < Updated.Length; Index++)
        {
            Blocks[Index] = Updated[Index];
        }

        return true;
    }

    /// <summary>Retains a straight-channel claim only when the renderer-owned facing ports already align.</summary>
    /// <param name="Source">Parent with measured required capacity.</param>
    /// <param name="Target">Child in its independently placed rank.</param>
    /// <param name="State">Complete prepared ownership, not yet published.</param>
    /// <param name="Horizontal">Whether flow advances across columns.</param>
    /// <returns>Whether midpoint boundary ports actually share a track; it never repositions either node.</returns>
    private static bool PortsAlreadyAlign(DiagramGridLayout.GridNode Source, DiagramGridLayout.GridNode Target,
        FlowchartLogicalGrid.Snapshot State, bool Horizontal)
    {
        FlowchartLayoutNodeBounds A = Source.CellNode!.Bounds(State.Regions[Source.Id], Source.GeometryProvider, State);
        FlowchartLayoutNodeBounds B = Target.CellNode!.Bounds(State.Regions[Target.Id], Target.GeometryProvider, State);
        bool Forward = Horizontal ? State.Regions[Source.Id].StartColumn < State.Regions[Target.Id].StartColumn
            : State.Regions[Source.Id].StartRow < State.Regions[Target.Id].StartRow;
        string Exit = Horizontal ? Forward ? "right" : "left" : Forward ? "bottom" : "top";
        string Entry = Horizontal ? Forward ? "left" : "right" : Forward ? "top" : "bottom";
        if (!(A.Geometry ?? NodeGeometry.Rectangle(A.Width, A.Height)).ConnectionBoundary.TryPort(Exit, 0.5, out NodeGeometryPort P)
            || !(B.Geometry ?? NodeGeometry.Rectangle(B.Width, B.Height)).ConnectionBoundary.TryPort(Entry, 0.5, out NodeGeometryPort Q))
        {
            return false;
        }

        double Sign = Source.CellNode.UsesRoutingCoordinates ? -1 : 1;
        return Math.Abs(Horizontal ? A.CenterY + (Sign * P.Y) - B.CenterY - (Sign * Q.Y) : A.CenterX + P.X - B.CenterX - Q.X) <= 0.001;
    }
}
