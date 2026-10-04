namespace MermaidRecognition.Diagnostics;

/// <summary>
/// Source location of a parsed Mermaid construct.
/// </summary>
public sealed class MermaidSourceSpan(
    int StartOffset,
    int EndOffset,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    private readonly int _StartOffset = StartOffset;

    public int StartOffset => _StartOffset;

    private readonly int _EndOffset = EndOffset;

    public int EndOffset => _EndOffset;

    private readonly int _StartLine = StartLine;

    public int StartLine => _StartLine;

    private readonly int _StartColumn = StartColumn;

    public int StartColumn => _StartColumn;

    private readonly int _EndLine = EndLine;

    public int EndLine => _EndLine;

    private readonly int _EndColumn = EndColumn;

    public int EndColumn => _EndColumn;
}

