namespace MermaidRecognition.Layout;

/// <summary>
/// Adopted MSAGL edge router used for a trial render.
/// </summary>
public enum AdoptedMsaglRoutingStyle
{
    /// <summary>
    /// Orthogonal channels from <c>RectilinearEdgeRouter</c>.
    /// </summary>
    Rectilinear,

    /// <summary>
    /// Smooth obstacle-avoiding curves from <c>SplineRouter</c>.
    /// </summary>
    Spline
}
