using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartLinkStyleTests
{
    [Test]
    public void LinkStyleDefault_IsCaptured()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                linkStyle default stroke:#f00
            """);

        LinkStyleDirective DefaultStyle = Contract.LinkStyles.Single(Style => Style.IsDefault);
        Assert.That(DefaultStyle.Style.Stroke, Is.EqualTo("#f00"));
    }

    [Test]
    public void LinkStyleIndex_TargetsCreationIndex()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                A --> C
                linkStyle 1 stroke:#00f
            """);

        LinkStyleDirective Indexed = Contract.LinkStyles.Single(Style => !Style.IsDefault);
        Assert.That(Indexed.EdgeIndexes, Is.EqualTo(new[] { 1 }));
        Assert.That(Indexed.Style.Stroke, Is.EqualTo("#00f"));
    }

    [Test]
    public void LinkStyleInterpolateOnly_SetsCurveOnEdge()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A --> B
                linkStyle 0 interpolate basis
            """);

        LinkStyleDirective Style = Contract.LinkStyles.Single();
        Assert.That(Style.Curve, Is.EqualTo("basis"));
        Assert.That(Contract.Edges.Single().Curve, Is.EqualTo("basis"));
    }
}
