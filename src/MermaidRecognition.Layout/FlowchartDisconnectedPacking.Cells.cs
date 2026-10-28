using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Scores disconnected component packing from complete logical region candidates.</summary>
internal static partial class FlowchartDisconnectedPacking
{
    /// <summary>Enumerates declared-order row packings, scores derived geometry and atomically accepts only the best legal improvement.</summary>
    /// <param name="Contract">Declared order and graph/group ownership.</param>
    /// <param name="Nodes">All nodes in one authoritative grid.</param>
    /// <returns>Whether a complete improving assignment was published.</returns>
    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        if (Nodes.Count < 2)
        {
            return false;
        }

        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        Dictionary<string, int> Declared = Contract.Nodes.Select((Node, Index) => (Node.Id, Index)).GroupBy(Item => Item.Id, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First().Index, StringComparer.Ordinal);
        List<DiagramGridLayout.GridNode>[] Components = [.. FindComponents(Contract, Nodes).OrderBy(Members => Members.Min(Node => Declared.GetValueOrDefault(Node.Id, int.MaxValue)))];
        if (Components.Length < 2)
        {
            return false;
        }

        FlowchartCellRegion[] Envelopes = [.. Components.Select(Members => Envelope(Members, Before.Regions))];
        double BestScore = CellScore(Before.Regions);
        FlowchartCellRepair.Assignment? Best = null;
        for (int PerRow = 1; PerRow <= Components.Length; PerRow++)
        {
            Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
            int Row = 0, Column = 0, RowSpan = 0;
            for (int Index = 0; Index < Components.Length; Index++)
            {
                if (Index > 0 && Index % PerRow == 0) { Row = checked(Row + RowSpan); Column = 0; RowSpan = 0; }
                FlowchartCellRegion Region = Envelopes[Index];
                bool Fixed = Components[Index].Any(Node => Node.PlacementHintLocked);
                foreach (DiagramGridLayout.GridNode? Node in Components[Index])
                {
                    FlowchartCellRegion Original = Before.Regions[Node.Id];
                    if (!Fixed)
                    {
                        Targets[Node.Id] = new(checked(Original.StartRow + Row - Region.StartRow),
                        checked(Original.StartColumn + Column - Region.StartColumn), Original.RowSpan, Original.ColumnSpan);
                    }
                }
                Column = checked(Column + Region.ColumnSpan);
                RowSpan = Math.Max(RowSpan, Region.RowSpan);
            }
            FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Nodes, Targets);
            if (Plan == null)
            {
                continue;
            }

            double CandidateScore = CellScore(Targets);
            if (CandidateScore < BestScore - 0.0001) { Best = Plan; BestScore = CandidateScore; }
        }
        return Best?.TryApply() == true;

        // The existing score sees only geometry derived forward from each
        // candidate envelope. None of its pixel measurements become cells.
        double CellScore(IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
        {
            Piece[] Pieces = [.. Components.Select((Members, Index) =>
            {
                FlowchartCellRegion Region = Envelope(Members, Regions); (double X, double Y) Origin = Grid.Origin(Region); (double Width, double Height) Size = Grid.Measure(Region);
                return new Piece(Members, Origin.X, -Origin.Y, Size.Width, Size.Height, Index);
            })];
            return Score([.. Pieces.Select(Piece => new Position(Piece.Left, Piece.Top))], Pieces, DiagramGridMetrics.NODE_GAP_PX);
        }
    }

    /// <summary>Finds a component's logical bounding rectangle while preserving every member's separate contiguous ownership.</summary>
    /// <param name="Members">The connected component or root-group member set.</param>
    /// <param name="Regions">Complete proposed ownership.</param>
    /// <returns>A logical envelope, never an additional occupied node region.</returns>
    private static FlowchartCellRegion Envelope(IReadOnlyList<DiagramGridLayout.GridNode> Members,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
    {
        int Row = Members.Min(Node => Regions[Node.Id].StartRow), Column = Members.Min(Node => Regions[Node.Id].StartColumn);
        return new(Row, Column, Members.Max(Node => Regions[Node.Id].EndRow) - Row, Members.Max(Node => Regions[Node.Id].EndColumn) - Column);
    }
}
