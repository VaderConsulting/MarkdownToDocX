namespace MermaidRecognition.Contract;

/// <summary>
/// Diagram-level accessibility metadata (<c>accTitle</c>, <c>accDescr</c>) (spec §3.15).
/// </summary>
public sealed class DiagramAccessibilityContract
{
    private string? _Title;

    public string? Title
    {
        get
        {
            return _Title;
        }
        set
        {
            _Title = value;
        }
    }

    private string? _Description;

    public string? Description
    {
        get
        {
            return _Description;
        }
        set
        {
            _Description = value;
        }
    }
}
