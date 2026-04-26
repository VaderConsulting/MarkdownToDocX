namespace MermaidSharp.Diagrams.Class;

public class ClassRelationship
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public RelationshipType Type { get; set; } = RelationshipType.Association;
    public string? Label { get; set; }
    public string? FromCardinality { get; set; }
    public string? ToCardinality { get; set; }

    /// <summary>
    /// For <c>&lt;|--</c> / <c>&lt;|..</c> the hollow triangle sits on the <see cref="FromId"/> side; for <c>--|&gt;</c> / <c>..|&gt;</c> it sits on the <see cref="ToId"/> side. Ignored for other relationship types.
    /// </summary>
    public bool InheritanceMarkerOnFromEndpoint { get; init; }
}