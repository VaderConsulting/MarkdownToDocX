namespace MermaidRecognition.Render;

/// <summary>
/// Structural routing plan for one edge, derived from topology — not label semantics.
/// </summary>
public sealed class FlowchartEdgeRoute
{
    private string _EdgeKey = string.Empty;

    public string EdgeKey
    {
        get
        {
            return _EdgeKey;
        }
        set
        {
            _EdgeKey = value;
        }
    }

    private string _SourceId = string.Empty;

    public string SourceId
    {
        get
        {
            return _SourceId;
        }
        set
        {
            _SourceId = value;
        }
    }

    private string _TargetId = string.Empty;

    public string TargetId
    {
        get
        {
            return _TargetId;
        }
        set
        {
            _TargetId = value;
        }
    }

    private FlowchartRouteKind _Kind = FlowchartRouteKind.SimpleLink;

    public FlowchartRouteKind Kind
    {
        get
        {
            return _Kind;
        }
        set
        {
            _Kind = value;
        }
    }

    private int _SourceBranchIndex;

    /// <summary>
    /// Outgoing branch index at the source (0-based, ordered by creation index).
    /// </summary>
    public int SourceBranchIndex
    {
        get
        {
            return _SourceBranchIndex;
        }
        set
        {
            _SourceBranchIndex = value;
        }
    }

    private int _TargetJoinIndex;

    /// <summary>
    /// Incoming join index at the target (0-based, ordered by creation index).
    /// </summary>
    public int TargetJoinIndex
    {
        get
        {
            return _TargetJoinIndex;
        }
        set
        {
            _TargetJoinIndex = value;
        }
    }
}
