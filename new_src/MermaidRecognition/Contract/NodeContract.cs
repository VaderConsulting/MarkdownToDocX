namespace MermaidRecognition.Contract;

/// <summary>
/// Recognised vertex (spec §4.3).
/// </summary>
public sealed class NodeContract
{
    private string _Id = string.Empty;

    public string Id
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

    private string? _Shape;

    public string? Shape
    {
        get
        {
            return _Shape;
        }
        set
        {
            _Shape = value;
        }
    }

    private string? _ShapeSource;

    public string? ShapeSource
    {
        get
        {
            return _ShapeSource;
        }
        set
        {
            _ShapeSource = value;
        }
    }

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get
        {
            return _ParentGroupId;
        }
        set
        {
            _ParentGroupId = value;
        }
    }

    private readonly List<MermaidDeclaration> _Declarations = new List<MermaidDeclaration>();

    public IReadOnlyList<MermaidDeclaration> Declarations
    {
        get
        {
            return _Declarations;
        }
    }

    public List<MermaidDeclaration> DeclarationsMutable
    {
        get
        {
            return _Declarations;
        }
    }

    private readonly Dictionary<string, MermaidMetadataValue> _Metadata =
        new Dictionary<string, MermaidMetadataValue>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MermaidMetadataValue> Metadata
    {
        get
        {
            return _Metadata;
        }
    }

    public Dictionary<string, MermaidMetadataValue> MetadataMutable
    {
        get
        {
            return _Metadata;
        }
    }

    private readonly List<string> _Classes = new List<string>();

    public IReadOnlyList<string> Classes
    {
        get
        {
            return _Classes;
        }
    }

    public List<string> ClassesMutable
    {
        get
        {
            return _Classes;
        }
    }

    private readonly RecognitionStyle _InlineStyle = new RecognitionStyle();

    public RecognitionStyle InlineStyle
    {
        get
        {
            return _InlineStyle;
        }
    }
}
