using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Offers whole-cell alternatives to routed scoring without assigning centres or fractional reservations.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>A complete moving unit with an unpublished integer translation.</summary>
    /// <param name="Key">Deterministic identity.</param>
    /// <param name="Subject">Nominated endpoint or group.</param>
    /// <param name="Nodes">Complete moving membership.</param>
    /// <param name="Rows">Whole logical rows.</param>
    /// <param name="Columns">Whole logical columns.</param>
    internal sealed record CellRouteMoveTarget(string Key, string Subject, IReadOnlyList<GridNode> Nodes, int Rows, int Columns);

    /// <summary>Computes rule 2.10's per-source representative strictly for ordering.</summary>
    /// <param name="Region">Source's occupied inclusive interval A through B.</param>
    /// <param name="Horizontal">Whether the cross axis uses rows.</param>
    /// <returns>(A+B)/2; this value is never assigned to a region.</returns>
    internal static double CellSourceRepresentative(FlowchartCellRegion Region, bool Horizontal) => Horizontal
        ? ((double)Region.StartRow + Region.EndRow - 1) / 2 : ((double)Region.StartColumn + Region.EndColumn - 1) / 2;

    /// <summary>Orders a target by its dominant source, averaging separate source representatives on a tie.</summary>
    /// <param name="Contract">Visible relationships, including parallel links.</param>
    /// <param name="Regions">Complete regions and group envelopes.</param>
    /// <param name="Target">Nominated target.</param>
    /// <param name="Horizontal">Cross-axis direction.</param>
    /// <returns>An ordering key, never a placement coordinate.</returns>
    internal static double CellSourceOrder(DiagramContract Contract, IReadOnlyDictionary<string, FlowchartCellRegion> Regions, string Target, bool Horizontal)
    {
        IGrouping<string, EdgeContract>[] Sources = [.. Contract.Edges.Where(Edge => Edge.TargetId == Target && Regions.ContainsKey(Edge.SourceId)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).GroupBy(Edge => Edge.SourceId)];
        if (Sources.Length == 0)
        {
            return CellSourceRepresentative(Regions[Target], Horizontal);
        }

        int Most = Sources.Max(Source => Source.Count());
        return Sources.Where(Source => Source.Count() == Most).Average(Source => CellSourceRepresentative(Regions[Source.Key], Horizontal));
    }

    /// <summary>Interleaves whole-cell peer-boundary and compass proposals; the full routed score alone accepts moves.</summary>
    /// <param name="Contract">Topology and membership.</param>
    /// <param name="Placed">Complete cell-bound diagram.</param>
    /// <param name="Horizontal">Whether cross-axis intervals use rows.</param>
    /// <param name="Ids">Endpoints nominated by bent or missing routes.</param>
    /// <returns>Unpublished translations, bounded and interleaved using the existing move limit.</returns>
    internal static IEnumerable<CellRouteMoveTarget> CellRouteMoveCandidates(DiagramContract Contract, List<GridNode> Placed, bool Horizontal, IEnumerable<string> Ids)
    {
        Dictionary<string, FlowchartCellRegion> Regions = new Dictionary<string, FlowchartCellRegion>(FlowchartCellRepair.Capture(Placed).Regions, StringComparer.Ordinal);
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        EdgeContract[] Edges = [.. Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        foreach (GroupContract Group in Contract.Groups)
        {
            FlowchartCellRegion[] Members = [.. Placed.Where(Node => IsInsideGroup(Contract, Node.Id, Group.Id)).Select(Node => Regions[Node.Id])];
            if (Members.Length > 0)
            {
                Regions.TryAdd(Group.Id, CellRouteEnvelope(Members));
            }
        }
        Dictionary<string, int> Declared = Contract.Nodes.Select((Node, Index) => (Node.Id, Index)).ToDictionary(Item => Item.Id, Item => Item.Index);
        List<List<CellRouteMoveTarget>> Options = []; HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string Id in Ids.Distinct(StringComparer.Ordinal).Where(Regions.ContainsKey)
            .OrderBy(Id => CellSourceOrder(Contract, Regions, Id, Horizontal)).ThenBy(Id => Declared.GetValueOrDefault(Id, int.MaxValue)))
        {
            List<IReadOnlyList<GridNode>> Units = [Placed.Where(Node => Node.Id == Id || IsInsideGroup(Contract, Node.Id, Id)).ToArray()];
            if (ById.TryGetValue(Id, out GridNode? Start))
            {
                IReadOnlyList<GridNode> Tail = CellOutputChain(Start, ById, Edges);
                if (Tail.Count > 1)
                {
                    Units.Add(Tail);
                }
            }
            foreach (IReadOnlyList<GridNode> Moving in Units)
            {
                if (Moving.Count == 0 || Moving.Any(Node => Node.PlacementHintLocked))
                {
                    continue;
                }

                string Key = string.Join("|", Moving.Select(Node => Node.Id).Order(StringComparer.Ordinal));
                if (!Seen.Add(Key))
                {
                    continue;
                }

                FlowchartCellRegion Bounds = CellRouteEnvelope(Moving.Select(Node => Regions[Node.Id]).ToArray());
                int Cross = Horizontal ? Bounds.StartRow : Bounds.StartColumn, Span = Horizontal ? Bounds.RowSpan : Bounds.ColumnSpan;
                HashSet<string> Members = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                List<(int Rows, int Columns)> Deltas = [];
                foreach (string Peer in Edges.Where(Edge => Members.Contains(Edge.SourceId) != Members.Contains(Edge.TargetId))
                    .Select(Edge => Members.Contains(Edge.SourceId) ? Edge.TargetId : Edge.SourceId).Distinct().Where(Regions.ContainsKey))
                {
                    FlowchartCellRegion Other = Regions[Peer];
                    foreach (int Target in new[] { Horizontal ? Other.StartRow : Other.StartColumn,
                        checked((Horizontal ? Other.EndRow : Other.EndColumn) - Span) })
                    {
                        Deltas.Add(Horizontal ? (checked(Target - Cross), 0) : (0, checked(Target - Cross)));
                    }
                }
                for (int Distance = 1; Distance <= FlowchartLayoutRules.RepairCandidateMoves; Distance++)
                {
                    Deltas.AddRange([(-Distance, 0), (Distance, 0), (0, -Distance), (0, Distance)]);
                }

                Options.Add([.. Deltas.Where(Delta => Delta != (0, 0)).Distinct().Take(FlowchartLayoutRules.RepairCandidateMoves).Select(Delta => new CellRouteMoveTarget(Key + "|" + Delta.Rows + "," + Delta.Columns, Id, Moving, Delta.Rows, Delta.Columns))]);
            }
        }
        for (int Index = 0; Index < FlowchartLayoutRules.RepairCandidateMoves; Index++)
        {
            foreach (List<CellRouteMoveTarget> Unit in Options)
            {
                if (Index < Unit.Count)
                {
                    yield return Unit[Index];
                }
            }
        }
    }

    /// <summary>Measures a complete integer envelope without a rendered midpoint.</summary>
    /// <param name="Members">Nonempty member reservations.</param>
    /// <returns>A whole-cell envelope for a complete unit.</returns>
    private static FlowchartCellRegion CellRouteEnvelope(IReadOnlyList<FlowchartCellRegion> Members) => new(Members.Min(Region => Region.StartRow), Members.Min(Region => Region.StartColumn),
        Members.Max(Region => Region.EndRow) - Members.Min(Region => Region.StartRow), Members.Max(Region => Region.EndColumn) - Members.Min(Region => Region.StartColumn));
}
