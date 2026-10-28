using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout.Dagre;

internal static partial class DagreLayoutEngine
{
    /// <summary>Counts crossings for fixed grid columns through the existing adjacent-layer graph and original Dagre counter.</summary>
    /// <param name="Vertices">Complete fixed row and column coordinates; every vertex uses the same synthetic block.</param>
    /// <param name="Links">All relationships with present endpoints.</param>
    /// <returns>The crossing count without running sweeps or changing the given order.</returns>
    internal static int CountBandCoordinateCrossings(IReadOnlyList<BandVertex> Vertices,
        IReadOnlyList<(string Source, string Target)> Links)
    {
        CompleteBandGraph Model = BuildCompleteBandGraph(["grid"], Vertices, Links);
        return CrossCount(Model.Graph, Model.Rows);
    }

    /// <summary>A relationship endpoint in a local rank, retaining its fixed column inside a solid owner frame.</summary>
    /// <param name="Id">Leaf or frame endpoint identity.</param>
    /// <param name="Block">Whole block moved by the ordering pass.</param>
    /// <param name="Row">Local rank in the stacked semantic bands.</param>
    /// <param name="Offset">Fixed within-frame column; zero for an ungrouped element.</param>
    internal sealed record BandVertex(string Id, string Block, int Row, double Offset);

    /// <summary>Reduces crossings over all relationships, splitting long links with dummy vertices between every adjacent row.</summary>
    /// <param name="Initial">Frames and every ungrouped element, in stable initial order.</param>
    /// <param name="Vertices">Complete leaf and frame endpoint inventory.</param>
    /// <param name="Links">Every declared relationship with present endpoints, including within-band links.</param>
    /// <param name="Allowed">Optional ordering constraints, such as an unwrapped triggering sequence.</param>
    /// <returns>Shared block permutation and complete adjacent-row crossing counts.</returns>
    internal static (IReadOnlyList<string> Order, int Before, int After) OrderCompleteBandBlocks(IReadOnlyList<string> Initial,
        IReadOnlyList<BandVertex> Vertices, IReadOnlyList<(string Source, string Target)> Links, Func<IReadOnlyList<string>, bool>? Allowed = null)
    {
        List<string> Order = [.. Initial];
        CompleteBandGraph Model = BuildCompleteBandGraph(Order, Vertices, Links);
        int Before = CrossCount(Model.Graph, Model.Rows), Best = Before;
        Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal) { [string.Join("\n", Order)] = Best };
        for (int Round = 0; Round < OrderSweepPatience; Round++)
        {
            int Previous = Best;
            foreach (bool Down in new[] { true, false })
            {
                foreach (int Row in Down ? Enumerable.Range(0, Model.Rows.Count) : Enumerable.Range(0, Model.Rows.Count).Reverse())
            {
                int Neighbour = Down ? Row - 1 : Row + 1;
                if (Neighbour < 0 || Neighbour >= Model.Rows.Count)
                    {
                        continue;
                    }

                    var Members = Vertices.Where(Vertex => Model.RowOf[Vertex.Id] == Row).Select(Vertex => Vertex.Block).Distinct().ToArray();
                var Movable = Order.Where(Members.Contains).ToArray();
                    List<string> References = Model.Rows[Neighbour];
                    List<(string Reference, string Movable)> Connections = [];
                foreach (BandVertex? Vertex in Vertices.Where(Vertex => Model.RowOf[Vertex.Id] == Row))
                    {
                        foreach (DagreEdge Edge in Down ? Model.Graph.InEdges(Vertex.Id) : Model.Graph.OutEdges(Vertex.Id))
                {
                    string Reference = Down ? Edge.V : Edge.W;
                    if (References.Contains(Reference))
                            {
                                Connections.Add((Reference, Vertex.Block));
                            }
                        }
                    }

                    IReadOnlyList<string> Sorted = OrderBand(Movable, References, Connections);
                if (Sorted.SequenceEqual(Movable))
                    {
                        continue;
                    }

                    List<string> Trial = ReplaceBandOrder(Order, Movable, Sorted);
                if (Allowed?.Invoke(Trial) == false)
                    {
                        continue;
                    }

                    string Signature = string.Join("\n", Trial);
                if (Counts.TryGetValue(Signature, out int Cached) && Cached >= Best)
                    {
                        continue;
                    }

                    CompleteBandGraph Candidate = BuildCompleteBandGraph(Trial, Vertices, Links);
                int Count = CrossCount(Candidate.Graph, Candidate.Rows);
                Counts[Signature] = Count;
                if (Count < Best) { Order = Trial; Best = Count; Model = Candidate; }
            }
            }

            foreach (List<string> Row in Model.Rows.ToArray())
            {
                var Members = Order.Where(Id => Vertices.Any(Vertex => Vertex.Block == Id && Row.Contains(Vertex.Id))).ToArray();
                for (int Index = 1; Index < Members.Length; Index++)
                {
                    List<string> Trial = [.. Order]; int Left = Trial.IndexOf(Members[Index - 1]), Right = Trial.IndexOf(Members[Index]);
                    (Trial[Left], Trial[Right]) = (Trial[Right], Trial[Left]);
                    if (Allowed?.Invoke(Trial) == false)
                    {
                        continue;
                    }

                    string Signature = string.Join("\n", Trial);
                    if (Counts.TryGetValue(Signature, out int Cached) && Cached >= Best)
                    {
                        continue;
                    }

                    CompleteBandGraph Candidate = BuildCompleteBandGraph(Trial, Vertices, Links);
                    int Count = CrossCount(Candidate.Graph, Candidate.Rows);
                    Counts[Signature] = Count;
                    if (Count >= Best)
                    {
                        continue;
                    }

                    Order = Trial; Best = Count; Model = Candidate;
                    (Members[Index - 1], Members[Index]) = (Members[Index], Members[Index - 1]);
                }
            }
            if (Best >= Previous)
            {
                break;
            }
        }
        return (Order, Before, Best);
    }

    /// <summary>One disposable ordering graph, including explicit intermediate vertices for non-adjacent and same-rank links.</summary>
    /// <param name="Graph">Original Dagre graph type consumed by its crossing counter.</param>
    /// <param name="Rows">Ordered adjacent layers.</param>
    /// <param name="RowOf">Actual graph row for every declared endpoint.</param>
    private sealed record CompleteBandGraph(DagreGraph Graph, List<List<string>> Rows, Dictionary<string, int> RowOf);

    /// <summary>Projects a shared block permutation into complete adjacent rows without copying any barycentre or crossing algorithm.</summary>
    /// <param name="Order">Current block order.</param>
    /// <param name="Vertices">Fixed local rank and aspect offsets.</param>
    /// <param name="Links">All relationships.</param>
    /// <returns>Fresh graph; dummy positions interpolate endpoint columns and never become placed nodes.</returns>
    private static CompleteBandGraph BuildCompleteBandGraph(IReadOnlyList<string> Order, IReadOnlyList<BandVertex> Vertices,
        IReadOnlyList<(string Source, string Target)> Links)
    {
        DagreGraph Graph = new DagreGraph(Multigraph: true);
        var Ranks = Vertices.Select(Vertex => Vertex.Row).Distinct().Order().ToArray();
        Dictionary<string, int> RowOf = Vertices.ToDictionary(Vertex => Vertex.Id, Vertex => Array.IndexOf(Ranks, Vertex.Row) * 2, StringComparer.Ordinal);
        List<List<string>> Rows = [.. Enumerable.Range(0, Math.Max(1, Ranks.Length * 2)).Select(_ => new List<string>())];
        Dictionary<string, double> X = new Dictionary<string, double>(StringComparer.Ordinal);
        double Pitch = Vertices.Select(Vertex => Vertex.Offset).DefaultIfEmpty(0).Max() + 1;
        Dictionary<string, int> Position = Order.Select((Id, Index) => (Id, Index)).ToDictionary(Item => Item.Id, Item => Item.Index, StringComparer.Ordinal);
        foreach (BandVertex Vertex in Vertices)
        {
            Graph.SetNode(Vertex.Id, new(), true); Rows[RowOf[Vertex.Id]].Add(Vertex.Id);
            X[Vertex.Id] = (Position[Vertex.Block] * Pitch) + Vertex.Offset;
        }
        int EdgeIndex = 0;
        foreach ((string Source, string Target) Link in Links)
        {
            if (Link.Source == Link.Target)
            {
                continue;
            }

            string Source = Link.Source, Target = Link.Target;
            int First = RowOf[Source], Last = RowOf[Target];
            if (First > Last) { (Source, Target) = (Target, Source); (First, Last) = (Last, First); }
            if (First == Last)
            {
                // Same-rank relationships use a separate endpoint row so they
                // also enter TwoLayerCrossCount rather than disappearing.
                string Endpoint = "same-row:" + EdgeIndex;
                Last++; Graph.SetNode(Endpoint, new(), true); Rows[Last].Add(Endpoint); X[Endpoint] = X[Target]; Target = Endpoint;
            }
            string Previous = Source;
            for (int Row = First + 1; Row < Last; Row++)
            {
                string Dummy = "band-dummy:" + EdgeIndex + ":" + Row;
                Graph.SetNode(Dummy, new(), true); Rows[Row].Add(Dummy);
                X[Dummy] = X[Source] + ((X[Target] - X[Source]) * (Row - First) / (Last - First));
                Graph.SetEdge(Previous, Dummy, new() { Weight = 1 }, true, EdgeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)); Previous = Dummy;
            }
            Graph.SetEdge(Previous, Target, new() { Weight = 1 }, true, EdgeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)); EdgeIndex++;
        }
        foreach (List<string>? Row in Rows)
        {
            Row.Sort((A, B) => { int Compare = X[A].CompareTo(X[B]); return Compare != 0 ? Compare : string.CompareOrdinal(A, B); });
        }

        return new(Graph, Rows, RowOf);
    }
}
