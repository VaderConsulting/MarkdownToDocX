namespace MermaidRecognition.Diagnostics;

/// <summary>
/// Recognised-but-unsupported or parse-time diagnostic for a Mermaid block.
/// </summary>
public sealed class MermaidDiagnostic
{
    public MermaidDiagnostic(
        MermaidDiagnosticSeverity Severity,
        string Code,
        string Message,
        MermaidSourceSpan? SourceSpan,
        string? AffectedId)
    {
        _Severity = Severity;
        _Code = Code;
        _Message = Message;
        _SourceSpan = SourceSpan;
        _AffectedId = AffectedId;
    }

    private readonly MermaidDiagnosticSeverity _Severity;

    public MermaidDiagnosticSeverity Severity => _Severity;

    private readonly string _Code;

    public string Code => _Code;

    private readonly string _Message;

    public string Message => _Message;

    private readonly MermaidSourceSpan? _SourceSpan;

    public MermaidSourceSpan? SourceSpan => _SourceSpan;

    private readonly string? _AffectedId;

    public string? AffectedId => _AffectedId;
}

