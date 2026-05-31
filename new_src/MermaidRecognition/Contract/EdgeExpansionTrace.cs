namespace MermaidRecognition.Contract;

/// <summary>
/// Audit trail for grouped or chained edge expansion (Mermaid creation order).
/// </summary>
public sealed class EdgeExpansionTrace
{
    public EdgeExpansionTrace(
        MermaidSourceSpan StatementSpan,
        int LinkOrdinalInStatement,
        int StartOrdinal,
        int EndOrdinal,
        IReadOnlyList<string> StartSet,
        IReadOnlyList<string> EndSet,
        string RawLinkToken)
    {
        _StatementSpan = StatementSpan;
        _LinkOrdinalInStatement = LinkOrdinalInStatement;
        _StartOrdinal = StartOrdinal;
        _EndOrdinal = EndOrdinal;
        _StartSet = StartSet;
        _EndSet = EndSet;
        _RawLinkToken = RawLinkToken;
    }

    private readonly MermaidSourceSpan _StatementSpan;

    public MermaidSourceSpan StatementSpan
    {
        get
        {
            return _StatementSpan;
        }
    }

    private readonly int _LinkOrdinalInStatement;

    public int LinkOrdinalInStatement
    {
        get
        {
            return _LinkOrdinalInStatement;
        }
    }

    private readonly int _StartOrdinal;

    public int StartOrdinal
    {
        get
        {
            return _StartOrdinal;
        }
    }

    private readonly int _EndOrdinal;

    public int EndOrdinal
    {
        get
        {
            return _EndOrdinal;
        }
    }

    private readonly IReadOnlyList<string> _StartSet;

    public IReadOnlyList<string> StartSet
    {
        get
        {
            return _StartSet;
        }
    }

    private readonly IReadOnlyList<string> _EndSet;

    public IReadOnlyList<string> EndSet
    {
        get
        {
            return _EndSet;
        }
    }

    private readonly string _RawLinkToken;

    public string RawLinkToken
    {
        get
        {
            return _RawLinkToken;
        }
    }
}

