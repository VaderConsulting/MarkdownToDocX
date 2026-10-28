using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class DiagonalPlacementHintTests
{
    [TestCase("NW", -1, -1)]
    [TestCase("NE", -1, 1)]
    [TestCase("SE", 1, 1)]
    [TestCase("SW", 1, -1)]
    public void InitialLayoutHonoursBothGapsAndKeepsConnectionsOrthogonal(string Direction, int RowSign, int ColumnSign)
    {
        string Source = "enterprise\napplication-component a\napplication-component b " + Direction + " a +1 +3\na -flow-> b\n";
        var Document = EnterpriseValidator.Analyze(Source).Document;
        var Model = EnterpriseFlowchartContractMapper.Build(Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Model, new() { UseCellRegions = true }, out var Layout), Is.True);
        var A = Layout.CellNodes!.Single(Node => Node.Id == "a").CellNode!.Region;
        var B = Layout.CellNodes!.Single(Node => Node.Id == "b").CellNode!.Region;
        Assert.That(RowSign < 0 ? B.EndRow <= A.StartRow - 1 : B.StartRow >= A.EndRow + 1, Is.True);
        Assert.That(ColumnSign < 0 ? B.EndColumn <= A.StartColumn - 3 : B.StartColumn >= A.EndColumn + 3, Is.True);
        Assert.That(Model.Nodes.Single(Node => Node.Id == "b").PlacementHint!.IsActive, Is.True);
        Assert.That(FlowchartRouteRuleChecker.Check(Model, Layout), Is.Empty);
        foreach (var Route in Layout.EdgeRoutes)
            for (int Index = 1; Index < Route.Points.Count; Index++)
                Assert.That(Math.Abs(Route.Points[Index].X - Route.Points[Index - 1].X) < 0.001
                    || Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y) < 0.001, Is.True);
    }
}
