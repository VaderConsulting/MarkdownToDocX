namespace MermaidRecognition.Flowchart;

/// <summary>
/// Parse-time flowchart vertex (diagram database record).
/// </summary>
public sealed class FlowchartNodeRecord
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

    private string? _ParentSubgraphId;

    public string? ParentSubgraphId
    {
        get
        {
            return _ParentSubgraphId;
        }
        set
        {
            _ParentSubgraphId = value;
        }
    }

    private readonly List<MermaidDeclaration> _Declarations = new List<MermaidDeclaration>();

    public List<MermaidDeclaration> DeclarationsMutable
    {
        get
        {
            return _Declarations;
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

    private readonly List<string> _Classes = new List<string>();

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
