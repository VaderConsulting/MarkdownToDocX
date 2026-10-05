using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds a geometry graph with cluster containment for subgraphs and groups.
/// </summary>
public static class AdoptedMsaglGeometryGraphBuilder
{
    /// <summary>
    /// Builds the graph. Group identifiers become clusters so the adopted routers see containment.
    /// </summary>
    /// <param name="Contract">Flowchart contract.</param>
    /// <param name="Options">Render options used for node sizes and labels.</param>
    /// <param name="NodesById">Leaf nodes and clusters keyed by contract identifier.</param>
    /// <returns>The geometry graph.</returns>
    public static GeometryGraph Build(
        DiagramContract Contract,
        FlowchartRenderOptions Options,
        out Dictionary<string, Node> NodesById)
    {
        NodesById = new Dictionary<string, Node>(StringComparer.Ordinal);
        GeometryGraph Graph = new GeometryGraph();
        HashSet<string> GroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupIds.Add(Group.Id);
        }

        foreach (NodeContract NodeItem in Contract.Nodes)
        {
            if (GroupIds.Contains(NodeItem.Id))
            {
                continue;
            }

            FlowchartNodeDimensions Dimensions = FlowchartNodeSizeEstimator.Estimate(NodeItem, Options);
            ICurve Boundary = CurveFactory.CreateRectangle(Dimensions.Width, Dimensions.Height, new Point(0, 0));
            Node LayoutNode = new Node(Boundary, NodeItem.Id)
            {
                UserData = NodeItem.Id
            };
            Graph.Nodes.Add(LayoutNode);
            NodesById[NodeItem.Id] = LayoutNode;
        }

        Dictionary<string, Cluster> ClustersById = new Dictionary<string, Cluster>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            Cluster ClusterNode = new Cluster
            {
                UserData = Group.Id,
                BoundaryCurve = CurveFactory.CreateRectangle(40.0, 40.0, new Point(0, 0))
            };
            ClustersById[Group.Id] = ClusterNode;
            NodesById[Group.Id] = ClusterNode;
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            Cluster ClusterNode = ClustersById[Group.Id];
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (NodesById.TryGetValue(ChildId, out Node? Child) && Child is not Cluster)
                {
                    ClusterNode.AddChild(Child);
                }
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (ClustersById.TryGetValue(ChildGroupId, out Cluster? ChildCluster))
                {
                    ClusterNode.AddChild(ChildCluster);
                }
            }
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.IsNullOrEmpty(Group.ParentGroupId))
            {
                Graph.RootCluster.AddChild(ClustersById[Group.Id]);
            }
        }

        foreach (EdgeContract EdgeItem in Contract.Edges)
        {
            if (string.Equals(EdgeItem.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!NodesById.TryGetValue(EdgeItem.SourceId, out Node? Source)
                || !NodesById.TryGetValue(EdgeItem.TargetId, out Node? Target))
            {
                continue;
            }

            Edge LayoutEdge = new Edge(Source, Target)
            {
                UserData = EdgeItem
            };
            if (!string.IsNullOrWhiteSpace(EdgeItem.Label))
            {
                double Width = Math.Max(16.0, EdgeItem.Label.Length * 8.0);
                LayoutEdge.Label = new Label(Width, 16.0, LayoutEdge);
            }

            Graph.Edges.Add(LayoutEdge);
        }

        return Graph;
    }
}
