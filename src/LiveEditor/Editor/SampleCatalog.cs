using System.Collections.Generic;

namespace LiveEditor.Editor;

/// <summary>
/// Built-in Mermaid samples for the Samples menu.
/// </summary>
internal static class SampleCatalog
{
    public static IReadOnlyList<(string Title, string Source)> All { get; } = new List<(string, string)>
    {
        ("Enterprise - web shop",
            "enterprise\r\n"
            + "  title Web shop\r\n"
            + "  application-component webShop \"Web Shop\"\r\n"
            + "  application-service orderApi \"Order API\"\r\n"
            + "  data-object order \"Order\"\r\n"
            + "  webShop -serving-> orderApi\r\n"
            + "  orderApi -access(write)-> order\r\n"),
        ("Enterprise - with placement hints",
            "enterprise\r\n"
            + "  application-component a1 \"Part 1\"\r\n"
            + "  application-component a2 \"Part 2\" right-of a1\r\n"
            + "  application-component a3 \"Part 3\" right-of a2\r\n"
            + "  a1 -flow-> a2\r\n"
            + "  a2 -flow-> a3\r\n"),
        ("Flowchart - decision",
            "flowchart TD\r\n"
            + "  Start([Start]) --> Decision{Ready?}\r\n"
            + "  Decision -->|Yes| Work[Do work]\r\n"
            + "  Decision -->|No| Wait[Wait]\r\n"
            + "  Work --> Done([Done])\r\n"
            + "  Wait --> Decision\r\n"),
        ("Sequence - request",
            "sequenceDiagram\r\n"
            + "  participant C as Client\r\n"
            + "  participant S as Service\r\n"
            + "  C->>S: Request\r\n"
            + "  S-->>C: Response\r\n")
    };
}
