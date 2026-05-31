namespace MermaidRecognition.Sequence;

/// <summary>
/// Parse-time sequence diagram participant (diagram database record).
/// </summary>
public sealed class SequenceNodeRecord
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

    private string _DrawType = "participant";

    public string DrawType
    {
        get
        {
            return _DrawType;
        }
        set
        {
            _DrawType = value;
        }
    }

    private bool? _IsActive;

    public bool? IsActive
    {
        get
        {
            return _IsActive;
        }
        set
        {
            _IsActive = value;
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
