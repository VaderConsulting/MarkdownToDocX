// Port of dagre-d3-es 7.0.14 rank assignment.
// Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Dagre;

internal static partial class DagreLayoutEngine
{
    private static void AcyclicRun(DagreGraph Graph, DagreContext Context)
    {
        List<DagreEdge> Feedback = Graph.Label.Acyclicer == "greedy"
            ? GreedyFeedback(Graph)
            : DfsFeedback(Graph);
        foreach (DagreEdge Edge in Feedback)
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null)
            {
                continue;
            }

            Graph.RemoveEdge(Edge);
            Label.ForwardName = Edge.Name;
            Label.Reversed = true;
            Context.NextId++;
            string Name = "rev" + Context.NextId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Graph.SetEdge(Edge.W, Edge.V, Label, true, Name);
        }
    }

    private static void AcyclicUndo(DagreGraph Graph)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null || !Label.Reversed)
            {
                continue;
            }

            Graph.RemoveEdge(Edge);
            string? ForwardName = Label.ForwardName;
            Label.Reversed = false;
            Label.ForwardName = null;
            Graph.SetEdge(Edge.W, Edge.V, Label, true, ForwardName);
        }
    }

    private static List<DagreEdge> DfsFeedback(DagreGraph Graph)
    {
        List<DagreEdge> Feedback = [];
        HashSet<string> Stack = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string NodeId in Graph.Nodes())
        {
            DfsFeedbackVisit(Graph, NodeId, Stack, Visited, Feedback);
        }

        return Feedback;
    }

    private static void DfsFeedbackVisit(
        DagreGraph Graph,
        string NodeId,
        HashSet<string> Stack,
        HashSet<string> Visited,
        List<DagreEdge> Feedback)
    {
        if (!Visited.Add(NodeId))
        {
            return;
        }

        Stack.Add(NodeId);
        foreach (DagreEdge Edge in Graph.OutEdges(NodeId))
        {
            if (Stack.Contains(Edge.W))
            {
                Feedback.Add(Edge);
            }
            else
            {
                DfsFeedbackVisit(Graph, Edge.W, Stack, Visited, Feedback);
            }
        }

        Stack.Remove(NodeId);
    }

    private static List<DagreEdge> GreedyFeedback(DagreGraph Graph)
    {
        if (Graph.NodeCount <= 1)
        {
            return [];
        }

        DagreGraph FasGraph = new DagreGraph();
        double MaxIn = 0;
        double MaxOut = 0;
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel Entry = new DagreNodeLabel
            {
                FasNodeId = NodeId
            };
            FasGraph.SetNode(NodeId, Entry, true);
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? WeightLabel = Graph.Edge(Edge);
            double Weight = WeightLabel?.Weight ?? 1;
            DagreEdgeLabel? Previous = FasGraph.Edge(Edge.V, Edge.W);
            double PrevWeight = Previous != null && Previous.IsNumber ? Previous.Number : 0;
            FasGraph.SetEdge(Edge.V, Edge.W, new DagreEdgeLabel
            {
                IsNumber = true,
                Number = PrevWeight + Weight
            }, true);
            DagreNodeLabel? Source = FasGraph.Node(Edge.V);
            DagreNodeLabel? Target = FasGraph.Node(Edge.W);
            if (Source != null)
            {
                Source.FasOut += Weight;
                MaxOut = Math.Max(MaxOut, Source.FasOut);
            }

            if (Target != null)
            {
                Target.FasIn += Weight;
                MaxIn = Math.Max(MaxIn, Target.FasIn);
            }
        }

        int BucketCount = (int)MaxOut + (int)MaxIn + 3;
        List<DagreLinkedList> Buckets = [];
        for (int Index = 0; Index < BucketCount; Index++)
        {
            Buckets.Add(new DagreLinkedList());
        }

        int ZeroIndex = (int)MaxIn + 1;
        foreach (string NodeId in FasGraph.Nodes())
        {
            DagreNodeLabel? Entry = FasGraph.Node(NodeId);
            if (Entry != null)
            {
                AssignBucket(Buckets, ZeroIndex, Entry);
            }
        }

        List<DagreEdge> Simple = DoGreedyFeedback(FasGraph, Buckets, ZeroIndex);
        List<DagreEdge> Expanded = [];
        foreach (DagreEdge Edge in Simple)
        {
            Expanded.AddRange(Graph.OutEdgesTo(Edge.V, Edge.W));
        }

        return Expanded;
    }

    private static List<DagreEdge> DoGreedyFeedback(DagreGraph Graph, List<DagreLinkedList> Buckets, int ZeroIndex)
    {
        List<DagreEdge> Results = [];
        DagreLinkedList Sources = Buckets[Buckets.Count - 1];
        DagreLinkedList Sinks = Buckets[0];
        while (Graph.NodeCount > 0)
        {
            DagreNodeLabel? Entry;
            while ((Entry = Sinks.Dequeue()) != null)
            {
                RemoveFasNode(Graph, Buckets, ZeroIndex, Entry, false, Results);
            }

            while ((Entry = Sources.Dequeue()) != null)
            {
                RemoveFasNode(Graph, Buckets, ZeroIndex, Entry, false, Results);
            }

            if (Graph.NodeCount > 0)
            {
                for (int Index = Buckets.Count - 2; Index > 0; Index--)
                {
                    Entry = Buckets[Index].Dequeue();
                    if (Entry != null)
                    {
                        RemoveFasNode(Graph, Buckets, ZeroIndex, Entry, true, Results);
                        break;
                    }
                }
            }
        }

        return Results;
    }

    private static void RemoveFasNode(
        DagreGraph Graph,
        List<DagreLinkedList> Buckets,
        int ZeroIndex,
        DagreNodeLabel Entry,
        bool CollectPredecessors,
        List<DagreEdge> Results)
    {
        string NodeId = Entry.FasNodeId ?? string.Empty;
        foreach (DagreEdge Edge in Graph.InEdges(NodeId))
        {
            DagreEdgeLabel? WeightLabel = Graph.Edge(Edge);
            double Weight = WeightLabel != null && WeightLabel.IsNumber ? WeightLabel.Number : 0;
            DagreNodeLabel? Source = Graph.Node(Edge.V);
            if (CollectPredecessors)
            {
                Results.Add(new DagreEdge
                {
                    V = Edge.V,
                    W = Edge.W
                });
            }

            if (Source != null)
            {
                Source.FasOut -= Weight;
                AssignBucket(Buckets, ZeroIndex, Source);
            }
        }

        foreach (DagreEdge Edge in Graph.OutEdges(NodeId))
        {
            DagreEdgeLabel? WeightLabel = Graph.Edge(Edge);
            double Weight = WeightLabel != null && WeightLabel.IsNumber ? WeightLabel.Number : 0;
            DagreNodeLabel? Target = Graph.Node(Edge.W);
            if (Target != null)
            {
                Target.FasIn -= Weight;
                AssignBucket(Buckets, ZeroIndex, Target);
            }
        }

        Graph.RemoveNode(NodeId);
    }

    private static void AssignBucket(List<DagreLinkedList> Buckets, int ZeroIndex, DagreNodeLabel Entry)
    {
        if (Entry.FasOut == 0)
        {
            Buckets[0].Enqueue(Entry);
        }
        else if (Entry.FasIn == 0)
        {
            Buckets[Buckets.Count - 1].Enqueue(Entry);
        }
        else
        {
            int Index = (int)(Entry.FasOut - Entry.FasIn) + ZeroIndex;
            Buckets[Index].Enqueue(Entry);
        }
    }

    private static void Rank(DagreGraph Graph)
    {
        string? Ranker = Graph.Label.Ranker;
        if (Ranker == "tight-tree")
        {
            LongestPath(Graph);
            FeasibleTree(Graph);
            return;
        }

        if (Ranker == "longest-path")
        {
            LongestPath(Graph);
            return;
        }

        NetworkSimplex(Graph);
    }

    private static void LongestPath(DagreGraph Graph)
    {
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string Source in Graph.Sources())
        {
            LongestPathVisit(Graph, Source, Visited);
        }
    }

    private static int LongestPathVisit(DagreGraph Graph, string NodeId, HashSet<string> Visited)
    {
        DagreNodeLabel? Label = Graph.Node(NodeId);
        if (Label == null)
        {
            return 0;
        }

        if (!Visited.Add(NodeId))
        {
            return Label.Rank ?? 0;
        }

        int? Best = null;
        foreach (DagreEdge Edge in Graph.OutEdges(NodeId))
        {
            DagreEdgeLabel? EdgeLabel = Graph.Edge(Edge);
            int Minlen = EdgeLabel?.Minlen ?? 1;
            int Candidate = LongestPathVisit(Graph, Edge.W, Visited) - Minlen;
            if (Best == null || Candidate < Best.Value)
            {
                Best = Candidate;
            }
        }

        int Rank = Best ?? 0;
        Label.Rank = Rank;
        return Rank;
    }

    private static DagreGraph FeasibleTree(DagreGraph Graph)
    {
        DagreGraph Tree = new DagreGraph(Directed: false, Multigraph: false, Compound: false);
        List<string> Nodes = Graph.Nodes();
        if (Nodes.Count == 0)
        {
            return Tree;
        }

        string Start = Nodes[0];
        int Size = Graph.NodeCount;
        Tree.SetNode(Start, new DagreNodeLabel(), true);
        while (TightTree(Tree, Graph) < Size)
        {
            DagreEdge? Edge = FindMinSlackEdge(Tree, Graph);
            if (Edge == null)
            {
                break;
            }

            int Delta = Tree.HasNode(Edge.V) ? Slack(Graph, Edge) : -Slack(Graph, Edge);
            foreach (string NodeId in Tree.Nodes())
            {
                DagreNodeLabel? Label = Graph.Node(NodeId);
                if (Label?.Rank != null)
                {
                    Label.Rank += Delta;
                }
            }
        }

        return Tree;
    }

    private static int TightTree(DagreGraph Tree, DagreGraph Graph)
    {
        foreach (string NodeId in Tree.Nodes())
        {
            TightTreeVisit(Tree, Graph, NodeId);
        }

        return Tree.NodeCount;
    }

    private static void TightTreeVisit(DagreGraph Tree, DagreGraph Graph, string NodeId)
    {
        foreach (DagreEdge Edge in Graph.NodeEdges(NodeId))
        {
            string Other = NodeId == Edge.V ? Edge.W : Edge.V;
            if (!Tree.HasNode(Other) && Slack(Graph, Edge) == 0)
            {
                Tree.SetNode(Other, new DagreNodeLabel(), true);
                Tree.SetEdge(NodeId, Other, new DagreEdgeLabel(), true);
                TightTreeVisit(Tree, Graph, Other);
            }
        }
    }

    private static DagreEdge? FindMinSlackEdge(DagreGraph Tree, DagreGraph Graph)
    {
        DagreEdge? Best = null;
        int BestSlack = 0;
        bool Found = false;
        foreach (DagreEdge Edge in Graph.Edges())
        {
            if (Tree.HasNode(Edge.V) == Tree.HasNode(Edge.W))
            {
                continue;
            }

            int EdgeSlack = Slack(Graph, Edge);
            if (!Found || EdgeSlack < BestSlack)
            {
                Found = true;
                BestSlack = EdgeSlack;
                Best = Edge;
            }
        }

        return Best;
    }

    private static void NetworkSimplex(DagreGraph Graph)
    {
        DagreGraph Simplified = Simplify(Graph);
        LongestPath(Simplified);
        DagreGraph Tree = FeasibleTree(Simplified);
        InitLowLim(Tree, null);
        InitCutValues(Tree, Simplified);
        DagreEdge? Leaving;
        while ((Leaving = LeaveEdge(Tree)) != null)
        {
            DagreEdge? Entering = EnterEdge(Tree, Simplified, Leaving);
            if (Entering == null)
            {
                break;
            }

            ExchangeEdges(Tree, Simplified, Leaving, Entering);
        }
    }

    private static void InitCutValues(DagreGraph Tree, DagreGraph Graph)
    {
        List<string> Nodes = Walk(Tree, Tree.Nodes(), true);
        if (Nodes.Count == 0)
        {
            return;
        }

        Nodes.RemoveAt(Nodes.Count - 1);
        foreach (string Child in Nodes)
        {
            DagreNodeLabel? Label = Tree.Node(Child);
            if (Label?.TreeParent == null)
            {
                continue;
            }

            DagreEdgeLabel? TreeEdge = Tree.Edge(Child, Label.TreeParent);
            TreeEdge?.CutValue = CalcCutValue(Tree, Graph, Child);
        }
    }

    private static double CalcCutValue(DagreGraph Tree, DagreGraph Graph, string Child)
    {
        DagreNodeLabel? ChildLabel = Tree.Node(Child);
        if (ChildLabel?.TreeParent == null)
        {
            return 0;
        }

        string Parent = ChildLabel.TreeParent;
        bool ChildIsTail = true;
        DagreEdgeLabel? GraphEdge = Graph.Edge(Child, Parent);
        if (GraphEdge == null)
        {
            ChildIsTail = false;
            GraphEdge = Graph.Edge(Parent, Child);
        }

        if (GraphEdge == null)
        {
            return 0;
        }

        double CutValue = GraphEdge.Weight;
        foreach (DagreEdge Edge in Graph.NodeEdges(Child))
        {
            bool IsOut = Edge.V == Child;
            string Other = IsOut ? Edge.W : Edge.V;
            if (Other == Parent)
            {
                continue;
            }

            bool PointsToHead = IsOut == ChildIsTail;
            DagreEdgeLabel? OtherLabel = Graph.Edge(Edge);
            double OtherWeight = OtherLabel?.Weight ?? 0;
            CutValue += PointsToHead ? OtherWeight : -OtherWeight;
            if (Tree.HasEdge(Child, Other))
            {
                DagreEdgeLabel? OtherCut = Tree.Edge(Child, Other);
                double OtherCutValue = OtherCut?.CutValue ?? 0;
                CutValue += PointsToHead ? -OtherCutValue : OtherCutValue;
            }
        }

        return CutValue;
    }

    private static void InitLowLim(DagreGraph Tree, string? Root)
    {
        List<string> Nodes = Tree.Nodes();
        if (Nodes.Count == 0)
        {
            return;
        }

        string Start = Root ?? Nodes[0];
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        AssignLowLim(Tree, Visited, 1, Start, null);
    }

    private static int AssignLowLim(DagreGraph Tree, HashSet<string> Visited, int NextLim, string NodeId, string? Parent)
    {
        int Low = NextLim;
        DagreNodeLabel? Label = Tree.Node(NodeId);
        Visited.Add(NodeId);
        foreach (string Neighbour in Tree.Neighbors(NodeId))
        {
            if (!Visited.Contains(Neighbour))
            {
                NextLim = AssignLowLim(Tree, Visited, NextLim, Neighbour, NodeId);
            }
        }

        if (Label != null)
        {
            Label.Low = Low;
            Label.Lim = NextLim;
            NextLim++;
            if (Parent != null)
            {
                Label.TreeParent = Parent;
                Label.HasTreeParent = true;
            }
            else
            {
                Label.TreeParent = null;
                Label.HasTreeParent = false;
            }
        }

        return NextLim;
    }

    private static DagreEdge? LeaveEdge(DagreGraph Tree)
    {
        foreach (DagreEdge Edge in Tree.Edges())
        {
            DagreEdgeLabel? Label = Tree.Edge(Edge);
            if (Label?.CutValue != null && Label.CutValue.Value < 0)
            {
                return Edge;
            }
        }

        return null;
    }

    private static DagreEdge? EnterEdge(DagreGraph Tree, DagreGraph Graph, DagreEdge Edge)
    {
        string Tail = Edge.V;
        string Head = Edge.W;
        if (!Graph.HasEdge(Tail, Head))
        {
            Tail = Edge.W;
            Head = Edge.V;
        }

        DagreNodeLabel? TailLabel = Tree.Node(Tail);
        DagreNodeLabel? HeadLabel = Tree.Node(Head);
        if (TailLabel?.Lim == null || HeadLabel?.Lim == null)
        {
            return null;
        }

        DagreNodeLabel LimitLabel = TailLabel;
        bool Flip = false;
        if (TailLabel.Lim.Value > HeadLabel.Lim.Value)
        {
            LimitLabel = HeadLabel;
            Flip = true;
        }

        DagreEdge? Best = null;
        int BestSlack = 0;
        bool Found = false;
        foreach (DagreEdge Candidate in Graph.Edges())
        {
            DagreNodeLabel? CandidateTail = Tree.Node(Candidate.V);
            DagreNodeLabel? CandidateHead = Tree.Node(Candidate.W);
            if (CandidateTail == null || CandidateHead == null)
            {
                continue;
            }

            bool TailInside = IsDescendant(CandidateTail, LimitLabel);
            bool HeadInside = IsDescendant(CandidateHead, LimitLabel);
            if (Flip != TailInside || Flip == HeadInside)
            {
                continue;
            }

            int CandidateSlack = Slack(Graph, Candidate);
            if (!Found || CandidateSlack < BestSlack)
            {
                Found = true;
                BestSlack = CandidateSlack;
                Best = Candidate;
            }
        }

        return Best;
    }

    private static bool IsDescendant(DagreNodeLabel Node, DagreNodeLabel Root)
    {
        if (Node.Lim == null || Root.Low == null || Root.Lim == null)
        {
            return false;
        }

        return Root.Low.Value <= Node.Lim.Value && Node.Lim.Value <= Root.Lim.Value;
    }

    private static void ExchangeEdges(DagreGraph Tree, DagreGraph Graph, DagreEdge Leaving, DagreEdge Entering)
    {
        Tree.RemoveEdge(Leaving.V, Leaving.W);
        Tree.SetEdge(Entering.V, Entering.W, new DagreEdgeLabel(), true);
        InitLowLim(Tree, null);
        InitCutValues(Tree, Graph);
        UpdateRanks(Tree, Graph);
    }

    private static void UpdateRanks(DagreGraph Tree, DagreGraph Graph)
    {
        string? Root = null;
        foreach (string NodeId in Tree.Nodes())
        {
            DagreNodeLabel? Label = Graph.Node(NodeId);
            if (Label != null && !Label.HasTreeParent)
            {
                Root = NodeId;
                break;
            }
        }

        if (Root == null)
        {
            return;
        }

        List<string> Order = Walk(Tree, new List<string> { Root }, false);
        if (Order.Count > 0)
        {
            Order.RemoveAt(0);
        }

        foreach (string NodeId in Order)
        {
            DagreNodeLabel? TreeLabel = Tree.Node(NodeId);
            if (TreeLabel?.TreeParent == null)
            {
                continue;
            }

            string Parent = TreeLabel.TreeParent;
            DagreEdgeLabel? Edge = Graph.Edge(NodeId, Parent);
            bool Flipped = false;
            if (Edge == null)
            {
                Edge = Graph.Edge(Parent, NodeId);
                Flipped = true;
            }

            DagreNodeLabel? Node = Graph.Node(NodeId);
            DagreNodeLabel? ParentNode = Graph.Node(Parent);
            if (Edge == null || Node == null || ParentNode?.Rank == null)
            {
                continue;
            }

            Node.Rank = ParentNode.Rank.Value + (Flipped ? Edge.Minlen : -Edge.Minlen);
        }
    }
}
