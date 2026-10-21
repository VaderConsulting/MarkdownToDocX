using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Prevents production band layouts from exceeding the committed A110 track and A107 port findings.</summary>
[TestFixture, NonParallelizable]
public sealed class IntroducedStressFaultRegressionTests
{
    /// <summary>The completed A116 allocation must not be discarded in favour of the known more expensive ordinary fallback.</summary>
    [Test]
    public void A116KeepsCheaperCompleteAssignmentWithLineSpacedPorts()
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A116");
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
        Assert.Multiple(() =>
        {
            Assert.That(BandRowDriftMeasurements.PhysicalCrossings(Layout), Is.LessThanOrEqualTo(51));
            Assert.That(DiagramGridLayout.CloseCorridorSegmentPairs(Layout), Is.LessThanOrEqualTo(8));
            Assert.That(Score.OptimizationScore, Is.LessThanOrEqualTo(3363.000097054));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding =>
                Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
        });
        var Ports = Layout.EdgeRoutes.SelectMany(Route => new[] {
            (Id: Route.SourceId, Side: Route.ExitSide, Point: Route.Points[0]),
            (Id: Route.TargetId, Side: Route.EntrySide, Point: Route.Points[^1]) });
        foreach (var Group in Ports.GroupBy(Port => (Port.Id, Port.Side)))
        {
            var Ends = Group.ToArray();
            for (int I = 0; I < Ends.Length; I++) for (int J = I + 1; J < Ends.Length; J++)
                Assert.That(Math.Sqrt(Math.Pow(Ends[I].Point.X - Ends[J].Point.X, 2)
                    + Math.Pow(Ends[I].Point.Y - Ends[J].Point.Y, 2)),
                    Is.GreaterThanOrEqualTo(FlowchartLayoutRules.LineSpacing - 0.001),
                    Group.Key + " still contains under-spaced allocated ports.");
        }
    }

    /// <summary>A locally cheaper track choice cannot replace a better complete route assignment or introduce a port fault elsewhere.</summary>
    /// <param name="Id">Production control with a rejected reserved-track assignment.</param>
    /// <param name="MaximumBends">Current complete-assignment bend ceiling.</param>
    /// <param name="MaximumCrossings">Current complete-assignment physical crossing ceiling.</param>
    /// <param name="MaximumShared">Current complete-assignment shared-pair ceiling.</param>
    [TestCase("A117", 114, 61, 8), TestCase("A38", 11, 2, 0)]
    public void CompleteCorridorAssignmentRejectsLocalRegressions(string Id, int MaximumBends, int MaximumCrossings, int MaximumShared)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        Assert.Multiple(() =>
        {
            Assert.That(Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Is.LessThanOrEqualTo(MaximumBends));
            Assert.That(BandRowDriftMeasurements.PhysicalCrossings(Layout), Is.LessThanOrEqualTo(MaximumCrossings));
            Assert.That(Findings.Count(Finding => Finding.Rule == "5.1"), Is.LessThanOrEqualTo(MaximumShared));
            Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
            // The guard keeps the lowest-scoring complete legal candidate: A38 has 8 bends, 0 crossings and score 229.97.
            Assert.That(Layout.RenderContext.BandSpaceDiagnostics,
                Has.Some.Contains(Id == "A38" ? "CORRIDOR_ASSIGNMENT|keepReserved=True" : "CORRIDOR_ASSIGNMENT|keepReserved=False"));
        });
    }

    /// <summary>Gap reservation must retain A115's pre-sizing crossing ceiling and A46's nested equal-rank fan.</summary>
    /// <param name="Id">Production Enterprise diagram.</param>
    /// <param name="MaximumCrossings">Physical crossing ceiling before initial gap sizing.</param>
    /// <param name="MaximumBends">Pre-sizing bend ceiling, including the unrelated curved-junction control.</param>
    [TestCase("A115", 67, 85), TestCase("A46", 0, 12), TestCase("A47", 0, 5)]
    public void InitialCorridorSizingDoesNotAddCrossings(string Id, int MaximumCrossings, int MaximumBends)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(BandRowDriftMeasurements.PhysicalCrossings(Layout), Is.LessThanOrEqualTo(MaximumCrossings));
            Assert.That(Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Is.LessThanOrEqualTo(MaximumBends));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "10" or "3.1" or "4.1" or "3.2/4.7"), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
        });
    }

    /// <summary>Retains the committed limits and the shared stress watchdog classification.</summary>
    /// <returns>The two production regressions, with unchanged 180-second stress deadlines.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        yield return CatalogueScenarioPolicy.Apply(new TestCaseData("A110", 58)
            .SetName("ProductionA110_SharedTracksDoNotExceedCe6f1ce"), "A110");
        yield return CatalogueScenarioPolicy.Apply(new TestCaseData("A107", 2)
            .SetName("ProductionA107_PortFindingsDoNotExceedCe6f1ce"), "A107");
    }

    /// <summary>Checks the finished production layout, including routed wraps, port assignment and every final repair.</summary>
    /// <param name="Id">Enterprise stress scenario whose committed finding ceiling is protected.</param>
    /// <param name="Maximum">Measured finding count from the committed ce6f1ce production engine.</param>
    [TestCaseSource(nameof(Cases))]
    public void ProductionFindingsDoNotExceedCommittedEngine(string Id, int Maximum)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        string[] PortRules = ["3.1", "3.2/4.7", "3.3/5.2", "4.2", "4.11", "5.8"];
        var Relevant = Findings.Where(Finding => Id == "A110" ? Finding.Rule == "5.1" : PortRules.Contains(Finding.Rule)).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(Relevant.Length, Is.LessThanOrEqualTo(Maximum),
                Id + " exceeds the committed production finding count: " + string.Join("; ", Relevant.Select(Finding => Finding.Subject)));
            Assert.That(Findings.Where(Finding => Finding.Rule == "3.2/4.7"), Is.Empty);
            Assert.That(RouteRuleCatalogueAuditTests.OffSlotPortFaults(Contract, Layout), Is.Empty);
        });
    }
}
