namespace MermaidRecognition.Sequence;

/// <summary>
/// Parse-time sequence diagram message (diagram database record).
/// </summary>
public sealed class SequenceEdgeRecord
{
    private int _CreationIndex;

    public int CreationIndex
    {
        get
        {
            return _CreationIndex;
        }
        set
        {
            _CreationIndex = value;
        }
    }

    private string _SourceId = string.Empty;

    public string SourceId
    {
        get
        {
            return _SourceId;
        }
        set
        {
            _SourceId = value;
        }
    }

    private string _TargetId = string.Empty;

    public string TargetId
    {
        get
        {
            return _TargetId;
        }
        set
        {
            _TargetId = value;
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

    private string _ArrowToken = string.Empty;

    public string ArrowToken
    {
        get
        {
            return _ArrowToken;
        }
        set
        {
            _ArrowToken = value;
        }
    }

    private string? _ActivateTargetId;

    public string? ActivateTargetId
    {
        get
        {
            return _ActivateTargetId;
        }
        set
        {
            _ActivateTargetId = value;
        }
    }

    private string? _DeactivateSourceId;

    public string? DeactivateSourceId
    {
        get
        {
            return _DeactivateSourceId;
        }
        set
        {
            _DeactivateSourceId = value;
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
}
