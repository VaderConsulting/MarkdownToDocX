namespace MermaidRecognition.Core;

/// <summary>
/// Multi-kind Mermaid recognition entry point (spec §4.2).
/// </summary>
public static class MermaidRecognizer
{
    /// <summary>
    /// Detects diagram kind and parses source into a <see cref="DiagramContract"/>.
    /// Detection follows vendored <c>diagram-orchestration.ts</c>; behaviour is defined by Mermaid source, not Naiad.
    /// </summary>
    public static DiagramContract Parse(string Source, MermaidDetectorConfig? Config)
    {
        MermaidRemarkAnalyzer.MermaidRemarkAnalysis RemarkAnalysis = MermaidRemarkAnalyzer.Analyze(Source);
        string RecognitionSource = Source;
        if (RemarkAnalysis.Status == MermaidDiagramRemarkStatus.CatalogueRemarked)
        {
            RecognitionSource = RemarkAnalysis.CatalogueUnremarkedSource;
        }

        string DetectionText = RecognitionSourcePreprocessor.PrepareForDetection(RecognitionSource);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(DetectionText, Config);
        if (Detection == null && IsYamlFrontMatterOnlyTrap(Source, DetectionText))
        {
            Detection = new DiagramDetectionResult
            {
                MermaidDetectorId = "---",
                ProgrammeKindId = "yaml-front-matter"
            };
        }

        if (Detection == null)
        {
            DiagramContract Unknown = BuildUnknownDiagramContract(Source);
            Unknown.RemarkStatus = RemarkAnalysis.Status;
            return Unknown;
        }

        IKindRecognizer Recognizer = KindRecognizerRegistry.Get(Detection);
        DiagramContract Contract = Recognizer.Parse(RecognitionSource, Detection);
        if (string.IsNullOrEmpty(Contract.Kind))
        {
            Contract.Kind = Detection.ProgrammeKindId;
        }

        Contract.RemarkStatus = RemarkAnalysis.Status;
        if (RemarkAnalysis.Status == MermaidDiagramRemarkStatus.CatalogueRemarked)
        {
            Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                MermaidDiagnosticSeverity.Info,
                "recognition.catalogue-remarked",
                "Diagram source is catalogue-remarked (every line prefixed with %%); excluded from render gates. Recognition parsed unremarked shadow source.",
                null,
                null));
        }

        return Contract;
    }

    /// <summary>
    /// Parses with default detector configuration.
    /// </summary>
    public static DiagramContract Parse(string Source) => Parse(Source, null);

    static bool IsYamlFrontMatterOnlyTrap(string Source, string DetectionText)
    {
        if (!Source.TrimStart().StartsWith("---", StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(DetectionText);
    }

    static DiagramContract BuildUnknownDiagramContract(string Source)
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = "unknown"
        };
        Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
            MermaidDiagnosticSeverity.Error,
            "recognition.unknown-diagram",
            "No diagram type detected matching vendored Mermaid detectors.",
            null,
            null));

        Dictionary<string, string> InitValues = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(Source);
        foreach (KeyValuePair<string, string> Entry in InitValues)
        {
            Contract.Configuration.RawDirectiveValuesMutable[Entry.Key] = Entry.Value;
        }

        MermaidConfigurationPipeline.BuildEffectiveConfiguration(Contract.Configuration);

        return Contract;
    }
}
