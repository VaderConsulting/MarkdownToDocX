namespace MermaidRecognition.Contract;

/// <summary>
/// Recognised directed link (spec §4.4).
/// </summary>
public sealed class EdgeContract
{
    private string? _Id;

    public string? Id
    {
        get => _Id; set => _Id = value;
    }

    private bool _IsUserDefinedId;

    public bool IsUserDefinedId
    {
        get => _IsUserDefinedId; set => _IsUserDefinedId = value;
    }

    private string _SourceId = string.Empty;

    public string SourceId
    {
        get => _SourceId; set => _SourceId = value;
    }

    private string _TargetId = string.Empty;

    public string TargetId
    {
        get => _TargetId; set => _TargetId = value;
    }

    private EdgeEndpointMarker _MarkerStart = EdgeEndpointMarker.None;

    public EdgeEndpointMarker MarkerStart
    {
        get => _MarkerStart; set => _MarkerStart = value;
    }

    private EdgeEndpointMarker _MarkerEnd = EdgeEndpointMarker.None;

    public EdgeEndpointMarker MarkerEnd
    {
        get => _MarkerEnd; set => _MarkerEnd = value;
    }

    private string? _Stroke;

    public string? Stroke
    {
        get => _Stroke; set => _Stroke = value;
    }

    private int _MinLength = 1;

    public int MinLength
    {
        get => _MinLength; set => _MinLength = value;
    }

    private string? _Label;

    public string? Label
    {
        get => _Label; set => _Label = value;
    }

    private MermaidLabelType _LabelType = MermaidLabelType.Text;

    public MermaidLabelType LabelType
    {
        get => _LabelType; set => _LabelType = value;
    }

    private int _CreationIndex = -1;

    public int CreationIndex
    {
        get => _CreationIndex; set => _CreationIndex = value;
    }

    private EdgeExpansionTrace? _ExpansionTrace;

    public EdgeExpansionTrace? ExpansionTrace
    {
        get => _ExpansionTrace; set => _ExpansionTrace = value;
    }

    private MermaidSourceSpan? _SourceSpan;

    public MermaidSourceSpan? SourceSpan
    {
        get => _SourceSpan; set => _SourceSpan = value;
    }

    /// <summary>Optional explicit endpoint and track directions requested by the source diagram.</summary>
    public RoutingHintContract? RoutingHint { get; set; }

    private readonly List<string> _Classes = [];

    public IReadOnlyList<string> Classes => _Classes;

    public List<string> ClassesMutable => _Classes;

    private string? _Curve;

    public string? Curve
    {
        get => _Curve; set => _Curve = value;
    }

    private readonly Dictionary<string, MermaidMetadataValue> _Metadata =
        new Dictionary<string, MermaidMetadataValue>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MermaidMetadataValue> Metadata => _Metadata;

    public Dictionary<string, MermaidMetadataValue> MetadataMutable => _Metadata;
}
