namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parse-time class diagram relation (diagram database record).
/// </summary>
public sealed class ClassEdgeRecord
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

    private string _RelationToken = string.Empty;

    public string RelationToken
    {
        get => _RelationToken; set => _RelationToken = value;
    }

    private string? _SourceCardinality;

    public string? SourceCardinality
    {
        get => _SourceCardinality; set => _SourceCardinality = value;
    }

    private string? _TargetCardinality;

    public string? TargetCardinality
    {
        get => _TargetCardinality; set => _TargetCardinality = value;
    }

    private string? _RelationLabel;

    public string? RelationLabel
    {
        get => _RelationLabel; set => _RelationLabel = value;
    }
}
