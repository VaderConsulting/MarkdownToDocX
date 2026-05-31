namespace MermaidRecognition.KindRecognition;

/// <summary>
/// L1 recogniser for Mermaid-only diagram kinds (no Naiad native render path).
/// Emits <c>recognition.unsupported-native</c> on every parse.
/// </summary>
public sealed class MermaidOnlyKindRecognizer : IKindRecognizer
{
    private const string UNSUPPORTED_NATIVE_CODE = "recognition.unsupported-native";

    private readonly LinePatternKindRecognizer _Inner;
    private readonly string _ProgrammeKindId;

    public MermaidOnlyKindRecognizer(
        string ProgrammeKindId,
        string HeaderRegexPattern,
        params KindLinePattern[] ExtraPatterns)
    {
        _ProgrammeKindId = ProgrammeKindId;
        _Inner = KindLinePatternProfiles.CreateDefault(ProgrammeKindId, HeaderRegexPattern, null, ExtraPatterns);
    }

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = _Inner.Parse(Source, Detection);
        if (!Contract.Diagnostics.Any(Diagnostic => Diagnostic.Code == UNSUPPORTED_NATIVE_CODE))
        {
            Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                MermaidDiagnosticSeverity.Warning,
                UNSUPPORTED_NATIVE_CODE,
                "Diagram kind \"" + _ProgrammeKindId + "\" is recognised for audit but has no Naiad native render path.",
                null,
                null));
        }

        return Contract;
    }
}
