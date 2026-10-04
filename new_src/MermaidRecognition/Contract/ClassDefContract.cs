namespace MermaidRecognition.Contract;

/// <summary>
/// Mermaid <c>classDef</c> style template (spec §3.12).
/// </summary>
public sealed class ClassDefContract
{
    private string _ClassId = string.Empty;

    public string ClassId
    {
        get => _ClassId; set => _ClassId = value;
    }

    private readonly RecognitionStyle _Style = new RecognitionStyle();

    public RecognitionStyle Style => _Style;
}
