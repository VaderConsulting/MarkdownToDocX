namespace MermaidRecognition.Flowchart;

/// <summary>
/// Adapts <see cref="FlowchartRecognizer"/> to <see cref="IKindRecognizer"/> for <see cref="MermaidRecognizer"/>.
/// </summary>
public sealed class FlowchartKindRecognizer : IKindRecognizer
{
    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(Source);
        Contract.Kind = Detection.ProgrammeKindId;
        return Contract;
    }
}
