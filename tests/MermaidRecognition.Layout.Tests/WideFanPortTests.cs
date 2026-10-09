using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class WideFanPortTests
{
    [TestCase("LR")]
    [TestCase("RL")]
    public void Layout_WideParallelFan_UsesFacingCrossAxisPorts(string Direction)
    {
        string Branches = string.Join("\n", Enumerable.Range(0, 8).Select(Index => $"Root --> n{Index}[Branch] --> Sink"));
        var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nRoot[Root]\nSink[Sink]\n{Branches}");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var Root = Layout.NodeBounds.Single(Node => Node.NodeId == "Root");
        var Sink = Layout.NodeBounds.Single(Node => Node.NodeId == "Sink");
        foreach (var Child in Layout.NodeBounds.Where(Node => Node.NodeId != "Root" && Node.NodeId != "Sink"))
        {
            var Outward = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Root" && Edge.TargetId == Child.NodeId);
            var Inward = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == Child.NodeId && Edge.TargetId == "Sink");
            string Facing = Child.CenterY > Root.CenterY ? "top" : "bottom";
            Assert.That(Outward.ExitSide, Is.EqualTo(Facing));
            Assert.That(Inward.EntrySide, Is.EqualTo(Child.CenterY > Sink.CenterY ? "top" : "bottom"));
            Assert.That(Outward.Points[0].X, Is.EqualTo(Outward.Points[1].X).Within(0.5));
            Assert.That(Inward.Points[^1].X, Is.EqualTo(Inward.Points[^2].X).Within(0.5));
        }
    }
}
