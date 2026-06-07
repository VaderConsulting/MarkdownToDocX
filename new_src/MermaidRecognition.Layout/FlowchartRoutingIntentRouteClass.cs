namespace MermaidRecognition.Layout;

/// <summary>
/// Renderer-independent route classification used by the routing-intent diagnostic layer.
/// Inferred from graph topology and layout geometry, not from edge label text.
/// </summary>
public enum FlowchartRoutingIntentRouteClass
{
    Unknown,

    /// <summary>
    /// Main vertical or horizontal spine connection between aligned nodes.
    /// </summary>
    CentreSpine,

    /// <summary>
    /// Short labelled branch leaving a decision node to a nearby target.
    /// </summary>
    LocalDecisionBranch,

    /// <summary>
    /// Side branch rejoining the main path below or beside its source row.
    /// </summary>
    MergeBack,

    /// <summary>
    /// Local bypass that skips one or more intermediate spine nodes on the same column.
    /// </summary>
    SkipBypass,

    /// <summary>
    /// Long exterior bypass or exception path around the spine corridor.
    /// </summary>
    LongExceptionBypass,

    /// <summary>
    /// Edge targeting a node with multiple incoming routes requiring merge-aware port allocation.
    /// </summary>
    MultiInputMerge,

    /// <summary>
    /// Short connection between adjacent nodes on neighbouring ranks or lanes.
    /// </summary>
    ShortAdjacent,

    /// <summary>
    /// Connection between nodes in different layout lanes or columns.
    /// </summary>
    CrossLane,

    /// <summary>
    /// Backward or return loop edge, where supported by the layout direction.
    /// </summary>
    ReturnBackward,

    /// <summary>
    /// Route that must avoid obstacles such as decision diamonds or sibling branches.
    /// </summary>
    ObstacleAvoidance
}
