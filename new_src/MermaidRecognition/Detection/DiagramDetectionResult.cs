namespace MermaidRecognition.Detection;

/// <summary>
/// Result of diagram kind detection (vendored detector id and normalised programme kind).
/// </summary>
public sealed class DiagramDetectionResult
{
    private string _MermaidDetectorId = string.Empty;

    /// <summary>
    /// Gets or sets the Mermaid detector registry id (for example <c>flowchart-v2</c>, <c>sequence</c>).
    /// </summary>
    public string MermaidDetectorId
    {
        get
        {
            return _MermaidDetectorId;
        }
        set
        {
            _MermaidDetectorId = value;
        }
    }

    private string _ProgrammeKindId = string.Empty;

    /// <summary>
    /// Gets or sets the normalised Stage 11.A programme kind id (for example <c>flowchart</c>, <c>sequence</c>).
    /// </summary>
    public string ProgrammeKindId
    {
        get
        {
            return _ProgrammeKindId;
        }
        set
        {
            _ProgrammeKindId = value;
        }
    }
}
