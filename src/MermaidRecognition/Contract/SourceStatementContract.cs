namespace MermaidRecognition.Contract;

/// <summary>
/// One classified source line for catalogue and L1 gate evidence (§7.4.3).
/// </summary>
public sealed class SourceStatementContract
{
    private int _LineNumber;

    public int LineNumber
    {
        get => _LineNumber; set => _LineNumber = value;
    }

    private string _LineTypeId = string.Empty;

    public string LineTypeId
    {
        get => _LineTypeId; set => _LineTypeId = value;
    }

    private string _RawLine = string.Empty;

    public string RawLine
    {
        get => _RawLine; set => _RawLine = value;
    }
}
