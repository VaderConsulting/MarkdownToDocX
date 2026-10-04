namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Parsed state diagram transition from a source line.
/// </summary>
public sealed class StateTransitionParseResult
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
}
