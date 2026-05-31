namespace MDConverter.Options;

/// <summary>
/// Options for Mermaid diagram rendering in DOCX output.
/// </summary>
public sealed class MermaidOptions
{
    private bool _UseFlowchartRecognitionParser;

    internal bool HasUseFlowchartRecognitionParser { get; private set; }

    /// <summary>
    /// When true, flowchart/graph fenced blocks use <c>MermaidRecognition</c> parse output
    /// mapped to Naiad via <c>ContractToFlowchartModel</c> before SVG render. Default is false (legacy Naiad parser).
    /// </summary>
    public bool UseFlowchartRecognitionParser
    {
        get
        {
            return _UseFlowchartRecognitionParser;
        }
        set
        {
            _UseFlowchartRecognitionParser = value;
            HasUseFlowchartRecognitionParser = true;
        }
    }

    internal void SetResolvedValue(bool UseFlowchartRecognitionParser)
    {
        _UseFlowchartRecognitionParser = UseFlowchartRecognitionParser;
    }
}
