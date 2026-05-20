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
    public void F28_MinLengthLinks_UseStraightSegments()
    {
        const string input =
            """
            flowchart TB
            A[A] --> B[B]
            C[C] ---> D[D]
            E[E] ----> F[F]
            """;

#pragma warning disable IDE0007
        string Svg = Mermaid.Render(input);
        System.Text.RegularExpressions.MatchCollection Matches = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<path d=""([^""]+)""[^>]*class=""flowchart-link""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
#pragma warning restore IDE0007

        Assert.That(Matches.Count, Is.EqualTo(3));

#pragma warning disable IDE0007
        foreach (System.Text.RegularExpressions.Match Match in Matches)
        {
            string PathData = Match.Groups[1].Value;
            Assert.That(PathData, Does.Not.Contain('C'), "Min-length links must use straight polylines, not curveBasis: " + PathData);
            Assert.That(PathData, Does.Contain('L'), "Expected linear path commands: " + PathData);
        }

        // Long minlen chains must end above the target top so marker-end is not covered by node fill (F28).
        System.Text.RegularExpressions.Match PathCd = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"<path d=""M122,(\d+)L122,(\d+)""[^>]*class=""flowchart-link""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.That(PathCd.Success, Is.True);
        double EndY = double.Parse(PathCd.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        System.Text.RegularExpressions.Match NodeDTop = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"<path d=""M98,(\d+) H146 V\d+ H98 Z""[^>]*class=""node""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.That(NodeDTop.Success, Is.True);
        double TargetTopY = double.Parse(NodeDTop.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(EndY, Is.LessThan(TargetTopY - 0.5), "C→D path must stop above D so the arrowhead is visible");
#pragma warning restore IDE0007
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
