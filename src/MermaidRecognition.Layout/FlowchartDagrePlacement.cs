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
        Dictionary<string, (double Width, double Height)> MermaidDiamondSize = ApplyDiamondSize(Contract, Measured);

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
            Graph.SetNode(Group.Id, new DagreNodeLabel
            {
                Width = 0,
                Height = 0
            }, true);
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

        HashSet<EdgeContract> BackEdges = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        int EdgeIndex = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!Graph.HasNode(Edge.SourceId) || !Graph.HasNode(Edge.TargetId))
            {
                continue;
            }

            if (BackEdges.Contains(Edge)
                && !string.Equals(Edge.SourceId, Edge.TargetId, StringComparison.Ordinal)
                && !HasReverseEdge(Contract, Edge))
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

        AppendEdges(Graph, Built, 0, 0, MermaidDiamondSize);
        foreach (KeyValuePair<string, DagreGraph> Pair in Extracted)
        {
            (double X, double Y) Centre = ResolveCentre(Pair.Key, Graph, Extracted);
            DagreNodeLabel? ClusterLabel = FindLabel(Pair.Key, Graph, Extracted);
            double Left = Centre.X - ((ClusterLabel?.Width ?? 0) / 2);
            double Top = Centre.Y - ((ClusterLabel?.Height ?? 0) / 2);
            AppendEdges(Pair.Value, Built, Left, Top, MermaidDiamondSize);
        }

        Result = Built;
        return Result.NodeBounds.Count > 0;
    }

    private static Dictionary<string, (double Width, double Height)> ApplyDiamondSize(
        DiagramContract Contract,
        Dictionary<string, FlowchartNodeDimensions> Measured)
    {
        double StandardWidth = 0.0;
        double StandardHeight = 0.0;
        bool Found = false;
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (FlowchartDagrePresentation.IsDiamondShape(Node.Shape)
                || !Measured.TryGetValue(Node.Id, out FlowchartNodeDimensions? Size))
            {
                continue;
            }

            if (!IsStandardRectangle(Node.Shape))
            {
                continue;
            }

            Found = true;
            StandardWidth = Math.Max(StandardWidth, Size.Width);
            StandardHeight = Math.Max(StandardHeight, Size.Height);
        }

        Dictionary<string, (double Width, double Height)> MermaidSize =
            new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        if (!Found || StandardWidth < 1.0 || StandardHeight < 1.0)
        {
            return MermaidSize;
        }

        double DiamondWidth = Math.Max(1.0, Math.Round(StandardWidth));
        double DiamondHeight = Math.Max(1.0, Math.Round(StandardHeight)) * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR;
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!FlowchartDagrePresentation.IsDiamondShape(Node.Shape)
                || !Measured.TryGetValue(Node.Id, out FlowchartNodeDimensions? Size))
            {
                continue;
            }

            MermaidSize[Node.Id] = (Size.Width, Size.Height);
            Size.Width = DiamondWidth;
            Size.Height = DiamondHeight;
        }

        return MermaidSize;
    }

    private static bool IsStandardRectangle(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return true;
        }

        return string.Equals(Shape, "rect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "rectangle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Shape, "square", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendEdges(
        DagreGraph Graph,
        FlowchartLayoutResult Built,
        double OffsetX,
        double OffsetY,
        Dictionary<string, (double Width, double Height)> MermaidDiamondSize)
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

            string SourceId = Edge.V;
            string TargetId = Edge.W;
            List<DagrePoint> Points = new List<DagrePoint>(Label.Points);
            if (!string.IsNullOrEmpty(Label.ToCluster))
            {
                DagreNodeLabel? Cluster = Graph.Node(Label.ToCluster);
                if (Cluster != null && Cluster.Width > 0 && Cluster.Height > 0)
                {
                    Points = CutPathAtCluster(Points, Cluster);
                    TargetId = Label.ToCluster;
                }
            }

            if (!string.IsNullOrEmpty(Label.FromCluster))
            {
                DagreNodeLabel? Cluster = Graph.Node(Label.FromCluster);
                if (Cluster != null && Cluster.Width > 0 && Cluster.Height > 0)
                {
                    Points = CutPathLeavingCluster(Points, Cluster);
                    SourceId = Label.FromCluster;
                }
            }

            if (Points.Count < 2)
            {
                continue;
            }

            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute
            {
                EdgeId = Edge.Name ?? string.Empty,
                SourceId = SourceId,
                TargetId = TargetId,
                ExitSide = SideOfDiamond(Source, Points[0], Points[1], MermaidDiamondSize, SourceId),
                EntrySide = SideOfDiamond(Target, Points[Points.Count - 1], Points[Points.Count - 2], MermaidDiamondSize, TargetId)
            };
            foreach (DagrePoint Point in Points)
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

    /// <summary>
    /// Names the rectangle face a diamond edge actually meets. A waypoint that leans off a
    /// corner would otherwise report the neighbouring side.
    /// </summary>
    private static string SideOfDiamond(
        DagreNodeLabel Node,
        DagrePoint AtBoundary,
        DagrePoint Towards,
        Dictionary<string, (double Width, double Height)> MermaidDiamondSize,
        string NodeId)
    {
        if (!MermaidDiamondSize.ContainsKey(NodeId))
        {
            return SideOf(Node, AtBoundary, Towards);
        }

        string? Face = ClearRectangleFace(Node, AtBoundary);
        if (Face != null)
        {
            return Face;
        }

        return SideOf(Node, AtBoundary, Towards);
    }

    /// <summary>
    /// Returns the rectangle face under <paramref name="At"/> when the point is on one side and not a corner.
    /// </summary>
    private static string? ClearRectangleFace(DagreNodeLabel Node, DagrePoint At)
    {
        double HalfWidth = Node.Width / 2.0;
        double HalfHeight = Node.Height / 2.0;
        double DistanceLeft = Math.Abs(At.X - (Node.X - HalfWidth));
        double DistanceRight = Math.Abs(At.X - (Node.X + HalfWidth));
        double DistanceTop = Math.Abs(At.Y - (Node.Y - HalfHeight));
        double DistanceBottom = Math.Abs(At.Y - (Node.Y + HalfHeight));
        (string Name, double Distance)[] Faces =
        {
            ("left", DistanceLeft),
            ("right", DistanceRight),
            ("top", DistanceTop),
            ("bottom", DistanceBottom)
        };
        string BestName = Faces[0].Name;
        double Best = Faces[0].Distance;
        double Second = double.PositiveInfinity;
        for (int Index = 1; Index < Faces.Length; Index++)
        {
            if (Faces[Index].Distance < Best)
            {
                Second = Best;
                Best = Faces[Index].Distance;
                BestName = Faces[Index].Name;
            }
            else if (Faces[Index].Distance < Second)
            {
                Second = Faces[Index].Distance;
            }
        }

        if (Best > 2.0 || Second <= Best + 8.0)
        {
            return null;
        }

        return BestName;
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
            DagreEdgeLabel? Label = Graph.Edge(Edge);
            if (Label == null)
            {
                continue;
            }

            if (Anchor.ContainsKey(Source) && External.Contains(Source))
            {
                Label.FromCluster = Source;
                Source = Anchor[Source];
            }

            if (Anchor.ContainsKey(Target) && External.Contains(Target))
            {
                Label.ToCluster = Target;
                Target = Anchor[Target];
            }

            if (Source == Edge.V && Target == Edge.W)
            {
                continue;
            }

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

    private static List<DagrePoint> CutPathLeavingCluster(List<DagrePoint> Points, DagreNodeLabel Boundary)
    {
        List<DagrePoint> Reversed = new List<DagrePoint>(Points);
        Reversed.Reverse();
        List<DagrePoint> Cut = CutPathAtCluster(Reversed, Boundary);
        Cut.Reverse();
        return Cut;
    }

    /// <summary>
    /// Mermaid cuts a cluster edge at the subgraph rectangle (cutPathAtIntersect).
    /// </summary>
    private static List<DagrePoint> CutPathAtCluster(List<DagrePoint> Points, DagreNodeLabel Boundary)
    {
        List<DagrePoint> Kept = new List<DagrePoint>();
        if (Points.Count == 0)
        {
            return Kept;
        }

        DagrePoint LastOutside = Points[0];
        bool Inside = false;
        foreach (DagrePoint Point in Points)
        {
            if (!IsOutsideCluster(Boundary, Point) && !Inside)
            {
                DagrePoint Hit = IntersectCluster(Boundary, LastOutside, Point);
                bool Already = false;
                foreach (DagrePoint Existing in Kept)
                {
                    if (Existing.X == Hit.X && Existing.Y == Hit.Y)
                    {
                        Already = true;
                        break;
                    }
                }

                if (!Already)
                {
                    Kept.Add(Hit);
                }

                Inside = true;
            }
            else
            {
                LastOutside = Point;
                if (!Inside)
                {
                    Kept.Add(Point);
                }
            }
        }

        return Kept;
    }

    private static bool IsOutsideCluster(DagreNodeLabel Node, DagrePoint Point)
    {
        double Dx = Math.Abs(Point.X - Node.X);
        double Dy = Math.Abs(Point.Y - Node.Y);
        return Dx >= (Node.Width / 2.0) || Dy >= (Node.Height / 2.0);
    }

    private static DagrePoint IntersectCluster(DagreNodeLabel Node, DagrePoint Outside, DagrePoint Inside)
    {
        double CentreX = Node.X;
        double CentreY = Node.Y;
        double HalfWidth = Node.Width / 2.0;
        double HalfHeight = Node.Height / 2.0;
        double SpanX = Math.Abs(Outside.X - Inside.X);
        double SpanY = Math.Abs(Outside.Y - Inside.Y);
        double AlongX = Math.Abs(CentreX - Inside.X);
        double Radius = Inside.X < Outside.X ? HalfWidth - AlongX : HalfWidth + AlongX;
        double HitX;
        double HitY;
        if ((Math.Abs(CentreY - Outside.Y) * HalfWidth) > (Math.Abs(CentreX - Outside.X) * HalfHeight))
        {
            double GapY = Inside.Y < Outside.Y
                ? Outside.Y - HalfHeight - CentreY
                : CentreY - HalfHeight - Outside.Y;
            Radius = SpanY == 0 ? 0 : (SpanX * GapY) / SpanY;
            HitX = Inside.X < Outside.X ? Inside.X + Radius : Inside.X - SpanX + Radius;
            HitY = Inside.Y < Outside.Y ? Inside.Y + SpanY - GapY : Inside.Y - SpanY + GapY;
            if (Radius == 0)
            {
                HitX = Outside.X;
                HitY = Outside.Y;
            }

            if (SpanX == 0)
            {
                HitX = Outside.X;
            }

            if (SpanY == 0)
            {
                HitY = Outside.Y;
            }
        }
        else
        {
            if (Inside.X < Outside.X)
            {
                Radius = Outside.X - HalfWidth - CentreX;
            }
            else
            {
                Radius = CentreX - HalfWidth - Outside.X;
            }

            double GapY = SpanX == 0 ? 0 : (SpanY * Radius) / SpanX;
            HitX = Inside.X < Outside.X ? Inside.X + SpanX - Radius : Inside.X - SpanX + Radius;
            HitY = Inside.Y < Outside.Y ? Inside.Y + GapY : Inside.Y - GapY;
            if (Radius == 0)
            {
                HitX = Outside.X;
                HitY = Outside.Y;
            }

            if (SpanX == 0)
            {
                HitX = Outside.X;
            }

            if (SpanY == 0)
            {
                HitY = Outside.Y;
            }
        }

        return new DagrePoint
        {
            X = HitX,
            Y = HitY
        };
    }

    private static bool HasReverseEdge(DiagramContract Contract, EdgeContract Edge)
    {
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.SourceId, Edge.TargetId, StringComparison.Ordinal)
                && string.Equals(Candidate.TargetId, Edge.SourceId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
