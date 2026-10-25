namespace MermaidRecognition.Core;

/// <summary>
/// Kind of a selectable diagram item produced by <see cref="Parser"/>.
/// </summary>
public enum DiagramItemKind
{
    /// <summary>A node, participant, slice, or other labelled element.</summary>
    Node,

    /// <summary>A group, subgraph, or other container.</summary>
    Container,

    /// <summary>A link, message, or other relationship.</summary>
    Relationship
}
