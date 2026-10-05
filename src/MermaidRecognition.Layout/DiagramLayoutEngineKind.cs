namespace MermaidRecognition.Layout;

/// <summary>
/// Which layout and routing implementation to run.
/// </summary>
public enum DiagramLayoutEngineKind
{
    /// <summary>
    /// The current native Sugiyama placement plus the custom shared router.
    /// </summary>
    Custom = 0,

    /// <summary>
    /// Official MSAGL layered layout with the rectilinear obstacle router.
    /// </summary>
    MsaglRectilinear = 1,

    /// <summary>
    /// Official MSAGL layered layout with the spline obstacle router.
    /// </summary>
    MsaglSpline = 2
}
