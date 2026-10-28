using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks complete nesting alternatives on the original marked stars before integrating routing changes.</summary>
[TestFixture, NonParallelizable]
public sealed class StarFanNestingCatalogueTests
{
    private bool PreviousCells;

    /// <summary>Uses production cells for enterprise entry points that construct their own context.</summary>
    [SetUp]
    public void UseProductionCells()
    {
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out PreviousCells);
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
    }

    /// <summary>Restores the compatibility setting so unrelated legacy fixtures retain their original geometry.</summary>
    [TearDown]
    public void RestoreCellSwitch() => AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, PreviousCells);
    /// <summary>Selects the original sixteen-way fan-in and fan-out without altering their sources.</summary>
    /// <returns>Both marked enterprise stars.</returns>
    public static IEnumerable<TestCaseData> Cases() => RouteRuleCatalogueAuditTests.Catalogue()
        .Where(Case => ((RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Id is "A104" or "A105")
        .Select(Case => new TestCaseData(Case.Arguments[0]).SetName("StarNesting_" + ((RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Id));

    /// <summary>Selects an ordinary branch and a convergence containing parallel links, neither a distinct-leaf star.</summary>
    /// <returns>Both controls whose existing straight routes must remain unchanged.</returns>
    public static IEnumerable<TestCaseData> NonStarCases() => RouteRuleCatalogueAuditTests.Catalogue()
        .Where(Case => ((RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Id is "A22" or "A151")
        .Select(Case => new TestCaseData(Case.Arguments[0]).SetName("NonStarSidePolicy_" + ((RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Id));

    /// <summary>Keeps ordinary branches and parallel-link convergence outside the star overflow exception.</summary>
    /// <param name="Scenario">Unchanged catalogue control whose routes were all straight before the amendment.</param>
    [TestCaseSource(nameof(NonStarCases))]
    public void NonStarControlsKeepTheirStraightRoutes(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        Assert.That(FlowchartStarFanNesting.Measure(Layout.EdgeRoutes).Bends, Is.Zero);
    }

    /// <summary>An unbound preparatory or compatibility model cannot claim the logical-grid fan overflow exception.</summary>
    /// <param name="Scenario">Completed marked fan whose same routes are checked without cell authority.</param>
    [TestCaseSource(nameof(Cases))]
    public void OverflowRequiresAuthoritativeCellRegions(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        IReadOnlyList<DiagramGridLayout.GridNode>? Regions = Layout.CellNodes;
        try
        {
            Layout.CellNodes = null;
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Any(Finding => Finding.Rule is "4.11" or "5.8"), Is.True);
        }
        finally { Layout.CellNodes = Regions; }
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "4.11" or "5.8"), Is.Empty);
    }

    /// <summary>Requires zero same-fan crossings, exact legal slots and no extra bends after a complete assignment.</summary>
    /// <param name="Scenario">Unchanged catalogue source.</param>
    [TestCaseSource(nameof(Cases))]
    public void OriginalStarNestsWithoutCrossings(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        (int Bends, int Crossings, double Shared) Before = FlowchartStarFanNesting.Measure(Layout.EdgeRoutes);
        FlowchartStarFanNesting.Result Result = FlowchartStarFanNesting.Apply(Contract, Layout);
        (int Bends, int Crossings, double Shared) After = FlowchartStarFanNesting.Measure(Layout.EdgeRoutes);
        TestContext.Out.WriteLine($"STAR_NESTING|{Scenario.Id}|beforeBends={Before.Bends}|beforeCrossings={Before.Crossings}|afterBends={After.Bends}|afterCrossings={After.Crossings}|kept={Result.Kept}|rejected={Result.Rejected}");
        foreach (FlowchartRouteRuleChecker.Finding Finding in FlowchartRouteRuleChecker.Check(Contract, Layout))
        {
            TestContext.Out.WriteLine($"STAR_FINDING|{Scenario.Id}|{Finding}");
        }

        Assert.That(After.Crossings, Is.Zero);
        Assert.That(After.Shared, Is.Zero);
        Assert.That(After.Bends, Is.LessThanOrEqualTo(Before.Bends));
        Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "4.11" or "5.8"), Is.Empty);
    }

    /// <summary>Rejects a far-side terminal and restores the complete model when a fan option is discarded.</summary>
    /// <param name="Scenario">Original isolated fan with a legal nested overflow assignment.</param>
    [TestCaseSource(nameof(Cases))]
    public void FarSideIsStillForbiddenAndRejectedFanRestoresExactly(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        string Before = CellProductionParityTests.Snapshot(Contract, Layout);
        FlowchartLayoutEdgeRoute Live = Layout.EdgeRoutes.First();
        FlowchartLayoutEdgeRoute Trial = FlowchartStarFanNesting.Clone(Live);
        bool Outgoing = Scenario.Id == "A104";
        string Far = (Outgoing ? Live.ExitSide : Live.EntrySide) == "top" ? "bottom" : "top";
        if (Outgoing)
        {
            Trial.ExitSide = Far;
        }
        else
        {
            Trial.EntrySide = Far;
        }

        Assert.That(FlowchartStarFanNesting.TryPublish(Contract, Layout,
            new Dictionary<FlowchartLayoutEdgeRoute, FlowchartLayoutEdgeRoute> { [Live] = Trial }), Is.False);
        Assert.That(CellProductionParityTests.Snapshot(Contract, Layout), Is.EqualTo(Before));

        string Side = Outgoing ? Live.ExitSide : Live.EntrySide;
        try
        {
            if (Outgoing)
            {
                Live.ExitSide = Far;
            }
            else
            {
                Live.EntrySide = Far;
            }

            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
                .Any(Finding => Finding.Subject == Live.EdgeId && Finding.Rule == (Outgoing ? "5.8" : "4.11")), Is.True);
        }
        finally { if (Outgoing)
            {
                Live.ExitSide = Side;
            }
            else
            {
                Live.EntrySide = Side;
            }
        }
    }

    /// <summary>The overflow exception is unavailable when another fan branch crosses or shares its track.</summary>
    /// <param name="Scenario">Unchanged source for fan-in or fan-out.</param>
    [TestCaseSource(nameof(Cases))]
    public void SharedFanTrackDoesNotQualifyForTheSideException(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
        FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes[0];
        FlowchartLayoutEdgeRoute Other = Layout.EdgeRoutes[1];
        Other.Points.Clear();
        Other.Points.AddRange(Route.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Any(Finding => Finding.Rule == (Scenario.Id == "A104" ? "5.8" : "4.11")), Is.True);
    }

    /// <summary>Checks the actual enterprise painter paths and label rectangles, including all sixteen ledger labels.</summary>
    /// <param name="Scenario">Original marked enterprise document.</param>
    [TestCaseSource(nameof(Cases))]
    public void PaintedFanLabelsStayOnTheLeafRunAndOffOtherLines(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        using IDisposable Scope = FlowchartRenderContext.Activate(new() { UseCellRegions = true });
        EnterpriseDocument Document = EnterpriseValidator.Analyze(Scenario.Source).Document!;
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        Assert.That(Layout.RoutingContext, Is.Not.Null);
        Assert.That(Layout.RoutingContext!.Layout.RenderContext.UseCellRegions, Is.True);
        List<EnterpriseRoutedEdge> Routes = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes, Layout.RoutingContext!);
        Assert.That(Routes, Has.Count.EqualTo(16));
        for (int A = 0; A < Routes.Count; A++)
        {
            for (int B = A + 1; B < Routes.Count; B++)
            {
                Assert.That(FlowchartBranchRouteOrder.Intersections(Routes[A].SvgPoints, Routes[B].SvgPoints).Crossings, Is.Zero);
            }
        }

        EnterpriseRoutedEdge[] Labelled = [.. Routes.Where(Route => !string.IsNullOrWhiteSpace(Route.Label))];
        Assert.That(Labelled, Has.Length.EqualTo(Scenario.Id == "A105" ? 16 : 0));
        foreach (EnterpriseRoutedEdge? Route in Labelled)
        {
            Assert.That(Route.LabelBreak, Is.Not.Null);
            Assert.That(Route.LabelBreak!.SegmentStartIndex, Is.EqualTo(0), "A105 labels belong to the source leaf-side segment.");
            foreach (EnterpriseRoutedEdge? Other in Routes.Where(Other => Other != Route))
            {
                for (int Segment = 1; Segment < Other.SvgPoints.Count; Segment++)
            {
                    (double X, double Y) P = Other.SvgPoints[Segment - 1]; (double X, double Y) Q = Other.SvgPoints[Segment];
                bool Overlap = Math.Max(P.X, Q.X) > Route.LabelX - (Route.LabelWidth / 2)
                    && Math.Min(P.X, Q.X) < Route.LabelX + (Route.LabelWidth / 2)
                    && Math.Max(P.Y, Q.Y) > Route.LabelY - (Route.LabelHeight / 2)
                    && Math.Min(P.Y, Q.Y) < Route.LabelY + (Route.LabelHeight / 2);
                Assert.That(Overlap, Is.False, $"{Other.Relationship.SourceId} passes through a fan label.");
            }
            }
        }
    }
}
