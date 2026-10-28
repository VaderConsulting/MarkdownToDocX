using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans terminal side branches on exclusive whole-cell tracks.</summary>
internal static partial class FlowchartTerminalBranchPlacement
{
    /// <summary>Compacts eligible leaves beside a continuing or collecting sibling in one complete reservation transaction.</summary>
    /// <param name="Contract">Graph, return edges and group membership.</param>
    /// <param name="Nodes">Complete shared cell ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses logical rows.</param>
    /// <param name="CrossPitch">Existing minimum measured cross-axis separation.</param>
    /// <returns>Whether the complete plan was legal and accepted.</returns>
    internal static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal, int CrossPitch)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes); FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        EdgeContract[] Edges = [.. Contract.Edges.Where(Edge => !Back.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        int Pitch = Targets.Values.Max(Region => Horizontal ? Region.RowSpan : Region.ColumnSpan);
        foreach (DiagramGridLayout.GridNode? Leaf in Nodes.Where(Node => !Node.IsDiamond && !Node.PlacementHintLocked && !Edges.Any(Edge => Edge.SourceId == Node.Id)))
        {
            EdgeContract[] Incoming = [.. Edges.Where(Edge => Edge.TargetId == Leaf.Id)];
            if (Incoming.Length != 1 || !ById.TryGetValue(Incoming[0].SourceId, out DiagramGridLayout.GridNode? Parent)
                || Parent.IsDiamond || Parent.Rank >= Leaf.Rank || Parent.GroupId != Leaf.GroupId
                || Edges.Any(Edge => Edge.SourceId == Parent.Id && ById.TryGetValue(Edge.TargetId, out DiagramGridLayout.GridNode? Child) && Child.Rank != Leaf.Rank))
            {
                continue;
            }

            int Centre = CellCross(Targets[Parent.Id], Horizontal);
            bool Continuing = Edges.Any(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id
                && ById.TryGetValue(Edge.TargetId, out DiagramGridLayout.GridNode? Child) && Child.Rank == Leaf.Rank && Child.GroupId == Leaf.GroupId
                && CellCross(Targets[Child.Id], Horizontal) == Centre && Edges.Any(Next => Next.SourceId == Child.Id));
            DiagramGridLayout.GridNode?[] Collectors = [.. Edges.Where(Edge => Edge.SourceId == Parent.Id && Edge.TargetId != Leaf.Id)
                .Select(Edge => ById.GetValueOrDefault(Edge.TargetId)).Where(Child => Child != null && Child.Rank == Leaf.Rank
                    && Child.GroupId == Leaf.GroupId && !Child.IsDiamond && !Edges.Any(Next => Next.SourceId == Child.Id)
                    && Edges.Any(Edge => Edge.TargetId == Child.Id && Edge.SourceId != Parent.Id
                        && ById.TryGetValue(Edge.SourceId, out DiagramGridLayout.GridNode? Input) && Input.Rank == Parent.Rank && Input.GroupId == Parent.GroupId))];
            if (!Continuing && Collectors.Length == 0)
            {
                continue;
            }

            int Preferred = Continuing ? Math.Sign(CellCross(Targets[Leaf.Id], Horizontal) - Centre)
                : -Math.Sign(Collectors.Average(Child => CellCross(Targets[Child!.Id], Horizontal)) - Centre);
            if (!Continuing && (Preferred == 0 || Collectors.Any(Child => Math.Sign(CellCross(Targets[Child!.Id], Horizontal) - Centre) != -Preferred)))
            {
                continue;
            }

            FlowchartCellRegion Original = Targets[Leaf.Id]; FlowchartCellRegion Best = Original;
            int BestDistance = Math.Abs(CellCross(Original, Horizontal) - Centre);
            for (int Step = 1; Step <= Nodes.Count + 1; Step++)
            {
                foreach (int Side in Continuing ? new[] { Preferred == 0 ? -1 : Preferred, Preferred == 0 ? 1 : -Preferred } : [Preferred])
            {
                int Cross = checked(Centre + (Side * Step * Pitch));
                int Distance = Math.Abs(Cross - Centre);
                if (Distance >= BestDistance)
                    {
                        continue;
                    }

                    FlowchartCellRegion Candidate = new FlowchartCellRegion(Horizontal ? Cross : Original.StartRow,
                    Horizontal ? Original.StartColumn : Cross, Original.RowSpan, Original.ColumnSpan);
                    // Pixel clearance is measured only after proposing whole cells;
                    // it never supplies a reservation coordinate or inverse snap.
                    FlowchartLayoutNodeBounds Bounds = Leaf.CellNode!.Bounds(Candidate, Leaf.GeometryProvider);
                if (Math.Abs((Horizontal ? Bounds.CenterY : Bounds.CenterX) - (Horizontal ? Parent.Y : Parent.X)) < CrossPitch)
                    {
                        continue;
                    }

                    Targets[Leaf.Id] = Candidate;
                if (Grid.TryPrepare(Targets, [], out _)) { Best = Candidate; BestDistance = Distance; }
                Targets[Leaf.Id] = Best;
            }
            }

            Targets[Leaf.Id] = Best;
        }
        return FlowchartCellRepair.TryAssign(Nodes, Targets);
    }

    /// <summary>Selects a logical region's starting cross-axis coordinate.</summary>
    /// <param name="Region">Proposed authoritative rectangle.</param>
    /// <param name="Horizontal">Whether cross-axis positions are rows.</param>
    /// <returns>An integer logical coordinate.</returns>
    private static int CellCross(FlowchartCellRegion Region, bool Horizontal) => Horizontal ? Region.StartRow : Region.StartColumn;
}
