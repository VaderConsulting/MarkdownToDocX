using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans logical placement alignments without assigning derived pixel positions.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Classifies shared fan roots without forcing midpoint placement or tail alignment.</summary>
    /// <param name="Contract">Visible graph and return classification.</param>
    /// <param name="Placed">Complete shared ownership.</param>
    /// <param name="Horizontal">Whether cross-axis starts use rows.</param>
    /// <returns>Topologically eligible shared roots; no region, gap or ordering metadata changes.</returns>
    internal static HashSet<string> SelectCellSharedFanSources(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        var ById = IndexPlaced(Placed);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Visible = Contract.Edges.Where(Edge => !Back.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        var Selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var Source in Placed.Where(Node => !Node.IsDiamond && !Visible.Any(Edge => Edge.TargetId == Node.Id)))
        {
            var Children = Visible.Where(Edge => Edge.SourceId == Source.Id && ById.ContainsKey(Edge.TargetId)).Select(Edge => ById[Edge.TargetId]).Distinct().ToArray();
            if (Children.Length < 2 || Children.Select(Node => Node.Rank).Distinct().Count() != 1
                || Children.Any(Node => Node.IsDiamond || Node.Rank == Source.Rank || Node.GroupId != Source.GroupId)) continue;
            if (Children.All(Child =>
                {
                    var Others = Visible.Where(Edge => Edge.TargetId == Child.Id && Edge.SourceId != Source.Id).ToArray();
                    return Others.Length > 0 && Others.All(Edge => ById.TryGetValue(Edge.SourceId, out var Parent)
                        && Parent.Rank == Source.Rank && Parent.GroupId == Source.GroupId && !Parent.IsDiamond
                        && !Visible.Any(Incoming => Incoming.TargetId == Parent.Id) && Visible.Count(Outgoing => Outgoing.SourceId == Parent.Id) == 1);
                })) Selected.Add(Source.Id);
        }
        // Keep topology classification, not midpoint or tail-position writes.
        // Whole-cell source/tail candidates compete in the routed score.
        return Selected;
    }

    /// <summary>Finds a complete sole-output tail without crossing a decision, merge or group boundary.</summary>
    /// <param name="Start">First member, included in the returned chain.</param>
    /// <param name="ById">Present node lookup.</param>
    /// <param name="Edges">Caller-selected visible or forward relationships.</param>
    /// <param name="RequireRankChange">Whether a same-rank continuation terminates this caller's chain.</param>
    /// <returns>Traversal-ordered members; ownership has not changed.</returns>
    private static IReadOnlyList<GridNode> CellOutputChain(GridNode Start, IReadOnlyDictionary<string, GridNode> ById,
        IReadOnlyList<EdgeContract> Edges, bool RequireRankChange = true)
    {
        var Result = new List<GridNode>(); var Seen = new HashSet<string>(StringComparer.Ordinal); var Current = Start;
        while (Seen.Add(Current.Id))
        {
            Result.Add(Current);
            var Next = Edges.Where(Edge => Edge.SourceId == Current.Id).ToArray();
            if (Next.Length != 1 || !ById.TryGetValue(Next[0].TargetId, out var Child) || Child.IsDiamond
                || Child.GroupId != Start.GroupId || RequireRankChange && Child.Rank == Current.Rank
                || Edges.Count(Edge => Edge.TargetId == Child.Id) != 1) break;
            Current = Child;
        }
        return Result;
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

}