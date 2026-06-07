using System.Globalization;

namespace MermaidRecognition.Layout;

/// <summary>
/// One explainable routing defect for a single edge or merge target.
/// </summary>
public sealed class FlowchartEdgeRouteDiagnosticFinding
{
    /// <summary>
    /// Creates a diagnostic finding with layout-space port coordinates.
    /// </summary>
    public FlowchartEdgeRouteDiagnosticFinding(
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
        this.Kind = Kind;
        this.EdgeKey = EdgeKey;
        this.SourceNodeId = SourceNodeId;
        this.TargetNodeId = TargetNodeId;
        this.SemanticRouteClass = SemanticRouteClass;
        this.RouteKind = RouteKind;
        this.SourcePortX = SourcePortX;
        this.SourcePortY = SourcePortY;
        this.TargetPortX = TargetPortX;
        this.TargetPortY = TargetPortY;
        this.Reason = Reason;
    }

    /// <summary>
    /// The defect category.
    /// </summary>
    public FlowchartEdgeRouteDiagnosticKind Kind { get; }

    /// <summary>
    /// Human-readable edge identifier, typically <c>source-&gt;target</c>.
    /// </summary>
    public string EdgeKey { get; }

    /// <summary>
    /// Source node identifier from the diagram contract.
    /// </summary>
    public string SourceNodeId { get; }

    /// <summary>
    /// Target node identifier from the diagram contract.
    /// </summary>
    public string TargetNodeId { get; }

    /// <summary>
    /// Semantic route class assigned before path generation.
    /// </summary>
    public FlowchartEdgeSemanticRouteClass SemanticRouteClass { get; }

    /// <summary>
    /// Resolved route kind used for rendering.
    /// </summary>
    public FlowchartEdgeRouteKind RouteKind { get; }

    /// <summary>
    /// Layout-space source attach X coordinate.
    /// </summary>
    public double SourcePortX { get; }

    /// <summary>
    /// Layout-space source attach Y coordinate.
    /// </summary>
    public double SourcePortY { get; }

    /// <summary>
    /// Layout-space target attach X coordinate.
    /// </summary>
    public double TargetPortX { get; }

    /// <summary>
    /// Layout-space target attach Y coordinate.
    /// </summary>
    public double TargetPortY { get; }

    /// <summary>
    /// Plain-language explanation of the violated routing rule.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Returns a single-line diagnostic suitable for test output and logs.
    /// </summary>
    public string FormatLine()
    {
        return string.Format(
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
}
