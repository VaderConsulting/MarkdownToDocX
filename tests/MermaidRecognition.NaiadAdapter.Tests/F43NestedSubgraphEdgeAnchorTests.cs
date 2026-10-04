using MermaidRecognition.Core;
using MermaidRecognition.NaiadAdapter;

using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Layout;
using MermaidSharp.Models;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F43NestedSubgraphEdgeAnchorTests
{
    const string F43_SOURCE = """
        flowchart TB
        subgraph Outer["Outer subgraph"]
          direction TB
          A["Top node"] --> B["Middle node"]
          subgraph Inner["Inner subgraph"]
            direction LR
            C["Left"] --> D["Right"]
          end
          B --> C
          D --> E["Bottom node"]
        end
        """;

    [Test]
    public void Render_F43_TbEdges_ExitFromSourceBottomNotTop()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(F43_SOURCE));
        FlowchartRenderer Renderer = new FlowchartRenderer(new DagreLayoutEngine());
        Renderer.Render(Model, RenderOptions.Default);

        AssertEdgeExitsFromBottom(Model, "A", "B");
        AssertEdgeExitsFromBottom(Model, "B", "C");
        AssertEdgeExitsFromBottom(Model, "D", "E");
        Assert.That(Model.GetNode("A")!.Position.Y, Is.LessThan(Model.GetNode("Inner")!.Position.Y),
            "Top node should stay above the inner subgraph cluster");
        Assert.That(Model.GetNode("B")!.Position.Y, Is.LessThan(Model.GetNode("Inner")!.Position.Y),
            "Middle node should stay above the inner subgraph cluster");
    }

    static void AssertEdgeExitsFromBottom(FlowchartModel Model, string SourceId, string TargetId)
    {
        Edge Edge = Model.Edges.Single(Candidate =>
            string.Equals(Candidate.SourceId, SourceId, StringComparison.Ordinal)
            && string.Equals(Candidate.TargetId, TargetId, StringComparison.Ordinal));
        Node Source = Model.GetNode(SourceId)!;
        Assert.That(Edge.Points.Count, Is.GreaterThanOrEqualTo(2),
            () => $"Edge {SourceId}->{TargetId} should have a routed polyline");

        double SourceTop = Source.Position.Y - (Source.Height / 2.0);
        double SourceBottom = Source.Position.Y + (Source.Height / 2.0);
        Position FirstPoint = Edge.Points[0];
        Assert.That(FirstPoint.Y, Is.GreaterThanOrEqualTo(SourceBottom - 4.0),
            () => $"Edge {SourceId}->{TargetId} should leave the bottom of the source, not the top");
        Assert.That(Math.Abs(FirstPoint.Y - SourceTop), Is.GreaterThan(4.0),
            () => $"Edge {SourceId}->{TargetId} should not attach at the top face of the source");
    }
}
