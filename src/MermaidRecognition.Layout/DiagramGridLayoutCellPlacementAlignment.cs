using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans logical placement alignments without assigning derived pixel positions.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Plans independent odd input fans in neighbouring whole-cell lanes while retaining anchored inputs.</summary>
    /// <param name="Contract">Visible incoming and upstream relationships.</param>
    /// <param name="Placed">Complete shared grid ownership.</param>
    /// <param name="Horizontal">Whether input lanes use logical rows.</param>
    /// <returns>Whether the complete legal fan plan was accepted.</returns>
    internal static bool CompactCellIndependentFanIn(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var Target in Placed)
        {
            var Incoming = Visible.Where(Edge => Edge.TargetId == Target.Id).ToArray();
            if (Incoming.Length < 3 || Incoming.Length % 2 == 0 || Incoming.Select(Edge => Edge.SourceId).Distinct().Count() != Incoming.Length) continue;
            var Parents = Incoming.Where(Edge => ById.ContainsKey(Edge.SourceId)).Select(Edge => ById[Edge.SourceId])
                .OrderBy(Node => Horizontal ? Targets[Node.Id].StartRow : Targets[Node.Id].StartColumn).ToArray();
            if (Parents.Length != Incoming.Length || Parents.Select(Node => Node.Rank).Distinct().Count() != 1
                || Parents.Any(Parent => Parent.Rank == Target.Rank || Parent.GroupId != Target.GroupId || Visible.Count(Edge => Edge.SourceId == Parent.Id) != 1)
                || Placed.Any(Node => Node.Rank == Parents[0].Rank && !Parents.Contains(Node))) continue;
            int Centre = Horizontal ? Targets[Target.Id].StartRow : Targets[Target.Id].StartColumn;
            int Pitch = Parents.Max(Node => Horizontal ? Targets[Node.Id].RowSpan : Targets[Node.Id].ColumnSpan);
            var Anchored = Parents.Where(Node => Node.PlacementHintLocked || Visible.Any(Edge => Edge.TargetId == Node.Id)).ToArray();
            foreach (var Parent in Anchored)
            {
                int Offset = Array.IndexOf(Parents, Parent) - Parents.Length / 2;
                int Distance = (Horizontal ? Targets[Parent.Id].StartRow : Targets[Parent.Id].StartColumn) - Centre;
                if (Offset != 0 && Distance % Offset == 0) Pitch = Math.Max(Pitch, Distance / Offset);
            }
            if (Anchored.Any(Parent => (Horizontal ? Targets[Parent.Id].StartRow : Targets[Parent.Id].StartColumn)
                != checked(Centre + (Array.IndexOf(Parents, Parent) - Parents.Length / 2) * Pitch))) continue;
            for (int Index = 0; Index < Parents.Length; Index++)
            {
                var Parent = Parents[Index]; var Region = Targets[Parent.Id];
                int Cross = checked(Centre + (Index - Parents.Length / 2) * Pitch);
                Targets[Parent.Id] = new(Horizontal ? Cross : Region.StartRow, Horizontal ? Region.StartColumn : Cross, Region.RowSpan, Region.ColumnSpan);
            }
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Assigns two terminal decision outcomes to opposite logical side lanes as one exclusive reservation plan.</summary>
    /// <param name="Contract">Graph used to select sole-input terminal children.</param>
    /// <param name="Placed">Complete diagram sharing one authoritative grid.</param>
    /// <param name="Horizontal">Whether cross-axis side lanes use rows.</param>
    /// <returns>Whether all selected child reservations were accepted together.</returns>
    internal static bool AlignCellTerminalDecisionTargets(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Ordering = new Dictionary<GridNode, int>();
        foreach (var Parent in Placed.Where(Node => Node.IsDiamond))
        {
            var Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToArray();
            if (Outgoing.Length != 2 || Outgoing[0].TargetId == Outgoing[1].TargetId) continue;
            var Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).ToArray();
            if (Children.Length != 2 || Children[0].Rank != Children[1].Rank
                || Children.Any(Child => Child.Rank == Parent.Rank || Child.GroupId != Parent.GroupId
                    || Visible.Any(Edge => Edge.SourceId == Child.Id) || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1)) continue;
            var Ordered = Children.OrderBy(Child => Child.Column).ThenBy(Child => Child.Id, StringComparer.Ordinal).ToArray();
            var Anchor = Targets[Parent.Id];
            for (int Index = 0; Index < Ordered.Length; Index++)
            {
                var Child = Ordered[Index];
                if (Child.PlacementHintLocked) continue;
                var Region = Targets[Child.Id];
                int Cross = Horizontal ? (Index == 0 ? checked(Anchor.StartRow - Region.RowSpan) : Anchor.EndRow)
                    : (Index == 0 ? checked(Anchor.StartColumn - Region.ColumnSpan) : Anchor.EndColumn);
                Targets[Child.Id] = new(Horizontal ? Cross : Region.StartRow,
                    Horizontal ? Region.StartColumn : Cross, Region.RowSpan, Region.ColumnSpan);
                Ordering[Child] = Parent.Column + (Index == 0 ? -1 : 1);
            }
        }
        if (!FlowchartCellRepair.TryAssign(Placed, Targets)) return false;
        foreach (var Pair in Ordering) Pair.Key.Column = Pair.Value;
        return true;
    }

    /// <summary>Centres an odd terminal fan and its unbranched input chain by one complete logical reservation plan.</summary>
    /// <param name="Contract">Visible graph and common-group restrictions.</param>
    /// <param name="Placed">All diagram nodes sharing cell authority.</param>
    /// <param name="Horizontal">Whether cross-axis alignment uses rows.</param>
    /// <returns>Whether all proposed reservations were legal and accepted together.</returns>
    internal static bool CentreCellTerminalFans(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Ordering = new Dictionary<GridNode, int>();
        foreach (var Parent in Placed.Where(Node => !Node.IsDiamond))
        {
            var Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToArray();
            if (Outgoing.Length < 3 || Outgoing.Length % 2 == 0 || Outgoing.Select(Edge => Edge.TargetId).Distinct().Count() != Outgoing.Length) continue;
            var Children = Outgoing.Where(Edge => ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).ToArray();
            if (Children.Length != Outgoing.Length || Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Child => Child.Rank == Parent.Rank || Child.GroupId != Parent.GroupId
                    || Visible.Any(Edge => Edge.SourceId == Child.Id) || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1)) continue;
            var Middle = Children.OrderBy(Node => Horizontal ? Targets[Node.Id].StartRow : Targets[Node.Id].StartColumn).ElementAt(Children.Length / 2);
            foreach (var Node in PlanCellInputChain(Parent, Targets[Middle.Id], Targets, ById, Visible, Horizontal)) Ordering[Node] = Middle.Column;
        }
        if (!FlowchartCellRepair.TryAssign(Placed, Targets)) return false;
        foreach (var Pair in Ordering) Pair.Key.Column = Pair.Value;
        return true;
    }

    /// <summary>Plans each unbranched upstream chain against its decision's logical cross-axis start, with complete collision rejection.</summary>
    /// <param name="Contract">Graph whose return edges do not determine forward alignment.</param>
    /// <param name="Placed">Complete diagram ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>Whether the complete assignment was legal and accepted.</returns>
    internal static bool AlignCellDecisionInputs(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Forward = Contract.Edges.Where(Edge => !BackEdges.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var Decision in Placed.Where(Node => Node.IsDiamond))
        {
            var Incoming = Forward.Where(Edge => Edge.TargetId == Decision.Id).ToArray();
            if (Incoming.Length != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Input)
                || Input.IsDiamond || Input.GroupId != Decision.GroupId || Forward.Count(Edge => Edge.SourceId == Input.Id) != 1) continue;
            PlanCellInputChain(Input, Targets[Decision.Id], Targets, ById, Forward, Horizontal);
        }
        return FlowchartCellRepair.TryAssign(Placed, Targets);
    }

    /// <summary>Plans an upstream chain on a logical cross-axis start, stopping before any hint, branch, decision or group boundary.</summary>
    /// <param name="Start">First member to align.</param>
    /// <param name="Anchor">Declared logical alignment reference.</param>
    /// <param name="Targets">Private complete ownership plan.</param>
    /// <param name="ById">All present nodes.</param>
    /// <param name="Edges">Caller-selected visible or forward edges.</param>
    /// <param name="Horizontal">Whether the cross axis is rows.</param>
    /// <returns>Members planned for alignment; the caller updates ordering only after publishing the complete plan.</returns>
    private static IReadOnlyList<GridNode> PlanCellInputChain(GridNode Start, FlowchartCellRegion Anchor,
        Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyDictionary<string, GridNode> ById,
        IReadOnlyList<EdgeContract> Edges, bool Horizontal)
    {
        var Visited = new HashSet<string>(StringComparer.Ordinal); var Aligned = new List<GridNode>();
        var Current = Start;
        while (Visited.Add(Current.Id))
        {
            if (Current.PlacementHintLocked) break;
            var Region = Targets[Current.Id];
            Targets[Current.Id] = new(Horizontal ? Anchor.StartRow : Region.StartRow,
                Horizontal ? Region.StartColumn : Anchor.StartColumn, Region.RowSpan, Region.ColumnSpan);
            Aligned.Add(Current);
            var Incoming = Edges.Where(Edge => Edge.TargetId == Current.Id).ToArray();
            if (Incoming.Length != 1 || !ById.TryGetValue(Incoming[0].SourceId, out var Previous)
                || Previous.IsDiamond || Previous.GroupId != Start.GroupId || Edges.Count(Edge => Edge.SourceId == Previous.Id) != 1) break;
            Current = Previous;
        }
        return Aligned;
    }

    /// <summary>Plans every sole terminal child's cross-axis reservation before applying the complete assignment.</summary>
    /// <param name="Contract">Visible graph used by the existing terminal-child selection.</param>
    /// <param name="Placed">Complete shared cell ownership.</param>
    /// <param name="Horizontal">Whether cross-axis alignment uses rows.</param>
    /// <returns>Whether the complete legal plan was accepted.</returns>
    internal static bool AlignCellTerminalChildren(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Placed);
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = IndexPlaced(Placed);
        var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Aligned = new List<(GridNode Parent, GridNode Child)>();
        foreach (var Parent in Placed)
        {
            var Outgoing = Visible.Where(Edge => Edge.SourceId == Parent.Id).ToArray();
            if (Outgoing.Length != 1 || !ById.TryGetValue(Outgoing[0].TargetId, out var Child)
                || Child.PlacementHintLocked || Child.GroupId != Parent.GroupId || Child.Rank == Parent.Rank
                || Visible.Count(Edge => Edge.TargetId == Child.Id) != 1 || Visible.Any(Edge => Edge.SourceId == Child.Id)) continue;
            var Region = Targets[Child.Id]; var Anchor = Targets[Parent.Id];
            Targets[Child.Id] = new(Horizontal ? Anchor.StartRow : Region.StartRow,
                Horizontal ? Region.StartColumn : Anchor.StartColumn, Region.RowSpan, Region.ColumnSpan);
            Aligned.Add((Parent, Child));
        }
        if (!FlowchartCellRepair.TryAssign(Placed, Targets)) return false;
        // Logical ordering metadata follows only an accepted complete plan.
        foreach (var Pair in Aligned) Pair.Child.Column = Pair.Parent.Column;
        return true;
    }
}
