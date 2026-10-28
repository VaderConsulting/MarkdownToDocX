using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.LinePattern;

/// <summary>
/// Recogniser for YAML front-matter trap sources (detector id <c>---</c>).
/// </summary>
public sealed class YamlFrontMatterKindRecognizer : IKindRecognizer
{
    private const string YAML_FRONT_MATTER_CODE = "recognition.yaml-front-matter";

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = "yaml-front-matter",
            Header = "---"
        };

        MermaidConfigurationPipeline.ParseLeadingInitDirectives(Source, Contract.Configuration);

        string[] Lines = Source.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                continue;
            }

            SourceStatementContract Statement = new SourceStatementContract
            {
                LineNumber = LineIndex + 1,
                LineTypeId = "yaml-front-matter.line",
                RawLine = Line
            };
            Contract.SourceStatementsMutable.Add(Statement);
        }

        Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
            MermaidDiagnosticSeverity.Warning,
            YAML_FRONT_MATTER_CODE,
            "Source begins with YAML front matter (---) without a detectable diagram body after preprocessing.",
            null,
            null));

        Contract.Configuration.BuildEffectiveConfiguration("yaml-front-matter");

        return Contract;
    }
}
