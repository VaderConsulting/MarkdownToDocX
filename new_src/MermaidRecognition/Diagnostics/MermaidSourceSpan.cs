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

    public int StartOffset
    {
        get
        {
            return _StartOffset;
        }
    }

    private readonly int _EndOffset;

    public int EndOffset
    {
        get
        {
            return _EndOffset;
        }
    }

    private readonly int _StartLine;

    public int StartLine
    {
        get
        {
            return _StartLine;
        }
    }

    private readonly int _StartColumn;

    public int StartColumn
    {
        get
        {
            return _StartColumn;
        }
    }

    private readonly int _EndLine;

    public int EndLine
    {
        get
        {
            return _EndLine;
        }
    }

    private readonly int _EndColumn;

    public int EndColumn
    {
        get
        {
            return _EndColumn;
        }
    }
}

