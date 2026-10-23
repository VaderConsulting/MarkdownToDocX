using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class FlowchartDecisionBranchLabelTests
{
    private const string YesNoTdSource = """
        flowchart TD
        A["Incoming messages"] --> B{"Matches filter?"}
        B -- Yes --> C["Forward"]
        B -- No --> D["Discard / divert"]
        """;

    [TestCase("Y", true)]
    [TestCase("y", true)]
    [TestCase(" Yes ", true)]
    [TestCase("YES", true)]
    [TestCase("yes", true)]
    [TestCase("N", false)]
    [TestCase("maybe", false)]
    public void IsYesBranchLabel_MatchesRule311(string Label, bool Expected)
    {
        Assert.That(FlowchartDecisionBranchLabelHelper.IsYesBranchLabel(Label), Is.EqualTo(Expected));
    }

    [TestCase("N", true)]
    [TestCase("n", true)]
    [TestCase(" No ", true)]
    [TestCase("NO", true)]
    [TestCase("no", true)]
    [TestCase("Y", false)]
    [TestCase("maybe", false)]
    public void IsNoBranchLabel_MatchesRule311(string Label, bool Expected)
    {
        Assert.That(FlowchartDecisionBranchLabelHelper.IsNoBranchLabel(Label), Is.EqualTo(Expected));
    }

    [Test]
    public void TryLayout_TdDecision_YAndNLabels_MatchYesNoPortPlacement()
    {
        (double YesX, double YesY, double NoX, double NoY) Baseline = ResolveBranchSourcePorts(YesNoTdSource);
        (double AltYesX, double AltYesY, double AltNoX, double AltNoY) = ResolveBranchSourcePorts("""
            flowchart TD
            A["Incoming messages"] --> B{"Matches filter?"}
            B -- Y --> C["Forward"]
            B -- N --> D["Discard / divert"]
            """);

        Assert.That(AltYesX, Is.EqualTo(Baseline.YesX).Within(0.5));
        Assert.That(AltYesY, Is.EqualTo(Baseline.YesY).Within(0.5));
        Assert.That(AltNoX, Is.EqualTo(Baseline.NoX).Within(0.5));
        Assert.That(AltNoY, Is.EqualTo(Baseline.NoY).Within(0.5));
    }

    [Test]
    public void TryLayout_TdDecision_UpperCaseYesNoLabels_MatchCanonicalYesNoPortPlacement()
    {
        (double YesX, double YesY, double NoX, double NoY) Baseline = ResolveBranchSourcePorts(YesNoTdSource);
        (double AltYesX, double AltYesY, double AltNoX, double AltNoY) = ResolveBranchSourcePorts("""
            flowchart TD
            A["Incoming messages"] --> B{"Matches filter?"}
            B -- YES --> C["Forward"]
            B -- NO --> D["Discard / divert"]
            """);

        Assert.That(AltYesX, Is.EqualTo(Baseline.YesX).Within(0.5));
        Assert.That(AltYesY, Is.EqualTo(Baseline.YesY).Within(0.5));
        Assert.That(AltNoX, Is.EqualTo(Baseline.NoX).Within(0.5));
        Assert.That(AltNoY, Is.EqualTo(Baseline.NoY).Within(0.5));
    }

    private static (double YesX, double YesY, double NoX, double NoY) ResolveBranchSourcePorts(string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        FlowchartLayoutEdgeRoute YesRoute = FindRoute(Layout, "B", "C");
        FlowchartLayoutEdgeRoute NoRoute = FindRoute(Layout, "B", "D");
        return (YesRoute.Points[0].X, YesRoute.Points[0].Y, NoRoute.Points[0].X, NoRoute.Points[0].Y);
    }

    private static FlowchartLayoutEdgeRoute FindRoute(FlowchartLayoutResult Layout, string SourceId, string TargetId)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            if (string.Equals(Route.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Route.TargetId, TargetId, StringComparison.Ordinal))
            {
                return Route;
            }
        }

        throw new AssertionException($"Missing route {SourceId}->{TargetId}");
    }
}
