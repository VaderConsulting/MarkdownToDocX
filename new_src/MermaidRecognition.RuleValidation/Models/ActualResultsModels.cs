using System.Collections.Generic;

namespace MermaidRecognition.RuleValidation.Models;

/// <summary>
/// Root <c>actual-results.json</c> document.
/// </summary>
public sealed class ActualResultsDocument
{
    public string SchemaVersion { get; set; } = "2.0.0";

    public string RunId { get; set; } = string.Empty;

    public ActualResultsSource Source { get; set; } = new ActualResultsSource();

    public List<DiagramActualResult> Diagrams { get; set; } = new List<DiagramActualResult>();
}

/// <summary>
/// Source metadata for an actual-results run.
/// </summary>
public sealed class ActualResultsSource
{
    public string? SourceFile { get; set; }

    public string? SourceHashSha256 { get; set; }

    public int DiagramCount { get; set; }

    public int ActiveDiagramCount { get; set; }

    public int RemarkDiagramCount { get; set; }
}

/// <summary>
/// Actual diagnostic facts for one diagram.
/// </summary>
public sealed class DiagramActualResult
{
    public string DiagramId { get; set; } = string.Empty;

    public string Kind { get; set; } = "flowchart";

    public string Direction { get; set; } = string.Empty;

    public bool IsFullyRemarked { get; set; }

    public bool ParserSucceeded { get; set; }

    public bool LayoutSucceeded { get; set; }

    public string? ScenarioTitle { get; set; }

    public Dictionary<string, object> Recognition { get; set; } = new Dictionary<string, object>();

    public Dictionary<string, object> Config { get; set; } = new Dictionary<string, object>();

    public List<NodeActualResult> Nodes { get; set; } = new List<NodeActualResult>();

    public List<EdgeActualResult> Edges { get; set; } = new List<EdgeActualResult>();

    public List<GroupActualResult> Groups { get; set; } = new List<GroupActualResult>();

    public Dictionary<string, object> Layout { get; set; } = new Dictionary<string, object>();

    public Dictionary<string, object> Routing { get; set; } = new Dictionary<string, object>();

    public List<Dictionary<string, object>> Labels { get; set; } = new List<Dictionary<string, object>>();

    public List<Dictionary<string, object>> Markers { get; set; } = new List<Dictionary<string, object>>();

    public List<Dictionary<string, object>> Diagnostics { get; set; } = new List<Dictionary<string, object>>();

    public Dictionary<string, object> Implementation { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Actual node facts for rule evaluation.
/// </summary>
public sealed class NodeActualResult
{
    public string NodeId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string LabelType { get; set; } = "text";

    public string Shape { get; set; } = string.Empty;

    public BoundsActualResult Bounds { get; set; } = new BoundsActualResult();

    public int Rank { get; set; }

    public int Lane { get; set; }

    public string? ParentGroupId { get; set; }

    public bool IsRendered { get; set; }

    public bool IsConnected { get; set; }

    public int DeclarationCount { get; set; } = 1;

    public int InDegree { get; set; }

    public int OutDegree { get; set; }

    public List<string> IncomingEdgeIds { get; set; } = new List<string>();

    public List<string> OutgoingEdgeIds { get; set; } = new List<string>();

    public List<Dictionary<string, object>> NeighbourFacts { get; set; } = new List<Dictionary<string, object>>();

    public Dictionary<string, object> Facts { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Layout bounds for one node.
/// </summary>
public sealed class BoundsActualResult
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public double CentreX { get; set; }

    public double CentreY { get; set; }
}

/// <summary>
/// Actual edge facts for rule evaluation.
/// </summary>
public sealed class EdgeActualResult
{
    public string EdgeId { get; set; } = string.Empty;

    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public int CreationIndex { get; set; }

    public bool IsVisible { get; set; } = true;

    public string Stroke { get; set; } = "Normal";

    public string ArrowStart { get; set; } = "None";

    public string ArrowEnd { get; set; } = "Point";

    public int MinLength { get; set; } = 1;

    public string? Label { get; set; }

    public string? LabelType { get; set; }

    public bool IsBidirectional { get; set; }

    public bool IsPrimaryFlow { get; set; }

    public string RouteClass { get; set; } = "Unknown";

    public string SourcePort { get; set; } = "Unknown";

    public string TargetPort { get; set; } = "Unknown";

    public string PathFamily { get; set; } = "Unknown";

    public PathActualResult Path { get; set; } = new PathActualResult();

    public List<Dictionary<string, object>> CollisionFacts { get; set; } = new List<Dictionary<string, object>>();

    public Dictionary<string, object> LabelFacts { get; set; } = new Dictionary<string, object>();

    public Dictionary<string, object> MarkerFacts { get; set; } = new Dictionary<string, object>();

    public Dictionary<string, object> Facts { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Routed path geometry for one edge.
/// </summary>
public sealed class PathActualResult
{
    public List<PointActualResult> Points { get; set; } = new List<PointActualResult>();

    public List<Dictionary<string, object>> Segments { get; set; } = new List<Dictionary<string, object>>();

    public BoundsActualResult? BoundingBox { get; set; }
}

/// <summary>
/// One layout-space point on an edge path.
/// </summary>
public sealed class PointActualResult
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>
/// Subgraph group facts for one diagram.
/// </summary>
public sealed class GroupActualResult
{
    public string GroupId { get; set; } = string.Empty;

    public bool IsSubgraph { get; set; } = true;

    public int NestingDepth { get; set; } = 1;

    public string? ParentGroupId { get; set; }

    public string? LocalDirection { get; set; }

    public BoundsActualResult Bounds { get; set; } = new BoundsActualResult();

    public List<string> MemberNodeIds { get; set; } = new List<string>();

    public Dictionary<string, object> Facts { get; set; } = new Dictionary<string, object>();
}
