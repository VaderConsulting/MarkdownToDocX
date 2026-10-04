namespace MermaidRecognition.Layout;

/// <summary>
/// Intended boundary region for edge attach at a node perimeter.
/// </summary>
public enum FlowchartBoundaryRegionIntent
{
    Unknown,

    TopCentre,

    BottomCentre,

    LeftCentre,

    RightCentre,

    TopLeft,

    TopRight,

    BottomLeft,

    BottomRight,

    /// <summary>
    /// Decision branch may leave from the right or lower-right quadrant.
    /// </summary>
    RightOrBottomRight,

    /// <summary>
    /// Branch target may be entered from the top or left quadrant.
    /// </summary>
    TopOrLeft,

    /// <summary>
    /// Exterior far-margin attach used by long exception routes.
    /// </summary>
    FarMarginExterior
}
