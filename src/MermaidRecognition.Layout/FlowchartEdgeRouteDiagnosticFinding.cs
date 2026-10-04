using System.Globalization;

namespace MermaidRecognition.Layout;

/// <summary>
/// One explainable routing defect for a single edge or merge target.
/// </summary>
/// <remarks>
/// Creates a diagnostic finding with layout-space port coordinates.
/// </remarks>
public sealed class FlowchartEdgeRouteDiagnosticFinding(
    FlowchartEdgeRouteDiagnosticKind Kind,
    string EdgeKey,
    string SourceNodeId,
    string TargetNodeId,
    FlowchartEdgeSemanticRouteClass SemanticRouteClass,
    FlowchartEdgeRouteKind RouteKind,
    double SourcePortX,
    double SourcePortY,
    double TargetPortX,
    double TargetPortY,
    string Reason)
{

    /// <summary>
    /// The defect category.
    /// </summary>
    public FlowchartEdgeRouteDiagnosticKind Kind { get; } = Kind;

    /// <summary>
    /// Human-readable edge identifier, typically <c>source-&gt;target</c>.
    /// </summary>
    public string EdgeKey { get; } = EdgeKey;

    /// <summary>
    /// Source node identifier from the diagram contract.
    /// </summary>
    public string SourceNodeId { get; } = SourceNodeId;

    /// <summary>
    /// Target node identifier from the diagram contract.
    /// </summary>
    public string TargetNodeId { get; } = TargetNodeId;

    /// <summary>
    /// Semantic route class assigned before path generation.
    /// </summary>
    public FlowchartEdgeSemanticRouteClass SemanticRouteClass { get; } = SemanticRouteClass;

    /// <summary>
    /// Resolved route kind used for rendering.
    /// </summary>
    public FlowchartEdgeRouteKind RouteKind { get; } = RouteKind;

    /// <summary>
    /// Layout-space source attach X coordinate.
    /// </summary>
    public double SourcePortX { get; } = SourcePortX;

    /// <summary>
    /// Layout-space source attach Y coordinate.
    /// </summary>
    public double SourcePortY { get; } = SourcePortY;

    /// <summary>
    /// Layout-space target attach X coordinate.
    /// </summary>
    public double TargetPortX { get; } = TargetPortX;

    /// <summary>
    /// Layout-space target attach Y coordinate.
    /// </summary>
    public double TargetPortY { get; } = TargetPortY;

    /// <summary>
    /// Plain-language explanation of the violated routing rule.
    /// </summary>
    public string Reason { get; } = Reason;

    /// <summary>
    /// Returns a single-line diagnostic suitable for test output and logs.
    /// </summary>
    public string FormatLine() => string.Format(
            CultureInfo.InvariantCulture,
            "[{0}] {1} class={2} kind={3} ports=({4:F1},{5:F1})->({6:F1},{7:F1}) {8}",
            Kind,
            EdgeKey,
            SemanticRouteClass,
            RouteKind,
            SourcePortX,
            SourcePortY,
            TargetPortX,
            TargetPortY,
            Reason);
}
