namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Outcome of parsing an enterprise diagram block.
/// </summary>
public sealed class EnterpriseParseResult
{
    private readonly EnterpriseDocument _Document;

    private readonly List<EnterpriseDiagnostic> _Diagnostics;

    /// <summary>
    /// Creates a parse result.
    /// </summary>
    /// <param name="Document">Parsed document, possibly incomplete when errors exist.</param>
    /// <param name="Diagnostics">Parser diagnostics.</param>
    public EnterpriseParseResult(EnterpriseDocument Document, List<EnterpriseDiagnostic> Diagnostics)
    {
        _Document = Document ?? new EnterpriseDocument();
        _Diagnostics = Diagnostics ?? new List<EnterpriseDiagnostic>();
    }

    /// <summary>Gets the parsed document.</summary>
    public EnterpriseDocument Document
    {
        get
        {
            return _Document;
        }
    }

    /// <summary>Gets parser diagnostics.</summary>
    public IReadOnlyList<EnterpriseDiagnostic> Diagnostics
    {
        get
        {
            return _Diagnostics;
        }
    }

    /// <summary>Gets whether any error diagnostic was produced.</summary>
    public bool HasErrors
    {
        get
        {
            foreach (EnterpriseDiagnostic Diagnostic in _Diagnostics)
            {
                if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
