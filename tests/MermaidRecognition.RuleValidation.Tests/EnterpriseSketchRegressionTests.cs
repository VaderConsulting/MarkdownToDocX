using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Enterprise catalogue sketch regressions for A23, A26, A29, A33, A38, and A40.</summary>
[TestFixture, NonParallelizable, Explicit("Enterprise sketch targets; enable while iterating enterprise layout rules 2.24/2.25.")]
public sealed class EnterpriseSketchRegressionTests
{
    [Test]
    public void A23_EmploymentToManagedPayroll_IsStraightHorizontalChord()
    {
        var layout = Layout("A23");
        var route = Route(layout, "payrollPkg", "employment");
        Assert.Multiple(() =>
        {
            Assert.That(route.Points, Has.Count.EqualTo(2));
            Assert.That(route.ExitSide, Is.EqualTo("right"));
            Assert.That(route.EntrySide, Is.EqualTo("left"));
            Assert.That(Math.Abs(route.Points[0].Y - route.Points[1].Y), Is.LessThanOrEqualTo(1.0));
        });
    }

    [Test]
    public void A26_SalesFunction_SitsBelowSalesCapability_WithStraightRealisation()
    {
        var layout = Layout("A26");
        var salesFn = Box(layout, "salesFn");
        var salesCap = Box(layout, "salesCap");
        var route = Route(layout, "salesFn", "salesCap");
        Assert.Multiple(() =>
        {
            Assert.That(salesFn.CenterX, Is.EqualTo(salesCap.CenterX).Within(1.0));
            Assert.That(salesFn.CenterY, Is.GreaterThan(salesCap.CenterY));
            Assert.That(route.Points, Has.Count.EqualTo(2));
            Assert.That(route.ExitSide, Is.EqualTo("top"));
            Assert.That(route.EntrySide, Is.EqualTo("bottom"));
        });
    }

    [Test]
    public void A29_MotivationMainChain_StacksInOneColumn()
    {
        var layout = Layout("A29");
        string[] spine = ["cfo", "compliance", "lateFilings", "onTimePay", "zeroLate", "staffTrust"];
        double column = Box(layout, spine[0]).CenterX;
        foreach (string id in spine)
        {
            Assert.That(Box(layout, id).CenterX, Is.EqualTo(column).Within(1.5), id);
        }
    }

    [Test]
    public void A33_PayrollSuiteToRunPayroll_AttachesFrameAndEntersPayRunFromBelow()
    {
        var layout = Layout("A33");
        var route = Route(layout, "payrollSuite", "payRun");
        var frame = Cluster(layout, "payrollSuite");
        var payRun = Box(layout, "payRun");
        double frameTop = frame.CenterY + frame.Height / 2.0;
        double payRunBottom = payRun.CenterY - payRun.Height / 2.0;
        Assert.Multiple(() =>
        {
            Assert.That(route.Points[0].Y, Is.LessThanOrEqualTo(frameTop + 1.0));
            Assert.That(route.EntrySide, Is.EqualTo("bottom").Or.EqualTo("top"));
            Assert.That(route.Points[^1].Y, Is.GreaterThanOrEqualTo(payRunBottom - 1.0));
            Assert.That(Math.Abs(route.Points[^1].X - payRun.CenterX), Is.LessThanOrEqualTo(1.0));
        });
    }

    [Test]
    public void A38_PriceToStock_IsStraightVertical_AndPriceToPlaceOrder_ExitsRight()
    {
        var layout = Layout("A38");
        var vertical = Route(layout, "priceSvc", "stockSvc");
        var branch = Route(layout, "priceSvc", "placeOrder");
        Assert.Multiple(() =>
        {
            Assert.That(vertical.Points, Has.Count.EqualTo(2));
            Assert.That(Math.Abs(vertical.Points[0].X - vertical.Points[1].X), Is.LessThanOrEqualTo(1.0));
            Assert.That(branch.ExitSide, Is.EqualTo("right"));
        });
    }

    [Test]
    public void A40_AccessChain_StacksProcessesAboveObjects()
    {
        var layout = Layout("A40");
        string[] processes = ["payRun", "audit", "archive"];
        double column = Box(layout, "timesheet").CenterX;
        foreach (string id in processes)
        {
            Assert.That(Box(layout, id).CenterX, Is.EqualTo(column).Within(1.5), id);
        }

        Assert.That(Box(layout, "payRun").CenterY, Is.LessThan(Box(layout, "timesheet").CenterY));
    }

    private static FlowchartLayoutNodeBounds Box(FlowchartLayoutResult layout, string id) =>
        layout.NodeBounds.Single(Box => Box.NodeId == id);

    private static FlowchartLayoutNodeBounds Cluster(FlowchartLayoutResult layout, string id) =>
        layout.ClusterBounds.Single(Box => Box.NodeId == id);

    private static FlowchartLayoutEdgeRoute Route(FlowchartLayoutResult layout, string sourceId, string targetId) =>
        layout.EdgeRoutes.Single(Route => Route.SourceId == sourceId && Route.TargetId == targetId);

    private static FlowchartLayoutResult Layout(string id)
    {
        var scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == id);
        var contract = EnterpriseFlowchartContractMapper.Build(
            EnterpriseValidator.Analyze(scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(
            contract, new FlowchartRenderContext { UseCellRegions = true }, out var layout), Is.True);
        return layout;
    }
}
