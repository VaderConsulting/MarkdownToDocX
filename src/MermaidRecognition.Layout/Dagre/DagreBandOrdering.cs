using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout.Dagre;

/// <summary>Adapts semantic bands to the existing Dagre barycentre sweep without duplicating its ordering algorithm.</summary>
internal static partial class DagreLayoutEngine
{
    /// <summary>Orders solid blocks across semantic bands using alternating barycentre sweeps and strict adjacent exchanges.</summary>
    /// <param name="Initial">Stable initial block order, shared by all parts of a frame.</param>
    /// <param name="Bands">Semantic ranks containing each block, with repeated frame identities intentionally retained across ranks.</param>
    /// <param name="Connections">Declared connections between blocks and their semantic ranks.</param>
    /// <returns>A shared block order with no increase in the complete two-layer crossing count.</returns>
    internal static IReadOnlyList<string> OrderBands(IReadOnlyList<string> Initial,
        IReadOnlyDictionary<int, IReadOnlyList<string>> Bands,
        IReadOnlyList<(int SourceBand, string Source, int TargetBand, string Target)> Connections)
    {
        List<string> Order = [.. Initial];
        int Best = CountBandCrossings(Order, Connections);
        for (int Round = 0; Round < OrderSweepPatience; Round++)
        {
            int Before = Best;
            foreach (bool Down in new[] { true, false })
            {
                foreach (int Band in Down ? Bands.Keys.Order() : Bands.Keys.OrderDescending())
            {
                var Movable = Order.Where(Bands[Band].Contains).ToArray();
                    (int ReferenceBand, string Reference, int MovableBand, string Movable)[] Links = [.. Connections.SelectMany(Edge => new[] {
                    (ReferenceBand: Edge.SourceBand, Reference: Edge.Source, MovableBand: Edge.TargetBand, Movable: Edge.Target),
                    (ReferenceBand: Edge.TargetBand, Reference: Edge.Target, MovableBand: Edge.SourceBand, Movable: Edge.Source) })
                    .Where(Edge => Edge.MovableBand == Band && (Down ? Edge.ReferenceBand < Band : Edge.ReferenceBand > Band))];
                var References = Order.Where(Id => Links.Any(Link => Link.Reference == Id)).ToArray();
                    IReadOnlyList<string> Sorted = OrderBand(Movable, References, Links.Select(Link => (Link.Reference, Link.Movable)));
                    List<string> Trial = ReplaceBandOrder(Order, Movable, Sorted);
                int Count = CountBandCrossings(Trial, Connections);
                if (Count < Best) { Order = Trial; Best = Count; }
            }
            }

            foreach (int Band in Bands.Keys.Order())
            {
                var Members = Order.Where(Bands[Band].Contains).ToArray();
                for (int Index = 1; Index < Members.Length; Index++)
                {
                    List<string> Trial = [.. Order];
                    int Left = Trial.IndexOf(Members[Index - 1]), Right = Trial.IndexOf(Members[Index]);
                    (Trial[Left], Trial[Right]) = (Trial[Right], Trial[Left]);
                    int Count = CountBandCrossings(Trial, Connections);
                    if (Count >= Best)
                    {
                        continue;
                    }

                    Order = Trial; Best = Count;
                    (Members[Index - 1], Members[Index]) = (Members[Index], Members[Index - 1]);
                }
            }
            if (Best >= Before)
            {
                break;
            }
        }
        return Order;
    }

    /// <summary>Replaces only the selected rank's positions in the shared block permutation.</summary>
    /// <param name="Order">Current shared block order.</param>
    /// <param name="Members">Blocks present in the rank being swept.</param>
    /// <param name="Sorted">Barycentre proposal for those blocks.</param>
    /// <returns>A private permutation; every frame keeps the same position in every band.</returns>
    private static List<string> ReplaceBandOrder(IReadOnlyList<string> Order, IReadOnlyList<string> Members, IReadOnlyList<string> Sorted)
    {
        int Index = 0;
        return [.. Order.Select(Id => Members.Contains(Id) ? Sorted[Index++] : Id)];
    }

    /// <summary>Measures all connected band pairs with Dagre's original two-layer crossing counter.</summary>
    /// <param name="Order">Complete shared block permutation.</param>
    /// <param name="Connections">Connections annotated with semantic ranks.</param>
    /// <returns>The sum of crossings between distinct block endpoints, including links that skip a rank.</returns>
    private static int CountBandCrossings(IReadOnlyList<string> Order,
        IReadOnlyList<(int SourceBand, string Source, int TargetBand, string Target)> Connections)
    {
        int Count = 0;
        foreach (IGrouping<(int Low, int High), (int SourceBand, string Source, int TargetBand, string Target)> Pair in Connections.Where(Edge => Edge.SourceBand != Edge.TargetBand && Edge.Source != Edge.Target)
            .GroupBy(Edge => (Low: Math.Min(Edge.SourceBand, Edge.TargetBand), High: Math.Max(Edge.SourceBand, Edge.TargetBand))))
        {
            DagreGraph Graph = new DagreGraph(Multigraph: true);
            List<string> North = [.. Order.Select(Id => "north:" + Id)];
            List<string> South = [.. Order.Select(Id => "south:" + Id)];
            foreach (string Id in North.Concat(South))
            {
                Graph.SetNode(Id, new(), true);
            }

            int Index = 0;
            foreach ((int SourceBand, string Source, int TargetBand, string Target) Edge in Pair)
            {
                Graph.SetEdge("north:" + (Edge.SourceBand == Pair.Key.Low ? Edge.Source : Edge.Target),
                    "south:" + (Edge.SourceBand == Pair.Key.High ? Edge.Source : Edge.Target), new() { Weight = 1 }, true,
                    (Index++).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            Count += TwoLayerCrossCount(Graph, North, South);
        }
        return Count;
    }

    /// <summary>Orders one movable band against fixed neighbouring elements, retaining source order unless crossings fall.</summary>
    /// <param name="Movable">Stable initial order of the nodes or solid blocks being ordered.</param>
    /// <param name="References">Fixed neighbouring nodes in page-column order.</param>
    /// <param name="Connections">Connections directed from a fixed reference to a movable item, including repeated links.</param>
    /// <returns>The existing order or a barycentre order with fewer two-layer crossings.</returns>
    internal static IReadOnlyList<string> OrderBand(IReadOnlyList<string> Movable, IReadOnlyList<string> References,
        IEnumerable<(string Reference, string Movable)> Connections)
    {
        if (Movable.Count < 2 || References.Count == 0)
        {
            return Movable;
        }

        const string Root = "band-root";
        DagreGraph Graph = new DagreGraph(Compound: true, Multigraph: true);
        DagreGraph Constraints = new DagreGraph();
        Graph.SetNode(Root, new(), true);
        List<string> North = [.. References.Select(Id => "reference:" + Id)];
        List<string> South = [.. Movable.Select(Id => "movable:" + Id)];
        for (int Index = 0; Index < North.Count; Index++)
        {
            Graph.SetNode(North[Index], new() { Order = Index }, true);
        }

        foreach (string Id in South) { Graph.SetNode(Id, new(), true); Graph.SetParent(Id, Root); }
        int EdgeIndex = 0;
        foreach ((string Reference, string Movable) Connection in Connections)
        {
            Graph.SetEdge("reference:" + Connection.Reference, "movable:" + Connection.Movable,
                new() { Weight = 1 }, true, (EdgeIndex++).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        // SortSubgraph invokes Barycenter and ResolveConflicts; the same crossing
        // counter used by the original sweep decides whether to publish its order.
        List<string> Sorted = SortSubgraph(Graph, Root, Constraints, false).Vs;
        return TwoLayerCrossCount(Graph, North, Sorted) < TwoLayerCrossCount(Graph, North, South)
            ? Sorted.Select(Id => Id["movable:".Length..]).ToArray() : Movable;
    }
}
