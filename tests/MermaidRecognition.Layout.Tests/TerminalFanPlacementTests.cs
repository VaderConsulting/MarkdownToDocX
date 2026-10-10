using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class TerminalFanPlacementTests
{
    [TestCase("TB", false, 3)]
    [TestCase("BT", false, 3)]
    [TestCase("LR", true, 3)]
    [TestCase("RL", true, 3)]
    [TestCase("TB", false, 5)]
    [TestCase("LR", true, 5)]
    public void Layout_IndependentTerminalFan_CentresParentOnMedianChild(string Direction, bool Horizontal, int Count)
    {
        string Mermaid = "flowchart " + Direction + "\nInput --> Hub\n" +
            string.Join("\n", Enumerable.Range(0, Count).Select(Index => $"Hub --> Child{Index}"));
        var Contract = MermaidRecognizer.Parse(Mermaid);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out var Layout), Is.True);
        var Parent = Layout.NodeBounds.Single(Node => Node.NodeId == "Hub");
        double Cross(FlowchartLayoutNodeBounds Node) => Horizontal ? Node.CenterY : Node.CenterX;
        var Children = Layout.NodeBounds.Where(Node => Node.NodeId.StartsWith("Child", StringComparison.Ordinal))
            .OrderBy(Cross).ToArray();
        Assert.That(Cross(Parent), Is.EqualTo(Cross(Children[Count / 2])).Within(0.5));
        Assert.That(Cross(Layout.NodeBounds.Single(Node => Node.NodeId == "Input")), Is.EqualTo(Cross(Parent)).Within(0.5));
        Assert.That(Children.Select(Cross).Distinct().Count(), Is.EqualTo(Count));
        var CentreRoute = Layout.EdgeRoutes.Single(Edge => Edge.SourceId == "Hub" && Edge.TargetId == Children[Count / 2].NodeId);
        Assert.That(CentreRoute.Points.All(Point => Math.Abs((Horizontal ? Point.Y : Point.X) - Cross(Parent)) < 0.5), Is.True,
            "The median child has one straight centre connection.");
    }
}