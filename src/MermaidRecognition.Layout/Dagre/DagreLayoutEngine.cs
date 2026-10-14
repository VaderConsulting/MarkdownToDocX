// Port of dagre-d3-es 7.0.14 layout (the algorithm Mermaid's flowchart renderer calls).
// Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace MermaidRecognition.Layout.Dagre;

/// <summary>
/// Runs the dagre layout pipeline on a compound multigraph.
/// </summary>
internal static partial class DagreLayoutEngine
{
    /// <summary>
    /// Writes coordinates, sizes, ranks, orders and edge points back onto <paramref name="Input"/>.
    /// </summary>
    public static void Layout(DagreGraph Input)
    {
        DagreContext Context = new DagreContext();
        DagreGraph LayoutGraph = BuildLayoutGraph(Input);
        RunLayout(LayoutGraph, Context);
        UpdateInputGraph(Input, LayoutGraph);
    }

    private static void RunLayout(DagreGraph Graph, DagreContext Context)
    {
        Timed("MakeSpaceForEdgeLabels", () => MakeSpaceForEdgeLabels(Graph));
        Timed("RemoveSelfEdges", () => RemoveSelfEdges(Graph));
        Timed("AcyclicRun", () => AcyclicRun(Graph, Context));
        Timed("NestingRun", () => NestingRun(Graph, Context));
        Timed("Rank", () => Rank(AsNonCompoundGraph(Graph)));
        Timed("InjectEdgeLabelProxies", () => InjectEdgeLabelProxies(Graph, Context));
        Timed("RemoveEmptyRanks", () => RemoveEmptyRanks(Graph));
        Timed("NestingCleanup", () => NestingCleanup(Graph));
        Timed("NormalizeRanks", () => NormalizeRanks(Graph));
        Timed("AssignRankMinMax", () => AssignRankMinMax(Graph));
        Timed("RemoveEdgeLabelProxies", () => RemoveEdgeLabelProxies(Graph));
        Timed("NormalizeRun", () => NormalizeRun(Graph, Context));
        Timed("ParentDummyChains", () => ParentDummyChains(Graph));
        Timed("AddBorderSegments", () => AddBorderSegments(Graph, Context));
        Timed("Order", () => Order(Graph, Context));
        Timed("InsertSelfEdges", () => InsertSelfEdges(Graph, Context));
        Timed("AdjustCoordinateSystem", () => AdjustCoordinateSystem(Graph));
        Timed("Position", () => Position(Graph));
        Timed("PositionSelfEdges", () => PositionSelfEdges(Graph));
        Timed("RemoveBorderNodes", () => RemoveBorderNodes(Graph));
        Timed("NormalizeUndo", () => NormalizeUndo(Graph));
        Timed("FixupEdgeLabelCoords", () => FixupEdgeLabelCoords(Graph));
        Timed("UndoCoordinateSystem", () => UndoCoordinateSystem(Graph));
        Timed("TranslateGraph", () => TranslateGraph(Graph));
        Timed("AssignNodeIntersects", () => AssignNodeIntersects(Graph));
        Timed("ReversePointsForReversedEdges", () => ReversePointsForReversedEdges(Graph));
        Timed("AcyclicUndo", () => AcyclicUndo(Graph));

        static void Timed(string Name, Action Action)
        {
            _ = Name;
            Action();
        }
    }

    private static DagreGraph BuildLayoutGraph(DagreGraph Input)
    {
        DagreGraph Graph = new DagreGraph(Directed: true, Multigraph: true, Compound: true);
        DagreGraphLabel Source = Input.Label;
        DagreGraphLabel Copy = new DagreGraphLabel
        {
            NodeSep = Source.NodeSep,
            EdgeSep = Source.EdgeSep,
            RankSep = Source.RankSep,
            MarginX = Source.MarginX,
            MarginY = Source.MarginY,
            RankDir = string.IsNullOrEmpty(Source.RankDir) ? "tb" : Source.RankDir,
            Acyclicer = Source.Acyclicer,
            Ranker = Source.Ranker,
            Align = Source.Align
        };
        Graph.SetGraph(Copy);

        foreach (string NodeId in Input.Nodes())
        {
            DagreNodeLabel? SourceNode = Input.Node(NodeId);
            DagreNodeLabel Label = new DagreNodeLabel();
            if (SourceNode != null)
            {
                Label.Width = SourceNode.Width;
                Label.Height = SourceNode.Height;
            }

            Graph.SetNode(NodeId, Label, true);
            if (Input.Parent(NodeId) != null)
            {
                Graph.SetParent(NodeId, Input.Parent(NodeId));
            }
        }

        foreach (DagreEdge Edge in Input.Edges())
        {
            DagreEdgeLabel? SourceEdge = Input.Edge(Edge);
            DagreEdgeLabel Label = new DagreEdgeLabel();
            if (SourceEdge != null && !SourceEdge.IsNumber)
            {
                Label.Minlen = SourceEdge.Minlen;
                Label.Weight = SourceEdge.Weight;
                Label.Width = SourceEdge.Width;
                Label.Height = SourceEdge.Height;
                Label.LabelOffset = SourceEdge.LabelOffset;
                Label.LabelPos = string.IsNullOrEmpty(SourceEdge.LabelPos) ? "r" : SourceEdge.LabelPos;
            }

            Graph.SetEdge(Edge.V, Edge.W, Label, true, Edge.Name);
        }

        return Graph;
    }

    private static void UpdateInputGraph(DagreGraph Input, DagreGraph LayoutGraph)
    {
        foreach (string NodeId in Input.Nodes())
        {
            DagreNodeLabel? InputLabel = Input.Node(NodeId);
            DagreNodeLabel? LayoutLabel = LayoutGraph.Node(NodeId);
            if (InputLabel == null || LayoutLabel == null)
            {
                continue;
            }

            InputLabel.X = LayoutLabel.X;
            InputLabel.Y = LayoutLabel.Y;
            InputLabel.Rank = LayoutLabel.Rank;
            InputLabel.Order = LayoutLabel.Order;
            int KidCount = LayoutGraph.Children(NodeId).Count;
            if (KidCount > 0)
            {
                InputLabel.Width = LayoutLabel.Width;
                InputLabel.Height = LayoutLabel.Height;
            }
        }

        foreach (DagreEdge Edge in Input.Edges())
        {
            DagreEdgeLabel? InputLabel = Input.Edge(Edge);
            DagreEdgeLabel? LayoutLabel = LayoutGraph.Edge(Edge);
            if (InputLabel == null || LayoutLabel == null)
            {
                continue;
            }

            InputLabel.Points.Clear();
            foreach (DagrePoint Point in LayoutLabel.Points)
            {
                InputLabel.Points.Add(new DagrePoint
                {
                    X = Point.X,
                    Y = Point.Y
                });
            }

            InputLabel.HasPoints = true;
            if (LayoutLabel.HasX)
            {
                InputLabel.HasX = true;
                InputLabel.X = LayoutLabel.X;
            }

            if (LayoutLabel.HasY)
            {
                InputLabel.HasY = true;
                InputLabel.Y = LayoutLabel.Y;
            }
        }

        Input.Label.Width = LayoutGraph.Label.Width;
        Input.Label.Height = LayoutGraph.Label.Height;
    }

    private static void MakeSpaceForEdgeLabels(DagreGraph Graph)
    {
        Graph.Label.RankSep /= 2;
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null)
            {
                continue;
            }

            Label.Minlen *= 2;
            if (!string.Equals(Label.LabelPos, "c", StringComparison.OrdinalIgnoreCase))
            {
                if (Graph.Label.RankDir == "TB" || Graph.Label.RankDir == "BT")
                {
                    Label.Width += Label.LabelOffset;
                }
                else
                {
                    Label.Height += Label.LabelOffset;
                }
            }
        }
    }

    private static void RemoveSelfEdges(DagreGraph Graph)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            if (Edge.V != Edge.W)
            {
                continue;
            }

            DagreNodeLabel? Node = Graph.Node(Edge.V);
            if (Node == null)
            {
                continue;
            }

            if (Node.SelfEdges == null)
            {
                Node.SelfEdges = new List<DagreSelfEdge>();
            }

            DagreEdgeLabel Label = Graph.Edge(Edge) ?? new DagreEdgeLabel();
            Node.SelfEdges.Add(new DagreSelfEdge
            {
                Edge = Edge,
                Label = Label
            });
            Graph.RemoveEdge(Edge);
        }
    }

    private static void InsertSelfEdges(DagreGraph Graph, DagreContext Context)
    {
        List<List<string>> Layers = BuildLayerMatrix(Graph);
        foreach (List<string> Layer in Layers)
        {
            int OrderShift = 0;
            for (int Index = 0; Index < Layer.Count; Index++)
            {
                string NodeId = Layer[Index];
                if (NodeId == null)
                {
                    continue;
                }

                DagreNodeLabel? Node = Graph.Node(NodeId);
                if (Node == null)
                {
                    continue;
                }

                Node.Order = Index + OrderShift;
                if (Node.SelfEdges == null)
                {
                    continue;
                }

                foreach (DagreSelfEdge SelfEdge in Node.SelfEdges)
                {
                    OrderShift++;
                    DagreNodeLabel Attrs = new DagreNodeLabel
                    {
                        Width = SelfEdge.Label.Width,
                        Height = SelfEdge.Label.Height,
                        Rank = Node.Rank,
                        Order = Index + OrderShift,
                        EdgeObj = SelfEdge.Edge,
                        EdgeLabel = SelfEdge.Label,
                        Dummy = "selfedge"
                    };
                    AddDummyNode(Graph, Context, "selfedge", Attrs, "_se");
                }

                Node.SelfEdges = null;
            }
        }
    }

    private static void PositionSelfEdges(DagreGraph Graph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node == null || Node.Dummy != "selfedge" || Node.EdgeObj == null || Node.EdgeLabel == null)
            {
                continue;
            }

            DagreNodeLabel? SelfNode = Graph.Node(Node.EdgeObj.V);
            if (SelfNode == null)
            {
                continue;
            }

            double X = SelfNode.X + (SelfNode.Width / 2);
            double Y = SelfNode.Y;
            double Dx = Node.X - X;
            double Dy = SelfNode.Height / 2;
            Graph.SetEdge(Node.EdgeObj, Node.EdgeLabel, true);
            Graph.RemoveNode(NodeId);
            Node.EdgeLabel.Points.Clear();
            Node.EdgeLabel.HasPoints = true;
            Node.EdgeLabel.Points.Add(new DagrePoint { X = X + (2 * Dx / 3), Y = Y - Dy });
            Node.EdgeLabel.Points.Add(new DagrePoint { X = X + (5 * Dx / 6), Y = Y - Dy });
            Node.EdgeLabel.Points.Add(new DagrePoint { X = X + Dx, Y = Y });
            Node.EdgeLabel.Points.Add(new DagrePoint { X = X + (5 * Dx / 6), Y = Y + Dy });
            Node.EdgeLabel.Points.Add(new DagrePoint { X = X + (2 * Dx / 3), Y = Y + Dy });
            Node.EdgeLabel.HasX = true;
            Node.EdgeLabel.HasY = true;
            Node.EdgeLabel.X = Node.X;
            Node.EdgeLabel.Y = Node.Y;
        }
    }

    private static void InjectEdgeLabelProxies(DagreGraph Graph, DagreContext Context)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            DagreNodeLabel? Source = Graph.Node(Edge.V);
            DagreNodeLabel? Target = Graph.Node(Edge.W);
            if (Label == null || Source?.Rank == null || Target?.Rank == null)
            {
                continue;
            }

            if (Label.Width == 0 || Label.Height == 0)
            {
                continue;
            }

            double Mid = ((Target.Rank.Value - Source.Rank.Value) / 2.0) + Source.Rank.Value;
            int Rank = (int)Mid;
            if (Math.Abs(Mid - Rank) > 0.0000001)
            {
                continue;
            }

            DagreNodeLabel Proxy = new DagreNodeLabel
            {
                Rank = Rank
            };
            Proxy.EdgeObj = Edge;
            AddDummyNode(Graph, Context, "edge-proxy", Proxy, "_ep");
        }
    }

    private static void RemoveEdgeLabelProxies(DagreGraph Graph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node == null || Node.Dummy != "edge-proxy" || Node.EdgeObj == null)
            {
                continue;
            }

            DagreEdgeLabel? Edge = Graph.Edge(Node.EdgeObj);
            if (Edge != null)
            {
                Edge.LabelRank = Node.Rank;
            }

            Graph.RemoveNode(NodeId);
        }
    }

    private static void AssignRankMinMax(DagreGraph Graph)
    {
        int MaxRankValue = 0;
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.BorderTop == null || Node.BorderBottom == null)
            {
                continue;
            }

            DagreNodeLabel? Top = Graph.Node(Node.BorderTop);
            DagreNodeLabel? Bottom = Graph.Node(Node.BorderBottom);
            if (Top?.Rank == null || Bottom?.Rank == null)
            {
                continue;
            }

            Node.MinRank = Top.Rank;
            Node.MaxRank = Bottom.Rank;
            if (Node.MaxRank.Value > MaxRankValue)
            {
                MaxRankValue = Node.MaxRank.Value;
            }
        }

        Graph.Label.MaxRank = MaxRankValue;
    }

    private static void TranslateGraph(DagreGraph Graph)
    {
        double MinX = double.PositiveInfinity;
        double MaxX = 0;
        double MinY = double.PositiveInfinity;
        double MaxY = 0;
        double MarginX = Graph.Label.MarginX;
        double MarginY = Graph.Label.MarginY;

        void Extremes(double X, double Y, double Width, double Height)
        {
            MinX = Math.Min(MinX, X - (Width / 2));
            MaxX = Math.Max(MaxX, X + (Width / 2));
            MinY = Math.Min(MinY, Y - (Height / 2));
            MaxY = Math.Max(MaxY, Y + (Height / 2));
        }

        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node == null)
            {
                continue;
            }

            Extremes(Node.X, Node.Y, Node.Width, Node.Height);
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label != null && Label.HasX)
            {
                Extremes(Label.X, Label.Y, Label.Width, Label.Height);
            }
        }

        MinX -= MarginX;
        MinY -= MarginY;
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node == null)
            {
                continue;
            }

            Node.X -= MinX;
            Node.Y -= MinY;
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
                Point.X -= MinX;
                Point.Y -= MinY;
            }

            if (Label.HasX)
            {
                Label.X -= MinX;
            }

            if (Label.HasY)
            {
                Label.Y -= MinY;
            }
        }

        Graph.Label.Width = MaxX - MinX + MarginX;
        Graph.Label.Height = MaxY - MinY + MarginY;
    }

    private static void AssignNodeIntersects(DagreGraph Graph)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            DagreNodeLabel? Source = Graph.Node(Edge.V);
            DagreNodeLabel? Target = Graph.Node(Edge.W);
            if (Label == null || Source == null || Target == null)
            {
                continue;
            }

            DagrePoint TowardsSource;
            DagrePoint TowardsTarget;
            if (!Label.HasPoints || Label.Points.Count == 0)
            {
                Label.Points.Clear();
                Label.HasPoints = true;
                TowardsSource = new DagrePoint { X = Target.X, Y = Target.Y };
                TowardsTarget = new DagrePoint { X = Source.X, Y = Source.Y };
            }
            else
            {
                TowardsSource = Label.Points[0];
                TowardsTarget = Label.Points[Label.Points.Count - 1];
            }

            Label.Points.Insert(0, IntersectRect(Source, TowardsSource));
            Label.Points.Add(IntersectRect(Target, TowardsTarget));
        }
    }

    private static void FixupEdgeLabelCoords(DagreGraph Graph)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null || !Label.HasX)
            {
                continue;
            }

            string Pos = Label.LabelPos ?? "r";
            if (Pos == "l" || Pos == "r")
            {
                Label.Width -= Label.LabelOffset;
            }

            if (Pos == "l")
            {
                Label.X -= (Label.Width / 2) + Label.LabelOffset;
            }
            else if (Pos == "r")
            {
                Label.X += (Label.Width / 2) + Label.LabelOffset;
            }
        }
    }

    private static void ReversePointsForReversedEdges(DagreGraph Graph)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label != null && Label.Reversed)
            {
                Label.Points.Reverse();
            }
        }
    }

    private static void RemoveBorderNodes(DagreGraph Graph)
    {
        foreach (string NodeId in Graph.Nodes())
        {
            if (Graph.Children(NodeId).Count == 0)
            {
                continue;
            }

            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.BorderTop == null || Node.BorderBottom == null || Node.BorderLeftByRank == null || Node.BorderRightByRank == null)
            {
                continue;
            }

            DagreNodeLabel? Top = Graph.Node(Node.BorderTop);
            DagreNodeLabel? Bottom = Graph.Node(Node.BorderBottom);
            string? LeftId = null;
            string? RightId = null;
            foreach (KeyValuePair<int, string> Pair in Node.BorderLeftByRank)
            {
                LeftId = Pair.Value;
            }

            foreach (KeyValuePair<int, string> Pair in Node.BorderRightByRank)
            {
                RightId = Pair.Value;
            }

            DagreNodeLabel? Left = LeftId == null ? null : Graph.Node(LeftId);
            DagreNodeLabel? Right = RightId == null ? null : Graph.Node(RightId);
            if (Top == null || Bottom == null || Left == null || Right == null)
            {
                continue;
            }

            Node.Width = Math.Abs(Right.X - Left.X);
            Node.Height = Math.Abs(Bottom.Y - Top.Y);
            Node.X = Left.X + (Node.Width / 2);
            Node.Y = Top.Y + (Node.Height / 2);
        }

        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Dummy == "border")
            {
                Graph.RemoveNode(NodeId);
            }
        }
    }

    internal static string AddDummyNode(DagreGraph Graph, DagreContext Context, string Type, DagreNodeLabel Attrs, string Prefix)
    {
        string Id;
        do
        {
            Context.NextId++;
            Id = Prefix + Context.NextId.ToString(CultureInfo.InvariantCulture);
        }
        while (Graph.HasNode(Id));

        Attrs.Dummy = Type;
        Graph.SetNode(Id, Attrs, true);
        return Id;
    }

    internal static DagreGraph AsNonCompoundGraph(DagreGraph Graph)
    {
        DagreGraph Simplified = new DagreGraph(Directed: true, Multigraph: Graph.IsMultigraph, Compound: false);
        Simplified.SetGraph(Graph.Label);
        foreach (string NodeId in Graph.Nodes())
        {
            if (Graph.Children(NodeId).Count == 0)
            {
                Simplified.SetNode(NodeId, Graph.Node(NodeId), true);
            }
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            Simplified.SetEdge(Edge, Graph.Edge(Edge), true);
        }

        return Simplified;
    }

    internal static DagreGraph Simplify(DagreGraph Graph)
    {
        DagreGraph Simplified = new DagreGraph(Directed: true, Multigraph: false, Compound: false);
        Simplified.SetGraph(Graph.Label);
        foreach (string NodeId in Graph.Nodes())
        {
            Simplified.SetNode(NodeId, Graph.Node(NodeId), true);
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            DagreEdgeLabel? Existing = Simplified.Edge(Edge.V, Edge.W);
            double Weight = Existing?.Weight ?? 0;
            int Minlen = Existing?.Minlen ?? 1;
            if (Label != null)
            {
                Weight += Label.Weight;
                if (Label.Minlen > Minlen)
                {
                    Minlen = Label.Minlen;
                }
            }

            Simplified.SetEdge(Edge.V, Edge.W, new DagreEdgeLabel
            {
                Weight = Weight,
                Minlen = Minlen
            }, true);
        }

        return Simplified;
    }

    internal static DagrePoint IntersectRect(DagreNodeLabel Rect, DagrePoint Point)
    {
        double X = Rect.X;
        double Y = Rect.Y;
        double Dx = Point.X - X;
        double Dy = Point.Y - Y;
        double W = Rect.Width / 2;
        double H = Rect.Height / 2;
        double Sx;
        double Sy;
        if ((Math.Abs(Dy) * W) > (Math.Abs(Dx) * H))
        {
            if (Dy < 0)
            {
                H = -H;
            }

            Sx = (H * Dx) / Dy;
            Sy = H;
        }
        else
        {
            if (Dx < 0)
            {
                W = -W;
            }

            Sx = W;
            Sy = (W * Dy) / Dx;
        }

        return new DagrePoint
        {
            X = X + Sx,
            Y = Y + Sy
        };
    }

    internal static List<List<string>> BuildLayerMatrix(DagreGraph Graph)
    {
        int Max = MaxRank(Graph);
        List<List<string>> Layering = new List<List<string>>();
        for (int Rank = 0; Rank <= Max; Rank++)
        {
            Layering.Add(new List<string>());
        }

        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank == null || Node.Order == null)
            {
                continue;
            }

            List<string> Layer = Layering[Node.Rank.Value];
            while (Layer.Count <= Node.Order.Value)
            {
                Layer.Add(string.Empty);
            }

            Layer[Node.Order.Value] = NodeId;
        }

        return Layering;
    }

    internal static void NormalizeRanks(DagreGraph Graph)
    {
        int? Min = null;
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank == null)
            {
                continue;
            }

            if (Min == null || Node.Rank.Value < Min.Value)
            {
                Min = Node.Rank;
            }
        }

        if (Min == null)
        {
            return;
        }

        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank != null)
            {
                Node.Rank -= Min.Value;
            }
        }
    }

    private static void RemoveEmptyRanks(DagreGraph Graph)
    {
        int? Offset = null;
        int MaxSeen = 0;
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank == null)
            {
                continue;
            }

            if (Offset == null || Node.Rank.Value < Offset.Value)
            {
                Offset = Node.Rank;
            }

            if (Node.Rank.Value > MaxSeen)
            {
                MaxSeen = Node.Rank.Value;
            }
        }

        if (Offset == null)
        {
            return;
        }

        int Span = MaxSeen - Offset.Value;
        List<List<string>?> Layers = new List<List<string>?>();
        for (int Index = 0; Index <= Span; Index++)
        {
            Layers.Add(null);
        }

        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank == null)
            {
                continue;
            }

            int Rank = Node.Rank.Value - Offset.Value;
            if (Layers[Rank] == null)
            {
                Layers[Rank] = new List<string>();
            }

            Layers[Rank]!.Add(NodeId);
        }

        int Delta = 0;
        int Factor = Graph.Label.HasNodeRankFactor ? Graph.Label.NodeRankFactor : 0;
        for (int Index = 0; Index < Layers.Count; Index++)
        {
            bool Empty = Layers[Index] == null;
            bool Drop = Empty && (Factor == 0 || (Index % Factor) != 0);
            if (Drop)
            {
                Delta--;
            }
            else if (Delta != 0 && Layers[Index] != null)
            {
                foreach (string NodeId in Layers[Index]!)
                {
                    DagreNodeLabel? Node = Graph.Node(NodeId);
                    if (Node?.Rank != null)
                    {
                        Node.Rank += Delta;
                    }
                }
            }
        }
    }

    internal static int MaxRank(DagreGraph Graph)
    {
        int Max = int.MinValue;
        foreach (string NodeId in Graph.Nodes())
        {
            DagreNodeLabel? Node = Graph.Node(NodeId);
            if (Node?.Rank != null && Node.Rank.Value > Max)
            {
                Max = Node.Rank.Value;
            }
        }

        if (Max == int.MinValue)
        {
            return 0;
        }

        return Max;
    }

    internal static List<string> Walk(DagreGraph Graph, IReadOnlyList<string> Starts, bool Postorder)
    {
        List<string> Acc = new List<string>();
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string Start in Starts)
        {
            Visit(Graph, Start, Postorder, Visited, Acc);
        }

        return Acc;
    }

    private static void Visit(DagreGraph Graph, string NodeId, bool Postorder, HashSet<string> Visited, List<string> Acc)
    {
        if (!Visited.Add(NodeId))
        {
            return;
        }

        if (!Postorder)
        {
            Acc.Add(NodeId);
        }

        List<string> Next = Graph.IsDirected ? Graph.Successors(NodeId) : Graph.Neighbors(NodeId);
        foreach (string Neighbour in Next)
        {
            Visit(Graph, Neighbour, Postorder, Visited, Acc);
        }

        if (Postorder)
        {
            Acc.Add(NodeId);
        }
    }

    internal static int Slack(DagreGraph Graph, DagreEdge Edge)
    {
        DagreNodeLabel? Source = Graph.Node(Edge.V);
        DagreNodeLabel? Target = Graph.Node(Edge.W);
        DagreEdgeLabel? Label = Graph.Edge(Edge);
        int SourceRank = Source?.Rank ?? 0;
        int TargetRank = Target?.Rank ?? 0;
        int Minlen = Label?.Minlen ?? 1;
        return TargetRank - SourceRank - Minlen;
    }
}

/// <summary>
/// Per-layout identifier counter. Dagre's geometry does not depend on the absolute ids.
/// </summary>
internal sealed class DagreContext
{
    public int NextId { get; set; }
}
