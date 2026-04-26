namespace MermaidSharp.Diagrams.Class;

public class ClassMember
{
    public required string Name { get; init; }
    public string? Type { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Public;
    public bool IsStatic { get; set; }

    /// <summary>True when the source used Mermaid type-first form (for example <c>+String name</c>).</summary>
    public bool MemberDeclaresTypeBeforeName { get; init; }
}