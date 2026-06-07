using System.Collections.Generic;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Routing intent for one edge, serialisable for automated validation and debugging.
/// </summary>
public sealed class FlowchartEdgeRoutingIntentRecord
{
    private string _EdgeId = string.Empty;

    private string _SourceNodeId = string.Empty;

    private string _TargetNodeId = string.Empty;

    private List<string> _ExpectedBehaviour = new List<string>();

    private List<string> _InvalidBehaviours = new List<string>();

    /// <summary>
    /// Stable edge identifier, typically <c>E001</c> or <c>source-&gt;target</c>.
    /// </summary>
    public string EdgeId
    {
        get
        {
            return _EdgeId;
        }
        set
        {
            _EdgeId = value;
        }
    }

    /// <summary>
    /// Source node identifier from the diagram contract.
    /// </summary>
    public string SourceNodeId
    {
        get
        {
            return _SourceNodeId;
        }
        set
        {
            _SourceNodeId = value;
        }
    }

    /// <summary>
    /// Target node identifier from the diagram contract.
    /// </summary>
    public string TargetNodeId
    {
        get
        {
            return _TargetNodeId;
        }
        set
        {
            _TargetNodeId = value;
        }
    }

    /// <summary>
    /// Inferred route class for this edge.
    /// </summary>
    public FlowchartRoutingIntentRouteClass RouteClass { get; set; }

    /// <summary>
    /// Semantic route class assigned before path generation.
    /// </summary>
    public FlowchartEdgeSemanticRouteClass SemanticRouteClass { get; set; }

    /// <summary>
    /// Structural topology route kind from <see cref="FlowchartRouter"/>.
    /// </summary>
    public FlowchartRouteKind StructuralRouteKind { get; set; }

    /// <summary>
    /// Resolved geometry route kind from <see cref="FlowchartEdgeRouteResolver"/>.
    /// </summary>
    public FlowchartEdgeRouteKind ResolvedRouteKind { get; set; }

    /// <summary>
    /// Intended source boundary region.
    /// </summary>
    public FlowchartBoundaryRegionIntent SourceBoundaryIntent { get; set; }

    /// <summary>
    /// Intended target boundary region.
    /// </summary>
    public FlowchartBoundaryRegionIntent TargetBoundaryIntent { get; set; }

    /// <summary>
    /// Intended path shape before SVG generation.
    /// </summary>
    public FlowchartPathShapeIntent PathShapeIntent { get; set; }

    /// <summary>
    /// Intended label placement strategy.
    /// </summary>
    public FlowchartLabelPlacementIntent LabelPlacementIntent { get; set; }

    /// <summary>
    /// Obstacle-avoidance requirement for this route.
    /// </summary>
    public FlowchartObstacleAvoidanceIntent ObstacleAvoidanceIntent { get; set; }

    /// <summary>
    /// Behaviours the renderer should satisfy for this edge.
    /// </summary>
    public List<string> ExpectedBehaviour
    {
        get
        {
            return _ExpectedBehaviour;
        }
        set
        {
            _ExpectedBehaviour = value ?? new List<string>();
        }
    }

    /// <summary>
    /// Behaviours that are invalid or discouraged for this edge.
    /// </summary>
    public List<string> InvalidBehaviours
    {
        get
        {
            return _InvalidBehaviours;
        }
        set
        {
            _InvalidBehaviours = value ?? new List<string>();
        }
    }

    /// <summary>
    /// Layout-space resolved source port X coordinate, when available.
    /// </summary>
    public double? SourcePortX { get; set; }

    /// <summary>
    /// Layout-space resolved source port Y coordinate, when available.
    /// </summary>
    public double? SourcePortY { get; set; }

    /// <summary>
    /// Layout-space resolved target port X coordinate, when available.
    /// </summary>
    public double? TargetPortX { get; set; }

    /// <summary>
    /// Layout-space resolved target port Y coordinate, when available.
    /// </summary>
    public double? TargetPortY { get; set; }
}
