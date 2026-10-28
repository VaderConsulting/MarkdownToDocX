using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class F59SubgraphRetryLoopTests
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
    public void F59_SubgraphMembership_AndCrossBoundaryEdges()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(F59_SOURCE);

        GroupContract Worker = Contract.Groups.Single(Group => Group.Id == "Worker");
        Assert.That(Worker.Title, Is.EqualTo("Worker pool"));
        Assert.That(Worker.Direction, Is.EqualTo("TopToBottom"));
        Assert.That(Worker.ChildNodeIds, Is.EquivalentTo(["Q", "W", "P", "Wait"]));

        NodeContract Client = Contract.Nodes.Single(Node => Node.Id == "Client");
        NodeContract Out = Contract.Nodes.Single(Node => Node.Id == "Out");
        NodeContract Queue = Contract.Nodes.Single(Node => Node.Id == "Q");
        NodeContract Process = Contract.Nodes.Single(Node => Node.Id == "P");

        Assert.That(Client.ParentGroupId, Is.Null);
        Assert.That(Out.ParentGroupId, Is.Null);
        Assert.That(Queue.ParentGroupId, Is.EqualTo("Worker"));
        Assert.That(Process.ParentGroupId, Is.EqualTo("Worker"));

        Assert.That(Contract.Edges, Has.Count.EqualTo(6));
        Assert.That(Contract.Edges.Any(Edge => Edge.SourceId == "Client" && Edge.TargetId == "Q"), Is.True);
        Assert.That(Contract.Edges.Any(Edge => Edge.SourceId == "W" && Edge.TargetId == "P" && Edge.Label == "Yes"), Is.True);
        Assert.That(Contract.Edges.Any(Edge => Edge.SourceId == "P" && Edge.TargetId == "Out"), Is.True);
    }
}
