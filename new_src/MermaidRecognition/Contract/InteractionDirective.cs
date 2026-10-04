namespace MermaidRecognition.Contract;

/// <summary>
/// Mermaid <c>click</c> / hyperlink / tooltip interaction on a diagram element.
/// </summary>
public sealed class InteractionDirective
{
    public InteractionDirective(
        string TargetId,
        string? Callback,
        string? Url,
        string? Tooltip,
        string? TargetWindow)
    {
        _TargetId = TargetId;
        _Callback = Callback;
        _Url = Url;
        _Tooltip = Tooltip;
        _TargetWindow = TargetWindow;
    }

    private readonly string _TargetId;

    public string TargetId => _TargetId;

    private readonly string? _Callback;

    public string? Callback => _Callback;

    private readonly string? _Url;

    public string? Url => _Url;

    private readonly string? _Tooltip;

    public string? Tooltip => _Tooltip;

    private readonly string? _TargetWindow;

    public string? TargetWindow => _TargetWindow;
}

