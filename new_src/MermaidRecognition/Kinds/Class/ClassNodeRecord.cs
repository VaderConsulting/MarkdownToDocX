using MermaidRecognition.Contract;

namespace MermaidRecognition.Class;

/// <summary>
/// Parse-time class diagram vertex (diagram database record).
/// </summary>
public sealed class ClassNodeRecord
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

    private readonly List<MermaidDeclaration> _Declarations = new List<MermaidDeclaration>();

    public List<MermaidDeclaration> DeclarationsMutable
    {
        get
        {
            return _Declarations;
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

    private string? _ParentNamespaceId;

    public string? ParentNamespaceId
    {
        get
        {
            return _ParentNamespaceId;
        }
        set
        {
            _ParentNamespaceId = value;
        }
    }
}
