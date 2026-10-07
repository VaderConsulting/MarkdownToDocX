using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout.Dagre;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Places a flowchart with the dagre pipeline Mermaid uses.
/// </summary>
public static class FlowchartDagrePlacement
{
    /// <summary>
    /// Dagre's default gap between edge lanes. Mermaid does not override it.
    /// </summary>
    public const double DAGRE_EDGE_SEPARATION_PX = 20;

    /// <summary>
    /// Extra rank gap Mermaid adds when it lays out an extracted subgraph.
    /// </summary>
    public const double SUBGRAPH_RANKSEP_EXTRA_PX = 25;

    /// <summary>
    /// Builds node centres, cluster bounds and edge polylines for <paramref name="Contract"/>.
    /// </summary>
    public static bool TryPlace(DiagramContract Contract, out FlowchartLayoutResult Result)
    {
        Result = new FlowchartLayoutResult();
        if (Contract == null || Contract.Nodes.Count == 0)
        {
            return false;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        string RankDir = ToRankDir(Direction);
        Dictionary<string, FlowchartNodeDimensions> Measured =
            FlowchartGeometryGraphBuilder.BuildMeasuredDimensions(Contract, Options);

        DagreGraph Graph = new DagreGraph(true, true, true);
        Graph.SetGraph(new DagreGraphLabel
        {
            RankDir = RankDir,
            NodeSep = Options.NodeSpacingPx,
            RankSep = Options.RankSpacingPx,
            EdgeSep = DAGRE_EDGE_SEPARATION_PX,
            MarginX = Options.DiagramPaddingPx,
            MarginY = Options.DiagramPaddingPx
        });

        Dictionary<string, string> ClusterDirection = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> ClusterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            ClusterIds.Add(Group.Id);
            ClusterDirection[Group.Id] = string.IsNullOrWhiteSpace(Group.Direction)
                ? string.Empty
                : ToRankDir(FlowchartDirectionNormalizer.Normalize(Group.Direction));
            if (!Measured.ContainsKey(Group.Id))
            {
                Graph.SetNode(Group.Id, new DagreNodeLabel
                {
                    Width = 0,
                    Height = 0
                }, true);
            }
        }

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (ClusterIds.Contains(Node.Id))
            {
                continue;
            }

            FlowchartNodeDimensions Size = Measured[Node.Id];
            Graph.SetNode(Node.Id, new DagreNodeLabel
            {
                Width = Size.Width,
                Height = Size.Height
            }, true);
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (!string.IsNullOrEmpty(Group.ParentGroupId))
            {
                Graph.SetParent(Group.Id, Group.ParentGroupId);
            }

            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (Graph.HasNode(ChildId))
                {
                    Graph.SetParent(ChildId, Group.Id);
                }
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (Graph.HasNode(ChildGroupId))
                {
                    Graph.SetParent(ChildGroupId, Group.Id);
                }
            }
        }

        int EdgeIndex = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!Graph.HasNode(Edge.SourceId) || !Graph.HasNode(Edge.TargetId))
            {
                continue;
            }

            DagreEdgeLabel Label = new DagreEdgeLabel
            {
                Minlen = Math.Max(1, Edge.MinLength),
                Weight = 1
            };
            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                FlowchartLabelDimensions LabelSize = FlowchartNodeSizeEstimator.MeasureWrappedLabel(Edge.Label, Options);
                Label.Width = Math.Max(1, LabelSize.Width);
                Label.Height = Math.Max(1, LabelSize.Height);
                Label.LabelPos = "c";
            }

            string Name = string.IsNullOrEmpty(Edge.Id) ? "e" + EdgeIndex.ToString() : Edge.Id;
            Graph.SetEdge(Edge.SourceId, Edge.TargetId, Label, true, Name);
            EdgeIndex++;
        }

        RetargetClusterEdges(Graph);
        Dictionary<string, DagreGraph> Extracted = new Dictionary<string, DagreGraph>(StringComparer.Ordinal);
        ExtractIsolatedClusters(Graph, ClusterDirection, Extracted);
        DagreLayoutEngine.Layout(Graph);

        FlowchartLayoutResult Built = new FlowchartLayoutResult
        {
            GraphWidth = Graph.Label.Width,
            GraphHeight = Graph.Label.Height
        };

        foreach (NodeContract Node in Contract.Nodes)
        {
            if (ClusterIds.Contains(Node.Id))
            {
                continue;
            }

            if (!Measured.TryGetValue(Node.Id, out FlowchartNodeDimensions? Size))
            {
                continue;
            }

            (double X, double Y) Centre = ResolveCentre(Node.Id, Graph, Extracted);
            Built.NodeBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Centre.X,
                CenterY = Centre.Y,
                Width = Size.Width,
                Height = Size.Height
            });
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            (double X, double Y) Centre = ResolveCentre(Group.Id, Graph, Extracted);
            DagreNodeLabel? ClusterLabel = FindLabel(Group.Id, Graph, Extracted);
            Built.ClusterBoundsMutable.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Group.Id,
                CenterX = Centre.X,
                CenterY = Centre.Y,
                Width = ClusterLabel?.Width ?? 0,
                Height = ClusterLabel?.Height ?? 0
            });
        }

        AppendEdges(Graph, Built, 0, 0);
        foreach (KeyValuePair<string, DagreGraph> Pair in Extracted)
        {
            (double X, double Y) Centre = ResolveCentre(Pair.Key, Graph, Extracted);
            DagreNodeLabel? ClusterLabel = FindLabel(Pair.Key, Graph, Extracted);
            double Left = Centre.X - ((ClusterLabel?.Width ?? 0) / 2);
            double Top = Centre.Y - ((ClusterLabel?.Height ?? 0) / 2);
            AppendEdges(Pair.Value, Built, Left, Top);
        }

        Result = Built;
        return Result.NodeBounds.Count > 0;
    }

    private static void AppendEdges(DagreGraph Graph, FlowchartLayoutResult Built, double OffsetX, double OffsetY)
    {
        foreach (DagreEdge Edge in Graph.Edges())
        {
            if (Edge.V.StartsWith("_", StringComparison.Ordinal) || Edge.W.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            DagreEdgeLabel? Label = Graph.Edge(Edge);
            DagreNodeLabel? Source = Graph.Node(Edge.V);
            DagreNodeLabel? Target = Graph.Node(Edge.W);
            if (Label == null || Source == null || Target == null || Label.Points.Count < 2)
            {
                continue;
            }

            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute
            {
                SourceId = Edge.V,
                TargetId = Edge.W,
                ExitSide = SideOf(Source, Label.Points[0], Label.Points[1]),
                EntrySide = SideOf(Target, Label.Points[Label.Points.Count - 1], Label.Points[Label.Points.Count - 2])
            };
            foreach (DagrePoint Point in Label.Points)
            {
                Route.Points.Add(new FlowchartLayoutPoint
                {
                    X = Point.X + OffsetX,
                    Y = Point.Y + OffsetY
                });
            }

            Built.EdgeRoutesMutable.Add(Route);
        }
    }

    private static string SideOf(DagreNodeLabel Node, DagrePoint AtBoundary, DagrePoint Towards)
    {
        double Dx = Towards.X - AtBoundary.X;
        double Dy = Towards.Y - AtBoundary.Y;
        if (Math.Abs(Dx) >= Math.Abs(Dy))
        {
            return Dx >= 0 ? "right" : "left";
        }

        return Dy >= 0 ? "bottom" : "top";
    }

    private static (double X, double Y) ResolveCentre(string NodeId, DagreGraph Outer, Dictionary<string, DagreGraph> Extracted)
    {
        if (Outer.HasNode(NodeId))
        {
            DagreNodeLabel? Label = Outer.Node(NodeId);
            return (Label?.X ?? 0, Label?.Y ?? 0);
        }

        foreach (KeyValuePair<string, DagreGraph> Pair in Extracted)
        {
            if (!Pair.Value.HasNode(NodeId))
            {
                continue;
            }

            (double X, double Y) Cluster = ResolveCentre(Pair.Key, Outer, Extracted);
            DagreNodeLabel? ClusterLabel = FindLabel(Pair.Key, Outer, Extracted);
            DagreNodeLabel? Inner = Pair.Value.Node(NodeId);
            double Left = Cluster.X - ((ClusterLabel?.Width ?? 0) / 2);
            double Top = Cluster.Y - ((ClusterLabel?.Height ?? 0) / 2);
            return (Left + (Inner?.X ?? 0), Top + (Inner?.Y ?? 0));
        }

        return (0, 0);
    }

    private static DagreNodeLabel? FindLabel(string NodeId, DagreGraph Outer, Dictionary<string, DagreGraph> Extracted)
    {
        if (Outer.HasNode(NodeId))
        {
            return Outer.Node(NodeId);
        }

        foreach (DagreGraph Inner in Extracted.Values)
        {
            if (Inner.HasNode(NodeId))
            {
                return Inner.Node(NodeId);
            }
        }

        return null;
    }

    private static void RetargetClusterEdges(DagreGraph Graph)
    {
        Dictionary<string, string> Anchor = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string NodeId in Graph.Nodes())
        {
            if (Graph.Children(NodeId).Count > 0)
            {
                Anchor[NodeId] = FindLeaf(Graph, NodeId);
            }
        }

        HashSet<string> External = new HashSet<string>(StringComparer.Ordinal);
        foreach (DagreEdge Edge in Graph.Edges())
        {
            foreach (string ClusterId in Anchor.Keys)
            {
                bool SourceInside = IsInside(Graph, Edge.V, ClusterId);
                bool TargetInside = IsInside(Graph, Edge.W, ClusterId);
                if (SourceInside != TargetInside)
                {
                    External.Add(ClusterId);
                }
            }
        }

        foreach (DagreEdge Edge in Graph.Edges())
        {
            string Source = Edge.V;
            string Target = Edge.W;
            if (Anchor.ContainsKey(Source) && External.Contains(Source))
            {
                Source = Anchor[Source];
            }

            if (Anchor.ContainsKey(Target) && External.Contains(Target))
            {
                Target = Anchor[Target];
            }

            if (Source == Edge.V && Target == Edge.W)
            {
                continue;
            }

            DagreEdgeLabel? Label = Graph.Edge(Edge);
            Graph.RemoveEdge(Edge);
            if (Source != Target)
            {
                Graph.SetEdge(Source, Target, Label, true, Edge.Name);
            }
        }
    }

    private static void ExtractIsolatedClusters(
        DagreGraph Graph,
        Dictionary<string, string> ClusterDirection,
        Dictionary<string, DagreGraph> Extracted)
    {
        List<string> Snapshot = Graph.Nodes();
        foreach (string ClusterId in Snapshot)
        {
            if (!Graph.HasNode(ClusterId) || Graph.Children(ClusterId).Count == 0)
            {
                continue;
            }

            if (HasExternalEdge(Graph, ClusterId))
            {
                continue;
            }

            string Direction = string.Equals(Graph.Label.RankDir, "TB", StringComparison.Ordinal) ? "LR" : "TB";
            if (ClusterDirection.TryGetValue(ClusterId, out string? Declared) && !string.IsNullOrEmpty(Declared))
            {
                Direction = Declared;
            }

            DagreGraph Inner = new DagreGraph(true, true, true);
            Inner.SetGraph(new DagreGraphLabel
            {
                RankDir = Direction,
                NodeSep = Graph.Label.NodeSep,
                RankSep = Graph.Label.RankSep + SUBGRAPH_RANKSEP_EXTRA_PX,
                EdgeSep = Graph.Label.EdgeSep,
                MarginX = Graph.Label.MarginX,
                MarginY = Graph.Label.MarginY
            });

            List<string> Members = CopyOrder(Graph, ClusterId);
            foreach (string Member in Members)
            {
                DagreNodeLabel? Label = Graph.Node(Member);
                Inner.SetNode(Member, new DagreNodeLabel
                {
                    Width = Label?.Width ?? 0,
                    Height = Label?.Height ?? 0
                }, true);
            }

            foreach (string Member in Members)
            {
                string? MemberParent = Graph.Parent(Member);
                if (MemberParent != null && MemberParent != ClusterId && Members.Contains(MemberParent))
                {
                    Inner.SetParent(Member, MemberParent);
                }
            }

            foreach (DagreEdge Edge in Graph.Edges())
            {
                if (Members.Contains(Edge.V) && Members.Contains(Edge.W))
                {
                    DagreEdgeLabel? Label = Graph.Edge(Edge);
                    Inner.SetEdge(Edge.V, Edge.W, Label == null ? new DagreEdgeLabel() : Label.Clone(), true, Edge.Name);
                }
            }

            ExtractIsolatedClusters(Inner, ClusterDirection, Extracted);
            DagreLayoutEngine.Layout(Inner);
            DagreNodeLabel? Cluster = Graph.Node(ClusterId);
            if (Cluster != null)
            {
                Cluster.Width = Inner.Label.Width;
                Cluster.Height = Inner.Label.Height;
            }

            foreach (string Member in Members)
            {
                Graph.RemoveNode(Member);
            }

            Extracted[ClusterId] = Inner;
        }
    }

    private static List<string> CopyOrder(DagreGraph Graph, string RootId)
    {
        List<string> Order = new List<string>();
        AppendCopyOrder(Graph, RootId, RootId, Order);
        return Order;
    }

    private static void AppendCopyOrder(DagreGraph Graph, string ClusterId, string RootId, List<string> Order)
    {
        List<string> Nodes = new List<string>(Graph.Children(ClusterId));
        if (ClusterId != RootId)
        {
            Nodes.Add(ClusterId);
        }

        foreach (string NodeId in Nodes)
        {
            if (Graph.Children(NodeId).Count > 0 && NodeId != ClusterId)
            {
                AppendCopyOrder(Graph, NodeId, RootId, Order);
            }
            else if (NodeId != RootId)
            {
                Order.Add(NodeId);
            }
        }
    }

    private static bool HasExternalEdge(DagreGraph Graph, string ClusterId)
    {
        HashSet<string> Members = Descendants(Graph, ClusterId);
        foreach (DagreEdge Edge in Graph.Edges())
        {
            bool SourceInside = Members.Contains(Edge.V);
            bool TargetInside = Members.Contains(Edge.W);
            if (SourceInside != TargetInside)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> Descendants(DagreGraph Graph, string ClusterId)
    {
        HashSet<string> Result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string Child in Graph.Children(ClusterId))
        {
            Result.Add(Child);
            foreach (string Nested in Descendants(Graph, Child))
            {
                Result.Add(Nested);
            }
        }

        return Result;
    }

    private static bool IsInside(DagreGraph Graph, string NodeId, string ClusterId)
    {
        string? Parent = Graph.Parent(NodeId);
        while (Parent != null)
        {
            if (Parent == ClusterId)
            {
                return true;
            }

            Parent = Graph.Parent(Parent);
        }

        return false;
    }

    private static string FindLeaf(DagreGraph Graph, string NodeId)
    {
        List<string> Children = Graph.Children(NodeId);
        if (Children.Count == 0)
        {
            return NodeId;
        }

        foreach (string Child in Children)
        {
            string Leaf = FindLeaf(Graph, Child);
            if (!string.IsNullOrEmpty(Leaf))
            {
                return Leaf;
            }
        }

        return NodeId;
    }

    private static string ToRankDir(FlowchartDirection Direction)
    {
        switch (Direction)
        {
            case FlowchartDirection.BottomToTop:
                return "BT";
            case FlowchartDirection.LeftToRight:
                return "LR";
            case FlowchartDirection.RightToLeft:
                return "RL";
            default:
                return "TB";
        }
    }
}
