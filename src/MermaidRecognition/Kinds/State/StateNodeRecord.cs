using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Parse-time state node record.
/// </summary>
public sealed class StateNodeRecord
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

    private string? _StateType;

    public string? StateType
    {
        get => _StateType; set => _StateType = value;
    }

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get => _ParentGroupId; set => _ParentGroupId = value;
    }

    private readonly List<string> _Classes = [];

    public List<string> ClassesMutable => _Classes;

    private readonly RecognitionStyle _InlineStyle = new RecognitionStyle();

    public RecognitionStyle InlineStyle => _InlineStyle;
}
