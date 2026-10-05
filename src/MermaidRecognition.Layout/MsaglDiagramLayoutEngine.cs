using System;
using System.Collections.Generic;

using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;

using MermaidRecognition.Contract;
using MermaidRecognition.Render;

using MsaglCluster = Microsoft.Msagl.Core.Layout.Cluster;
using MsaglEdge = Microsoft.Msagl.Core.Layout.Edge;
using MsaglGraph = Microsoft.Msagl.Core.Layout.GeometryGraph;
using MsaglLabel = Microsoft.Msagl.Core.Layout.Label;
using MsaglNode = Microsoft.Msagl.Core.Layout.Node;

namespace MermaidRecognition.Layout;

/// <summary>
/// Official MSAGL layered layout with either the rectilinear or the spline obstacle router.
/// </summary>
public sealed class MsaglDiagramLayoutEngine : IDiagramLayoutEngine
{
    private const double DIAGRAM_PADDING_PX = 24.0;

    private const int CURVE_SAMPLE_MIN = 8;

    /// <inheritdoc />
    public bool TryLayout(DiagramContract Contract, DiagramLayoutProfile Profile, out DiagramLayoutSolution Solution)
    {
        Solution = new DiagramLayoutSolution
        {
            EngineKind = Profile == null ? DiagramLayoutEngineKind.MsaglSpline : Profile.EngineKind
        };

        if (Contract == null || Contract.Nodes.Count == 0)
        {
            return false;
        }

        DiagramLayoutProfile EffectiveProfile = Profile ?? DiagramLayoutProfile.FlowchartMsaglSpline();
        if (TryLayoutInternal(Contract, EffectiveProfile, UseClusters: true, out Solution))
        {
            return true;
        }

        return TryLayoutInternal(Contract, EffectiveProfile, UseClusters: false, out Solution);
    }

    private static bool TryLayoutInternal(
        DiagramContract Contract,
        DiagramLayoutProfile Profile,
        bool UseClusters,
        out DiagramLayoutSolution Solution)
    {
        Solution = new DiagramLayoutSolution
        {
            EngineKind = Profile.EngineKind
        };

        try
        {
            FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
            Dictionary<string, FlowchartNodeDimensions> Measured =
                FlowchartGeometryGraphBuilder.BuildMeasuredDimensions(Contract, RenderOptions);
            HashSet<string> GroupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GroupContract Group in Contract.Groups)
            {
                GroupIds.Add(Group.Id);
            }

            MsaglGraph Graph = new MsaglGraph();
            Dictionary<string, MsaglNode> NodesById = new Dictionary<string, MsaglNode>(StringComparer.Ordinal);
            Dictionary<string, MsaglCluster> ClustersById = new Dictionary<string, MsaglCluster>(StringComparer.Ordinal);

            if (UseClusters)
            {
                foreach (GroupContract Group in Contract.Groups)
                {
                    MsaglCluster Cluster = new MsaglCluster();
                    Cluster.UserData = Group.Id;
                    Cluster.RectangularBoundary = new RectangularClusterBoundary
                    {
                        LeftMargin = Profile.ClusterMarginPx,
                        RightMargin = Profile.ClusterMarginPx,
                        TopMargin = Profile.ClusterMarginPx + 16.0,
                        BottomMargin = Profile.ClusterMarginPx
                    };
                    ClustersById[Group.Id] = Cluster;
                }

                foreach (GroupContract Group in Contract.Groups)
                {
                    MsaglCluster Cluster = ClustersById[Group.Id];
                    if (!string.IsNullOrEmpty(Group.ParentGroupId)
                        && ClustersById.TryGetValue(Group.ParentGroupId, out MsaglCluster? ParentCluster))
                    {
                        ParentCluster.AddChild(Cluster);
                    }
                    else
                    {
                        Graph.RootCluster.AddChild(Cluster);
                    }
                }
            }

            Dictionary<string, string> ParentGroupByNode = BuildParentGroupByNode(Contract, GroupIds);
            foreach (NodeContract NodeContract in Contract.Nodes)
            {
                if (GroupIds.Contains(NodeContract.Id))
                {
                    continue;
                }

                if (!Measured.TryGetValue(NodeContract.Id, out FlowchartNodeDimensions Dimensions))
                {
                    continue;
                }

                ICurve Boundary = CurveFactory.CreateRectangle(
                    Math.Max(24.0, Dimensions.Width),
                    Math.Max(20.0, Dimensions.Height),
                    new Point(0.0, 0.0));
                MsaglNode LayoutNode = new MsaglNode(Boundary, NodeContract.Id);
                LayoutNode.Padding = Profile.EdgePaddingPx;
                Graph.Nodes.Add(LayoutNode);
                NodesById[NodeContract.Id] = LayoutNode;

                if (UseClusters
                    && ParentGroupByNode.TryGetValue(NodeContract.Id, out string? ParentId)
                    && ClustersById.TryGetValue(ParentId, out MsaglCluster? Parent))
                {
                    Parent.AddChild(LayoutNode);
                }
                else
                {
                    Graph.RootCluster.AddChild(LayoutNode);
                }
            }

            foreach (EdgeContract EdgeContract in Contract.Edges)
            {
                if (!TryResolveEndpoint(EdgeContract.SourceId, NodesById, ClustersById, out MsaglNode? Source)
                    || !TryResolveEndpoint(EdgeContract.TargetId, NodesById, ClustersById, out MsaglNode? Target)
                    || Source == null
                    || Target == null)
                {
                    continue;
                }

                MsaglEdge LayoutEdge = new MsaglEdge(Source, Target);
                LayoutEdge.UserData = EdgeContract.SourceId + "->" + EdgeContract.TargetId;
                LayoutEdge.ArrowheadAtTarget = true;
                if (!string.IsNullOrWhiteSpace(EdgeContract.Label))
                {
                    double LabelWidth = Math.Max(20.0, EdgeContract.Label.Trim().Length * 7.0);
                    LayoutEdge.Label = new MsaglLabel(LabelWidth, 14.0, LayoutEdge);
                    LayoutEdge.Label.UserData = EdgeContract.Label.Trim();
                }

                Graph.Edges.Add(LayoutEdge);
            }

            SugiyamaLayoutSettings Settings = new SugiyamaLayoutSettings
            {
                LayerSeparation = Profile.LayerSeparationPx,
                NodeSeparation = Profile.NodeSeparationPx,
                ClusterMargin = Profile.ClusterMarginPx
            };
            Settings.EdgeRoutingSettings.Padding = Profile.EdgePaddingPx;
            Settings.EdgeRoutingSettings.PolylinePadding = Profile.EdgePaddingPx;
            Settings.EdgeRoutingSettings.EdgeRoutingMode = Profile.EngineKind == DiagramLayoutEngineKind.MsaglRectilinear
                ? EdgeRoutingMode.Rectilinear
                : EdgeRoutingMode.Spline;

            foreach (KeyValuePair<string, MsaglCluster> Entry in ClustersById)
            {
                Settings.ClusterSettings[Entry.Value] = Settings;
            }

            LayoutHelpers.CalculateLayout(Graph, Settings, null, string.Empty);

            double MinX = Graph.Left;
            double MaxX = Graph.Right;
            double MinY = Graph.Bottom;
            double MaxY = Graph.Top;
            if (double.IsNaN(MinX) || double.IsNaN(MaxY) || MaxX <= MinX)
            {
                return false;
            }

            double Pad = DIAGRAM_PADDING_PX;
            Solution.Width = (MaxX - MinX) + (Pad * 2.0);
            Solution.Height = (MaxY - MinY) + (Pad * 2.0);

            foreach (KeyValuePair<string, MsaglNode> Entry in NodesById)
            {
                Rectangle Box = Entry.Value.BoundingBox;
                Solution.NodeBounds.Add(new FlowchartLayoutNodeBounds
                {
                    NodeId = Entry.Key,
                    CenterX = ToSvgX(Box.Center.X, MinX, Pad),
                    CenterY = ToSvgY(Box.Center.Y, MaxY, Pad),
                    Width = Box.Width,
                    Height = Box.Height
                });
            }

            foreach (KeyValuePair<string, MsaglCluster> Entry in ClustersById)
            {
                Rectangle Box = Entry.Value.BoundingBox;
                if (Entry.Value.RectangularBoundary != null && Entry.Value.RectangularBoundary.Rect.Width > 1.0)
                {
                    Box = Entry.Value.RectangularBoundary.Rect;
                }

                Solution.GroupBounds.Add(new FlowchartLayoutNodeBounds
                {
                    NodeId = Entry.Key,
                    CenterX = ToSvgX(Box.Center.X, MinX, Pad),
                    CenterY = ToSvgY(Box.Center.Y, MaxY, Pad),
                    Width = Math.Max(1.0, Box.Width),
                    Height = Math.Max(1.0, Box.Height)
                });
            }

            foreach (MsaglEdge LayoutEdge in Graph.Edges)
            {
                string SourceId = ReadUserId(LayoutEdge.Source);
                string TargetId = ReadUserId(LayoutEdge.Target);
                string Label = LayoutEdge.Label?.UserData as string ?? string.Empty;
                List<(double X, double Y)> Samples = SampleCurve(LayoutEdge.Curve, MinX, MaxY, Pad);
                if (Samples.Count < 2)
                {
                    Point Start = LayoutEdge.Source.Center;
                    Point End = LayoutEdge.Target.Center;
                    Samples.Add((ToSvgX(Start.X, MinX, Pad), ToSvgY(Start.Y, MaxY, Pad)));
                    Samples.Add((ToSvgX(End.X, MinX, Pad), ToSvgY(End.Y, MaxY, Pad)));
                }

                DiagramLayoutRoutedEdge Routed = new DiagramLayoutRoutedEdge(SourceId, TargetId, Label, Samples);
                if (LayoutEdge.Label != null)
                {
                    Routed.LabelX = ToSvgX(LayoutEdge.Label.Center.X, MinX, Pad);
                    Routed.LabelY = ToSvgY(LayoutEdge.Label.Center.Y, MaxY, Pad);
                }

                Solution.Edges.Add(Routed);
            }

            return Solution.NodeBounds.Count > 0;
        }
        catch (Exception)
        {
            Solution = new DiagramLayoutSolution
            {
                EngineKind = Profile.EngineKind
            };
            return false;
        }
    }

    private static Dictionary<string, string> BuildParentGroupByNode(
        DiagramContract Contract,
        HashSet<string> GroupIds)
    {
        Dictionary<string, string> ParentByNode = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (GroupIds.Contains(ChildId))
                {
                    continue;
                }

                ParentByNode[ChildId] = Group.Id;
            }
        }

        return ParentByNode;
    }

    private static bool TryResolveEndpoint(
        string Id,
        IReadOnlyDictionary<string, MsaglNode> NodesById,
        IReadOnlyDictionary<string, MsaglCluster> ClustersById,
        out MsaglNode? Node)
    {
        if (NodesById.TryGetValue(Id, out MsaglNode? Leaf))
        {
            Node = Leaf;
            return true;
        }

        if (ClustersById.TryGetValue(Id, out MsaglCluster? Cluster))
        {
            Node = Cluster;
            return true;
        }

        Node = null;
        return false;
    }

    private static string ReadUserId(MsaglNode Node)
    {
        if (Node.UserData is string Text && !string.IsNullOrEmpty(Text))
        {
            return Text;
        }

        return string.Empty;
    }

    private static List<(double X, double Y)> SampleCurve(ICurve? Curve, double MinX, double MaxY, double Pad)
    {
        List<(double X, double Y)> Samples = [];
        if (Curve == null)
        {
            return Samples;
        }

        double Length = Math.Max(1.0, Curve.Length);
        int Count = Math.Max(CURVE_SAMPLE_MIN, (int)Math.Ceiling(Length / 8.0));
        double Start = Curve.ParStart;
        double End = Curve.ParEnd;
        if (Math.Abs(End - Start) < 1e-9)
        {
            Point Only = Curve.Start;
            Samples.Add((ToSvgX(Only.X, MinX, Pad), ToSvgY(Only.Y, MaxY, Pad)));
            return Samples;
        }

        for (int Index = 0; Index <= Count; Index++)
        {
            double Parameter = Start + ((End - Start) * Index / Count);
            Point Point = Curve[Parameter];
            Samples.Add((ToSvgX(Point.X, MinX, Pad), ToSvgY(Point.Y, MaxY, Pad)));
        }

        return Samples;
    }

    private static double ToSvgX(double X, double MinX, double Pad)
    {
        return X - MinX + Pad;
    }

    private static double ToSvgY(double Y, double MaxY, double Pad)
    {
        return MaxY - Y + Pad;
    }
}
