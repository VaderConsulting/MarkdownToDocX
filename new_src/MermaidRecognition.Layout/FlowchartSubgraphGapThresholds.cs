using System;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves minimum subgraph cluster gaps used by layout and validation.
/// </summary>
public static class FlowchartSubgraphGapThresholds
{
    /// <summary>
    /// Minimum exterior gap between nested child and parent cluster borders on the cross-flow axis.
    /// </summary>
    public static double ResolveNestedCrossAxisGapPx() => FlowchartSubgraphBoundsBuilder.NESTED_CLUSTER_GAP_PX;

    /// <summary>
    /// Minimum exterior gap between nested child and parent cluster borders on the primary flow axis.
    /// </summary>
    public static double ResolveNestedFlowAxisGapPx(FlowchartRenderOptions Options) => Math.Max(
            FlowchartSubgraphBoundsBuilder.NESTED_CLUSTER_GAP_PX,
            Options.RankSpacingPx);

    /// <summary>
    /// Minimum gap between direct member node bodies and a nested child cluster box on the flow axis.
    /// </summary>
    public static double ResolveDirectMemberNestedGapPx(FlowchartRenderOptions Options) => Math.Max(
            FlowchartSubgraphLayoutPostProcessor.SUBGRAPH_EXTERNAL_GAP_PX,
            Options.RankSpacingPx);
}
