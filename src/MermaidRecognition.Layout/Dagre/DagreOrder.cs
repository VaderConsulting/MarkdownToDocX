// Port of dagre-d3-es 7.0.14 order.js.
// Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Dagre;

internal sealed class DagreBarycenter
{
    public string V { get; set; } = string.Empty;

    public bool HasBarycenter { get; set; }

    public double Barycenter { get; set; }

    public double Weight { get; set; }

    public List<string> Vs { get; set; } = new List<string>();

    public int Index { get; set; }

    public int Indegree { get; set; }

    public List<DagreBarycenter> Incoming { get; } = new List<DagreBarycenter>();

    public List<DagreBarycenter> Outgoing { get; } = new List<DagreBarycenter>();

    public bool Merged { get; set; }
}

internal static partial class DagreLayoutEngine
{
    private static void Order(DagreGraph Graph, DagreContext Context)
    {
        int Max = MaxRank(Graph);
        List<DagreGraph> Down = new List<DagreGraph>();
        for (int Rank = 1; Rank <= Max; Rank++)
        {
            Down.Add(BuildLayerGraph(Graph, Context, Rank, true));
        }

        List<DagreGraph> Up = new List<DagreGraph>();
        for (int Rank = Max - 1; Rank >= 0; Rank--)
        {
            Up.Add(BuildLayerGraph(Graph, Context, Rank, false));
        }

        List<List<string>> Layering = InitOrder(Graph);
        AssignOrder(Graph, Layering);
        double BestCrossings = double.PositiveInfinity;
        List<List<string>>? Best = null;
        int LastBest = 0;
        for (int Iteration = 0; LastBest < 4; Iteration++, LastBest++)
        {
            bool DownSweep = Iteration % 2 == 1;
            bool BiasRight = Iteration % 4 >= 2;
            SweepLayerGraphs(DownSweep ? Down : Up, BiasRight);
            Layering = BuildLayerMatrix(Graph);
            int Crossings = CrossCount(Graph, Layering);
            if (Crossings < BestCrossings)
            {
                LastBest = 0;
                Best = CloneLayering(Layering);
                BestCrossings = Crossings;
            }
        }

        if (Best != null)
        {
            AssignOrder(Graph, Best);
        }
    }

    private static List<List<string>> CloneLayering(List<List<string>> Layering)
    {
        List<List<string>> Copy = new List<List<string>>();
        foreach (List<string> Layer in Layering)
        {
            Copy.Add(new List<string>(Layer));
        }

        return Copy;
    }

    private static void AssignOrder(DagreGraph Graph, List<List<string>> Layering)
    {
        foreach (List<string> Layer in Layering)
        {
            for (int Index = 0; Index < Layer.Count; Index++)
            {
                string NodeId = Layer[Index];
                if (string.IsNullOrEmpty(NodeId))
                {
                    continue;
                }

                DagreNodeLabel? Node = Graph.Node(NodeId);
                if (Node != null)
                {
                    Node.Order = Index;
                }
            }
        }
    }

    private static List<List<string>> InitOrder(DagreGraph Graph)
    {
        List<string> Simple = new List<string>();
        foreach (string NodeId in Graph.Nodes())
        {
            if (Graph.Children(NodeId).Count == 0)
            {
                Simple.Add(NodeId);
            }
        }

        int Max = 0;
        foreach (string NodeId in Simple)
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank != null && Node.Rank.Value > Max)
            {
                Max = Node.Rank.Value;
            }
        }

        List<List<string>> Layers = new List<List<string>>();
        for (int Rank = 0; Rank <= Max; Rank++)
        {
            Layers.Add(new List<string>());
        }

        Dictionary<string, int> Insertion = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int Index = 0; Index < Simple.Count; Index++)
        {
            Insertion[Simple[Index]] = Index;
        }

        Simple.Sort(delegate (string Left, string Right)
        {
            int LeftRank = Graph.Node(Left)?.Rank ?? 0;
            int RightRank = Graph.Node(Right)?.Rank ?? 0;
            int RankCompare = LeftRank.CompareTo(RightRank);
            if (RankCompare != 0)
            {
                return RankCompare;
            }

            return Insertion[Left].CompareTo(Insertion[Right]);
        });

        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string NodeId in Simple)
        {
            InitOrderVisit(Graph, NodeId, Visited, Layers);
        }

        return Layers;
    }

    private static void InitOrderVisit(DagreGraph Graph, string NodeId, HashSet<string> Visited, List<List<string>> Layers)
    {
        if (!Visited.Add(NodeId))
        {
            return;
        }

        DagreNodeLabel? Node = Graph.Node(NodeId);
        if (Node?.Rank == null)
        {
            return;
        }

        Layers[Node.Rank.Value].Add(NodeId);
        foreach (string Successor in Graph.Successors(NodeId))
        {
            InitOrderVisit(Graph, Successor, Visited, Layers);
        }
    }

    private static DagreGraph BuildLayerGraph(DagreGraph Graph, DagreContext Context, int Rank, bool UseInEdges)
    {
        string Root = "_root" + Context.NextId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Context.NextId++;
        while (Graph.HasNode(Root))
        {
            Context.NextId++;
            Root = "_root" + Context.NextId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        DagreGraph Result = new DagreGraph(Directed: true, Multigraph: false, Compound: true);
        DagreGraphLabel LayerLabel = new DagreGraphLabel
        {
            Root = Root
        };
        Result.SetGraph(LayerLabel);
        Result.SetDefaultNodeLabel(delegate (string NodeId)
        {
            return Graph.Node(NodeId);
        });

        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node == null)
            {
                continue;
            }

            bool OnRank = Node.Rank == Rank;
            bool Spans = Node.MinRank != null && Node.MaxRank != null && Node.MinRank.Value <= Rank && Rank <= Node.MaxRank.Value;
            if (!OnRank && !Spans)
            {
                continue;
            }

            Result.SetNode(NodeId, null, false);
            string? Parent = Graph.Parent(NodeId);
            Result.SetParent(NodeId, Parent ?? Root);
            List<DagreEdge> Related = UseInEdges ? Graph.InEdges(NodeId) : Graph.OutEdges(NodeId);
            foreach (DagreEdge Edge in Related)
            {
                string Other = Edge.V == NodeId ? Edge.W : Edge.V;
                DagreEdgeLabel? Existing = Result.Edge(Other, NodeId);
                double Weight = Existing?.Weight ?? 0;
                DagreEdgeLabel? SourceLabel = Graph.Edge(Edge);
                double Added = SourceLabel?.Weight ?? 0;
                Result.SetEdge(Other, NodeId, new DagreEdgeLabel
                {
                    Weight = Added + Weight
                }, true);
            }

            if (Node.MinRank != null)
            {
                string? Left = null;
                string? Right = null;
                if (Node.BorderLeftByRank != null)
                {
                    Node.BorderLeftByRank.TryGetValue(Rank, out Left);
                }

                if (Node.BorderRightByRank != null)
                {
                    Node.BorderRightByRank.TryGetValue(Rank, out Right);
                }

                Result.SetNode(NodeId, new DagreNodeLabel
                {
                    LayerBorderLeft = Left,
                    LayerBorderRight = Right
                }, true);
            }
        }

        return Result;
    }

    private static void SweepLayerGraphs(List<DagreGraph> LayerGraphs, bool BiasRight)
    {
        DagreGraph Constraints = new DagreGraph();
        foreach (DagreGraph LayerGraph in LayerGraphs)
        {
            string? Root = LayerGraph.Label.Root;
            if (Root == null)
            {
                continue;
            }

            DagreBarycenter Sorted = SortSubgraph(LayerGraph, Root, Constraints, BiasRight);
            for (int Index = 0; Index < Sorted.Vs.Count; Index++)
            {
                DagreNodeLabel? Node = LayerGraph.Node(Sorted.Vs[Index]);
                if (Node != null)
                {
                    Node.Order = Index;
                }
            }

            AddSubgraphConstraints(LayerGraph, Constraints, Sorted.Vs);
        }
    }

    private static void AddSubgraphConstraints(DagreGraph Graph, DagreGraph Constraints, List<string> NodeIds)
    {
        Dictionary<string, string> Previous = new Dictionary<string, string>(StringComparer.Ordinal);
        string? RootPrevious = null;
        foreach (string NodeId in NodeIds)
        {
            string? Child = Graph.Parent(NodeId);
            while (Child != null)
            {
                string? Parent = Graph.Parent(Child);
                string? PreviousChild;
                if (Parent != null)
                {
                    Previous.TryGetValue(Parent, out PreviousChild);
                    Previous[Parent] = Child;
                }
                else
                {
                    PreviousChild = RootPrevious;
                    RootPrevious = Child;
                }

                if (PreviousChild != null && PreviousChild != Child)
                {
                    Constraints.SetEdge(PreviousChild, Child, null, false);
                    break;
                }

                Child = Parent;
            }
        }
    }

    private static DagreBarycenter SortSubgraph(DagreGraph Graph, string NodeId, DagreGraph Constraints, bool BiasRight)
    {
        List<string> Movable = Graph.Children(NodeId);
        DagreNodeLabel? Node = Graph.Node(NodeId);
        string? BorderLeft = Node?.LayerBorderLeft;
        string? BorderRight = Node?.LayerBorderRight;
        if (BorderLeft != null)
        {
            Movable.Remove(BorderLeft);
            if (BorderRight != null)
            {
                Movable.Remove(BorderRight);
            }
        }

        List<DagreBarycenter> Barycenters = Barycenter(Graph, Movable);
        Dictionary<string, DagreBarycenter> Subgraphs = new Dictionary<string, DagreBarycenter>(StringComparer.Ordinal);
        foreach (DagreBarycenter Entry in Barycenters)
        {
            if (Graph.Children(Entry.V).Count > 0)
            {
                DagreBarycenter Subgraph = SortSubgraph(Graph, Entry.V, Constraints, BiasRight);
                Subgraphs[Entry.V] = Subgraph;
                if (Subgraph.HasBarycenter)
                {
                    MergeBarycenters(Entry, Subgraph);
                }
            }
        }

        List<DagreBarycenter> Entries = ResolveConflicts(Barycenters, Constraints);
        foreach (DagreBarycenter Entry in Entries)
        {
            List<string> Flat = new List<string>();
            foreach (string Member in Entry.Vs)
            {
                if (Subgraphs.TryGetValue(Member, out DagreBarycenter? Subgraph))
                {
                    Flat.AddRange(Subgraph.Vs);
                }
                else
                {
                    Flat.Add(Member);
                }
            }

            Entry.Vs = Flat;
        }

        DagreBarycenter Result = SortEntries(Entries, BiasRight);
        if (BorderLeft != null)
        {
            List<string> WithBorders = new List<string>();
            WithBorders.Add(BorderLeft);
            WithBorders.AddRange(Result.Vs);
            if (BorderRight != null)
            {
                WithBorders.Add(BorderRight);
            }

            Result.Vs = WithBorders;
            List<string> LeftPreds = Graph.Predecessors(BorderLeft);
            if (LeftPreds.Count > 0 && BorderRight != null)
            {
                List<string> RightPreds = Graph.Predecessors(BorderRight);
                if (RightPreds.Count > 0)
                {
                    DagreNodeLabel? LeftNode = Graph.Node(LeftPreds[0]);
                    DagreNodeLabel? RightNode = Graph.Node(RightPreds[0]);
                    if (!Result.HasBarycenter)
                    {
                        Result.Barycenter = 0;
                        Result.Weight = 0;
                        Result.HasBarycenter = true;
                    }

                    int LeftOrder = LeftNode?.Order ?? 0;
                    int RightOrder = RightNode?.Order ?? 0;
                    Result.Barycenter = ((Result.Barycenter * Result.Weight) + LeftOrder + RightOrder) / (Result.Weight + 2);
                    Result.Weight += 2;
                }
            }
        }

        return Result;
    }

    private static void MergeBarycenters(DagreBarycenter Target, DagreBarycenter Other)
    {
        if (Target.HasBarycenter)
        {
            Target.Barycenter = ((Target.Barycenter * Target.Weight) + (Other.Barycenter * Other.Weight)) / (Target.Weight + Other.Weight);
            Target.Weight += Other.Weight;
        }
        else
        {
            Target.Barycenter = Other.Barycenter;
            Target.Weight = Other.Weight;
            Target.HasBarycenter = true;
        }
    }

    private static List<DagreBarycenter> Barycenter(DagreGraph Graph, List<string> Movable)
    {
        List<DagreBarycenter> Result = new List<DagreBarycenter>();
        foreach (string NodeId in Movable)
        {
            List<DagreEdge> Incoming = Graph.InEdges(NodeId);
            if (Incoming.Count == 0)
            {
                Result.Add(new DagreBarycenter
                {
                    V = NodeId
                });
                continue;
            }

            double Sum = 0;
            double Weight = 0;
            foreach (DagreEdge Edge in Incoming)
            {
                DagreEdgeLabel? Label = Graph.Edge(Edge);
                DagreNodeLabel? Source = Graph.Node(Edge.V);
                double EdgeWeight = Label?.Weight ?? 0;
                int Order = Source?.Order ?? 0;
                Sum += EdgeWeight * Order;
                Weight += EdgeWeight;
            }

            Result.Add(new DagreBarycenter
            {
                V = NodeId,
                HasBarycenter = true,
                Barycenter = Weight == 0 ? 0 : Sum / Weight,
                Weight = Weight
            });
        }

        return Result;
    }

    private static List<DagreBarycenter> ResolveConflicts(List<DagreBarycenter> Entries, DagreGraph Constraints)
    {
        Dictionary<string, DagreBarycenter> Mapped = new Dictionary<string, DagreBarycenter>(StringComparer.Ordinal);
        for (int Index = 0; Index < Entries.Count; Index++)
        {
            DagreBarycenter Entry = Entries[Index];
            DagreBarycenter Copy = new DagreBarycenter
            {
                V = Entry.V,
                Index = Index,
                HasBarycenter = Entry.HasBarycenter,
                Barycenter = Entry.Barycenter,
                Weight = Entry.Weight
            };
            Copy.Vs.Add(Entry.V);
            Mapped[Entry.V] = Copy;
        }

        foreach (DagreEdge Edge in Constraints.Edges())
        {
            if (!Mapped.TryGetValue(Edge.V, out DagreBarycenter? Source))
            {
                continue;
            }

            if (!Mapped.TryGetValue(Edge.W, out DagreBarycenter? Target))
            {
                continue;
            }

            Target.Indegree++;
            Source.Outgoing.Add(Target);
        }

        List<DagreBarycenter> Sources = new List<DagreBarycenter>();
        foreach (DagreBarycenter Entry in Mapped.Values)
        {
            if (Entry.Indegree == 0)
            {
                Sources.Add(Entry);
            }
        }

        return DoResolveConflicts(Sources);
    }

    private static List<DagreBarycenter> DoResolveConflicts(List<DagreBarycenter> Sources)
    {
        List<DagreBarycenter> Entries = new List<DagreBarycenter>();
        while (Sources.Count > 0)
        {
            DagreBarycenter Entry = Sources[Sources.Count - 1];
            Sources.RemoveAt(Sources.Count - 1);
            Entries.Add(Entry);
            for (int Index = Entry.Incoming.Count - 1; Index >= 0; Index--)
            {
                DagreBarycenter Incoming = Entry.Incoming[Index];
                if (Incoming.Merged)
                {
                    continue;
                }

                if (!Incoming.HasBarycenter || !Entry.HasBarycenter || Incoming.Barycenter >= Entry.Barycenter)
                {
                    MergeEntries(Entry, Incoming);
                }
            }

            foreach (DagreBarycenter Outgoing in Entry.Outgoing)
            {
                Outgoing.Incoming.Add(Entry);
                Outgoing.Indegree--;
                if (Outgoing.Indegree == 0)
                {
                    Sources.Add(Outgoing);
                }
            }
        }

        List<DagreBarycenter> Result = new List<DagreBarycenter>();
        foreach (DagreBarycenter Entry in Entries)
        {
            if (!Entry.Merged)
            {
                Result.Add(Entry);
            }
        }

        return Result;
    }

    private static void MergeEntries(DagreBarycenter Target, DagreBarycenter Source)
    {
        double Sum = 0;
        double Weight = 0;
        if (Target.Weight != 0)
        {
            Sum += Target.Barycenter * Target.Weight;
            Weight += Target.Weight;
        }

        if (Source.Weight != 0)
        {
            Sum += Source.Barycenter * Source.Weight;
            Weight += Source.Weight;
        }

        List<string> Combined = new List<string>();
        Combined.AddRange(Source.Vs);
        Combined.AddRange(Target.Vs);
        Target.Vs = Combined;
        Target.Barycenter = Weight == 0 ? 0 : Sum / Weight;
        Target.Weight = Weight;
        Target.HasBarycenter = Weight != 0 || Target.HasBarycenter || Source.HasBarycenter;
        Target.Index = Math.Min(Source.Index, Target.Index);
        Source.Merged = true;
    }

    private static DagreBarycenter SortEntries(List<DagreBarycenter> Entries, bool BiasRight)
    {
        List<DagreBarycenter> Sortable = new List<DagreBarycenter>();
        List<DagreBarycenter> Unsortable = new List<DagreBarycenter>();
        foreach (DagreBarycenter Entry in Entries)
        {
            if (Entry.HasBarycenter)
            {
                Sortable.Add(Entry);
            }
            else
            {
                Unsortable.Add(Entry);
            }
        }

        Unsortable.Sort(delegate (DagreBarycenter Left, DagreBarycenter Right)
        {
            return Right.Index.CompareTo(Left.Index);
        });
        Sortable.Sort(delegate (DagreBarycenter Left, DagreBarycenter Right)
        {
            if (Left.Barycenter < Right.Barycenter)
            {
                return -1;
            }

            if (Left.Barycenter > Right.Barycenter)
            {
                return 1;
            }

            if (!BiasRight)
            {
                return Left.Index.CompareTo(Right.Index);
            }

            return Right.Index.CompareTo(Left.Index);
        });

        List<string> NodeIds = new List<string>();
        double Sum = 0;
        double Weight = 0;
        int Cursor = 0;
        Cursor = ConsumeUnsortable(NodeIds, Unsortable, Cursor);
        foreach (DagreBarycenter Entry in Sortable)
        {
            Cursor += Entry.Vs.Count;
            NodeIds.AddRange(Entry.Vs);
            Sum += Entry.Barycenter * Entry.Weight;
            Weight += Entry.Weight;
            Cursor = ConsumeUnsortable(NodeIds, Unsortable, Cursor);
        }

        DagreBarycenter Result = new DagreBarycenter
        {
            Vs = NodeIds
        };
        if (Weight != 0)
        {
            Result.HasBarycenter = true;
            Result.Barycenter = Sum / Weight;
            Result.Weight = Weight;
        }

        return Result;
    }

    private static int ConsumeUnsortable(List<string> NodeIds, List<DagreBarycenter> Unsortable, int Index)
    {
        while (Unsortable.Count > 0 && Unsortable[Unsortable.Count - 1].Index <= Index)
        {
            DagreBarycenter Last = Unsortable[Unsortable.Count - 1];
            Unsortable.RemoveAt(Unsortable.Count - 1);
            NodeIds.AddRange(Last.Vs);
            Index++;
        }

        return Index;
    }

    private static int CrossCount(DagreGraph Graph, List<List<string>> Layering)
    {
        int Count = 0;
        for (int Index = 1; Index < Layering.Count; Index++)
        {
            Count += TwoLayerCrossCount(Graph, Layering[Index - 1], Layering[Index]);
        }

        return Count;
    }

    private static int TwoLayerCrossCount(DagreGraph Graph, List<string> North, List<string> South)
    {
        Dictionary<string, int> SouthPos = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int Index = 0; Index < South.Count; Index++)
        {
            if (!string.IsNullOrEmpty(South[Index]))
            {
                SouthPos[South[Index]] = Index;
            }
        }

        List<(int Pos, double Weight)> Entries = new List<(int Pos, double Weight)>();
        foreach (string NodeId in North)
        {
            if (string.IsNullOrEmpty(NodeId))
            {
                continue;
            }

            List<(int Pos, double Weight)> Outgoing = new List<(int Pos, double Weight)>();
            foreach (DagreEdge Edge in Graph.OutEdges(NodeId))
            {
                if (!SouthPos.TryGetValue(Edge.W, out int Pos))
                {
                    continue;
                }

                DagreEdgeLabel? Label = Graph.Edge(Edge);
                Outgoing.Add((Pos, Label?.Weight ?? 0));
            }

            Outgoing.Sort(delegate ((int Pos, double Weight) Left, (int Pos, double Weight) Right)
            {
                return Left.Pos.CompareTo(Right.Pos);
            });
            Entries.AddRange(Outgoing);
        }

        int FirstIndex = 1;
        while (FirstIndex < South.Count)
        {
            FirstIndex <<= 1;
        }

        int TreeSize = (2 * FirstIndex) - 1;
        FirstIndex -= 1;
        double[] Tree = new double[TreeSize];
        double Crossings = 0;
        foreach ((int Pos, double Weight) Entry in Entries)
        {
            int Index = Entry.Pos + FirstIndex;
            if (Index < 0 || Index >= Tree.Length)
            {
                continue;
            }

            Tree[Index] += Entry.Weight;
            double WeightSum = 0;
            while (Index > 0)
            {
                if ((Index % 2) != 0)
                {
                    if (Index + 1 < Tree.Length)
                    {
                        WeightSum += Tree[Index + 1];
                    }
                }

                Index = (Index - 1) >> 1;
                Tree[Index] += Entry.Weight;
            }

            Crossings += Entry.Weight * WeightSum;
        }

        return (int)Crossings;
    }
}
