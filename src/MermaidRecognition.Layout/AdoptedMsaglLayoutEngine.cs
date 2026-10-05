using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Layout.Layered;
using MermaidRecognition.Layout.Sugiyama.Routing;
using MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Runs adopted LayeredLayout with cluster containment, then the adopted MSAGL router.
/// </summary>
public static class AdoptedMsaglLayoutEngine
{
    /// <summary>
    /// Last layout or routing exception, when <see cref="TryLayoutAndRoute"/> returns false.
    /// </summary>
    public static string LastError { get; private set; } = string.Empty;

    /// <summary>
    /// Lays out nodes and routes edges with the adopted router named by <paramref name="Profile"/>.
    /// </summary>
    /// <param name="Contract">Flowchart contract.</param>
    /// <param name="Profile">Router and padding options.</param>
    /// <param name="Graph">The routed geometry graph when this method returns true.</param>
    /// <param name="NodesById">Nodes and clusters by contract identifier.</param>
    /// <returns>True when layout produced at least one node.</returns>
    public static bool TryLayoutAndRoute(
        DiagramContract Contract,
        AdoptedMsaglRoutingProfile Profile,
        out GeometryGraph Graph,
        out Dictionary<string, Node> NodesById)
    {
        Graph = new GeometryGraph();
        NodesById = new Dictionary<string, Node>(StringComparer.Ordinal);
        LastError = string.Empty;
        if (Contract == null || Contract.Nodes.Count == 0)
        {
            return false;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        Graph = AdoptedMsaglGeometryGraphBuilder.Build(Contract, Options, out NodesById);
        PrepareArrowheads(Graph, Profile);
        SugiyamaLayoutSettings Settings = new SugiyamaLayoutSettings
        {
            LayerSeparation = Options.RankSpacingPx,
            NodeSeparation = Options.NodeSpacingPx
        };
        Settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.None;
        Settings.EdgeRoutingSettings.Padding = Profile.ObstaclePaddingPx;
        Settings.EdgeRoutingSettings.PolylinePadding = Profile.PolylinePaddingPx;
        Settings.EdgeRoutingSettings.CornerRadius = Profile.CornerRadiusPx;
        Settings.EdgeRoutingSettings.BendPenalty = 30.0;

        try
        {
            List<Edge> ClusterEndpointEdges = DeferClusterEndpointEdges(Graph);
            LayeredLayout Layout = new LayeredLayout(Graph, Settings);
            Layout.Run();
            FitClusterBoundaries(Graph);
            foreach (Edge ClusterEdge in ClusterEndpointEdges)
            {
                Graph.Edges.Add(ClusterEdge);
            }

            RouteWithAdoptedRouter(Graph, Profile);
            EdgeLabelPlacement Labels = new EdgeLabelPlacement(Graph);
            Labels.Run();
        }
        catch (Exception Exception)
        {
            LastError = Exception.ToString();
            return false;
        }

        Graph.UpdateBoundingBox();
        return NodesById.Count > 0;
    }

    private static List<Edge> DeferClusterEndpointEdges(GeometryGraph Graph)
    {
        List<Edge> Deferred = [];
        List<Edge> Snapshot = [];
        foreach (Edge LayoutEdge in Graph.Edges)
        {
            Snapshot.Add(LayoutEdge);
        }

        foreach (Edge LayoutEdge in Snapshot)
        {
            if (LayoutEdge.Source is Cluster || LayoutEdge.Target is Cluster)
            {
                Deferred.Add(LayoutEdge);
                Graph.Edges.Remove(LayoutEdge);
            }
        }

        return Deferred;
    }

    private static void RouteWithAdoptedRouter(GeometryGraph Graph, AdoptedMsaglRoutingProfile Profile)
    {
        if (Profile.Style == AdoptedMsaglRoutingStyle.Rectilinear)
        {
            RectilinearEdgeRouter Router = new RectilinearEdgeRouter(
                Graph,
                Profile.ObstaclePaddingPx,
                Math.Max(0.1, Profile.CornerRadiusPx),
                true)
            {
                BendPenaltyAsAPercentageOfDistance = 30.0
            };
            Router.Run();
            return;
        }

        double TightPadding = Math.Max(0.1, Profile.ObstaclePaddingPx);
        double LoosePadding = Math.Max(0.1, Profile.PolylinePaddingPx);
        SplineRouter Spline = new SplineRouter(Graph, TightPadding, LoosePadding, Math.PI / 6.0);
        Spline.Run();
    }

    private static void PrepareArrowheads(GeometryGraph Graph, AdoptedMsaglRoutingProfile Profile)
    {
        foreach (Edge LayoutEdge in Graph.Edges)
        {
            if (LayoutEdge.EdgeGeometry.TargetArrowhead == null)
            {
                LayoutEdge.EdgeGeometry.TargetArrowhead = new Arrowhead
                {
                    Length = Profile.ArrowheadLengthPx,
                    Width = Profile.ArrowheadWidthPx
                };
            }
        }
    }

    private static void FitClusterBoundaries(GeometryGraph Graph)
    {
        foreach (Cluster ClusterNode in Graph.RootCluster.AllClustersDepthFirst())
        {
            if (object.ReferenceEquals(ClusterNode, Graph.RootCluster))
            {
                continue;
            }

            Rectangle Bounds = ClusterNode.BoundingBox;
            if (Bounds.Width <= 0.0 || Bounds.Height <= 0.0)
            {
                continue;
            }

            Bounds.Pad(16.0);
            ClusterNode.BoundaryCurve = CurveFactory.CreateRectangle(
                Bounds.Width,
                Bounds.Height,
                Bounds.Center);
        }
    }
}
