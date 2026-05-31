namespace MermaidRecognition;

/// <summary>
/// Parses one detected Mermaid diagram kind into a <see cref="DiagramContract"/>.
/// </summary>
public interface IKindRecognizer
{
    /// <summary>
    /// Parses full source (including init blocks) for the detected kind.
    /// </summary>
    DiagramContract Parse(string Source, DiagramDetectionResult Detection);
}
