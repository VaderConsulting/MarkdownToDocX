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

    public MermaidDiagnosticSeverity Severity
    {
        get
        {
            return _Severity;
        }
    }

    private readonly string _Code;

    public string Code
    {
        get
        {
            return _Code;
        }
    }

    private readonly string _Message;

    public string Message
    {
        get
        {
            return _Message;
        }
    }

    private readonly MermaidSourceSpan? _SourceSpan;

    public MermaidSourceSpan? SourceSpan
    {
        get
        {
            return _SourceSpan;
        }
    }

    private readonly string? _AffectedId;

    public string? AffectedId
    {
        get
        {
            return _AffectedId;
        }
    }
}

