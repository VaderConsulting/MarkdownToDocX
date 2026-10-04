namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parse-time sequence diagram message (diagram database record).
/// </summary>
public sealed class SequenceEdgeRecord
{
    private int _CreationIndex;

    public int CreationIndex
    {
        get => _CreationIndex; set => _CreationIndex = value;
    }

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

    private string _ArrowToken = string.Empty;

    public string ArrowToken
    {
        get => _ArrowToken; set => _ArrowToken = value;
    }

    private string? _ActivateTargetId;

    public string? ActivateTargetId
    {
        get => _ActivateTargetId; set => _ActivateTargetId = value;
    }

    private string? _DeactivateSourceId;

    public string? DeactivateSourceId
    {
        get => _DeactivateSourceId; set => _DeactivateSourceId = value;
    }

    private string? _ParentGroupId;

    public string? ParentGroupId
    {
        get => _ParentGroupId; set => _ParentGroupId = value;
    }
}
