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

    public MermaidSourceSpan StatementSpan => _StatementSpan;

    private readonly int _LinkOrdinalInStatement;

    public int LinkOrdinalInStatement => _LinkOrdinalInStatement;

    private readonly int _StartOrdinal;

    public int StartOrdinal => _StartOrdinal;

    private readonly int _EndOrdinal;

    public int EndOrdinal => _EndOrdinal;

    private readonly IReadOnlyList<string> _StartSet;

    public IReadOnlyList<string> StartSet => _StartSet;

    private readonly IReadOnlyList<string> _EndSet;

    public IReadOnlyList<string> EndSet => _EndSet;

    private readonly string _RawLinkToken;

    public string RawLinkToken => _RawLinkToken;
}

