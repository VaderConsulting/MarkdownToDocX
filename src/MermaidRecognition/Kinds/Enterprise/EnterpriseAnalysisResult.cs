namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Combined parse and validation outcome.
/// </summary>
public sealed class EnterpriseAnalysisResult
{
    private readonly EnterpriseDocument _Document;

    private readonly List<EnterpriseDiagnostic> _Diagnostics;

    /// <summary>
    /// Creates an analysis result.
    /// </summary>
    /// <param name="Document">Parsed document.</param>
    /// <param name="Diagnostics">Parser and validator diagnostics.</param>
    public EnterpriseAnalysisResult(EnterpriseDocument Document, List<EnterpriseDiagnostic> Diagnostics)
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

    /// <summary>Gets all diagnostics.</summary>
    public IReadOnlyList<EnterpriseDiagnostic> Diagnostics
    {
        get
        {
            return _Diagnostics;
        }
    }

    /// <summary>Gets error diagnostics only.</summary>
    public IReadOnlyList<EnterpriseDiagnostic> Errors
    {
        get
        {
            List<EnterpriseDiagnostic> Errors = new List<EnterpriseDiagnostic>();
            foreach (EnterpriseDiagnostic Diagnostic in _Diagnostics)
            {
                if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
                {
                    Errors.Add(Diagnostic);
                }
            }

            return Errors;
        }
    }

    /// <summary>Gets whether any error diagnostic exists.</summary>
    public bool HasErrors
    {
        get
        {
            return Errors.Count > 0;
        }
    }
}
