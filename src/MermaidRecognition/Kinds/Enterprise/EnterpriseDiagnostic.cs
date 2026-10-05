namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// A parser or validator message with a diagram-relative line number.
/// </summary>
public sealed class EnterpriseDiagnostic
{
    private readonly MermaidDiagnosticSeverity _Severity;

    private readonly int _LineNumber;

    private readonly string _Message;

    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="Severity">Error, warning, or info.</param>
    /// <param name="LineNumber">1-based line in the diagram block; 0 when the message is diagram-wide.</param>
    /// <param name="Message">Human-readable text, including the <c>line N:</c> prefix for errors.</param>
    public EnterpriseDiagnostic(MermaidDiagnosticSeverity Severity, int LineNumber, string Message)
    {
        _Severity = Severity;
        _LineNumber = LineNumber;
        _Message = Message ?? string.Empty;
    }

    /// <summary>Gets the severity.</summary>
    public MermaidDiagnosticSeverity Severity
    {
        get
        {
            return _Severity;
        }
    }

    /// <summary>Gets the 1-based line number.</summary>
    public int LineNumber
    {
        get
        {
            return _LineNumber;
        }
    }

    /// <summary>Gets the message text.</summary>
    public string Message
    {
        get
        {
            return _Message;
        }
    }
}
