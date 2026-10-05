using System;
using System.Collections.Generic;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Maps <see cref="DiagramContract"/> flowchart facts to an adopted Sugiyama <see cref="GeometryGraph"/>.
/// Does not read edge label text for structure (spec §11.8).
/// </summary>
public static class FlowchartGeometryGraphBuilder
{
    /// <summary>
    /// Measures display node dimensions for every contract node.
    /// </summary>
    public static Dictionary<string, FlowchartNodeDimensions> BuildMeasuredDimensions(
        DiagramContract Contract,
        FlowchartRenderOptions Options)
    {
        Dictionary<string, FlowchartNodeDimensions> MeasuredById =
            new Dictionary<string, FlowchartNodeDimensions>(StringComparer.Ordinal);

        foreach (NodeContract NodeContract in Contract.Nodes)
        {
            MeasuredById[NodeContract.Id] = FlowchartNodeSizeEstimator.Estimate(NodeContract, Options);
        }

        return MeasuredById;
    }

    /// <summary>
    /// Builds a geometry graph from a flowchart contract using measured node sizes (spec §3.17.3).
    /// For LR/RL diagrams, layout dimensions are transposed so rank spacing applies along the flow axis.
    /// For TB/BT diagrams, loop-back edges are left out so their placeholder nodes do not push the main chain sideways.
    /// </summary>
    public static GeometryGraph Build(
        DiagramContract Contract,
        FlowchartRenderOptions Options,
        FlowchartDirection Direction,
        out Dictionary<string, Node> NodesById)
    {
        NodesById = new Dictionary<string, Node>(StringComparer.Ordinal);
        GeometryGraph Graph = new GeometryGraph();
        Dictionary<string, FlowchartNodeDimensions> MeasuredById = BuildMeasuredDimensions(Contract, Options);
        bool SwapLayoutDimensions = ShouldSwapLayoutDimensions(Direction);

        HashSet<string> GroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            GroupIds.Add(Group.Id);
        }

        foreach (NodeContract NodeContract in Contract.Nodes)
        {
            if (GroupIds.Contains(NodeContract.Id))
            {
                continue;
            }

            FlowchartNodeDimensions Dimensions = MeasuredById[NodeContract.Id];
            double LayoutWidth = SwapLayoutDimensions ? Dimensions.Height : Dimensions.Width;
            double LayoutHeight = SwapLayoutDimensions ? Dimensions.Width : Dimensions.Height;

            ICurve Boundary = CurveFactory.CreateRectangle(LayoutWidth, LayoutHeight, new Point(0, 0));
            Node LayoutNode = new Node(Boundary, NodeContract.Id)
            {
                UserData = NodeContract.Id
            };
            Graph.Nodes.Add(LayoutNode);
            NodesById[NodeContract.Id] = LayoutNode;
        }

        HashSet<EdgeContract> SkippedBackEdges = SwapLayoutDimensions ? new HashSet<EdgeContract>() : FlowchartBackEdgeHelper.FindBackEdges(Contract);
        foreach (EdgeContract EdgeContract in Contract.Edges)
        {
            if (SkippedBackEdges.Contains(EdgeContract) && !string.Equals(EdgeContract.SourceId, EdgeContract.TargetId, StringComparison.Ordinal) && !HasReverseEdge(Contract, EdgeContract))
            {
                continue;
            }

            if (!NodesById.TryGetValue(EdgeContract.SourceId, out Node? SourceNode)
                || !NodesById.TryGetValue(EdgeContract.TargetId, out Node? TargetNode))
            {
                continue;
            }

            Edge LayoutEdge = new Edge(SourceNode, TargetNode);
            Graph.Edges.Add(LayoutEdge);
        }

        return Graph;
    }

    private static bool HasReverseEdge(DiagramContract Contract, EdgeContract Edge)
    {
        foreach (EdgeContract Candidate in Contract.Edges)
        {
            if (string.Equals(Candidate.SourceId, Edge.TargetId, StringComparison.Ordinal) && string.Equals(Candidate.TargetId, Edge.SourceId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSwapLayoutDimensions(FlowchartDirection Direction) => Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
}
