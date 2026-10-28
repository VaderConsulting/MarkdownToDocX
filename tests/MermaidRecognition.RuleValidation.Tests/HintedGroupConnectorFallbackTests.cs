using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Protects hinted group-endpoint routes through strict hint replay and complete layout.</summary>
[TestFixture, NonParallelizable]
public sealed class HintedGroupConnectorFallbackTests
{
    /// <summary>Replays the captured A167 endpoint clearance without depending on the placement algorithm that produced it.</summary>
    [Test]
    public void CapturedTitleClearanceKeepsExactHintedPortsAndNormalRuns()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A167");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = CapturedBounds();
        FlowchartLayoutResult Layout = new FlowchartLayoutResult { RenderContext = new() { UseCellRegions = true } };
        FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute
        {
            EdgeId = "vendorOp->devCustodian:12", SourceId = "vendorOp", TargetId = "devCustodian",
            ExitSide = "bottom", EntrySide = "top", ExitNormalY = -1, EntryNormalY = 1
        };
        Route.Points.Add(new() { X = 5690, Y = 2310 });
        Route.Points.Add(new() { X = 4935, Y = 2430 });
        Layout.EdgeRoutesMutable.Add(Route);
        // This unmodified sibling owns the second bottom slot on vendorOp.
        FlowchartLayoutEdgeRoute Sibling = new FlowchartLayoutEdgeRoute
        {
            EdgeId = "india->vendorOp:4", SourceId = "india", TargetId = "vendorOp",
            ExitSide = "top", EntrySide = "bottom", ExitNormalY = 1, EntryNormalY = -1
        };
        Sibling.Points.Add(new() { X = 6015, Y = 90 });
        Sibling.Points.Add(new() { X = 5770, Y = 2310 });
        Layout.EdgeRoutesMutable.Add(Sibling);
        (double X, double Y)[] SiblingBefore = [.. Sibling.Points.Select(Point => (Point.X, Point.Y))];
        using IDisposable Active = FlowchartRenderContext.Activate(Layout.RenderContext);
        FlowchartRoutingHints.Apply(Layout, Contract, Bounds, Contract.Nodes.ToDictionary(Node => Node.Id),
            MermaidRecognition.Kinds.Flowchart.FlowchartDirection.TopToBottom,
            RepairAutomaticRoutesEnabled: false, ReportUnroutedRoutes: true, OnlyRoutes: new HashSet<FlowchartLayoutEdgeRoute> { Route });
        if (Route.Points.Count < 2)
        {
            TestContext.Out.WriteLine("CAPTURED_HINT_FAILURE|" + JsonSerializer.Serialize(new
                { Layout.RenderContext.LayoutStageViolations, Bands = FlowchartFrameTitleBandHelper.Build(Contract, Bounds) }));
        }

        Assert.That(Route.Points.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That((Route.Points[0].X, Route.Points[0].Y), Is.EqualTo((5690d, 2310d)));
        Assert.That((Route.Points[^1].X, Route.Points[^1].Y), Is.EqualTo((4935d, 2430d)));
        Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route,
            Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()), Is.True);
        Assert.That(Layout.RenderContext.LayoutStageViolations.Where(Item => Item.Kind == "NoLegalRoute"), Is.Empty);
        Assert.That(Sibling.Points.Select(Point => (Point.X, Point.Y)), Is.EqualTo(SiblingBefore));
        RoutingHintContract Hint = Contract.Edges.Single(Edge => Edge.Id == Route.EdgeId).RoutingHint!;
        Assert.That(Hint.IsActive, Is.True);
        Assert.That(Hint.UnhonouredReason, Is.Null);
    }

    /// <summary>Returns immutable captured routing bounds, including the obstacles and title strips that defeated padded hint search.</summary>
    /// <returns>A private complete node/frame inventory for the captured connector test.</returns>
    private static Dictionary<string, FlowchartLayoutNodeBounds> CapturedBounds()
    {
        (string Id, double X, double Y, double Width, double Height)[] Values =
        [
            ("visualiser", 915, 2385, 330, 90), ("analyst", 1635, 2385, 330, 90),
            ("collaborator", 1995, 2385, 330, 90), ("dataMgr", 2355, 2385, 330, 90),
            ("admin", 2715, 2385, 330, 90), ("owner", 3855, 2385, 330, 90),
            ("secCustodian", 4215, 2385, 330, 90), ("platCustodian", 4575, 2385, 330, 90),
            ("devCustodian", 4935, 2385, 330, 90), ("australia", 5655, 4785, 330, 90),
            ("india", 6015, 45, 330, 90), ("nz", 6375, 2385, 330, 90),
            ("publicUser", 3465, 2385, 330, 90), ("explorer", 165, 2385, 330, 90),
            ("sdt", 915, 2145, 330, 90), ("cyber", 525, 2385, 330, 90),
            ("support", 3105, 2385, 330, 90), ("azure", 915, 1995, 330, 90),
            ("primary", 915, 1845, 330, 90), ("secondary", 1275, 1845, 330, 90),
            ("locNsw", 915, 1695, 330, 90), ("locVic", 1275, 1695, 330, 90),
            ("trusted", 5250, 2370, 240, 120), ("landgateOp", 5490, 2355, 240, 150),
            ("vendorOp", 5730, 2370, 240, 120), ("regRoles", 1815, 2415, 2190, 210),
            ("mgmtRoles", 4395, 2430, 1470, 240)
        ];
        return Values.ToDictionary(Value => Value.Id, Value => new FlowchartLayoutNodeBounds
        {
            NodeId = Value.Id, CenterX = Value.X, CenterY = Value.Y, Width = Value.Width, Height = Value.Height
        }, StringComparer.Ordinal);
    }

    /// <summary>Requires the A167 vendor assignment to survive complete layout on its declared south and north ports.</summary>
    [Test]
    public void A167_HintedVendorAssignmentRemainsDrawnAndLegal()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A167");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Route => Route.SourceId == "vendorOp" && Route.TargetId == "devCustodian");
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
        Assert.That(Layout.EdgeRoutes, Has.Count.EqualTo(Contract.Edges.Count));
        Assert.That(Route.Points.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(Route.ExitSide, Is.EqualTo("bottom"));
        Assert.That(Route.EntrySide, Is.EqualTo("top"));
        Assert.That(FlowchartAllocatedPortFallback.IsLegal(Contract, Bounds, Route,
            Route.Points.Select(Point => (Point.X, Point.Y)).ToArray()), Is.True);
        Assert.That(Layout.RenderContext.LayoutStageViolations.Where(Item => Item.Kind == "NoLegalRoute"), Is.Empty);
        RoutingHintContract Hint = Contract.Edges.Single(Edge => Edge.SourceId == "vendorOp" && Edge.TargetId == "devCustodian").RoutingHint!;
        Assert.That(Hint.IsActive, Is.True);
        Assert.That(Hint.UnhonouredReason, Is.Null);
    }
}
