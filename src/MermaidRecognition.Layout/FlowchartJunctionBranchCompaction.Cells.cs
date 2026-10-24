using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Compacts aligned junction branches through complete logical chain and collector plans.</summary>
internal static partial class FlowchartJunctionBranchCompaction
{
    /// <summary>Proposes nearest whole-cell side tracks, preserving occupied-rank order and measured body clearance.</summary>
    /// <param name="Contract">Visible graph, return classification and group exemption.</param>
    /// <param name="Nodes">Complete shared grid ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>Unpublished branch translations for complete routed scoring; collectors are never centred here.</returns>
    internal static IEnumerable<DiagramGridLayout.CellRouteMoveTarget> CellCandidates(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Contract.Groups.Count > 0) yield break;
        var Before = FlowchartCellRepair.Capture(Nodes); var Grid = Nodes[0].CellNode!.Grid;
        var Targets = new Dictionary<string, FlowchartCellRegion>(Before.Regions, StringComparer.Ordinal);
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !Back.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            && ById.ContainsKey(Edge.SourceId) && ById.ContainsKey(Edge.TargetId)).ToArray();
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle" && Node.GroupId == null))
        {
            var Outgoing = Edges.Where(Edge => Edge.SourceId == Junction.Id).ToArray();
            if (Outgoing.Length < 2) continue;
            int Centre = Cross(Targets[Junction.Id], Horizontal);
            foreach (var Branch in Outgoing.Select(Edge => ById[Edge.TargetId]).OrderBy(Node => Math.Abs(Cross(Targets[Node.Id], Horizontal) - Centre)))
            {
                if (Branch.GroupId != null || Branch.IsDiamond || Branch.Shape == "circle") continue;
                int Original = Cross(Targets[Branch.Id], Horizontal), Side = Math.Sign(Original - Centre);
                if (Side == 0) continue;
                var Chain = new HashSet<DiagramGridLayout.GridNode> { Branch }; var Pending = new Queue<DiagramGridLayout.GridNode>(); Pending.Enqueue(Branch);
                while (Pending.TryDequeue(out var Current))
                foreach (var Edge in Edges.Where(Edge => Edge.SourceId == Current.Id || Edge.TargetId == Current.Id))
                {
                    var Other = ById[Edge.SourceId == Current.Id ? Edge.TargetId : Edge.SourceId];
                    if (Other.GroupId != null || Other.Shape == "circle" || Other.IsDiamond || Cross(Targets[Other.Id], Horizontal) != Original || !Chain.Add(Other)) continue;
                    Pending.Enqueue(Other);
                }
                if (Chain.Any(Node => Node.PlacementHintLocked)) continue;
                for (int Distance = 1; Distance < Math.Abs(Original - Centre) && Distance <= FlowchartLayoutRules.RepairCandidateMoves; Distance++)
                {
                    int Candidate = checked(Centre + Side * Distance);
                    if (Chain.Any(Node => Nodes.Any(Other => !Chain.Contains(Other) && MainOverlap(Targets[Node.Id], Targets[Other.Id], Horizontal)
                        && (long)(Cross(Targets[Other.Id], Horizontal) - Original) * (Cross(Targets[Other.Id], Horizontal) - Candidate) <= 0))) continue;
                    var Trial = new Dictionary<string, FlowchartCellRegion>(Targets, StringComparer.Ordinal);
                    foreach (var Node in Chain)
                    {
                        var Region = Targets[Node.Id]; Trial[Node.Id] = new(Horizontal ? Candidate : Region.StartRow,
                            Horizontal ? Region.StartColumn : Candidate, Region.RowSpan, Region.ColumnSpan);
                    }
                    if (!Grid.TryPrepare(Trial, [], out _) || !Clear(Chain, Nodes, Trial, Horizontal)) continue;
                    yield return new($"junction|{Junction.Id}|{Branch.Id}|{Candidate}", Branch.Id,
                        Chain.OrderBy(Node => Node.Id, StringComparer.Ordinal).ToArray(),
                        Horizontal ? Candidate - Original : 0, Horizontal ? 0 : Candidate - Original);
                }
            }
        }
    }

    /// <summary>Checks forward-derived clearance without converting any measured drawing coordinate into cell ownership.</summary>
    /// <param name="Moving">Complete aligned branch.</param>
    /// <param name="Nodes">All obstacles.</param>
    /// <param name="Regions">Complete private candidate.</param>
    /// <param name="Horizontal">Whether the main axis is horizontal.</param>
    /// <returns>Whether every unrelated body retains the existing branch clearance.</returns>
    private static bool Clear(HashSet<DiagramGridLayout.GridNode> Moving, List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions, bool Horizontal)
    {
        foreach (var Node in Moving)
        foreach (var Other in Nodes.Where(Other => !Moving.Contains(Other)))
        {
            var A = Node.CellNode!.Bounds(Regions[Node.Id], Node.GeometryProvider);
            var B = Other.CellNode!.Bounds(Regions[Other.Id], Other.GeometryProvider);
            double Main = Math.Abs(Horizontal ? A.CenterX - B.CenterX : A.CenterY - B.CenterY);
            double Across = Math.Abs(Horizontal ? A.CenterY - B.CenterY : A.CenterX - B.CenterX);
            if (Main < (Horizontal ? A.Width + B.Width : A.Height + B.Height) / 2 + DiagramGridMetrics.NODE_GAP_PX
                && Across < (Horizontal ? A.Height + B.Height : A.Width + B.Width) / 2 + 2 * DiagramGridMetrics.NODE_GAP_PX) return false;
        }
        return true;
    }

    /// <summary>Tests whether two reserved main-axis intervals own a common rank strip.</summary>
    /// <param name="A">First region.</param>
    /// <param name="B">Second region.</param>
    /// <param name="Horizontal">Whether main-axis intervals use columns.</param>
    /// <returns>Whether the intervals overlap.</returns>
    private static bool MainOverlap(FlowchartCellRegion A, FlowchartCellRegion B, bool Horizontal) => Horizontal
        ? A.StartColumn < B.EndColumn && B.StartColumn < A.EndColumn : A.StartRow < B.EndRow && B.StartRow < A.EndRow;

    /// <summary>Selects the integer starting cross-axis coordinate.</summary>
    /// <param name="Region">Authoritative proposed reservation.</param>
    /// <param name="Horizontal">Whether the cross axis is rows.</param>
    /// <returns>The whole-cell cross-axis start.</returns>
    private static int Cross(FlowchartCellRegion Region, bool Horizontal) => Horizontal ? Region.StartRow : Region.StartColumn;
}
