namespace MermaidRecognition.Contract;

/// <summary>
/// Recognised vertex (spec §4.3).
/// </summary>
public sealed class NodeContract
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

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get => _ParentGroupId; set => _ParentGroupId = value;
    }

    private readonly List<MermaidDeclaration> _Declarations = [];

    public IReadOnlyList<MermaidDeclaration> Declarations => _Declarations;

    public List<MermaidDeclaration> DeclarationsMutable => _Declarations;

    private readonly Dictionary<string, MermaidMetadataValue> _Metadata =
        new Dictionary<string, MermaidMetadataValue>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MermaidMetadataValue> Metadata => _Metadata;

    public Dictionary<string, MermaidMetadataValue> MetadataMutable => _Metadata;

    private readonly List<string> _Classes = [];

    public IReadOnlyList<string> Classes => _Classes;

    public List<string> ClassesMutable => _Classes;

    private readonly RecognitionStyle _InlineStyle = new RecognitionStyle();

    public RecognitionStyle InlineStyle => _InlineStyle;
}
