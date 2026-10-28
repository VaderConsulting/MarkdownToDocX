using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class DiagonalPlacementHintTests
{
    [TestCase(-2, 3)]
    [TestCase(0, 0)]
    public void InitialLayoutReservesAbsoluteCellsBeforeAutomaticNodes(int Row, int Column)
    {
        string Source = $"enterprise\napplication-component a\napplication-component b at {Row} {Column}\na -flow-> b\n";
        var Model = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Model, new() { UseCellRegions = true }, out var Layout), Is.True);
        var A = Layout.CellNodes!.Single(Node => Node.Id == "a").CellNode!.Region;
        var B = Layout.CellNodes!.Single(Node => Node.Id == "b").CellNode!.Region;
        Assert.That(B.StartRow, Is.EqualTo(Row));
        Assert.That(B.StartColumn, Is.EqualTo(Column));
        Assert.That(A.Overlaps(B), Is.False);
        Assert.That(Model.Nodes.Single(Node => Node.Id == "b").PlacementHint!.IsActive, Is.True);
        Assert.That(FlowchartRouteRuleChecker.Check(Model, Layout), Is.Empty);
    }

    [TestCase("SE a +2 +3", "SE a +2 +6")]
    [TestCase("at 3 2", "at 3 6")]
    public void GeometryCacheDoesNotReuseAnImageForADifferentHorizontalPosition(string First, string Second)
    {
        string Source = "enterprise\ntitle Cell position cache regression\napplication-component a\napplication-component b ";
        string A = EnterpriseSvgRenderer.Render(EnterpriseValidator.Analyze(Source + First).Document);
        string B = EnterpriseSvgRenderer.Render(EnterpriseValidator.Analyze(Source + Second).Document);
        Assert.That(B, Is.Not.EqualTo(A));
        Assert.That(EnterpriseSvgRenderer.Render(EnterpriseValidator.Analyze(Source + First).Document), Is.EqualTo(A));
    }

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
