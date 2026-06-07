using MermaidRecognition;
using MermaidRecognition.Flowchart;
using MermaidSharp;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F77ClassDefCommaTargetsTests
{
    const string F77_SOURCE = """
        flowchart LR
        A[Alpha] --> B[Beta] --> C[Gamma]
        classDef lane fill:#eef,stroke:#369
        class A,C lane
        """;

    [Test]
    public void Parse_CommaSeparatedClassTargets_AssignsLaneToAAndCOnly()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(F77_SOURCE);

        ClassDefContract LaneDef = Contract.ClassDefs.Single();
        Assert.That(LaneDef.ClassId, Is.EqualTo("lane"));
        Assert.That(LaneDef.Style.Fill, Is.EqualTo("#eef"));
        Assert.That(LaneDef.Style.Stroke, Is.EqualTo("#369"));

        NodeContract NodeA = Contract.Nodes.Single(Node => Node.Id == "A");
        NodeContract NodeB = Contract.Nodes.Single(Node => Node.Id == "B");
        NodeContract NodeC = Contract.Nodes.Single(Node => Node.Id == "C");

        Assert.That(NodeA.Classes, Contains.Item("lane"));
        Assert.That(NodeC.Classes, Contains.Item("lane"));
        Assert.That(NodeB.Classes, Is.Empty);
        Assert.That(NodeA.InlineStyle.Fill, Is.EqualTo("#eef"));
        Assert.That(NodeA.InlineStyle.Stroke, Is.EqualTo("#369"));
    }

    [Test]
    public void RenderSvg_F77_AppliesLaneFillAndStrokeOnAAndC()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            F77_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.That(Rendered, Is.True);

        Assert.That(
            Svg,
            Does.Contain("#eeeeff").IgnoreCase,
            "Expected lane fill #eef expanded to #eeeeff in SVG");
        Assert.That(
            Svg,
            Does.Contain("style=\"fill:#eeeeff").IgnoreCase
                .Or.Contain("style=\"fill: #eeeeff").IgnoreCase,
            "Expected inline style so theme CSS cannot override classDef fill");
        Assert.That(
            Svg,
            Does.Contain("#336699").IgnoreCase,
            "Expected lane stroke #369 expanded to #336699 in SVG");
    }
}
