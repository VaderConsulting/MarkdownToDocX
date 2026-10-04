namespace MermaidRecognition.Diagnostics;

/// <summary>
/// Source location of a parsed Mermaid construct.
/// </summary>
public sealed class MermaidSourceSpan
{
    public MermaidSourceSpan(
        int StartOffset,
        int EndOffset,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn)
    {
        _StartOffset = StartOffset;
        _EndOffset = EndOffset;
        _StartLine = StartLine;
        _StartColumn = StartColumn;
        _EndLine = EndLine;
        _EndColumn = EndColumn;
    }

    private readonly int _StartOffset;

    public int StartOffset => _StartOffset;

    private readonly int _EndOffset;

    public int EndOffset => _EndOffset;

    private readonly int _StartLine;

    public int StartLine => _StartLine;

    private readonly int _StartColumn;

    public int StartColumn => _StartColumn;

    private readonly int _EndLine;

    public int EndLine => _EndLine;

    private readonly int _EndColumn;

    public int EndColumn => _EndColumn;
}

