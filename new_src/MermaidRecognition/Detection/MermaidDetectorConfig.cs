namespace MermaidRecognition.Detection;

/// <summary>
/// Optional Mermaid config values that affect diagram detection (vendored detector behaviour).
/// </summary>
public sealed class MermaidDetectorConfig
{
    private string? _FlowchartDefaultRenderer;

    /// <summary>
    /// Gets or sets <c>flowchart.defaultRenderer</c> (for example <c>elk</c>, <c>dagre-wrapper</c>).
    /// </summary>
    public string? FlowchartDefaultRenderer
    {
        get => _FlowchartDefaultRenderer; set => _FlowchartDefaultRenderer = value;
    }

    private string? _StateDefaultRenderer;

    /// <summary>
    /// Gets or sets <c>state.defaultRenderer</c> (for example <c>dagre-wrapper</c>).
    /// </summary>
    public string? StateDefaultRenderer
    {
        get => _StateDefaultRenderer; set => _StateDefaultRenderer = value;
    }

    private string? _ClassDefaultRenderer;

    /// <summary>
    /// Gets or sets <c>class.defaultRenderer</c> (for example <c>dagre-wrapper</c>).
    /// </summary>
    public string? ClassDefaultRenderer
    {
        get => _ClassDefaultRenderer; set => _ClassDefaultRenderer = value;
    }
}
