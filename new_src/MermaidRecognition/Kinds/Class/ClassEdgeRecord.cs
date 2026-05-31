namespace MermaidRecognition.Class;

/// <summary>
/// Parse-time class diagram relation (diagram database record).
/// </summary>
public sealed class ClassEdgeRecord
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

    private string _RelationToken = string.Empty;

    public string RelationToken
    {
        get
        {
            return _RelationToken;
        }
        set
        {
            _RelationToken = value;
        }
    }

    private string? _SourceCardinality;

    public string? SourceCardinality
    {
        get
        {
            return _SourceCardinality;
        }
        set
        {
            _SourceCardinality = value;
        }
    }

    private string? _TargetCardinality;

    public string? TargetCardinality
    {
        get
        {
            return _TargetCardinality;
        }
        set
        {
            _TargetCardinality = value;
        }
    }

    private string? _RelationLabel;

    public string? RelationLabel
    {
        get
        {
            return _RelationLabel;
        }
        set
        {
            _RelationLabel = value;
        }
    }
}
