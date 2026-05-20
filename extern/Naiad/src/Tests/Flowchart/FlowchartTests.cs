public class FlowchartTests : TestBase
{
    [Test]
    public Task Simple()
    {
        const string input =
            """
            flowchart LR
                A[Start] --> B[Process] --> C[End]
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task F08_TwoNodesOneCharLabels()
    {
        const string input =
            """
            flowchart LR
            A[A] --> B[B]
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task F13_CylinderDatabase()
    {
        const string input =
            """
            flowchart TB
            Store[(Database / cylinder)]
            App[Application] --> Store
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task F14_Hexagon()
    {
        const string input =
            """
            flowchart LR
            A{{Hexagon}} --> B[Next]
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task Complex()
    {
        const string input =
            """
            flowchart TD
                A[Christmas] -->|Get money| B(Go shopping)
                B --> C{Let me think}
                C -->|One| D[Laptop]
                C -->|Two| E[iPhone]
                C -->|Three| F[fa:fa-car Car]
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task Shapes()
    {
        const string input =
            """
            flowchart TD
                A[Rectangle]
                B(Rounded)
                C{Diamond}
                D((Circle))
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task EdgeLabels()
    {
        const string input =
            """
            flowchart LR
                A --> |Yes| B
                A --> |No| C
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task F28_MinLengthLinks()
    {
        const string input =
            """
            flowchart TB
            A[A] --> B[B]
            C[C] ---> D[D]
            E[E] ----> F[F]
            """;

        return VerifySvg(input);
    }

    [Test]
    public Task GraphKeyword()
    {
        const string input =
            """
            graph TD
                A --> B --> C
            """;

        return VerifySvg(input);
    }

    [Test]
    public void LeadingAndTrailingWhitespace()
    {
        const string input =
            """
            flowchart LR
                A[Start] --> B[Process] --> C[End]
            """;

        var expected = Mermaid.Render(input);
        var actual = Mermaid.Render("\r\n\r\n" + input + "\r\n\r\n");

        Assert.That(actual, Is.EqualTo(expected));
    }
}
