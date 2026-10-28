using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Maintains earlier group anchors when a later explicit member hint changes the group's logical envelope.</summary>
internal static partial class FlowchartPlacementHints
{
    /// <summary>Prepares compensating whole-group translations privately instead of rejecting a compatible member hint because its group minimum moved.</summary>
    /// <param name="Contract">Complete declared hint graph.</param>
    /// <param name="Nodes">Authoritative ownership participants.</param>
    /// <param name="ById">Declared node lookup.</param>
    /// <param name="Groups">Nested group membership.</param>
    /// <param name="Targets">Unpublished complete assignment; caller restores it on failure.</param>
    /// <param name="Accepted">Earlier source-ordered constraints.</param>
    /// <param name="Current">The member constraint that must remain satisfied too.</param>
    /// <returns>Whether every earlier constraint and the current constraint hold together without shared cells.</returns>
    private static bool TryRetainCellGroupAnchors(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById, IReadOnlyDictionary<string, GroupContract> Groups,
        Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyList<HintItem> Accepted, HintItem Current)
    {
        for (int Depth = 0; Depth < FlowchartLayoutRules.RepairChainDepth; Depth++)
        {
            HintItem[] Broken = [.. Accepted.Where(Item => !CellHintMatches(Item, Nodes, ById, Groups, Targets))];
            if (Broken.Length == 0)
            {
                return CellHintMatches(Current, Nodes, ById, Groups, Targets);
            }

            bool Changed = false;
            foreach (HintItem? Item in Broken.Where(Item => Item.IsGroup && Item.Hint.Kind != PlacementHintKind.Push))
            {
                List<DiagramGridLayout.GridNode> Members = UnitNodes(Item.Id, true, Nodes, ById, Groups);
                List<DiagramGridLayout.GridNode> Peer = UnitNodes(Item.Hint.PeerId ?? string.Empty, Groups.ContainsKey(Item.Hint.PeerId ?? string.Empty), Nodes, ById, Groups);
                DiagramGridLayout.GridNode[] Moving = DiagramGridLayout.CellHintDependentUnit(Contract, Nodes, Item.Id);
                HashSet<string> Ids = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                if (Members.Count == 0 || Peer.Count == 0 || Peer.Any(Node => Ids.Contains(Node.Id)))
                {
                    continue;
                }

                FlowchartCellRegion Owner = CellEnvelope(Members, Targets); FlowchartCellRegion Anchor = CellEnvelope(Peer, Targets);
                int Rows = 0, Columns = 0, RowDirection = 0, ColumnDirection = 0;
                if (Item.Hint.IsDiagonal)
                {
                    var Target = DiagonalTarget(Item.Hint, Owner, Anchor);
                    (RowDirection, ColumnDirection) = DiagonalSigns(Item.Hint.Kind);
                    Rows = RowDirection < 0 ? Math.Min(0, checked(Target.StartRow - Owner.StartRow))
                        : Math.Max(0, checked(Target.StartRow - Owner.StartRow));
                    Columns = ColumnDirection < 0 ? Math.Min(0, checked(Target.StartColumn - Owner.StartColumn))
                        : Math.Max(0, checked(Target.StartColumn - Owner.StartColumn));
                }
                switch (Item.Hint.Kind)
                {
                    case PlacementHintKind.Above:
                        Rows = Math.Min(0, checked(Anchor.StartRow - Owner.EndRow - Item.Hint.Offset));
                        Columns = Anchor.StartColumn - Owner.StartColumn; RowDirection = -1; break;
                    case PlacementHintKind.Below:
                        Rows = Math.Max(0, checked(Anchor.EndRow - Owner.StartRow + Item.Hint.Offset));
                        Columns = Anchor.StartColumn - Owner.StartColumn; RowDirection = 1; break;
                    case PlacementHintKind.LeftOf:
                        Columns = Math.Min(0, checked(Anchor.StartColumn - Owner.EndColumn - Item.Hint.Offset));
                        Rows = Anchor.StartRow - Owner.StartRow; ColumnDirection = -1; break;
                    case PlacementHintKind.RightOf:
                        Columns = Math.Max(0, checked(Anchor.EndColumn - Owner.StartColumn + Item.Hint.Offset));
                        Rows = Anchor.StartRow - Owner.StartRow; ColumnDirection = 1; break;
                }
                DiagramGridLayout.GridNode[] Others = [.. Nodes.Where(Node => !Ids.Contains(Node.Id))];
                Dictionary<string, FlowchartCellRegion> Changes;
                // As in ordinary explicit hint placement, advance only along
                // the requested direction past the finite occupied regions.
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

                    Rows = checked(Rows + RowDirection); Columns = checked(Columns + ColumnDirection);
                }
                foreach (KeyValuePair<string, FlowchartCellRegion> Change in Changes)
                {
                    Targets[Change.Key] = Change.Value;
                }

                Changed |= Rows != 0 || Columns != 0;
            }
            if (!Changed)
            {
                return false;
            }
        }
        return Accepted.Append(Current).All(Item => CellHintMatches(Item, Nodes, ById, Groups, Targets));
    }
}
