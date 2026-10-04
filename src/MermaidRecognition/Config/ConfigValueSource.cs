namespace MermaidRecognition.Config;

/// <summary>
/// Provenance of an entry in <see cref="DiagramConfigContract.EffectiveValues"/>.
/// </summary>
public enum ConfigValueSource
{
    Default,
    DirectiveOverride,
    ThemeResolved,
    Computed
}

