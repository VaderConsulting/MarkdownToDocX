namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Parse-time flowchart vertex (diagram database record).
/// </summary>
public sealed class FlowchartNodeRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get => _Id; set => _Id = value;
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

    private string? _Shape;

    public string? Shape
    {
        get => _Shape; set => _Shape = value;
    }

    private string? _ShapeSource;

    public string? ShapeSource
    {
        get => _ShapeSource; set => _ShapeSource = value;
    }

    private string? _ParentSubgraphId;

    public string? ParentSubgraphId
    {
        get => _ParentSubgraphId; set => _ParentSubgraphId = value;
    }

    private readonly List<MermaidDeclaration> _Declarations = [];

    public List<MermaidDeclaration> DeclarationsMutable => _Declarations;

    private readonly Dictionary<string, MermaidMetadataValue> _Metadata =
        new Dictionary<string, MermaidMetadataValue>(StringComparer.Ordinal);

    public Dictionary<string, MermaidMetadataValue> MetadataMutable => _Metadata;

    private readonly List<string> _Classes = [];

    public List<string> ClassesMutable => _Classes;

    private readonly RecognitionStyle _InlineStyle = new RecognitionStyle();

    public RecognitionStyle InlineStyle => _InlineStyle;
}
