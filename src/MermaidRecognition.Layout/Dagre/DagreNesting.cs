// Port of dagre-d3-es 7.0.14 nesting-graph.js and parent-dummy-chains.js.
// Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Dagre;

internal static partial class DagreLayoutEngine
{
    private static void NestingRun(DagreGraph Graph, DagreContext Context)
    {
        DagreNodeLabel RootLabel = new DagreNodeLabel();
        string Root = AddDummyNode(Graph, Context, "root", RootLabel, "_root");
        Dictionary<string, int> Depths = TreeDepths(Graph);
        int Height = 0;
        foreach (int Depth in Depths.Values)
        {
            if (Depth > Height)
            {
                Height = Depth;
            }
        }

        Height -= 1;
        if (Height < 0)
        {
            Height = 0;
        }

        int NodeSep = (2 * Height) + 1;
        Graph.Label.NestingRoot = Root;
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            Label?.Minlen *= NodeSep;
        }

        double Weight = 1;
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label != null)
            {
                Weight += Label.Weight;
            }
        }

        foreach (string Child in Graph.Children(null))
        {
            NestingVisit(Graph, Context, Root, NodeSep, Weight, Height, Depths, Child);
        }

        Graph.Label.NodeRankFactor = NodeSep;
        Graph.Label.HasNodeRankFactor = true;
    }

    private static void NestingVisit(
        DagreGraph Graph,
        DagreContext Context,
        string Root,
        int NodeSep,
        double Weight,
        int Height,
        Dictionary<string, int> Depths,
        string NodeId)
    {
        List<string> Children = Graph.Children(NodeId);
        if (Children.Count == 0)
        {
            if (NodeId != Root)
            {
                Graph.SetEdge(Root, NodeId, new DagreEdgeLabel
                {
                    Weight = 0,
                    Minlen = NodeSep
                }, true);
            }

            return;
        }

        string Top = AddBorderNode(Graph, Context, "_bt", null, null);
        string Bottom = AddBorderNode(Graph, Context, "_bb", null, null);
        DagreNodeLabel? Label = Graph.Node(NodeId);
        Graph.SetParent(Top, NodeId);
        Label?.BorderTop = Top;

        Graph.SetParent(Bottom, NodeId);
        Label?.BorderBottom = Bottom;

        foreach (string Child in Children)
        {
            NestingVisit(Graph, Context, Root, NodeSep, Weight, Height, Depths, Child);
            DagreNodeLabel? ChildNode = Graph.Node(Child);
            string ChildTop = ChildNode?.BorderTop ?? Child;
            string ChildBottom = ChildNode?.BorderBottom ?? Child;
            double ThisWeight = ChildNode?.BorderTop != null ? Weight : 2 * Weight;
            int Minlen = ChildTop != ChildBottom ? 1 : Height - DepthOf(Depths, NodeId) + 1;
            Graph.SetEdge(Top, ChildTop, new DagreEdgeLabel
            {
                Weight = ThisWeight,
                Minlen = Minlen,
                NestingEdge = true
            }, true);
            Graph.SetEdge(ChildBottom, Bottom, new DagreEdgeLabel
            {
                Weight = ThisWeight,
                Minlen = Minlen,
                NestingEdge = true
            }, true);
        }

        if (Graph.Parent(NodeId) == null)
        {
            Graph.SetEdge(Root, Top, new DagreEdgeLabel
            {
                Weight = 0,
                Minlen = Height + DepthOf(Depths, NodeId)
            }, true);
        }
    }

    private static int DepthOf(Dictionary<string, int> Depths, string NodeId)
    {
        if (Depths.TryGetValue(NodeId, out int Depth))
        {
            return Depth;
        }

        return 0;
    }

    private static Dictionary<string, int> TreeDepths(DagreGraph Graph)
    {
        Dictionary<string, int> Depths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string Child in Graph.Children(null))
        {
            TreeDepthVisit(Graph, Child, 1, Depths);
        }

        return Depths;
    }

    private static void TreeDepthVisit(DagreGraph Graph, string NodeId, int Depth, Dictionary<string, int> Depths)
    {
        List<string> Children = Graph.Children(NodeId);
        if (Children.Count > 0)
        {
            foreach (string Child in Children)
            {
                TreeDepthVisit(Graph, Child, Depth + 1, Depths);
            }
        }

        Depths[NodeId] = Depth;
    }

    private static void NestingCleanup(DagreGraph Graph)
    {
        if (Graph.Label.NestingRoot != null)
        {
            Graph.RemoveNode(Graph.Label.NestingRoot);
            Graph.Label.NestingRoot = null;
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label != null && Label.NestingEdge)
            {
                Graph.RemoveEdge(Edge);
            }
        }
    }

    private static string AddBorderNode(DagreGraph Graph, DagreContext Context, string Prefix, int? Rank, int? Order)
    {
        DagreNodeLabel Node = new DagreNodeLabel
        {
            Width = 0,
            Height = 0
        };
        if (Rank != null)
        {
            Node.Rank = Rank;
            Node.Order = Order;
        }

        return AddDummyNode(Graph, Context, "border", Node, Prefix);
    }

    private static void AddBorderSegments(DagreGraph Graph, DagreContext Context)
    {
        foreach (string Child in Graph.Children(null))
        {
            AddBorderVisit(Graph, Context, Child);
        }
    }

    private static void AddBorderVisit(DagreGraph Graph, DagreContext Context, string NodeId)
    {
        foreach (string Child in Graph.Children(NodeId))
        {
            AddBorderVisit(Graph, Context, Child);
        }

        DagreNodeLabel? Node = Graph.Node(NodeId);
        if (Node?.MinRank == null || Node.MaxRank == null)
        {
            return;
        }

        Node.BorderLeftByRank = [];
        Node.BorderRightByRank = [];
        for (int Rank = Node.MinRank.Value; Rank < Node.MaxRank.Value + 1; Rank++)
        {
            AddBorderSegmentNode(Graph, Context, Node, "borderLeft", "_bl", Rank);
            AddBorderSegmentNode(Graph, Context, Node, "borderRight", "_br", Rank);
        }
    }

    private static void AddBorderSegmentNode(
        DagreGraph Graph,
        DagreContext Context,
        DagreNodeLabel Subgraph,
        string Prop,
        string Prefix,
        int Rank)
    {
        DagreNodeLabel Label = new DagreNodeLabel
        {
            Width = 0,
            Height = 0,
            Rank = Rank,
            BorderType = Prop,
            Dummy = "border"
        };
        Dictionary<int, string> Map = Prop == "borderLeft" ? Subgraph.BorderLeftByRank! : Subgraph.BorderRightByRank!;
        string Current = AddDummyNode(Graph, Context, "border", Label, Prefix);
        Map[Rank] = Current;
        Graph.SetParent(Current, SubgraphOwner(Graph, Subgraph));
        if (Map.TryGetValue(Rank - 1, out string? Previous))
        {
            Graph.SetEdge(Previous, Current, new DagreEdgeLabel
            {
                Weight = 1
            }, true);
        }
    }

    private static string SubgraphOwner(DagreGraph Graph, DagreNodeLabel Subgraph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            if (ReferenceEquals(Graph.Node(NodeId), Subgraph))
            {
                return NodeId;
            }
        }

        return string.Empty;
    }

    private static void NormalizeRun(DagreGraph Graph, DagreContext Context)
    {
        Graph.Label.DummyChains.Clear();
        foreach (DagreEdge Edge in Graph.Edges())
        {
            NormalizeEdge(Graph, Context, Edge);
        }
    }

    private static void NormalizeEdge(DagreGraph Graph, DagreContext Context, DagreEdge Edge)
    {
        DagreNodeLabel? Source = Graph.Node(Edge.V);
        DagreNodeLabel? Target = Graph.Node(Edge.W);
        DagreEdgeLabel? EdgeLabel = Graph.Edge(Edge);
        if (Source?.Rank == null || Target?.Rank == null || EdgeLabel == null)
        {
            return;
        }

        int SourceRank = Source.Rank.Value;
        int TargetRank = Target.Rank.Value;
        if (TargetRank == SourceRank + 1)
        {
            return;
        }

        Graph.RemoveEdge(Edge);
        string Current = Edge.V;
        int Rank = SourceRank;
        int Index = 0;
        Rank++;
        while (Rank < TargetRank)
        {
            EdgeLabel.Points.Clear();
            EdgeLabel.HasPoints = true;
            DagreNodeLabel Attrs = new DagreNodeLabel
            {
                Width = 0,
                Height = 0,
                EdgeLabel = EdgeLabel,
                EdgeObj = Edge,
                Rank = Rank
            };
            string Dummy = AddDummyNode(Graph, Context, "edge", Attrs, "_d");
            if (EdgeLabel.LabelRank != null && Rank == EdgeLabel.LabelRank.Value)
            {
                Attrs.Width = EdgeLabel.Width;
                Attrs.Height = EdgeLabel.Height;
                Attrs.Dummy = "edge-label";
                Attrs.LabelPos = EdgeLabel.LabelPos;
            }

            Graph.SetEdge(Current, Dummy, new DagreEdgeLabel
            {
                Weight = EdgeLabel.Weight
            }, true, Edge.Name);
            if (Index == 0)
            {
                Graph.Label.DummyChains.Add(Dummy);
            }

            Current = Dummy;
            Index++;
            Rank++;
        }

        Graph.SetEdge(Current, Edge.W, new DagreEdgeLabel
        {
            Weight = EdgeLabel.Weight
        }, true, Edge.Name);
    }

    private static void NormalizeUndo(DagreGraph Graph)
    {
        foreach (string Start in Graph.Label.DummyChains)
        {
            string Current = Start;
            DagreNodeLabel? Node = Graph.Node(Current);
            if (Node?.EdgeLabel == null || Node.EdgeObj == null)
            {
                continue;
            }

            DagreEdgeLabel Original = Node.EdgeLabel;
            Graph.SetEdge(Node.EdgeObj, Original, true);
            while (Node != null && Node.Dummy != null)
            {
                List<string> Next = Graph.Successors(Current);
                string? Successor = Next.Count > 0 ? Next[0] : null;
                Graph.RemoveNode(Current);
                Original.Points.Add(new DagrePoint
                {
                    X = Node.X,
                    Y = Node.Y
                });
                Original.HasPoints = true;
                if (Node.Dummy == "edge-label")
                {
                    Original.HasX = true;
                    Original.HasY = true;
                    Original.X = Node.X;
                    Original.Y = Node.Y;
                    Original.Width = Node.Width;
                    Original.Height = Node.Height;
                }

                if (Successor == null)
                {
                    break;
                }

                Current = Successor;
                Node = Graph.Node(Current);
            }
        }
    }

    private static void ParentDummyChains(DagreGraph Graph)
    {
        Dictionary<string, (int Low, int Lim)> Postorder = CompoundPostorder(Graph);
        foreach (string Start in new List<string>(Graph.Label.DummyChains))
        {
            DagreNodeLabel? Node = Graph.Node(Start);
            if (Node?.EdgeObj == null)
            {
                continue;
            }

            DagreEdge Edge = Node.EdgeObj;
            (List<string?> Path, string? Lca) = FindPath(Graph, Postorder, Edge.V, Edge.W);
            int PathIndex = 0;
            bool Ascending = true;
            string Current = Start;
            while (Current != Edge.W)
            {
                Node = Graph.Node(Current);
                if (Node == null)
                {
                    break;
                }

                string? PathNode = PathIndex < Path.Count ? Path[PathIndex] : null;
                if (Ascending)
                {
                    while (PathNode != Lca && PathNode != null && Node.Rank != null)
                    {
                        DagreNodeLabel? PathLabel = Graph.Node(PathNode);
                        if (PathLabel?.MaxRank == null || PathLabel.MaxRank.Value >= Node.Rank.Value)
                        {
                            break;
                        }

                        PathIndex++;
                        PathNode = PathIndex < Path.Count ? Path[PathIndex] : null;
                    }

                    if (PathNode == Lca)
                    {
                        Ascending = false;
                    }
                }

                if (!Ascending)
                {
                    while (PathIndex < Path.Count - 1 && Node.Rank != null)
                    {
                        string? Next = Path[PathIndex + 1];
                        DagreNodeLabel? NextLabel = Next == null ? null : Graph.Node(Next);
                        if (NextLabel?.MinRank == null || NextLabel.MinRank.Value > Node.Rank.Value)
                        {
                            break;
                        }

                        PathIndex++;
                    }

                    PathNode = PathIndex < Path.Count ? Path[PathIndex] : null;
                }

                if (PathNode != null)
                {
                    Graph.SetParent(Current, PathNode);
                }

                List<string> NextNodes = Graph.Successors(Current);
                if (NextNodes.Count == 0)
                {
                    break;
                }

                Current = NextNodes[0];
            }
        }
    }

    private static (List<string?> Path, string? Lca) FindPath(
        DagreGraph Graph,
        Dictionary<string, (int Low, int Lim)> Postorder,
        string Source,
        string Target)
    {
        List<string?> SourcePath = [];
        List<string?> TargetPath = [];
        int Low = 0;
        int Lim = 0;
        if (Postorder.TryGetValue(Source, out (int Low, int Lim) SourceSpan) && Postorder.TryGetValue(Target, out (int Low, int Lim) TargetSpan))
        {
            Low = Math.Min(SourceSpan.Low, TargetSpan.Low);
            Lim = Math.Max(SourceSpan.Lim, TargetSpan.Lim);
        }

        string? Parent = Source;
        string? Lca;
        do
        {
            Parent = Parent == null ? null : Graph.Parent(Parent);
            SourcePath.Add(Parent);
        }
        while (Parent != null && Postorder.TryGetValue(Parent, out (int Low, int Lim) Span) && (Span.Low > Low || Lim > Span.Lim));

        Lca = Parent;
        Parent = Target;
        while (true)
        {
            Parent = Parent == null ? null : Graph.Parent(Parent);
            if (Parent == Lca)
            {
                break;
            }

            TargetPath.Add(Parent);
        }

        TargetPath.Reverse();
        SourcePath.AddRange(TargetPath);
        return (SourcePath, Lca);
    }

    private static Dictionary<string, (int Low, int Lim)> CompoundPostorder(DagreGraph Graph)
    {
        Dictionary<string, (int Low, int Lim)> Result = new Dictionary<string, (int Low, int Lim)>(StringComparer.Ordinal);
        int Lim = 0;
        foreach (string Child in Graph.Children(null))
        {
            CompoundPostorderVisit(Graph, Child, Result, ref Lim);
        }

        return Result;
    }

    private static void CompoundPostorderVisit(
        DagreGraph Graph,
        string NodeId,
        Dictionary<string, (int Low, int Lim)> Result,
        ref int Lim)
    {
        int Low = Lim;
        foreach (string Child in Graph.Children(NodeId))
        {
            CompoundPostorderVisit(Graph, Child, Result, ref Lim);
        }

        Result[NodeId] = (Low, Lim);
        Lim++;
    }

    private static void AdjustCoordinateSystem(DagreGraph Graph)
    {
        string RankDir = Graph.Label.RankDir.ToLowerInvariant();
        if (RankDir == "lr" || RankDir == "rl")
        {
            SwapWidthHeight(Graph);
        }
    }

    private static void UndoCoordinateSystem(DagreGraph Graph)
    {
        string RankDir = Graph.Label.RankDir.ToLowerInvariant();
        if (RankDir == "bt" || RankDir == "rl")
        {
            ReverseY(Graph);
        }

        if (RankDir == "lr" || RankDir == "rl")
        {
            SwapXY(Graph);
            SwapWidthHeight(Graph);
        }
    }

    private static void SwapWidthHeight(DagreGraph Graph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node != null)
            {
                double Width = Node.Width;
                Node.Width = Node.Height;
                Node.Height = Width;
            }
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label != null)
            {
                double Width = Label.Width;
                Label.Width = Label.Height;
                Label.Height = Width;
            }
        }
    }

    private static void ReverseY(DagreGraph Graph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            Node?.Y = -Node.Y;
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null)
            {
                continue;
            }

            foreach (DagrePoint Point in Label.Points)
            {
                Point.Y = -Point.Y;
            }

            if (Label.HasY)
            {
                Label.Y = -Label.Y;
            }
        }
    }

    private static void SwapXY(DagreGraph Graph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node == null)
            {
                continue;
            }

            double X = Node.X;
            Node.X = Node.Y;
            Node.Y = X;
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null)
            {
                continue;
            }

            foreach (DagrePoint Point in Label.Points)
            {
                double X = Point.X;
                Point.X = Point.Y;
                Point.Y = X;
            }

            if (Label.HasX)
            {
                double X = Label.X;
                Label.X = Label.Y;
                Label.Y = X;
                Label.HasY = true;
            }
        }
    }
}
