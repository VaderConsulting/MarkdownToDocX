namespace MermaidRecognition.Diagnostics;

/// <summary>
/// Recognised-but-unsupported or parse-time diagnostic for a Mermaid block.
/// </summary>
public sealed class MermaidDiagnostic(
    MermaidDiagnosticSeverity Severity,
    string Code,
    string Message,
    MermaidSourceSpan? SourceSpan,
    string? AffectedId)
{
    private readonly MermaidDiagnosticSeverity _Severity = Severity;

    public MermaidDiagnosticSeverity Severity => _Severity;

    private readonly string _Code = Code;

    public string Code => _Code;

    private readonly string _Message = Message;

    public string Message => _Message;

    private readonly MermaidSourceSpan? _SourceSpan = SourceSpan;

    public MermaidSourceSpan? SourceSpan => _SourceSpan;

    private readonly string? _AffectedId = AffectedId;

    public string? AffectedId => _AffectedId;
}

