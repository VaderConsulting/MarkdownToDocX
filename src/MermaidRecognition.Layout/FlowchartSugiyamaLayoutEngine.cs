using System;
using System.Collections.Generic;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Layout.Layered;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Runs adopted Sugiyama layered layout on a flowchart contract (spec §11.1.11).
/// MSAGL classes are copied into <c>Sugiyama/</c> — see <c>SUGIYAMA_ADOPTION.md</c>.
/// </summary>
public static class FlowchartSugiyamaLayoutEngine
{
    /// <summary>
    /// Lays out the contract using Sugiyama; returns node centres and graph bounds.
    /// </summary>
    public static bool TryLayout(DiagramContract Contract, out FlowchartLayoutResult Result)
    {
        Result = new FlowchartLayoutResult();

        if (Contract == null || Contract.Nodes.Count == 0)
        {
            return false;
        }

        if (!NativeRenderCapabilities.CanRender(Contract))
        {
            return false;
        }

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, FlowchartNodeDimensions> MeasuredDimensionsById =
            FlowchartGeometryGraphBuilder.BuildMeasuredDimensions(Contract, RenderOptions);

        GeometryGraph Graph = FlowchartGeometryGraphBuilder.Build(
            Contract,
            RenderOptions,
            Direction,
            out Dictionary<string, Node> NodesById);
        SugiyamaLayoutSettings Settings = CreateSettings(Contract, RenderOptions);
        LayeredLayout Layout = new LayeredLayout(Graph, Settings);
        Layout.Run();

        List<FlowchartLayoutNodeBounds> RawBounds = [];

        foreach (KeyValuePair<string, Node> Entry in NodesById)
        {
            Node LayoutNode = Entry.Value;
            if (!MeasuredDimensionsById.TryGetValue(Entry.Key, out FlowchartNodeDimensions Measured))
            {
                continue;
            }

            RawBounds.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Entry.Key,
                CenterX = LayoutNode.Center.X,
                CenterY = LayoutNode.Center.Y,
                Width = Measured.Width,
                Height = Measured.Height
            });
        }

        List<FlowchartLayoutNodeBounds> RemappedBounds = FlowchartDirectionRemapper.Remap(RawBounds, Direction);
        RemappedBounds = FlowchartLayoutPostProcessor.Apply(Contract, RemappedBounds, Direction, RenderOptions);
        RemappedBounds = FlowchartLayoutCrossingSearch.ReduceSameRankInversions(Contract, RemappedBounds, Direction);
        RemappedBounds = FlowchartExternalNodePlacementSearch.Search(Contract, RemappedBounds, Direction);
        RemappedBounds = FlowchartLayoutCrossingSearch.ReduceSameRankInversions(Contract, RemappedBounds, Direction);
        (double GraphWidth, double GraphHeight) = FlowchartDirectionRemapper.ComputeGraphSize(RemappedBounds);

        FlowchartLayoutResult Built = new FlowchartLayoutResult
        {
            GraphWidth = GraphWidth,
            GraphHeight = GraphHeight
        };
        Built.NodeBoundsMutable.AddRange(RemappedBounds);

        Result = Built;
        return Result.NodeBounds.Count > 0;
    }

    private static SugiyamaLayoutSettings CreateSettings(DiagramContract Contract, FlowchartRenderOptions Options)
    {
        SugiyamaLayoutSettings Settings = new SugiyamaLayoutSettings
        {
            LayerSeparation = Options.RankSpacingPx,
            NodeSeparation = Options.NodeSpacingPx
        };
        return Settings;
    }
}
