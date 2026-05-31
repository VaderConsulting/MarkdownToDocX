namespace MermaidRecognition.Flowchart;

/// <summary>
/// Parse-time flowchart edge after expansion (diagram database record).
/// </summary>
public sealed class FlowchartEdgeRecord
{
    private string? _Id;

    public string? Id
    {
        get
        {
            return _Id;
        }
        set
        {
            _Id = value;
        }
    }

    private bool _IsUserDefinedId;

    public bool IsUserDefinedId
    {
        get
        {
            return _IsUserDefinedId;
        }
        set
        {
            _IsUserDefinedId = value;
        }
    }

    private int _CreationIndex;

    public int CreationIndex
    {
        get
        {
            return _CreationIndex;
        }
        set
        {
            _CreationIndex = value;
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

    private MermaidLabelType _LabelType = MermaidLabelType.Text;

    public MermaidLabelType LabelType
    {
        get
        {
            return _LabelType;
        }
        set
        {
            _LabelType = value;
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

    private EdgeExpansionTrace? _ExpansionTrace;

    public EdgeExpansionTrace? ExpansionTrace
    {
        get
        {
            return _ExpansionTrace;
        }
        set
        {
            _ExpansionTrace = value;
        }
    }

    private readonly List<string> _Classes = new List<string>();

    public List<string> ClassesMutable
    {
        get
        {
            return _Classes;
        }
    }

    private readonly Dictionary<string, MermaidMetadataValue> _Metadata =
        new Dictionary<string, MermaidMetadataValue>(StringComparer.Ordinal);

    public Dictionary<string, MermaidMetadataValue> MetadataMutable
    {
        get
        {
            return _Metadata;
        }
    }
}
