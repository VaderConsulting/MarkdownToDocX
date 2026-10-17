using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans circular fan junction alignment on logical starts without pixel centre writes.</summary>
internal static partial class FlowchartCircularJunctionLayout
{
    /// <summary>Plans both fan-out input chains and fan-in output chains before publishing any ownership changes.</summary>
    /// <param name="Contract">Graph and return-edge classification.</param>
    /// <param name="Nodes">Complete shared cell ownership.</param>
    /// <param name="Horizontal">Whether the cross axis uses logical rows.</param>
    /// <returns>Whether the complete legal assignment was accepted.</returns>
    internal static bool AlignCellFans(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        var Before = FlowchartCellRepair.Capture(Nodes);
        return FlowchartCellRepair.TryAssign(Nodes, PlanCellFans(Contract, Nodes, Horizontal, Before.Regions));
    }

    /// <summary>Builds the same complete fan/collector assignment on a caller's private candidate without publishing intermediate regions.</summary>
    /// <param name="Contract">Visible graph and return classification.</param>
    /// <param name="Nodes">All diagram nodes.</param>
    /// <param name="Horizontal">Whether the cross axis is rows.</param>
    /// <param name="Initial">Complete authoritative or privately proposed ownership.</param>
    /// <param name="IncludeFanOut">Whether to align fan-out input chains as well as collectors.</param>
    /// <returns>A complete private reservation plan.</returns>
    internal static IReadOnlyDictionary<string, FlowchartCellRegion> PlanCellFans(DiagramContract Contract,
        List<DiagramGridLayout.GridNode> Nodes, bool Horizontal, IReadOnlyDictionary<string, FlowchartCellRegion> Initial, bool IncludeFanOut = true)
    {
        var Targets = new Dictionary<string, FlowchartCellRegion>(Initial, StringComparer.Ordinal);
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Edges = Contract.Edges.Where(Edge => !Back.Contains(Edge) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (bool FanOut in IncludeFanOut ? new[] { true, false } : new[] { false })
        foreach (var Junction in Nodes.Where(Node => Node.Shape == "circle"))
        {
            var Links = Edges.Where(Edge => FanOut ? Edge.SourceId == Junction.Id : Edge.TargetId == Junction.Id).ToArray();
            if (Links.Length < 2 || Links.Length > 3) continue;
            var Ids = Links.Select(Edge => FanOut ? Edge.TargetId : Edge.SourceId).ToArray();
            if (Ids.Any(Id => !ById.ContainsKey(Id))) continue;
            var Neighbours = Ids.Select(Id => ById[Id]).ToArray();
            if (Neighbours.Select(Node => Node.Rank).Distinct().Count() != 1 || Neighbours.Any(Node => Node.GroupId != Junction.GroupId)) continue;
            var Positions = Neighbours.Select(Node => Horizontal ? Targets[Node.Id].StartRow : Targets[Node.Id].StartColumn).OrderBy(Value => Value).ToArray();
            // A half-cell median cannot become fractional occupancy. Choose
            // the lower whole start deterministically; routing scores can
            // subsequently compare legal whole-cell alternatives.
            int Centre = Positions.Length == 3 ? Positions[1] : checked((int)Math.Floor(((double)Positions[0] + Positions[^1]) / 2));
            var Current = Junction; var Seen = new HashSet<string>(StringComparer.Ordinal);
            while (Seen.Add(Current.Id))
            {
                if (Current.PlacementHintLocked) break;
                var Region = Targets[Current.Id];
                Targets[Current.Id] = new(Horizontal ? Centre : Region.StartRow, Horizontal ? Region.StartColumn : Centre, Region.RowSpan, Region.ColumnSpan);
                var Chain = Edges.Where(Edge => FanOut ? Edge.TargetId == Current.Id : Edge.SourceId == Current.Id).ToArray();
                if (Chain.Length != 1 || !ById.TryGetValue(FanOut ? Chain[0].SourceId : Chain[0].TargetId, out var Next)
                    || Next.GroupId != Current.GroupId || (FanOut ? Next.Rank >= Current.Rank : Next.Rank <= Current.Rank)
                    || Edges.Count(Edge => FanOut ? Edge.SourceId == Next.Id : Edge.TargetId == Next.Id) != 1) break;
                Current = Next;
            }
        }
        return Targets;
    }
}
