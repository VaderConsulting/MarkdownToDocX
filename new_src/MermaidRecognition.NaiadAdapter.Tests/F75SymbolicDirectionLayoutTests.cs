using MermaidRecognition;
using MermaidRecognition.Flowchart;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Layout;
using MermaidSharp.Models;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F75SymbolicDirectionLayoutTests
{
    const string F75_SOURCE = """
        flowchart <
        West[West] --> Centre[Centre] --> East[East]
        """;

    [Test]
    public void F75_FlowchartLessThan_LaysOutWestToEastRightToLeft()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(F75_SOURCE);
        Assert.That(Contract.Direction, Is.EqualTo(nameof(FlowchartDirection.RightToLeft)));

        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);
        Assert.That(Model.Direction, Is.EqualTo(Direction.RightToLeft));
        Assert.That(Model.Nodes, Has.Count.EqualTo(3));
        Assert.That(Model.Edges, Has.Count.EqualTo(2));

        FlowchartRenderer Renderer = new FlowchartRenderer(new DagreLayoutEngine());
        Renderer.Render(Model, RenderOptions.Default);

        Node NodeWest = Model.GetNode("West")!;
        Node NodeCentre = Model.GetNode("Centre")!;
        Node NodeEast = Model.GetNode("East")!;
        Assert.That(NodeWest.Position.X, Is.GreaterThan(NodeCentre.Position.X),
            "In RL, West (chain start) should appear to the right of Centre.");
        Assert.That(NodeCentre.Position.X, Is.GreaterThan(NodeEast.Position.X),
            "In RL, Centre should appear to the right of East (chain end).");
    }

    [Test]
    public void TryRenderFlowchartSvg_F75_Succeeds()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderFlowchartSvg(
            F75_SOURCE,
            RenderOptions.Default,
            out string Svg);
        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("West"));
        Assert.That(Svg, Does.Contain("East"));
    }
}
