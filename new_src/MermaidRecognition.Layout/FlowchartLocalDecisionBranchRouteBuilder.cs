using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds smooth local decision branches from diamond side ports to nearby margin targets.
/// </summary>
public static class FlowchartLocalDecisionBranchRouteBuilder
{
    /// <summary>
    /// Attempts to build a nearby labelled decision branch with a five-point curved guide.
    /// </summary>
    public static bool TryBuildRoute(
        EdgeContract Edge,
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        List<(double X, double Y)> LayoutPoints) => FlowchartDecisionPortAllocator.TryBuildDecisionFanOutCurvedRoute(
            Edge,
            Contract,
            BoundsById,
            SourceShape,
            TargetShape,
            Direction,
            LayoutPoints);
}
