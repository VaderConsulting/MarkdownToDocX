namespace MermaidRecognition.Layout;

/// <summary>
/// Categories of layout-space routing defects detected before or alongside SVG comparison.
/// </summary>
public enum FlowchartEdgeRouteDiagnosticKind
{
    LongDiagonalCrossing,

    UnnecessaryHardElbow,

    CentreSpineRegression,

    HookOrOvershoot,

    NodePassThrough,

    MergeAmbiguity,

    LabelAssociationFailure
}
