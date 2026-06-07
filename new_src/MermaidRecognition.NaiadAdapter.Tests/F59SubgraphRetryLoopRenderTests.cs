using MermaidRecognition;
using MermaidRecognition.NaiadAdapter;
using MermaidSharp;
using MermaidSharp.Diagrams.Flowchart;
using MermaidSharp.Models;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter.Tests;

[TestFixture]
public sealed class F59SubgraphRetryLoopRenderTests
{
    const string F59_SOURCE = """
        flowchart TB
        subgraph Worker["Worker pool"]
          direction TB
          Q[Queue] --> W{Worker free?}
          W -->|Yes| P[Process job]
          W -->|No| Wait[Wait]
          Wait --> Q
        end
        Client[Client] --> Q
        P --> Out[Output]
        """;

    [Test]
    public void TryRenderFlowchartSvg_F59_RendersWorkerPoolRegion()
    {
        bool Rendered = FlowchartRecognitionRenderBridge.TryRenderLegacyNaiadFlowchartSvg(
            F59_SOURCE,
            RenderOptions.Default,
            out string Svg);

        Assert.That(Rendered, Is.True);
        Assert.That(Svg, Does.Contain("fill=\"#ffffde\""));
        Assert.That(Svg, Does.Contain("Worker pool"));
    }

    [Test]
    public void Map_F59_AlignsClientAboveQueueAndPreservesDecisionBranchRoute()
    {
        FlowchartModel Model = ContractToFlowchartModel.Map(MermaidRecognizer.Parse(F59_SOURCE));
        FlowchartRenderer Renderer = new FlowchartRenderer();
        Renderer.Render(Model, RenderOptions.Default);

        Assert.That(Model.Subgraphs, Has.Count.EqualTo(1));
        Assert.That(Model.Subgraphs[0].Id, Is.EqualTo("Worker"));

        Node Client = Model.GetNode("Client")!;
        Node Queue = Model.GetNode("Q")!;
        Node Worker = Model.GetNode("W")!;
        Node Process = Model.GetNode("P")!;
        Edge ClientToQueue = Model.Edges.Single(Edge => Edge.SourceId == "Client" && Edge.TargetId == "Q");
        Edge YesEdge = Model.Edges.Single(Edge => Edge.SourceId == "W" && Edge.TargetId == "P");

        Assert.That(Math.Abs(Client.Position.X - Queue.Position.X), Is.LessThan(1.0));
        Assert.That(ClientToQueue.Points.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(
            ClientToQueue.Points.All(Point => Math.Abs(Point.X - Queue.Position.X) < 1.0)
                || Math.Abs(ClientToQueue.Points[0].X - ClientToQueue.Points[^1].X) < 1.0,
            Is.True,
            "Client→Queue should route in the hub column");

        Assert.That(YesEdge.Points.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(YesEdge.Points[0].X, Is.GreaterThan(Worker.Position.X));
        Assert.That(Process.Position.X, Is.GreaterThan(Worker.Position.X));
    }
}
