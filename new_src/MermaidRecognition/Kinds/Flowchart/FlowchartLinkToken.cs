namespace MermaidRecognition.Flowchart;

/// <summary>
/// Parsed link arrow between two node sets (one segment in a chained statement).
/// </summary>
public sealed class FlowchartLinkToken
{
    private string? _UserDefinedEdgeId;

    public string? UserDefinedEdgeId
    {
        get
        {
            return _UserDefinedEdgeId;
        }
        set
        {
            _UserDefinedEdgeId = value;
        }
    }

    private int _MinLength = 1;

    public int MinLength
    {
        get
        {
            return _MinLength;
        }
        set
        {
            _MinLength = value;
        }
    }

    private string? _Label;

    public string? Label
    {
        get
        {
            return _Label;
        }
        set
        {
            _Label = value;
        }
    }

    private string _Stroke = "normal";

    public string Stroke
    {
        get
        {
            return _Stroke;
        }
        set
        {
            _Stroke = value;
        }
    }

    private EdgeEndpointMarker _MarkerStart = EdgeEndpointMarker.None;

    public EdgeEndpointMarker MarkerStart
    {
        get
        {
            return _MarkerStart;
        }
        set
        {
            _MarkerStart = value;
        }
    }

    private EdgeEndpointMarker _MarkerEnd = EdgeEndpointMarker.None;

    public EdgeEndpointMarker MarkerEnd
    {
        get
        {
            return _MarkerEnd;
        }
        set
        {
            _MarkerEnd = value;
        }
    }
}
