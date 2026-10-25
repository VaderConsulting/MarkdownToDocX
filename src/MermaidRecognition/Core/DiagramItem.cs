namespace MermaidRecognition.Core;

/// <summary>
/// One selectable declaration from a diagram script.
/// The id is the 1-based source line number. Mermaid allows one declaration per line.
/// </summary>
public sealed class DiagramItem
{
    /// <summary>
    /// Creates an item.
    /// </summary>
    /// <param name="Id">1-based source line number. This is the LiveEditor selection id.</param>
    /// <param name="Kind">Node, container, or relationship.</param>
    /// <param name="Name">Syntax name when the kind has one, such as a node id; otherwise empty.</param>
    /// <param name="Label">Display label when known; otherwise empty.</param>
    /// <param name="TypeToken">Kind or shape token when known; otherwise empty.</param>
    /// <param name="SourceName">Relationship source name when Kind is Relationship; otherwise empty.</param>
    /// <param name="TargetName">Relationship target name when Kind is Relationship; otherwise empty.</param>
    public DiagramItem(
        int Id,
        DiagramItemKind Kind,
        string Name,
        string Label,
        string TypeToken,
        string SourceName,
        string TargetName)
    {
        this.Id = Id;
        this.Kind = Kind;
        this.Name = Name ?? string.Empty;
        this.Label = Label ?? string.Empty;
        this.TypeToken = TypeToken ?? string.Empty;
        this.SourceName = SourceName ?? string.Empty;
        this.TargetName = TargetName ?? string.Empty;
    }

    /// <summary>Gets the 1-based source line number. This is the item id.</summary>
    public int Id { get; }

    /// <summary>Gets whether this is a node, container, or relationship.</summary>
    public DiagramItemKind Kind { get; }

    /// <summary>Gets the syntax name when present.</summary>
    public string Name { get; }

    /// <summary>Gets the display label when present.</summary>
    public string Label { get; }

    /// <summary>Gets the type or shape token when present.</summary>
    public string TypeToken { get; }

    /// <summary>Gets the relationship source name when Kind is Relationship.</summary>
    public string SourceName { get; }

    /// <summary>Gets the relationship target name when Kind is Relationship.</summary>
    public string TargetName { get; }
}
