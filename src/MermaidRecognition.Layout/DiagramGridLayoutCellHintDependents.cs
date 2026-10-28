using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Builds complete unpublished moving units for hint-preserving cell repairs.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Closes a node or group over every direct and transitive active placement-hint dependent.</summary>
    /// <param name="Contract">Declared membership and active placement relationships.</param>
    /// <param name="Placed">Complete cell-backed diagram.</param>
    /// <param name="Id">Initially nominated node or group.</param>
    /// <returns>Declared-order members of the complete moving set; no regions, gaps or hint states are changed.</returns>
    internal static GridNode[] CellHintDependentUnit(DiagramContract Contract, List<GridNode> Placed, string Id)
    {
        HashSet<string> Moving = Placed.Where(Node => Node.Id == Id || IsInsideGroup(Contract, Node.Id, Id))
            .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        (string Id, string? Peer)[] Hints =
        [
            .. Contract.Nodes.Where(Node => Node.PlacementHint?.IsActive == true)
                        .Select(Node => (Node.Id, Peer: Node.PlacementHint!.PeerId))
,
            .. Contract.Groups.Where(Group => Group.PlacementHint?.IsActive == true)
                    .Select(Group => (Group.Id, Peer: Group.PlacementHint!.PeerId)),
        ];
        bool Changed;
        do
        {
            Changed = false;
            foreach ((string Id, string? Peer) Hint in Hints)
            {
                if (Hint.Peer == null || !Placed.Any(Node => Moving.Contains(Node.Id)
                    && (Node.Id == Hint.Peer || IsInsideGroup(Contract, Node.Id, Hint.Peer))))
                {
                    continue;
                }

                foreach (GridNode? Node in Placed.Where(Node => Node.Id == Hint.Id || IsInsideGroup(Contract, Node.Id, Hint.Id)))
                {
                    Changed |= Moving.Add(Node.Id);
                }
            }
        } while (Changed);
        return [.. Placed.Where(Node => Moving.Contains(Node.Id))];
    }
}
