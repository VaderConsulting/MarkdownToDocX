namespace MermaidRecognition.Layout;

/// <summary>
/// Option picker for the adopted MSAGL routers. Flowchart and enterprise share this type.
/// </summary>
public sealed class AdoptedMsaglRoutingProfile
{
    private readonly AdoptedMsaglRoutingStyle _Style;
    private readonly double _ObstaclePaddingPx;
    private readonly double _PolylinePaddingPx;
    private readonly double _CornerRadiusPx;
    private readonly double _ArrowheadLengthPx;
    private readonly double _ArrowheadWidthPx;
    private readonly string _LineStroke;

    /// <summary>
    /// Creates a profile.
    /// </summary>
    /// <param name="Style">Which adopted router to run.</param>
    /// <param name="ObstaclePaddingPx">Clearance around node and cluster obstacles.</param>
    /// <param name="PolylinePaddingPx">Extra spline clearance.</param>
    /// <param name="CornerRadiusPx">Rectilinear corner fit.</param>
    /// <param name="ArrowheadLengthPx">Arrowhead length in layout units.</param>
    /// <param name="ArrowheadWidthPx">Arrowhead base width.</param>
    /// <param name="LineStroke">SVG stroke colour.</param>
    public AdoptedMsaglRoutingProfile(
        AdoptedMsaglRoutingStyle Style,
        double ObstaclePaddingPx,
        double PolylinePaddingPx,
        double CornerRadiusPx,
        double ArrowheadLengthPx,
        double ArrowheadWidthPx,
        string LineStroke)
    {
        _Style = Style;
        _ObstaclePaddingPx = ObstaclePaddingPx;
        _PolylinePaddingPx = PolylinePaddingPx;
        _CornerRadiusPx = CornerRadiusPx;
        _ArrowheadLengthPx = ArrowheadLengthPx;
        _ArrowheadWidthPx = ArrowheadWidthPx;
        _LineStroke = LineStroke;
    }

    /// <summary>
    /// Default flowchart / enterprise rectilinear trial profile.
    /// </summary>
    public static AdoptedMsaglRoutingProfile SharedRectilinear
    {
        get
        {
            return new AdoptedMsaglRoutingProfile(
                AdoptedMsaglRoutingStyle.Rectilinear,
                8.0,
                4.0,
                3.0,
                10.0,
                7.0,
                "#333333");
        }
    }

    /// <summary>
    /// Default flowchart / enterprise spline trial profile.
    /// </summary>
    public static AdoptedMsaglRoutingProfile SharedSpline
    {
        get
        {
            return new AdoptedMsaglRoutingProfile(
                AdoptedMsaglRoutingStyle.Spline,
                8.0,
                4.0,
                0.0,
                10.0,
                7.0,
                "#333333");
        }
    }

    /// <summary>
    /// Which adopted router to run.
    /// </summary>
    public AdoptedMsaglRoutingStyle Style
    {
        get
        {
            return _Style;
        }
    }

    /// <summary>
    /// Clearance around obstacles.
    /// </summary>
    public double ObstaclePaddingPx
    {
        get
        {
            return _ObstaclePaddingPx;
        }
    }

    /// <summary>
    /// Extra spline clearance.
    /// </summary>
    public double PolylinePaddingPx
    {
        get
        {
            return _PolylinePaddingPx;
        }
    }

    /// <summary>
    /// Rectilinear corner fit.
    /// </summary>
    public double CornerRadiusPx
    {
        get
        {
            return _CornerRadiusPx;
        }
    }

    /// <summary>
    /// Arrowhead length.
    /// </summary>
    public double ArrowheadLengthPx
    {
        get
        {
            return _ArrowheadLengthPx;
        }
    }

    /// <summary>
    /// Arrowhead base width.
    /// </summary>
    public double ArrowheadWidthPx
    {
        get
        {
            return _ArrowheadWidthPx;
        }
    }

    /// <summary>
    /// Stroke colour for edges.
    /// </summary>
    public string LineStroke
    {
        get
        {
            return _LineStroke;
        }
    }
}
