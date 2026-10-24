using System.Text.Json;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <remarks>Deliberately compares the cells-off compatibility path with cells-on state; this is not a production-only quality baseline.</remarks>
/// <summary>Measures the fixed original worse-fifty after adding joint fan and chain assignments.</summary>
[TestFixture, NonParallelizable, Explicit("Runs both layout modes on the original fifty bend regressions.")]
public sealed class CellJointMoveBendReviewTests
{
    /// <summary>Frozen pre-change measurements, independent of whether later candidates improve a case.</summary>
    private static readonly Dictionary<string, (string Group, int Off, int On)> Baselines = new()
    {
        ["F30"] = ("shared-fan", 2, 3),
        ["F61"] = ("main-chain", 0, 4),
        ["F120"] = ("shared-fan", 2, 3),
        ["F136"] = ("other", 8, 14),
        ["A16"] = ("main-chain", 0, 2),
        ["A21"] = ("shared-fan", 2, 4),
        ["A22"] = ("fan-in", 2, 7),
        ["A29"] = ("main-chain", 5, 10),
        ["A30"] = ("main-chain", 0, 2),
        ["A37"] = ("shared-fan", 2, 6),
        ["A40"] = ("shared-fan", 2, 4),
        ["A41"] = ("fan-in", 0, 2),
        ["A42"] = ("fan-in", 0, 4),
        ["A46"] = ("fan-in", 15, 16),
        ["A47"] = ("other", 6, 8),
        ["A72"] = ("fan-in", 8, 9),
        ["A82"] = ("fan-in", 0, 2),
        ["A98"] = ("main-chain", 2, 4),
        ["A103"] = ("shared-fan", 0, 8),
        ["A110"] = ("main-chain", 150, 180),
        ["A112"] = ("main-chain", 31, 37),
        ["A114"] = ("other", 108, 143),
        ["A117"] = ("other", 99, 138),
        ["A132"] = ("fan-in", 2, 7),
        ["A134"] = ("fan-in", 2, 4),
        ["A136"] = ("main-chain", 4, 5),
        ["A141"] = ("main-chain", 11, 12),
        ["A161"] = ("shared-fan", 24, 26),
        ["A172"] = ("shared-fan", 0, 2),
        ["A173"] = ("fan-in", 0, 6),
        ["A174"] = ("main-chain", 0, 2),
        ["EIP36"] = ("shared-fan", 4, 5),
        ["EIP46"] = ("shared-fan", 4, 8),
        ["EIP63"] = ("shared-fan", 4, 8),
        ["EIP83"] = ("shared-fan", 1, 2),
        ["EIP99"] = ("shared-fan", 0, 2),
        ["EIP131"] = ("shared-fan", 0, 2),
        ["EIP162"] = ("shared-fan", 4, 8),
        ["EIP211"] = ("main-chain", 0, 2),
        ["EIP215"] = ("shared-fan", 7, 12),
        ["EIP225"] = ("shared-fan", 0, 8),
        ["EIP231"] = ("shared-fan", 0, 2),
        ["EIP240"] = ("shared-fan", 0, 8),
        ["EIP241"] = ("main-chain", 0, 2),
        ["EIP245"] = ("shared-fan", 0, 8),
        ["EIP246"] = ("shared-fan", 7, 12),
        ["EIP261"] = ("fan-in", 2, 4),
        ["EIP280"] = ("shared-fan", 4, 8),
        ["EIP315"] = ("shared-fan", 0, 8),
        ["EIP342"] = ("shared-fan", 0, 2),
    };

    /// <summary>Selects the original fifty sources and keeps the original cause groups for before/after accounting.</summary>
    /// <returns>Each source, historical group and measured bend baselines.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Baselines.TryGetValue(Scenario.Id, out var Before))
                yield return new TestCaseData(Scenario, Before.Group, Before.Off, Before.On).SetName("CellJointBendReview_" + Scenario.Id);
        }
    }

    /// <summary>Compares completed original-source layouts and reports which candidate families actually reached scoring.</summary>
    /// <param name="Scenario">Original catalogue source with all original hints.</param>
    /// <param name="Group">Historical regression group.</param>
    /// <param name="ExpectedOff">Previously verified cells-off bend count.</param>
    /// <param name="BeforeOn">Cells-on bend count before the joint candidate change.</param>
    [TestCaseSource(nameof(Cases))]
    public void CompareFinishedJointMoves(RouteRuleCatalogueAuditTests.Scenario Scenario, string Group, int ExpectedOff, int BeforeOn)
    {
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new FlowchartRenderContext { UseCellRegions = false }, out var Off), Is.True);
        int OffBends = Off.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
        Assert.That(OffBends, Is.EqualTo(ExpectedOff));
        var Counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var Available = new Dictionary<string, int>(StringComparer.Ordinal);
        var ProbeContract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        var Previous = FlowchartRenderContext.OperationCountObserver;
        FlowchartRenderContext.OperationCountObserver = (Name, Count) =>
        {
            Previous?.Invoke(Name, Count);
            if (Name.StartsWith("cell-candidate/", StringComparison.Ordinal) || Name.StartsWith("cell-winner/", StringComparison.Ordinal))
                Counts[Name] = Counts.GetValueOrDefault(Name) + Count;
        };
        try
        {
            MermaidRecognition.Contract.DiagramContract? CompletedContract = null;
            FlowchartLayoutResult? CompletedLayout = null;
            CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (OnContract, On) =>
            {
                CompletedContract = OnContract; CompletedLayout = On;
                int After = On.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
                TestContext.Out.WriteLine($"CELL_JOINT_BENDS|{Scenario.Id}|group={Group}|off={OffBends}|before={BeforeOn}|on={After}|candidates={On.RenderContext.MoveBeforeDetourBudget.CandidateMoves}|counts={JsonSerializer.Serialize(Counts)}|available={JsonSerializer.Serialize(Available)}");
                var Findings = FlowchartRouteRuleChecker.Check(OnContract, On);
                Assert.That(Findings.Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
                Assert.That(On.RenderContext.MoveBeforeDetourBudget.CandidateMoves, Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
                Assert.That(After, Is.LessThanOrEqualTo(BeforeOn), "Adding a candidate family must not lose an already better complete option.");
            }, ObserveBoundNodes: Nodes =>
            {
                Assert.That(CellGridAudit.Faults(CompletedContract!, CompletedLayout!, Nodes), Is.Empty);
                Assert.That(FlowchartPlacementHints.CellHintsMatch(CompletedContract!, Nodes.ToList(), FlowchartCellRepair.Capture(Nodes).Regions), Is.True);
            }, PrepareBeforeRouting: Nodes =>
            {
                var Before = FlowchartCellRepair.Capture(Nodes);
                bool Horizontal = MermaidRecognition.Render.FlowchartDirectionNormalizer.Normalize(ProbeContract.Direction) is MermaidRecognition.Kinds.Flowchart.FlowchartDirection.LeftToRight
                    or MermaidRecognition.Kinds.Flowchart.FlowchartDirection.RightToLeft;
                foreach (var Family in DiagramGridLayout.CellJointRouteAssignments(ProbeContract, Nodes, Horizontal,
                    ProbeContract.Nodes.Select(Node => Node.Id).Concat(ProbeContract.Groups.Select(Group => Group.Id)).ToHashSet(StringComparer.Ordinal))
                    .GroupBy(Option => Option.Key.Split('|')[0])) Available[Family.Key] = Family.Count();
                Assert.That(FlowchartCellRepair.Capture(Nodes), Is.SameAs(Before));
            });
        }
        finally { FlowchartRenderContext.OperationCountObserver = Previous; }
    }
}
