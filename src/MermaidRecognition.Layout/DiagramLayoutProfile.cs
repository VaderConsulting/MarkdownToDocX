namespace MermaidRecognition.Layout;

/// <summary>
/// Options a diagram kind may choose. Profiles do not reimplement layout.
/// </summary>
public sealed class DiagramLayoutProfile
{
    private DiagramLayoutEngineKind _EngineKind = DiagramLayoutEngineKind.Custom;

    private double _NodeSeparationPx = 40.0;

    private double _LayerSeparationPx = 55.0;

    private double _EdgePaddingPx = 8.0;

    private double _ClusterMarginPx = 18.0;

    private double _ArrowheadLengthPx = 10.0;

    private double _LabelOffsetPx = 10.0;

    /// <summary>
    /// Which engine implementation to run.
    /// </summary>
    public DiagramLayoutEngineKind EngineKind
    {
        get
        {
            return _EngineKind;
        }
        set
        {
            _EngineKind = value;
        }
    }

    /// <summary>
    /// Gap between sibling nodes, in pixels.
    /// </summary>
    public double NodeSeparationPx
    {
        get
        {
            return _NodeSeparationPx;
        }
        set
        {
            _NodeSeparationPx = value;
        }
    }

    /// <summary>
    /// Gap between ranks, in pixels.
    /// </summary>
    public double LayerSeparationPx
    {
        get
        {
            return _LayerSeparationPx;
        }
        set
        {
            _LayerSeparationPx = value;
        }
    }

    /// <summary>
    /// Padding the router keeps between a line and a box.
    /// </summary>
    public double EdgePaddingPx
    {
        get
        {
            return _EdgePaddingPx;
        }
        set
        {
            _EdgePaddingPx = value;
        }
    }

    /// <summary>
    /// Padding inside a group box.
    /// </summary>
    public double ClusterMarginPx
    {
        get
        {
            return _ClusterMarginPx;
        }
        set
        {
            _ClusterMarginPx = value;
        }
    }

    /// <summary>
    /// Arrowhead length in pixels.
    /// </summary>
    public double ArrowheadLengthPx
    {
        get
        {
            return _ArrowheadLengthPx;
        }
        set
        {
            _ArrowheadLengthPx = value;
        }
    }

    /// <summary>
    /// Offset from the edge to its label, in pixels.
    /// </summary>
    public double LabelOffsetPx
    {
        get
        {
            return _LabelOffsetPx;
        }
        set
        {
            _LabelOffsetPx = value;
        }
    }

    /// <summary>
    /// Flowchart defaults on the current custom engine.
    /// </summary>
    public static DiagramLayoutProfile FlowchartCustom()
    {
        return new DiagramLayoutProfile();
    }

    /// <summary>
    /// Flowchart defaults on MSAGL rectilinear routing.
    /// </summary>
    public static DiagramLayoutProfile FlowchartMsaglRectilinear()
    {
        return new DiagramLayoutProfile
        {
            EngineKind = DiagramLayoutEngineKind.MsaglRectilinear
        };
    }

    /// <summary>
    /// Flowchart defaults on MSAGL spline routing.
    /// </summary>
    public static DiagramLayoutProfile FlowchartMsaglSpline()
    {
        return new DiagramLayoutProfile
        {
            EngineKind = DiagramLayoutEngineKind.MsaglSpline
        };
    }
}
