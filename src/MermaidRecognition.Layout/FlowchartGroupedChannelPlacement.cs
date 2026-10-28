using MermaidRecognition.Contract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Places complete many-to-many sibling groups beside a measured shared routing channel.</summary>
internal static partial class FlowchartGroupedChannelPlacement
{
    /// <summary>Aligns unhinted source and target groups whose members form a complete bipartite block.</summary>
    /// <param name="Contract">The graph, group membership and authoritative hints.</param>
    /// <param name="Nodes">Mutable grid placement before frame fitting and port allocation.</param>
    /// <returns>True when at least one complete block was placed.</returns>
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id);
        Dictionary<string, NodeContract> Definitions = Contract.Nodes.ToDictionary(Node => Node.Id);
        EdgeContract[] Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        ILookup<string, EdgeContract> Outgoing = Edges.ToLookup(Edge => Edge.SourceId);
        ILookup<string, EdgeContract> Incoming = Edges.ToLookup(Edge => Edge.TargetId);
        HashSet<string> Used = [];
        bool Changed = false;
        foreach (DiagramGridLayout.GridNode Seed in Nodes)
        {
            if (Used.Contains(Seed.Id) || Seed.GroupId == null)
            {
                continue;
            }

            HashSet<string> TargetIds = [.. Outgoing[Seed.Id].Select(Edge => Edge.TargetId).Distinct()];
            if (TargetIds.Count < 2 || TargetIds.Any(Id => !ById.ContainsKey(Id)))
            {
                continue;
            }

            HashSet<string> SourceIds = [.. Incoming[TargetIds.First()].Select(Edge => Edge.SourceId).Distinct()];
            if (SourceIds.Count < 2 || SourceIds.Any(Id => !ById.ContainsKey(Id)) || SourceIds.Overlaps(TargetIds))
            {
                continue;
            }

            DiagramGridLayout.GridNode[] Sources = [.. Nodes.Where(Node => SourceIds.Contains(Node.Id))];
            DiagramGridLayout.GridNode[] Targets = [.. Nodes.Where(Node => TargetIds.Contains(Node.Id))];
            string? SourceGroup = Seed.GroupId, TargetGroup = Targets[0].GroupId;
            if (TargetGroup == null || SourceGroup == TargetGroup
                || Sources.Any(Node => Node.GroupId != SourceGroup || !Outgoing[Node.Id].Select(Edge => Edge.TargetId).ToHashSet().SetEquals(TargetIds))
                || Targets.Any(Node => Node.GroupId != TargetGroup || !Incoming[Node.Id].Select(Edge => Edge.SourceId).ToHashSet().SetEquals(SourceIds))
                || Nodes.Count(Node => Node.GroupId == SourceGroup) != Sources.Length
                || Nodes.Count(Node => Node.GroupId == TargetGroup) != Targets.Length)
            {
                continue;
            }

            DiagramGridLayout.GridNode[] Members = [.. Sources, .. Targets];
            if (Members.Any(Node => Node.PlacementHintLocked || Definitions[Node.Id].PlacementHint?.IsActive == true)
                || Contract.Groups.Any(Group => Group.PlacementHint?.IsActive == true
                    && Members.Any(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Group.Id))))
            {
                continue;
            }

            if (Members.Any(Node => Node.CellNode != null))
            {
                if (ApplyCellBlock(Nodes, Sources, Targets))
                {
                    foreach (DiagramGridLayout.GridNode? Node in Members)
                    {
                        Used.Add(Node.Id);
                    }

                    Changed = true;
                }
                continue;
            }

            // Capacity is measured before frames are fitted. All rectangular
            // nodes retain common dimensions when the busy sides need growth.
            int Height = Math.Max(Members.Max(Node => Node.Height), DiagramGridMetrics.CeilToGrid(
                (Math.Max(Sources.Length, Targets.Length) + 1) * DiagramGridMetrics.GRID_UNIT_PX));
            foreach (DiagramGridLayout.GridNode? Node in Nodes.Where(Node => Definitions[Node.Id].Shape is "rect" or "rounded"))
            {
                Node.Height = Math.Max(Node.Height, Height);
            }

            int Width = Members.Max(Node => Node.Width);
            int RowPitch = DiagramGridMetrics.CeilToGrid(Height + DiagramGridMetrics.GRID_UNIT_PX);
            int BentTracks = (Sources.Length * Targets.Length) - Math.Min(Sources.Length, Targets.Length);
            int Channel = DiagramGridMetrics.CeilToGrid((4 * DiagramGridRouteBuilder.CORNER_RADIUS_PX)
                + (Math.Max(0, BentTracks - 1) * DiagramGridMetrics.GRID_UNIT_PX));
            double Left = Members.Min(Node => Node.X);
            double Top = Members.Max(Node => Node.Y);
            for (int Index = 0; Index < Sources.Length; Index++)
            {
                Sources[Index].SetPositionFromFrameFit(Left, Top - (Index * RowPitch));
            }

            for (int Index = 0; Index < Targets.Length; Index++)
            {
                Targets[Index].SetPositionFromFrameFit(Left + Width + Channel + (2 * DiagramGridMetrics.GRID_UNIT_PX), Top - (Index * RowPitch));
            }

            foreach (DiagramGridLayout.GridNode? Node in Members)
            {
                Used.Add(Node.Id);
            }

            Changed = true;
        }
        return Changed;
    }
}
