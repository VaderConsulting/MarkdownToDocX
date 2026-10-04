namespace MDConverter.Pipeline;

/// <summary>
/// Accessibility text for an embedded Mermaid diagram image in DOCX (Mermaid <c>accTitle</c> / <c>accDescr</c>).
/// </summary>
public readonly struct MermaidImageAccessibility
{
    /// <summary>
    /// Default alt text when the diagram declares no accessibility metadata.
    /// </summary>
    public static MermaidImageAccessibility Default => new MermaidImageAccessibility(null, null);

    /// <summary>
    /// Creates accessibility metadata for a diagram image.
    /// </summary>
    /// <param name="Title">Mermaid <c>accTitle</c> (Word picture title).</param>
    /// <param name="Description">Mermaid <c>accDescr</c> (Word alt text description).</param>
    public MermaidImageAccessibility(string? Title, string? Description)
    {
        _Title = Title;
        _Description = Description;
    }

    private readonly string? _Title;

    /// <summary>
    /// Short accessible title (<c>accTitle</c>).
    /// </summary>
    public string? Title => _Title;

    private readonly string? _Description;

    /// <summary>
    /// Longer accessible description (<c>accDescr</c>).
    /// </summary>
    public string? Description => _Description;

    /// <summary>
    /// Word drawing description / alt text: <c>accDescr</c>, then <c>accTitle</c>, then a generic fallback.
    /// </summary>
    public string ResolveEmbeddedDescription()
    {
        if (!string.IsNullOrWhiteSpace(_Description))
        {
            return _Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_Title))
        {
            return _Title.Trim();
        }

        return "Mermaid diagram";
    }

    /// <summary>
    /// Word picture title from <c>accTitle</c>, or null when unset.
    /// </summary>
    public string? ResolveEmbeddedTitle()
    {
        if (string.IsNullOrWhiteSpace(_Title))
        {
            return null;
        }

        return _Title.Trim();
    }

    /// <summary>
    /// Returns true when either title or description is present.
    /// </summary>
    public bool HasAccessibilityMetadata() => !string.IsNullOrWhiteSpace(_Title) || !string.IsNullOrWhiteSpace(_Description);
}
