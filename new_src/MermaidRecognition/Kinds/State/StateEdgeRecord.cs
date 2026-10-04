namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Parse-time state transition edge record.
/// </summary>
public sealed class StateEdgeRecord
{
    private string _SourceId = string.Empty;

    public string SourceId
    {
        get => _SourceId; set => _SourceId = value;
    }

    private string _TargetId = string.Empty;

    public string TargetId
    {
        get => _TargetId; set => _TargetId = value;
    }

    private string? _Label;

    public string? Label
    {
        get => _Label; set => _Label = value;
    }

    private int _CreationIndex;

    public int CreationIndex
    {
        get => _CreationIndex; set => _CreationIndex = value;
    }
}
