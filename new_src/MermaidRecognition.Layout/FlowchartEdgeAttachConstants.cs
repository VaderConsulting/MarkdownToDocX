using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Shared attach constants for flowchart edge routing.
/// </summary>
public static class FlowchartEdgeAttachConstants
{
    /// <summary>
    /// Exterior clearance used only for cross endpoint markers.
    /// </summary>
    public const double EDGE_NODE_GAP_PX = 2.0;

    /// <summary>
    /// Returns the exterior offset from the shape boundary for the given endpoint marker.
    /// Cross markers keep a small gap so the line stops before the rendered cross.
    /// All other markers terminate on the shape boundary.
    /// </summary>
    /// <param name="EndpointMarker">The marker at the attach end.</param>
    public static double GetExteriorGapPx(EdgeEndpointMarker EndpointMarker)
    {
        if (EndpointMarker == EdgeEndpointMarker.Cross)
        {
            return EDGE_NODE_GAP_PX;
        }

        return 0.0;
    }
}
