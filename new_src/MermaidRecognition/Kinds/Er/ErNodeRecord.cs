using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Kinds.Er;

/// <summary>
/// Parse-time ER entity node with attribute declarations.
/// </summary>
public sealed class ErNodeRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get => _Id; set => _Id = value;
    }

    private string _Label = string.Empty;

    public string Label
    {
        get => _Label; set => _Label = value;
    }

    private readonly List<MermaidDeclaration> _Declarations = [];

    public List<MermaidDeclaration> DeclarationsMutable => _Declarations;

    private readonly List<string> _Classes = [];

    public List<string> ClassesMutable => _Classes;

    private readonly RecognitionStyle _InlineStyle = new RecognitionStyle();

    public RecognitionStyle InlineStyle => _InlineStyle;
}
