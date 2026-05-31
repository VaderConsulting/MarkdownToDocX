namespace MermaidRecognition.Er;

/// <summary>
/// Parsed ER relationship from a source line.
/// </summary>
public sealed class ErRelationshipParseResult
{
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

    private string _LeftMarker = string.Empty;

    public string LeftMarker
    {
        get
        {
            return _LeftMarker;
        }
        set
        {
            _LeftMarker = value;
        }
    }

    private string _Link = string.Empty;

    public string Link
    {
        get
        {
            return _Link;
        }
        set
        {
            _Link = value;
        }
    }

    private string _RightMarker = string.Empty;

    public string RightMarker
    {
        get
        {
            return _RightMarker;
        }
        set
        {
            _RightMarker = value;
        }
    }

    private string? _RoleLabel;

    public string? RoleLabel
    {
        get
        {
            return _RoleLabel;
        }
        set
        {
            _RoleLabel = value;
        }
    }
}
