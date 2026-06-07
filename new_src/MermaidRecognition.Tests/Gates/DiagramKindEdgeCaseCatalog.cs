namespace MermaidRecognition.Tests.Gates;

/// <summary>
/// Positive and negative edge-case sources for every supported programme diagram kind (pass 21).
/// </summary>
internal static class DiagramKindEdgeCaseCatalog
{
    public static IEnumerable<object[]> LinePatternHeaderOnlyCases()
    {
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
        yield return new object[] { "gantt", "gantt\n    title A" };
    }

    public static IEnumerable<object[]> StartedKindPositiveCases()
    {
        yield return new object[] { "flowchart", "flowchart TD\n    A --> B --> C" };
        yield return new object[] { "sequence", "sequenceDiagram\n    Alice->>Bob: Hello" };
        yield return new object[] { "classDiagram", "classDiagram\n    class Animal" };
        yield return new object[] { "stateDiagram", "stateDiagram-v2\n    [*] --> Still" };
        yield return new object[] { "erDiagram", "erDiagram\n    A ||--o{ B : rel" };
        yield return new object[] { "pie", "pie\n    \"A\" : 1" };
        yield return new object[] { "gantt", "gantt\n    title A\n    Task :a1, 2024-01-01, 1d" };
        yield return new object[] { "mindmap", "mindmap\n  root" };
        yield return new object[] { "gitGraph", "gitGraph\n    commit" };
    }

    public static IEnumerable<object[]> StartedKindNegativeCases()
    {
        yield return new object[] { "flowchart", "flowchart TD" };
        yield return new object[] { "sequence", "sequenceDiagram" };
        yield return new object[] { "classDiagram", "classDiagram" };
        yield return new object[] { "stateDiagram", "stateDiagram-v2" };
        yield return new object[] { "erDiagram", "erDiagram" };
        yield return new object[] { "pie", "pie title Empty" };
        yield return new object[] { "gantt", "gantt" };
        yield return new object[] { "mindmap", "mindmap" };
        yield return new object[] { "gitGraph", "gitGraph" };
    }

    public static IEnumerable<object[]> MermaidOnlyFixtureCases()
    {
        yield return new object[] { "eventmodeling", "eventmodeling.mmd" };
        yield return new object[] { "treeview", "treeview.mmd" };
        yield return new object[] { "ishikawa", "ishikawa.mmd" };
        yield return new object[] { "venn", "venn.mmd" };
        yield return new object[] { "wardley", "wardley.mmd" };
        yield return new object[] { "info", "info.mmd" };
    }
}
