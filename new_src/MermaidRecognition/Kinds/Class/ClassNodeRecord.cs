using MermaidRecognition.Contract;

namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parse-time class diagram vertex (diagram database record).
/// </summary>
public sealed class ClassNodeRecord
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

    private readonly List<MermaidDeclaration> _Declarations = [];

    public List<MermaidDeclaration> DeclarationsMutable => _Declarations;

    private readonly List<string> _Classes = [];

    public List<string> ClassesMutable => _Classes;

    private readonly RecognitionStyle _InlineStyle = new RecognitionStyle();

    public RecognitionStyle InlineStyle => _InlineStyle;

    private string? _ParentNamespaceId;

    public string? ParentNamespaceId
    {
        get => _ParentNamespaceId; set => _ParentNamespaceId = value;
    }
}
