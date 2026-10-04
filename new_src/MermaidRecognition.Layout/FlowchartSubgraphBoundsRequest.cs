using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Parameters for computing subgraph cluster bounds with direction-aware nested gaps.
/// </summary>
public readonly struct FlowchartSubgraphBoundsRequest
{
    /// <summary>
    /// Creates a bounds request.
    /// </summary>
    public FlowchartSubgraphBoundsRequest(FlowchartDirection Direction, double NestedFlowAxisGapPx)
    {
        this.Direction = Direction;
        this.NestedFlowAxisGapPx = NestedFlowAxisGapPx;
        HorizontalFlow = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        NestedCrossAxisGapPx = FlowchartSubgraphGapThresholds.ResolveNestedCrossAxisGapPx();
    }

    /// <summary>
    /// Diagram direction used to distinguish flow-axis from cross-axis nested gaps.
    /// </summary>
    public FlowchartDirection Direction { get; }

    /// <summary>
    /// Whether the primary flow axis is horizontal.
    /// </summary>
    public bool HorizontalFlow { get; }

    /// <summary>
    /// Minimum nested child-to-parent gap on the primary flow axis.
    /// </summary>
    public double NestedFlowAxisGapPx { get; }

    /// <summary>
    /// Minimum nested child-to-parent gap on the cross-flow axis.
    /// </summary>
    public double NestedCrossAxisGapPx { get; }

    /// <summary>
    /// Builds a request from contract direction and render options.
    /// </summary>
    public static FlowchartSubgraphBoundsRequest FromContract(
        DiagramContract Contract,
        FlowchartRenderOptions Options)
    {
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        double NestedFlowAxisGapPx = FlowchartSubgraphGapThresholds.ResolveNestedFlowAxisGapPx(Options);
        return new FlowchartSubgraphBoundsRequest(Direction, NestedFlowAxisGapPx);
    }

    /// <summary>
    /// Legacy request that applies the historical 24px nested gap on every side.
    /// </summary>
    public static FlowchartSubgraphBoundsRequest LegacyDefault => new FlowchartSubgraphBoundsRequest(
                FlowchartDirection.TopToBottom,
                FlowchartSubgraphBoundsBuilder.NESTED_CLUSTER_GAP_PX);
}
