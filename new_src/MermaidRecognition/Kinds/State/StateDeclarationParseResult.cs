namespace MermaidRecognition.State;

/// <summary>
/// Parsed state declaration from a source line.
/// </summary>
public sealed class StateDeclarationParseResult
{
    private string _StateId = string.Empty;

    public string StateId
    {
        get
        {
            return _StateId;
        }
        set
        {
            _StateId = value;
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

    private string? _StateType;

    public string? StateType
    {
        get
        {
            return _StateType;
        }
        set
        {
            _StateType = value;
        }
    }

    private bool _OpensComposite;

    public bool OpensComposite
    {
        get
        {
            return _OpensComposite;
        }
        set
        {
            _OpensComposite = value;
        }
    }
}
