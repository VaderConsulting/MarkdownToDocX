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
    public void F15_Asymmetric_UsesMermaidVertexOrder()
    {
        const string input =
            """
            flowchart LR
            A>Asymmetric flag] --> B[Target]
            """;

#pragma warning disable IDE0007
        string Svg = Mermaid.Render(input);
        System.Text.RegularExpressions.Match AsymmetricFill = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"<path d=""(M[^""]+Z)""[^>]*class=""node""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        System.Text.RegularExpressions.MatchCollection OutlineLines = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"<line x1=""([^""]+)"" y1=""([^""]+)"" x2=""([^""]+)"" y2=""([^""]+)"" stroke=""#9370DB"" stroke-width=""1""/>",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
#pragma warning restore IDE0007

        Assert.That(AsymmetricFill.Success, Is.True);
        Assert.That(
            AsymmetricFill.Groups[1].Value,
            Is.EqualTo("M0,24L-24,0L170.72,0L170.72,48L-24,48Z"));
        Assert.That(OutlineLines.Count, Is.EqualTo(5));
        Assert.That(Svg, Does.Contain("<line x1=\"-24\" y1=\"48\" x2=\"0\" y2=\"24\" stroke=\"#9370DB\" stroke-width=\"1\"/>"));
        Assert.That(Svg, Does.Contain("<line x1=\"0\" y1=\"24\" x2=\"-24\" y2=\"0\" stroke=\"#9370DB\" stroke-width=\"1\"/>"));
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
    public void F29_BinaryDecision_UsesCurvedBranchesToTopCentre()
    {
        const string input =
            """
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
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
        string YesPath = Matches[1].Groups[1].Value;
        string NoPath = Matches[2].Groups[1].Value;
        Assert.That(YesPath, Does.Contain('C'), "Yes branch must use curveBasis, not a straight polyline");
        Assert.That(NoPath, Does.Contain('C'), "No branch must use curveBasis, not a straight polyline");

        System.Text.RegularExpressions.Match AcceptNode = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"<path d=""M0,(\d+(?:\.\d+)?) H(\d+(?:\.\d+)?) V(\d+(?:\.\d+)?) H0 Z""[^>]*class=""node""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.That(AcceptNode.Success, Is.True);
        double AcceptCentreX = double.Parse(AcceptNode.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) / 2.0;
        double AcceptTopY = double.Parse(AcceptNode.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        System.Text.RegularExpressions.Match YesEnd = System.Text.RegularExpressions.Regex.Match(
            YesPath,
            @"L([\d.]+),([\d.]+)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.That(YesEnd.Success, Is.True);
        double YesEndX = double.Parse(YesEnd.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        double YesEndY = double.Parse(YesEnd.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(Math.Abs(YesEndX - AcceptCentreX), Is.LessThan(2.0));
        Assert.That(YesEndY, Is.LessThan(AcceptTopY + 2.0));
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
    public void F53_StadiumMergeAttachPointsAreSeparatedOnOutline()
    {
        const string input =
            """
            flowchart TD
            Success[Success] --> End([End])
            Error[Error] --> End
            """;

#pragma warning disable IDE0007
        string Svg = Mermaid.Render(input);
        System.Text.RegularExpressions.MatchCollection LinkPaths = System.Text.RegularExpressions.Regex.Matches(
            Svg,
            @"path d=""(M[^""]+)""[^>]*class=""flowchart-link""",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.That(LinkPaths.Count, Is.EqualTo(2));

        System.Text.RegularExpressions.Match LastPointA = System.Text.RegularExpressions.Regex.Match(
            LinkPaths[0].Groups[1].Value,
            @"([\d\.-]+),([\d\.-]+)\s*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        System.Text.RegularExpressions.Match LastPointB = System.Text.RegularExpressions.Regex.Match(
            LinkPaths[1].Groups[1].Value,
            @"([\d\.-]+),([\d\.-]+)\s*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        Assert.That(LastPointA.Success, Is.True);
        Assert.That(LastPointB.Success, Is.True);

        double EndX = double.Parse(LastPointA.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
            + double.Parse(LastPointB.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        EndX = EndX / 2.0;
        double AttachAX = double.Parse(LastPointA.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        double AttachBX = double.Parse(LastPointB.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        double AttachAY = double.Parse(LastPointA.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        double AttachBY = double.Parse(LastPointB.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        double Separation = Math.Abs(AttachAX - AttachBX);

        Assert.That(AttachAX, Is.LessThan(EndX - 5.0));
        Assert.That(AttachBX, Is.GreaterThan(EndX + 5.0));
        Assert.That(Separation, Is.GreaterThan(20.0));
        Assert.That(Math.Abs(AttachAX - AttachBX), Is.GreaterThan(40.0));
        Assert.That(Math.Abs(AttachAY - AttachBY), Is.LessThan(3.0));
#pragma warning restore IDE0007
    }

    [Test]
    public void F53_EndStadiumIsWidePillNotCircle()
    {
        const string input =
            """
            flowchart TD
            Success[Success] --> End([End])
            Error[Error] --> End
            """;

#pragma warning disable IDE0007
        string Svg = Mermaid.Render(input);
        System.Text.RegularExpressions.Match StadiumPath = System.Text.RegularExpressions.Regex.Match(
            Svg,
            @"path d=""(M([\d\.-]+),([\d\.-]+)\s+H([\d\.-]+),([\d\.-]+)\s+A([\d\.-]+),([\d\.-]+)[^""]+)""[^>]*class=""node""[^>]*>\s*<text[^>]*>\s*<tspan[^>]*>End</tspan>",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.That(StadiumPath.Success, Is.True, "Expected stadium path for End node.");

        double FlatLeftX = double.Parse(StadiumPath.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        double FlatRightX = double.Parse(StadiumPath.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        double Radius = double.Parse(StadiumPath.Groups[6].Value, System.Globalization.CultureInfo.InvariantCulture);
        double FlatWidth = Math.Abs(FlatRightX - FlatLeftX);
        double Height = Radius * 2.0;

        Assert.That(FlatWidth, Is.GreaterThan(Radius * 0.35), "End stadium flat section should be visible (mermaid.ts w = bbox + h/4 + padding).");
        Assert.That(Height, Is.LessThan(40.0), "End stadium height should follow label line-height 1.5 + padding, not the 2.5em squareRect box.");
#pragma warning restore IDE0007
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

    [Test]
    public void F05_BtForwardChain_UsesStraightVerticalEdgesNotMarginLoop()
    {
        const string input =
            """
            flowchart BT
            Base[Base] --> Middle[Middle] --> Apex[Apex]
            """;

#pragma warning disable IDE0007
        string Svg = Mermaid.Render(input);
        Assert.That(Svg, Does.Not.Contain("L117.2,20.2"), "Margin-loop back-edge routing must not apply to forward BT chain edges.");
        Assert.That(Svg, Does.Not.Contain("L117.2,138"), "Forward BT chain edges must not detour on the right margin.");
        Assert.That(Svg, Does.Contain("L49.6,138"), "Base to Middle should be a vertical line in the node column.");
        Assert.That(Svg, Does.Contain("L49.6,28"), "Middle to Apex should be a vertical line in the node column.");
#pragma warning restore IDE0007
    }
}
