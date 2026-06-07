namespace MermaidRecognition.Layout;

/// <summary>
/// Intended path geometry before SVG path generation.
/// </summary>
public enum FlowchartPathShapeIntent
{
    Unknown,

    /// <summary>
    /// Straight vertical or horizontal connector between aligned ports.
    /// </summary>
    StraightOrSoftVertical,

    /// <summary>
    /// Straight horizontal connector between aligned ports.
    /// </summary>
    StraightOrSoftHorizontal,

    /// <summary>
    /// Short smooth branch curve for local decision fan-out.
    /// </summary>
    ShortSmoothBranch,

    /// <summary>
    /// Smooth merge-back curve rejoining the main path.
    /// </summary>
    SmoothMergeBack,

    /// <summary>
    /// Orthogonal exterior corridor with explicit waypoints.
    /// </summary>
    OrthogonalCorridor,

    /// <summary>
    /// Long bypass curve or corridor spanning multiple ranks.
    /// </summary>
    LongBypassCorridor,

    /// <summary>
    /// Return loop with smooth routing around intervening nodes.
    /// </summary>
    ReturnLoopCurve
}
