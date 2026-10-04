namespace MDConverter.Options;

/// <summary>
/// Options for Mermaid diagram rendering in DOCX output.
/// </summary>
public sealed class MermaidOptions
{
    private bool _UseFlowchartRecognitionParser;

    private bool _UseNativeSugiyamaRenderer;

    internal bool HasUseFlowchartRecognitionParser { get; private set; }

    internal bool HasUseNativeSugiyamaRenderer { get; private set; }

    /// <summary>
    /// When true, flowchart/graph fenced blocks use <c>MermaidRecognition</c> before SVG render.
    /// Default is true.
    /// </summary>
    public bool UseFlowchartRecognitionParser
    {
        get => _UseFlowchartRecognitionParser;
        set
        {
            _UseFlowchartRecognitionParser = value;
            HasUseFlowchartRecognitionParser = true;
        }
    }

    /// <summary>
    /// When true (default), flowcharts render via native Sugiyama layout with legacy Naiad fallback.
    /// When false, uses legacy Naiad only (recognition contract path when <see cref="UseFlowchartRecognitionParser"/> is true).
    /// </summary>
    public bool UseNativeSugiyamaRenderer
    {
        get => _UseNativeSugiyamaRenderer;
        set
        {
            _UseNativeSugiyamaRenderer = value;
            HasUseNativeSugiyamaRenderer = true;
        }
    }

    internal void SetResolvedValue(bool UseFlowchartRecognitionParser) => _UseFlowchartRecognitionParser = UseFlowchartRecognitionParser;

    internal void SetResolvedNativeSugiyamaValue(bool UseNativeSugiyamaRenderer) => _UseNativeSugiyamaRenderer = UseNativeSugiyamaRenderer;
}
