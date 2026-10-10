using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class RetryOutcomePlacementTests
{
    [TestCase("LR", false)]
    [TestCase("RL", false)]
    [TestCase("LR", true)]
    [TestCase("RL", true)]
    public void Layout_HorizontalRetry_KeepsFinishAheadAndReturnBelow(string Direction, bool ReverseOrder)
    {
        string Branches = ReverseOrder
            ? "Gate -->|again| Repeat[Repeat]\nGate -->|complete| Exit[Exit]"
            : "Gate -->|complete| Exit[Exit]\nGate -->|again| Repeat[Repeat]";
        var Contract = MermaidRecognizer.Parse($"flowchart {Direction}\nEntry[Entry] --> Gate{{Gate}}\n{Branches}\nRepeat --> Entry");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var Gate = Layout.NodeBounds.Single(Node => Node.NodeId == "Gate");
        var Finish = Layout.NodeBounds.Single(Node => Node.NodeId == "Exit");
        var Retry = Layout.NodeBounds.Single(Node => Node.NodeId == "Repeat");
        var Input = Layout.NodeBounds.Single(Node => Node.NodeId == "Entry");
        Assert.That(Finish.CenterY, Is.EqualTo(Gate.CenterY).Within(0.5));
        Assert.That((Finish.CenterX - Gate.CenterX) * (Direction == "LR" ? 1 : -1), Is.GreaterThan(0));
        Assert.That(Retry.CenterX, Is.EqualTo(Gate.CenterX).Within(0.5));
        Assert.That(Retry.CenterY + Retry.Height / 2, Is.LessThan(Gate.CenterY - Gate.Height / 2 - 60));
        var Branch = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Gate" && Edge.TargetId == "Repeat");
        Assert.That(Branch.Points.All(Point => Math.Abs(Point.X - Gate.CenterX) < 0.5), Is.True);
        var Return = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Repeat" && Edge.TargetId == "Entry");
        Assert.That(Return.Points[0].X, Is.EqualTo(Retry.CenterX).Within(0.5));
        Assert.That(Return.Points[0].Y, Is.EqualTo(Retry.CenterY - Retry.Height / 2).Within(0.5));
        Assert.That(Return.Points[^1].X, Is.EqualTo(Input.CenterX).Within(0.5));
        Assert.That(Return.Points[^1].Y, Is.EqualTo(Input.CenterY - Input.Height / 2).Within(0.5));
        Assert.That(Return.Points.Min(Point => Point.Y), Is.LessThan(Retry.CenterY - Retry.Height / 2 - 30));
        Assert.That(Return.Points.Zip(Return.Points.Skip(1)).All(Pair =>
            Math.Abs(Pair.First.X - Pair.Second.X) < 0.5 || Math.Abs(Pair.First.Y - Pair.Second.Y) < 0.5), Is.True);
    }
}
