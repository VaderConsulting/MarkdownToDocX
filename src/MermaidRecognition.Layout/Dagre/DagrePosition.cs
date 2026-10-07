// Port of dagre-d3-es 7.0.14 position and coordinate assignment.
// Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Dagre;

internal static partial class DagreLayoutEngine
{
    private static void Position(DagreGraph Graph)
    {
        DagreGraph Flat = AsNonCompoundGraph(Graph);
        PositionY(Flat);
        Dictionary<string, double> Xs = PositionX(Flat);
        foreach (KeyValuePair<string, double> Pair in Xs)
        {
            DagreNodeLabel? Node = Flat.Node(Pair.Key);
            if (Node != null)
            {
                Node.X = Pair.Value;
            }
        }
    }

    private static void PositionY(DagreGraph Graph)
    {
        List<List<string>> Layering = BuildLayerMatrix(Graph);
        double RankSep = Graph.Label.RankSep;
        double Previous = 0;
        foreach (List<string> Layer in Layering)
        {
            double MaxHeight = 0;
            bool Any = false;
            foreach (string NodeId in Layer)
            {
                if (string.IsNullOrEmpty(NodeId))
                {
                    continue;
                }

                DagreNodeLabel? Node = Graph.Node(NodeId);
                if (Node == null)
                {
                    continue;
                }

                Any = true;
                if (Node.Height > MaxHeight)
                {
                    MaxHeight = Node.Height;
                }
            }

            if (Any)
            {
                foreach (string NodeId in Layer)
                {
                    if (string.IsNullOrEmpty(NodeId))
                    {
                        continue;
                    }

                    DagreNodeLabel? Node = Graph.Node(NodeId);
                    if (Node != null)
                    {
                        Node.Y = Previous + (MaxHeight / 2);
                    }
                }
            }

            Previous += MaxHeight + RankSep;
        }
    }

    private static Dictionary<string, double> PositionX(DagreGraph Graph)
    {
        List<List<string>> Layering = BuildLayerMatrix(Graph);
        Dictionary<string, HashSet<string>> Conflicts = FindType1Conflicts(Graph, Layering);
        MergeConflicts(Conflicts, FindType2Conflicts(Graph, Layering));
        Dictionary<string, Dictionary<string, double>> Alignments = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        foreach (string Vertical in new[] { "u", "d" })
        {
            List<List<string>> Adjusted = new List<List<string>>();
            if (Vertical == "u")
            {
                foreach (List<string> Layer in Layering)
                {
                    Adjusted.Add(new List<string>(Layer));
                }
            }
            else
            {
                for (int Index = Layering.Count - 1; Index >= 0; Index--)
                {
                    Adjusted.Add(new List<string>(Layering[Index]));
                }
            }

            foreach (string Horizontal in new[] { "l", "r" })
            {
                List<List<string>> Directed = Adjusted;
                if (Horizontal == "r")
                {
                    Directed = new List<List<string>>();
                    foreach (List<string> Layer in Adjusted)
                    {
                        List<string> Reversed = new List<string>(Layer);
                        Reversed.Reverse();
                        Directed.Add(Reversed);
                    }
                }

                bool UsePredecessors = Vertical == "u";
                (Dictionary<string, string> Root, Dictionary<string, string> Align) = VerticalAlignment(Graph, Directed, Conflicts, UsePredecessors);
                Dictionary<string, double> Xs = HorizontalCompaction(Graph, Directed, Root, Align, Horizontal == "r");
                if (Horizontal == "r")
                {
                    List<string> Keys = new List<string>(Xs.Keys);
                    foreach (string Key in Keys)
                    {
                        Xs[Key] = -Xs[Key];
                    }
                }

                Alignments[Vertical + Horizontal] = Xs;
            }
        }

        Dictionary<string, double> Smallest = FindSmallestWidthAlignment(Graph, Alignments);
        AlignCoordinates(Alignments, Smallest);
        return Balance(Alignments, Graph.Label.Align);
    }

    private static void MergeConflicts(Dictionary<string, HashSet<string>> Target, Dictionary<string, HashSet<string>> Extra)
    {
        foreach (KeyValuePair<string, HashSet<string>> Pair in Extra)
        {
            if (!Target.TryGetValue(Pair.Key, out HashSet<string>? Set))
            {
                Set = new HashSet<string>(StringComparer.Ordinal);
                Target[Pair.Key] = Set;
            }

            foreach (string Other in Pair.Value)
            {
                Set.Add(Other);
            }
        }
    }

    private static Dictionary<string, HashSet<string>> FindType1Conflicts(DagreGraph Graph, List<List<string>> Layering)
    {
        Dictionary<string, HashSet<string>> Conflicts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        for (int Index = 1; Index < Layering.Count; Index++)
        {
            VisitType1(Graph, Conflicts, Layering[Index - 1], Layering[Index]);
        }

        return Conflicts;
    }

    private static void VisitType1(DagreGraph Graph, Dictionary<string, HashSet<string>> Conflicts, List<string> Previous, List<string> Layer)
    {
        int K0 = 0;
        int ScanPos = 0;
        int PreviousLength = Previous.Count;
        string? Last = Layer.Count == 0 ? null : Layer[Layer.Count - 1];
        for (int Index = 0; Index < Layer.Count; Index++)
        {
            string NodeId = Layer[Index];
            if (string.IsNullOrEmpty(NodeId))
            {
                continue;
            }

            string? Other = FindOtherInnerSegmentNode(Graph, NodeId);
            int K1 = PreviousLength;
            if (Other != null)
            {
                DagreNodeLabel? OtherNode = Graph.Node(Other);
                if (OtherNode?.Order != null)
                {
                    K1 = OtherNode.Order.Value;
                }
            }

            if (Other != null || NodeId == Last)
            {
                for (int Scan = ScanPos; Scan <= Index && Scan < Layer.Count; Scan++)
                {
                    string ScanNode = Layer[Scan];
                    if (string.IsNullOrEmpty(ScanNode))
                    {
                        continue;
                    }

                    foreach (string Predecessor in Graph.Predecessors(ScanNode))
                    {
                        DagreNodeLabel? PredNode = Graph.Node(Predecessor);
                        DagreNodeLabel? ScanLabel = Graph.Node(ScanNode);
                        int PredOrder = PredNode?.Order ?? 0;
                        bool BothDummy = PredNode?.Dummy != null && ScanLabel?.Dummy != null;
                        if ((PredOrder < K0 || K1 < PredOrder) && !BothDummy)
                        {
                            AddConflict(Conflicts, Predecessor, ScanNode);
                        }
                    }
                }

                ScanPos = Index + 1;
                K0 = K1;
            }
        }
    }

    private static string? FindOtherInnerSegmentNode(DagreGraph Graph, string NodeId)
    {
        DagreNodeLabel? Node = Graph.Node(NodeId);
        if (Node?.Dummy == null)
        {
            return null;
        }

        foreach (string Predecessor in Graph.Predecessors(NodeId))
        {
            DagreNodeLabel? Pred = Graph.Node(Predecessor);
            if (Pred?.Dummy != null)
            {
                return Predecessor;
            }
        }

        return null;
    }

    private static Dictionary<string, HashSet<string>> FindType2Conflicts(DagreGraph Graph, List<List<string>> Layering)
    {
        Dictionary<string, HashSet<string>> Conflicts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        for (int Index = 1; Index < Layering.Count; Index++)
        {
            VisitType2(Graph, Conflicts, Layering[Index - 1], Layering[Index]);
        }

        return Conflicts;
    }

    private static void VisitType2(DagreGraph Graph, Dictionary<string, HashSet<string>> Conflicts, List<string> North, List<string> South)
    {
        int PreviousNorth = -1;
        int? NextNorth = null;
        int SouthPos = 0;
        for (int Lookahead = 0; Lookahead < South.Count; Lookahead++)
        {
            string NodeId = South[Lookahead];
            if (!string.IsNullOrEmpty(NodeId))
            {
                DagreNodeLabel? Node = Graph.Node(NodeId);
                if (Node?.Dummy == "border")
                {
                    List<string> Preds = Graph.Predecessors(NodeId);
                    if (Preds.Count > 0)
                    {
                        DagreNodeLabel? Pred = Graph.Node(Preds[0]);
                        if (Pred?.Order != null)
                        {
                            NextNorth = Pred.Order.Value;
                            ScanType2(Graph, Conflicts, South, SouthPos, Lookahead, PreviousNorth, NextNorth);
                            SouthPos = Lookahead;
                            PreviousNorth = NextNorth.Value;
                        }
                    }
                }
            }

            ScanType2(Graph, Conflicts, South, SouthPos, South.Count, NextNorth ?? int.MinValue, North.Count);
        }
    }

    private static void ScanType2(
        DagreGraph Graph,
        Dictionary<string, HashSet<string>> Conflicts,
        List<string> South,
        int SouthPos,
        int SouthEnd,
        int PreviousNorth,
        int? NextNorth)
    {
        for (int Index = SouthPos; Index < SouthEnd && Index < South.Count; Index++)
        {
            string NodeId = South[Index];
            if (string.IsNullOrEmpty(NodeId))
            {
                continue;
            }

            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Dummy == null)
            {
                continue;
            }

            foreach (string Predecessor in Graph.Predecessors(NodeId))
            {
                DagreNodeLabel? Pred = Graph.Node(Predecessor);
                if (Pred?.Dummy == null || Pred.Order == null)
                {
                    continue;
                }

                bool Before = Pred.Order.Value < PreviousNorth;
                bool After = NextNorth != null && Pred.Order.Value > NextNorth.Value;
                if (Before || After)
                {
                    AddConflict(Conflicts, Predecessor, NodeId);
                }
            }
        }
    }

    private static void AddConflict(Dictionary<string, HashSet<string>> Conflicts, string Left, string Right)
    {
        if (string.CompareOrdinal(Left, Right) > 0)
        {
            string Swap = Left;
            Left = Right;
            Right = Swap;
        }

        if (!Conflicts.TryGetValue(Left, out HashSet<string>? Set))
        {
            Set = new HashSet<string>(StringComparer.Ordinal);
            Conflicts[Left] = Set;
        }

        Set.Add(Right);
    }

    private static bool HasConflict(Dictionary<string, HashSet<string>> Conflicts, string Left, string Right)
    {
        if (string.CompareOrdinal(Left, Right) > 0)
        {
            string Swap = Left;
            Left = Right;
            Right = Swap;
        }

        return Conflicts.TryGetValue(Left, out HashSet<string>? Set) && Set.Contains(Right);
    }

    private static (Dictionary<string, string> Root, Dictionary<string, string> Align) VerticalAlignment(
        DagreGraph Graph,
        List<List<string>> Layering,
        Dictionary<string, HashSet<string>> Conflicts,
        bool UsePredecessors)
    {
        Dictionary<string, string> Root = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> Align = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, int> Pos = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (List<string> Layer in Layering)
        {
            for (int Order = 0; Order < Layer.Count; Order++)
            {
                string NodeId = Layer[Order];
                if (string.IsNullOrEmpty(NodeId))
                {
                    continue;
                }

                Root[NodeId] = NodeId;
                Align[NodeId] = NodeId;
                Pos[NodeId] = Order;
            }
        }

        foreach (List<string> Layer in Layering)
        {
            int PreviousIndex = -1;
            foreach (string NodeId in Layer)
            {
                if (string.IsNullOrEmpty(NodeId))
                {
                    continue;
                }

                List<string> Neighbours = UsePredecessors ? Graph.Predecessors(NodeId) : Graph.Successors(NodeId);
                if (Neighbours.Count == 0)
                {
                    continue;
                }

                Neighbours.Sort(delegate (string Left, string Right)
                {
                    int LeftPos = Pos.TryGetValue(Left, out int LeftOrder) ? LeftOrder : 0;
                    int RightPos = Pos.TryGetValue(Right, out int RightOrder) ? RightOrder : 0;
                    return LeftPos.CompareTo(RightPos);
                });
                double Middle = (Neighbours.Count - 1) / 2.0;
                int Start = (int)Math.Floor(Middle);
                int End = (int)Math.Ceiling(Middle);
                for (int Index = Start; Index <= End; Index++)
                {
                    string Neighbour = Neighbours[Index];
                    if (!Pos.ContainsKey(Neighbour) || !Align.ContainsKey(NodeId))
                    {
                        continue;
                    }

                    if (Align[NodeId] == NodeId && PreviousIndex < Pos[Neighbour] && !HasConflict(Conflicts, NodeId, Neighbour))
                    {
                        Align[Neighbour] = NodeId;
                        string NeighbourRoot = Root.TryGetValue(Neighbour, out string? Existing) ? Existing : Neighbour;
                        Align[NodeId] = NeighbourRoot;
                        Root[NodeId] = NeighbourRoot;
                        PreviousIndex = Pos[Neighbour];
                    }
                }
            }
        }

        return (Root, Align);
    }

    private static Dictionary<string, double> HorizontalCompaction(
        DagreGraph Graph,
        List<List<string>> Layering,
        Dictionary<string, string> Root,
        Dictionary<string, string> Align,
        bool ReverseSep)
    {
        Dictionary<string, double> Xs = new Dictionary<string, double>(StringComparer.Ordinal);
        DagreGraph Block = BuildBlockGraph(Graph, Layering, Root, ReverseSep);
        string BorderType = ReverseSep ? "borderLeft" : "borderRight";

        void Iterate(Action<string> SetXs, Func<string, List<string>> NextNodes)
        {
            List<string> Stack = Block.Nodes();
            HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
            while (Stack.Count > 0)
            {
                string Elem = Stack[Stack.Count - 1];
                Stack.RemoveAt(Stack.Count - 1);
                if (Visited.Contains(Elem))
                {
                    SetXs(Elem);
                }
                else
                {
                    Visited.Add(Elem);
                    Stack.Add(Elem);
                    Stack.AddRange(NextNodes(Elem));
                }
            }
        }

        Iterate(delegate (string Elem)
        {
            double Value = 0;
            foreach (DagreEdge Edge in Block.InEdges(Elem))
            {
                DagreEdgeLabel? Label = Block.Edge(Edge);
                double Separation = Label != null && Label.IsNumber ? Label.Number : 0;
                double Source = Xs.TryGetValue(Edge.V, out double SourceX) ? SourceX : 0;
                Value = Math.Max(Value, Source + Separation);
            }

            Xs[Elem] = Value;
        }, Block.Predecessors);

        Iterate(delegate (string Elem)
        {
            double Min = double.PositiveInfinity;
            foreach (DagreEdge Edge in Block.OutEdges(Elem))
            {
                DagreEdgeLabel? Label = Block.Edge(Edge);
                double Separation = Label != null && Label.IsNumber ? Label.Number : 0;
                if (Xs.TryGetValue(Edge.W, out double Target))
                {
                    Min = Math.Min(Min, Target - Separation);
                }
            }

            DagreNodeLabel? Node = Graph.Node(Elem);
            if (!double.IsPositiveInfinity(Min) && Node?.BorderType != BorderType)
            {
                double Current = Xs.TryGetValue(Elem, out double Existing) ? Existing : 0;
                Xs[Elem] = Math.Max(Current, Min);
            }
        }, Block.Successors);

        foreach (KeyValuePair<string, string> Pair in Align)
        {
            if (Root.TryGetValue(Pair.Key, out string? RootId) && Xs.TryGetValue(RootId, out double RootX))
            {
                Xs[Pair.Key] = RootX;
            }
        }

        return Xs;
    }

    private static DagreGraph BuildBlockGraph(
        DagreGraph Graph,
        List<List<string>> Layering,
        Dictionary<string, string> Root,
        bool ReverseSep)
    {
        DagreGraph Block = new DagreGraph();
        foreach (List<string> Layer in Layering)
        {
            string? Previous = null;
            foreach (string NodeId in Layer)
            {
                if (string.IsNullOrEmpty(NodeId))
                {
                    continue;
                }

                string NodeRoot = Root.TryGetValue(NodeId, out string? RootId) ? RootId : NodeId;
                Block.SetNode(NodeRoot, new DagreNodeLabel(), true);
                if (Previous != null)
                {
                    string PreviousRoot = Root.TryGetValue(Previous, out string? PreviousRootId) ? PreviousRootId : Previous;
                    DagreEdgeLabel? Existing = Block.Edge(PreviousRoot, NodeRoot);
                    double PreviousSep = Existing != null && Existing.IsNumber ? Existing.Number : 0;
                    double Separation = SeparationOf(Graph, NodeId, Previous, ReverseSep);
                    Block.SetEdge(PreviousRoot, NodeRoot, new DagreEdgeLabel
                    {
                        IsNumber = true,
                        Number = Math.Max(Separation, PreviousSep)
                    }, true);
                }

                Previous = NodeId;
            }
        }

        return Block;
    }

    private static double SeparationOf(DagreGraph Graph, string NodeId, string PreviousId, bool ReverseSep)
    {
        DagreNodeLabel? Node = Graph.Node(NodeId);
        DagreNodeLabel? Previous = Graph.Node(PreviousId);
        if (Node == null || Previous == null)
        {
            return 0;
        }

        double Sum = 0;
        Sum += Previous.Width / 2;
        double Delta = LabelDelta(Previous, true);
        if (Delta != 0)
        {
            Sum += ReverseSep ? Delta : -Delta;
        }

        Sum += (Previous.Dummy != null ? Graph.Label.EdgeSep : Graph.Label.NodeSep) / 2;
        Sum += (Node.Dummy != null ? Graph.Label.EdgeSep : Graph.Label.NodeSep) / 2;
        Sum += Node.Width / 2;
        Delta = LabelDelta(Node, false);
        if (Delta != 0)
        {
            Sum += ReverseSep ? Delta : -Delta;
        }

        return Sum;
    }

    private static double LabelDelta(DagreNodeLabel Node, bool Previous)
    {
        if (string.IsNullOrEmpty(Node.LabelPos))
        {
            return 0;
        }

        string Pos = Node.LabelPos.ToLowerInvariant();
        if (Previous)
        {
            if (Pos == "l")
            {
                return -Node.Width / 2;
            }

            if (Pos == "r")
            {
                return Node.Width / 2;
            }
        }
        else
        {
            if (Pos == "l")
            {
                return Node.Width / 2;
            }

            if (Pos == "r")
            {
                return -Node.Width / 2;
            }
        }

        return 0;
    }

    private static Dictionary<string, double> FindSmallestWidthAlignment(DagreGraph Graph, Dictionary<string, Dictionary<string, double>> Alignments)
    {
        Dictionary<string, double>? Best = null;
        double BestWidth = double.PositiveInfinity;
        foreach (Dictionary<string, double> Xs in Alignments.Values)
        {
            double Max = double.NegativeInfinity;
            double Min = double.PositiveInfinity;
            foreach (KeyValuePair<string, double> Pair in Xs)
            {
                DagreNodeLabel? Node = Graph.Node(Pair.Key);
                double Half = Node == null ? 0 : Node.Width / 2;
                Max = Math.Max(Pair.Value + Half, Max);
                Min = Math.Min(Pair.Value - Half, Min);
            }

            double Width = Max - Min;
            if (Width < BestWidth)
            {
                BestWidth = Width;
                Best = Xs;
            }
        }

        return Best ?? new Dictionary<string, double>(StringComparer.Ordinal);
    }

    private static void AlignCoordinates(Dictionary<string, Dictionary<string, double>> Alignments, Dictionary<string, double> AlignTo)
    {
        double AlignMin = double.PositiveInfinity;
        double AlignMax = double.NegativeInfinity;
        foreach (double Value in AlignTo.Values)
        {
            AlignMin = Math.Min(AlignMin, Value);
            AlignMax = Math.Max(AlignMax, Value);
        }

        foreach (string Vertical in new[] { "u", "d" })
        {
            foreach (string Horizontal in new[] { "l", "r" })
            {
                string Key = Vertical + Horizontal;
                if (!Alignments.TryGetValue(Key, out Dictionary<string, double>? Xs) || ReferenceEquals(Xs, AlignTo))
                {
                    continue;
                }

                double Min = double.PositiveInfinity;
                double Max = double.NegativeInfinity;
                foreach (double Value in Xs.Values)
                {
                    Min = Math.Min(Min, Value);
                    Max = Math.Max(Max, Value);
                }

                double Delta = Horizontal == "l" ? AlignMin - Min : AlignMax - Max;
                if (Delta != 0)
                {
                    Dictionary<string, double> Shifted = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, double> Pair in Xs)
                    {
                        Shifted[Pair.Key] = Pair.Value + Delta;
                    }

                    Alignments[Key] = Shifted;
                }
            }
        }
    }

    private static Dictionary<string, double> Balance(Dictionary<string, Dictionary<string, double>> Alignments, string? Align)
    {
        Dictionary<string, double> Result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (!Alignments.TryGetValue("ul", out Dictionary<string, double>? UpperLeft))
        {
            return Result;
        }

        foreach (string NodeId in UpperLeft.Keys)
        {
            if (!string.IsNullOrEmpty(Align))
            {
                string Key = Align.ToLowerInvariant();
                if (Alignments.TryGetValue(Key, out Dictionary<string, double>? Chosen) && Chosen.TryGetValue(NodeId, out double ChosenX))
                {
                    Result[NodeId] = ChosenX;
                }

                continue;
            }

            List<double> Values = new List<double>();
            foreach (Dictionary<string, double> Xs in Alignments.Values)
            {
                if (Xs.TryGetValue(NodeId, out double Value))
                {
                    Values.Add(Value);
                }
            }

            Values.Sort();
            if (Values.Count >= 4)
            {
                Result[NodeId] = (Values[1] + Values[2]) / 2;
            }
            else if (Values.Count > 0)
            {
                Result[NodeId] = Values[Values.Count / 2];
            }
        }

        return Result;
    }
}
