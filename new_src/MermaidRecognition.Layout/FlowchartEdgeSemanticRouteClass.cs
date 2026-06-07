namespace MermaidRecognition.Layout;

/// <summary>
/// Semantic route class assigned before path generation (spec flowchart routing model).
/// </summary>
public enum FlowchartEdgeSemanticRouteClass
{
    Unknown,

    /// <summary>
    /// Vertically aligned spine ports connected by a straight two-point segment.
    /// </summary>
    CentreSpine,

    /// <summary>
    /// Nearby labelled decision branch with a smooth local curve.
    /// </summary>
    LocalDecisionBranch,

    /// <summary>
    /// Side branch rejoining the main spine below its source row.
    /// </summary>
    MergeBack,

    /// <summary>
    /// Decision branch that skips intermediate spine nodes on the same column.
    /// </summary>
    SkipBypass,

    /// <summary>
    /// Wide exterior route around the spine corridor (far margin or chord intersection).
    /// </summary>
    LongExceptionRoute
}
