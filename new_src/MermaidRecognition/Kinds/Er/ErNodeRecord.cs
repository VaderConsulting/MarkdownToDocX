using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Er;

/// <summary>
/// Parse-time ER entity node with attribute declarations.
/// </summary>
public sealed class ErNodeRecord
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

    private string _Label = string.Empty;

    public string Label
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
}
