using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.LinePattern;

/// <summary>
/// One line-classification rule for L1 catalogue recognition.
/// </summary>
public sealed class KindLinePattern
{
    private string _LineTypeId = string.Empty;

    public string LineTypeId
    {
        get => _LineTypeId; set => _LineTypeId = value;
    }

    private Regex? _Pattern;

    public Regex? Pattern
    {
        get => _Pattern; set => _Pattern = value;
    }
}
