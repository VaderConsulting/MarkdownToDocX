using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans explicit placement in logical cells before publishing any derived geometry.</summary>
internal static partial class FlowchartPlacementHints
{
    /// <summary>Checks honoured logical relationships for a complete repair, allowing legal gap growth without converting hints into pixel pins.</summary>
    /// <param name="Contract">Active explicit placement relationships.</param>
    /// <param name="Nodes">All diagram members sharing the candidate grid.</param>
    /// <param name="Regions">Complete proposed ownership.</param>
    /// <returns>Whether every nonempty hinted unit retains its start alignment, direction and minimum whole-cell offset.</returns>
    internal static bool CellHintsMatch(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
    {
        using IDisposable? CostTiming = FlowchartCellCostTiming.Begin("hint-checks");
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        IEnumerable<HintItem> Items = Contract.Nodes.Where(Node => Node.PlacementHint?.IsActive == true && ById.ContainsKey(Node.Id))
            .Select(Node => new HintItem(Node.Id, Node.ParentGroupId, Node.PlacementHint!, false))
            .Concat(Contract.Groups.Where(Group => Group.PlacementHint?.IsActive == true)
                .Select(Group => new HintItem(Group.Id, Group.ParentGroupId, Group.PlacementHint!, true)));
        return Items.All(Item => UnitNodes(Item.Id, Item.IsGroup, Nodes, ById, Groups).Count == 0
            || CellHintMatches(Item, Nodes, ById, Groups, Regions));
    }

    /// <summary>Applies source-ordered hints as one complete ownership transaction; automatic placement locks cannot veto explicit hints.</summary>
    /// <param name="Contract">Declared hints and group membership.</param>
    /// <param name="Nodes">Complete diagram sharing one authoritative grid.</param>
    /// <returns>Whether the accepted plan changes any reservation.</returns>
    private static bool ApplyCells(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Nodes);
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        HintItem[] Hints = [.. Contract.Nodes.Where(Node => Node.PlacementHint?.IsActive == true && ById.ContainsKey(Node.Id))
            .Select(Node => new HintItem(Node.Id, Node.ParentGroupId, Node.PlacementHint!, false))
            .Concat(Contract.Groups.Where(Group => Group.PlacementHint?.IsActive == true)
                .Select(Group => new HintItem(Group.Id, Group.ParentGroupId, Group.PlacementHint!, true)))
            .OrderBy(Item => Item.Hint.SourceLine)];
        Dictionary<string, List<string>> Attached = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<(PlacementHintContract Hint, string Reason)> Rejections = [];
        List<HintItem> Accepted = [];
        foreach (HintItem? Item in Hints)
        {
            List<DiagramGridLayout.GridNode> Owner = UnitNodes(Item.Id, Item.IsGroup, Nodes, ById, Groups);
            // Empty group frames have no node occupancy. Their existing frame
            // placement remains responsible for the declared relationship.
            if (Owner.Count == 0)
            {
                continue;
            }

            List<DiagramGridLayout.GridNode> Moving = AttachedUnitNodes(Item.Id, Item.IsGroup, Attached, Nodes, ById, Groups);
            HashSet<string> MovingIds = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
            FlowchartCellRegion OwnerRegion = CellEnvelope(Owner, Targets);
            DiagramGridLayout.GridNode[] Others = [.. Nodes.Where(Node => !MovingIds.Contains(Node.Id))];
            int Rows = 0, Columns = 0, RowDirection = 0, ColumnDirection = 0;
            string? PeerId = Item.Hint.PeerId;
            if (Item.Hint.Kind == PlacementHintKind.Push)
            {
                DiagramGridLayout.GridNode[] Siblings = [.. Others.Where(Node => string.Equals(Node.GroupId, Item.ParentId, StringComparison.Ordinal))];
                if (Siblings.Length == 0)
                {
                    continue;
                }

                FlowchartCellRegion Boundary = CellEnvelope(Siblings, Targets);
                switch (Item.Hint.Direction)
                {
                    case LayoutHintDirection.North: Rows = Boundary.StartRow - OwnerRegion.EndRow; RowDirection = -1; break;
                    case LayoutHintDirection.South: Rows = Boundary.EndRow - OwnerRegion.StartRow; RowDirection = 1; break;
                    case LayoutHintDirection.West: Columns = Boundary.StartColumn - OwnerRegion.EndColumn; ColumnDirection = -1; break;
                    case LayoutHintDirection.East: Columns = Boundary.EndColumn - OwnerRegion.StartColumn; ColumnDirection = 1; break;
                    default: Rejections.Add((Item.Hint, "push hint has no direction")); continue;
                }
            }
            else
            {
                List<DiagramGridLayout.GridNode> Peer = UnitNodes(PeerId ?? string.Empty, Groups.ContainsKey(PeerId ?? string.Empty), Nodes, ById, Groups);
                if (Peer.Count == 0 || Peer.Any(Node => MovingIds.Contains(Node.Id)))
                {
                    Rejections.Add((Item.Hint, "placement peer has no independent cell region"));
                    continue;
                }
                FlowchartCellRegion Anchor = CellEnvelope(Peer, Targets);
                if (Item.Hint.IsDiagonal)
                {
                    var Target = DiagonalTarget(Item.Hint, OwnerRegion, Anchor);
                    Rows = checked(Target.StartRow - OwnerRegion.StartRow);
                    Columns = checked(Target.StartColumn - OwnerRegion.StartColumn);
                    (RowDirection, ColumnDirection) = DiagonalSigns(Item.Hint.Kind);
                }
                // Relative hints align starting rows/columns, even when the
                // two units have different spans. +n reserves n whole cells.
                switch (Item.Hint.Kind)
                {
                    case PlacementHintKind.Above:
                        Rows = checked(Anchor.StartRow - OwnerRegion.EndRow - Item.Hint.Offset);
                        Columns = Anchor.StartColumn - OwnerRegion.StartColumn; RowDirection = -1; break;
                    case PlacementHintKind.Below:
                        Rows = checked(Anchor.EndRow - OwnerRegion.StartRow + Item.Hint.Offset);
                        Columns = Anchor.StartColumn - OwnerRegion.StartColumn; RowDirection = 1; break;
                    case PlacementHintKind.LeftOf:
                        Columns = checked(Anchor.StartColumn - OwnerRegion.EndColumn - Item.Hint.Offset);
                        Rows = Anchor.StartRow - OwnerRegion.StartRow; ColumnDirection = -1; break;
                    case PlacementHintKind.RightOf:
                        Columns = checked(Anchor.EndColumn - OwnerRegion.StartColumn + Item.Hint.Offset);
                        Rows = Anchor.StartRow - OwnerRegion.StartRow; ColumnDirection = 1; break;
                }
            }
            Dictionary<string, FlowchartCellRegion> Changes;
            while (true)
            {
                Changes = Moving.ToDictionary(Node => Node.Id, Node =>
                {
                    FlowchartCellRegion Region = Targets[Node.Id];
                    return new FlowchartCellRegion(checked(Region.StartRow + Rows), checked(Region.StartColumn + Columns),
                        Region.RowSpan, Region.ColumnSpan);
                }, StringComparer.Ordinal);
                if (!Changes.Values.Any(Region => Others.Any(Node => Region.Overlaps(Targets[Node.Id]))))
                {
                    break;
                }
                // Each rejection advances along the requested direction. The
                // finite set of occupied regions bounds this monotone search.
                Rows = checked(Rows + RowDirection); Columns = checked(Columns + ColumnDirection);
            }
            Dictionary<string, FlowchartCellRegion> Previous = new Dictionary<string, FlowchartCellRegion>(Targets, StringComparer.Ordinal);
            foreach (KeyValuePair<string, FlowchartCellRegion> Change in Changes)
            {
                Targets[Change.Key] = Change.Value;
            }

            if (Grid.RegionConvention?.Invoke(Targets) == false)
            {
                Targets = Previous;
                Rejections.Add((Item.Hint, "Rule 2.23 convention: hint ignored under rule 2.21(g) because it changes a layer band or plateau order"));
                continue;
            }
            if (Accepted.Any(Prior => !CellHintMatches(Prior, Nodes, ById, Groups, Targets))
                && !TryRetainCellGroupAnchors(Contract, Nodes, ById, Groups, Targets, Accepted, Item))
            {
                Targets = Previous;
                Rejections.Add((Item.Hint, "placement conflicts with an earlier explicit hint"));
                continue;
            }
            if (Grid.RegionConvention?.Invoke(Targets) == false)
            {
                Targets = Previous;
                Rejections.Add((Item.Hint, "Rule 2.23 convention: hint ignored under rule 2.21(g) because it changes a layer band or plateau order"));
                continue;
            }
            Accepted.Add(Item);
            if (Item.Hint.Kind != PlacementHintKind.Push && PeerId != null)
            {
                if (!Attached.TryGetValue(PeerId, out List<string>? Dependents))
                {
                    Attached[PeerId] = Dependents = [];
                }

                Dependents.Add(Item.Id);
            }
        }
        // Commit all nodes together, deliberately bypassing automatic repair
        // locks: these are explicit hints, not speculative unhinted moves.
        if (!Grid.TryPrepare(Targets, [], out FlowchartLogicalGrid.Snapshot? After) || !Grid.TryCommit(Before, After!))
        {
            throw new InvalidOperationException("The complete hinted cell assignment could not be applied; original regions and gaps remain unchanged.");
        }

        foreach ((PlacementHintContract Hint, string Reason) Rejection in Rejections)
        {
            Reject(Rejection.Hint, Rejection.Reason);
        }

        return Targets.Any(Pair => Pair.Value != Before.Regions[Pair.Key]);
    }

    /// <summary>Protects source-earlier relationships when a later unit move would otherwise silently invalidate them.</summary>
    /// <param name="Item">An already accepted explicit hint.</param>
    /// <param name="Nodes">All diagram members.</param>
    /// <param name="ById">Node lookup.</param>
    /// <param name="Groups">Nested group lookup.</param>
    /// <param name="Regions">Complete proposed ownership.</param>
    /// <returns>Whether the prior hint still holds with whole-cell offset and start alignment.</returns>
    private static bool CellHintMatches(HintItem Item, List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById, IReadOnlyDictionary<string, GroupContract> Groups,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
    {
        List<DiagramGridLayout.GridNode> Members = UnitNodes(Item.Id, Item.IsGroup, Nodes, ById, Groups);
        FlowchartCellRegion Owner = CellEnvelope(Members, Regions);
        List<DiagramGridLayout.GridNode> Peers = Item.Hint.Kind == PlacementHintKind.Push
            ? [.. Nodes.Where(Node => !Members.Contains(Node) && string.Equals(Node.GroupId, Item.ParentId, StringComparison.Ordinal))]
            : UnitNodes(Item.Hint.PeerId!, Groups.ContainsKey(Item.Hint.PeerId!), Nodes, ById, Groups);
        if (Peers.Count == 0)
        {
            return true;
        }

        FlowchartCellRegion Peer = CellEnvelope(Peers, Regions);
        if (Item.Hint.IsDiagonal)
        {
            var (Row, Column) = DiagonalSigns(Item.Hint.Kind);
            return (Row < 0 ? (long)Owner.EndRow <= (long)Peer.StartRow - Item.Hint.Offset
                    : (long)Owner.StartRow >= (long)Peer.EndRow + Item.Hint.Offset)
                && (Column < 0 ? (long)Owner.EndColumn <= (long)Peer.StartColumn - (Item.Hint.ColumnOffset ?? Item.Hint.Offset)
                    : (long)Owner.StartColumn >= (long)Peer.EndColumn + (Item.Hint.ColumnOffset ?? Item.Hint.Offset));
        }
        if (Item.Hint.Kind == PlacementHintKind.Push)
        {
            return Item.Hint.Direction switch
        {
            LayoutHintDirection.North => Owner.EndRow <= Peer.StartRow,
            LayoutHintDirection.South => Owner.StartRow >= Peer.EndRow,
            LayoutHintDirection.West => Owner.EndColumn <= Peer.StartColumn,
            LayoutHintDirection.East => Owner.StartColumn >= Peer.EndColumn,
            _ => false
        };
        }

        return Item.Hint.Kind switch
        {
            PlacementHintKind.Above => Owner.StartColumn == Peer.StartColumn && Owner.EndRow <= Peer.StartRow - Item.Hint.Offset,
            PlacementHintKind.Below => Owner.StartColumn == Peer.StartColumn && Owner.StartRow >= Peer.EndRow + Item.Hint.Offset,
            PlacementHintKind.LeftOf => Owner.StartRow == Peer.StartRow && Owner.EndColumn <= Peer.StartColumn - Item.Hint.Offset,
            PlacementHintKind.RightOf => Owner.StartRow == Peer.StartRow && Owner.StartColumn >= Peer.EndColumn + Item.Hint.Offset,
            _ => false
        };
    }

    /// <summary>Measures a unit's logical reservation without reading node centres, frame pixels or derived sizes.</summary>
    /// <param name="Members">Nonempty node or nested group membership.</param>
    /// <param name="Regions">Private complete candidate ownership.</param>
    /// <returns>The contiguous envelope used only to align and translate the unit.</returns>
    private static FlowchartCellRegion CellEnvelope(IReadOnlyList<DiagramGridLayout.GridNode> Members,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
    {
        int Top = Members.Min(Node => Regions[Node.Id].StartRow), Left = Members.Min(Node => Regions[Node.Id].StartColumn);
        return new(Top, Left, Members.Max(Node => Regions[Node.Id].EndRow) - Top,
            Members.Max(Node => Regions[Node.Id].EndColumn) - Left);
    }
}
