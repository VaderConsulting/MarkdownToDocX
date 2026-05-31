using MermaidRecognition.Flowchart;

namespace MermaidRecognition.State;

/// <summary>
/// Parse-time state node record.
/// </summary>
public sealed class StateNodeRecord
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
