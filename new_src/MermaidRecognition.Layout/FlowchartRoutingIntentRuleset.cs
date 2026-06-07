using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>
/// Expected routing requirements for torture-test diagrams, loaded from <c>ruleset.json</c>.
/// </summary>
public sealed class FlowchartRoutingIntentRuleset
{
    private string _SchemaVersion = "1.0";

    private List<string> _GlobalInvalidBehaviours = new List<string>();

    private List<string> _GlobalForbiddenBehaviours = new List<string>();

    private List<FlowchartRoutingIntentDiagramExpectation> _Diagrams = new List<FlowchartRoutingIntentDiagramExpectation>();

    private List<FlowchartRoutingIntentDiagramExpectation> _DiagramIntentFixtures =
        new List<FlowchartRoutingIntentDiagramExpectation>();

    /// <summary>
    /// Ruleset schema version.
    /// </summary>
    public string SchemaVersion
    {
        get
        {
            return _SchemaVersion;
        }
        set
        {
            _SchemaVersion = value;
        }
    }

    /// <summary>
    /// Invalid behaviours that apply to every edge unless overridden.
    /// </summary>
    public List<string> GlobalInvalidBehaviours
    {
        get
        {
            return _GlobalInvalidBehaviours;
        }
        set
        {
            _GlobalInvalidBehaviours = value ?? new List<string>();
        }
    }

    /// <summary>
    /// Per-diagram routing expectations (legacy schema).
    /// </summary>
    public List<FlowchartRoutingIntentDiagramExpectation> Diagrams
    {
        get
        {
            return _Diagrams;
        }
        set
        {
            _Diagrams = value ?? new List<FlowchartRoutingIntentDiagramExpectation>();
        }
    }

    /// <summary>
    /// Global forbidden behaviours from the combined ruleset schema.
    /// </summary>
    public List<string> GlobalForbiddenBehaviours
    {
        get
        {
            return _GlobalForbiddenBehaviours;
        }
        set
        {
            _GlobalForbiddenBehaviours = value ?? new List<string>();
        }
    }

    /// <summary>
    /// Per-diagram edge-level intent fixtures from the combined ruleset schema.
    /// </summary>
    public List<FlowchartRoutingIntentDiagramExpectation> DiagramIntentFixtures
    {
        get
        {
            return _DiagramIntentFixtures;
        }
        set
        {
            _DiagramIntentFixtures = value ?? new List<FlowchartRoutingIntentDiagramExpectation>();
        }
    }

    /// <summary>
    /// Returns diagram expectations from either <see cref="Diagrams"/> or <see cref="DiagramIntentFixtures"/>.
    /// </summary>
    public IReadOnlyList<FlowchartRoutingIntentDiagramExpectation> ResolveDiagramExpectations()
    {
        if (_Diagrams.Count > 0)
        {
            return _Diagrams;
        }

        return _DiagramIntentFixtures;
    }

    /// <summary>
    /// Returns global forbidden behaviours from either legacy or combined ruleset fields.
    /// </summary>
    public IReadOnlyList<string> ResolveGlobalForbiddenBehaviours()
    {
        if (_GlobalInvalidBehaviours.Count > 0)
        {
            return _GlobalInvalidBehaviours;
        }

        return _GlobalForbiddenBehaviours;
    }
}

/// <summary>
/// Expected routing requirements for one diagram in a torture-test fixture.
/// </summary>
public sealed class FlowchartRoutingIntentDiagramExpectation
{
    private string _DiagramId = string.Empty;

    private string _Direction = string.Empty;

    private List<FlowchartRoutingIntentEdgeExpectation> _Edges = new List<FlowchartRoutingIntentEdgeExpectation>();

    /// <summary>
    /// Diagram identifier matching the torture-test markdown marker.
    /// </summary>
    public string DiagramId
    {
        get
        {
            return _DiagramId;
        }
        set
        {
            _DiagramId = value;
        }
    }

    /// <summary>
    /// Expected diagram direction.
    /// </summary>
    public string Direction
    {
        get
        {
            return _Direction;
        }
        set
        {
            _Direction = value;
        }
    }

    /// <summary>
    /// Per-edge routing expectations.
    /// </summary>
    public List<FlowchartRoutingIntentEdgeExpectation> Edges
    {
        get
        {
            return _Edges;
        }
        set
        {
            _Edges = value ?? new List<FlowchartRoutingIntentEdgeExpectation>();
        }
    }

    /// <summary>
    /// Optional validation mode metadata from the combined ruleset schema.
    /// </summary>
    public string ValidationMode { get; set; } = string.Empty;

    /// <summary>
    /// Optional fixture role metadata from the combined ruleset schema.
    /// </summary>
    public string FixtureRole { get; set; } = string.Empty;

    /// <summary>
    /// Optional edge count metadata from the combined ruleset schema.
    /// </summary>
    public int? EdgeCount { get; set; }
}

/// <summary>
/// Expected routing requirements for one edge.
/// </summary>
public sealed class FlowchartRoutingIntentEdgeExpectation
{
    private string _EdgeId = string.Empty;

    private string _SourceNodeId = string.Empty;

    private string _TargetNodeId = string.Empty;

    private string _RouteClass = string.Empty;

    private string _SourceBoundaryIntent = string.Empty;

    private string _TargetBoundaryIntent = string.Empty;

    private string _PathShapeIntent = string.Empty;

    private string _LabelPlacementIntent = string.Empty;

    private string _ObstacleAvoidanceIntent = string.Empty;

    private List<string> _ExpectedBehaviour = new List<string>();

    private List<string> _InvalidBehaviours = new List<string>();

    private List<string> _MustNotIncludeInvalidBehaviours = new List<string>();

    /// <summary>
    /// Optional stable edge identifier such as <c>E004</c>.
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
    /// Source node identifier used to match the edge.
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
    /// Target node identifier used to match the edge.
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
    /// Expected route class name.
    /// </summary>
    public string RouteClass
    {
        get
        {
            return _RouteClass;
        }
        set
        {
            _RouteClass = value;
        }
    }

    /// <summary>
    /// Expected source boundary intent name.
    /// </summary>
    public string SourceBoundaryIntent
    {
        get
        {
            return _SourceBoundaryIntent;
        }
        set
        {
            _SourceBoundaryIntent = value;
        }
    }

    /// <summary>
    /// Expected target boundary intent name.
    /// </summary>
    public string TargetBoundaryIntent
    {
        get
        {
            return _TargetBoundaryIntent;
        }
        set
        {
            _TargetBoundaryIntent = value;
        }
    }

    /// <summary>
    /// Expected path shape intent name.
    /// </summary>
    public string PathShapeIntent
    {
        get
        {
            return _PathShapeIntent;
        }
        set
        {
            _PathShapeIntent = value;
        }
    }

    /// <summary>
    /// Expected label placement intent name.
    /// </summary>
    public string LabelPlacementIntent
    {
        get
        {
            return _LabelPlacementIntent;
        }
        set
        {
            _LabelPlacementIntent = value;
        }
    }

    /// <summary>
    /// Expected obstacle avoidance intent name.
    /// </summary>
    public string ObstacleAvoidanceIntent
    {
        get
        {
            return _ObstacleAvoidanceIntent;
        }
        set
        {
            _ObstacleAvoidanceIntent = value;
        }
    }

    /// <summary>
    /// Required expected behaviours for this edge.
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
    /// Required invalid behaviours that must be present in the intent output.
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
    /// Invalid behaviours that must not appear in the intent output for this edge.
    /// </summary>
    public List<string> MustNotIncludeInvalidBehaviours
    {
        get
        {
            return _MustNotIncludeInvalidBehaviours;
        }
        set
        {
            _MustNotIncludeInvalidBehaviours = value ?? new List<string>();
        }
    }
}
