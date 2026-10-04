namespace MermaidRecognition.Contract;

/// <summary>
/// Audit trail for grouped or chained edge expansion (Mermaid creation order).
/// </summary>
public sealed class EdgeExpansionTrace(
    MermaidSourceSpan StatementSpan,
    int LinkOrdinalInStatement,
    int StartOrdinal,
    int EndOrdinal,
    IReadOnlyList<string> StartSet,
    IReadOnlyList<string> EndSet,
    string RawLinkToken)
{
    private readonly MermaidSourceSpan _StatementSpan = StatementSpan;

    public MermaidSourceSpan StatementSpan => _StatementSpan;

    private readonly int _LinkOrdinalInStatement = LinkOrdinalInStatement;

    public int LinkOrdinalInStatement => _LinkOrdinalInStatement;

    private readonly int _StartOrdinal = StartOrdinal;

    public int StartOrdinal => _StartOrdinal;

    private readonly int _EndOrdinal = EndOrdinal;

    public int EndOrdinal => _EndOrdinal;

    private readonly IReadOnlyList<string> _StartSet = StartSet;

    public IReadOnlyList<string> StartSet => _StartSet;

    private readonly IReadOnlyList<string> _EndSet = EndSet;

    public IReadOnlyList<string> EndSet => _EndSet;

    private readonly string _RawLinkToken = RawLinkToken;

    public string RawLinkToken => _RawLinkToken;
}

