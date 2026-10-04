namespace MermaidRecognition.Tests.Gates;

/// <summary>
/// Minimal smoke samples aligned with Naiad test-renders and vendored Mermaid headers.
/// </summary>
public static class L1SmokeSamples
{
    public static IEnumerable<object[]> ProgrammeKindAndSource()
    {
        yield return new object[] { "flowchart", "flowchart TD\n    A --> B" };
        yield return new object[] { "sequence", "sequenceDiagram\n    Alice->>Bob: Hello" };
        yield return new object[] { "classDiagram", "classDiagram\n    class Animal" };
        yield return new object[] { "stateDiagram", "stateDiagram-v2\n    [*] --> Still" };
        yield return new object[] { "erDiagram", "erDiagram\n    CUSTOMER ||--o{ ORDER : places" };
        yield return new object[] { "gantt", "gantt\n    title A\n    Task :a1, 2024-01-01, 1d" };
        yield return new object[] { "pie", "pie\n    \"A\" : 1" };
        yield return new object[] { "gitGraph", "gitGraph\n    commit" };
        yield return new object[] { "mindmap", "mindmap\n  root" };
        yield return new object[] { "timeline", "timeline\n    title History" };
        yield return new object[] { "journey", "journey\n    title User journey" };
        yield return new object[] { "quadrantChart", "quadrantChart\n    title Sample" };
        yield return new object[] { "xychart", "xychart-beta\n    title Chart" };
        yield return new object[] { "requirement", "requirementDiagram\n    requirement test" };
        yield return new object[] { "architecture", "architecture\n    service api(logic)" };
        yield return new object[] { "kanban", "kanban\n    title Board" };
        yield return new object[] { "sankey", "sankey-beta\n    A,B,1" };
        yield return new object[] { "block", "block-beta\n    columns 1" };
        yield return new object[] { "packet", "packet-beta\n    0-15: \"TCP\"" };
        yield return new object[] { "radar", "radar-beta\n    title R" };
        yield return new object[] { "treemap", "treemap\n    title T" };
        yield return new object[] { "c4", "C4Context\n    title Context" };
    }
}
