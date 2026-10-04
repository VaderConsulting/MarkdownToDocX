namespace MermaidRecognition.Contract;

/// <summary>
/// Mermaid <c>click</c> / hyperlink / tooltip interaction on a diagram element.
/// </summary>
public sealed class InteractionDirective(
    string TargetId,
    string? Callback,
    string? Url,
    string? Tooltip,
    string? TargetWindow)
{
    private readonly string _TargetId = TargetId;

    public string TargetId => _TargetId;

    private readonly string? _Callback = Callback;

    public string? Callback => _Callback;

    private readonly string? _Url = Url;

    public string? Url => _Url;

    private readonly string? _Tooltip = Tooltip;

    public string? Tooltip => _Tooltip;

    private readonly string? _TargetWindow = TargetWindow;

    public string? TargetWindow => _TargetWindow;
}

