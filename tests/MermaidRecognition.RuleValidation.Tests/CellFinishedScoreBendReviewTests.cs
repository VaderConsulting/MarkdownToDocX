using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <remarks>Deliberately compares the cells-off compatibility path with cells-on state; this is not a production-only quality baseline.</remarks>
/// <summary>Measures the original 81 bend increases against actual cells-off output without treating historical cell output as that baseline.</summary>
[TestFixture]
[NonParallelizable]
[Explicit("Runs the original 81 source scenarios in both layout modes for readiness reporting.")]
public sealed class CellFinishedScoreBendReviewTests
{
    /// <summary>Original reviewed set, preserved independently of whether a later run improves or worsens it.</summary>
    private static readonly HashSet<string> Reviewed = [
        "A100", "A103", "A107", "A11", "A110", "A112", "A114", "A116",
        "A117", "A125", "A132", "A134", "A136", "A141", "A142", "A150",
        "A151", "A16", "A161", "A17", "A170", "A171", "A172", "A173",
        "A174", "A18", "A20", "A21", "A22", "A26", "A28", "A29",
        "A30", "A31", "A37", "A38", "A40", "A41", "A42", "A46",
        "A47", "A48", "A50", "A72", "A79", "A82", "A98", "EIP131",
        "EIP141", "EIP142", "EIP146", "EIP162", "EIP189", "EIP194", "EIP211", "EIP215",
        "EIP225", "EIP231", "EIP240", "EIP241", "EIP245", "EIP246", "EIP261", "EIP280",
        "EIP285", "EIP295", "EIP315", "EIP342", "EIP36", "EIP46", "EIP63", "EIP73",
        "EIP77", "EIP83", "EIP99", "F120", "F136", "F137", "F30", "F59",
        "F61"
    ];

    /// <summary>Frozen finished-route bend counts measured at checkpoint 3d73b88.</summary>
    private static readonly IReadOnlyDictionary<string, int> CheckpointBends = new Dictionary<string, int>
    {
        ["A100"] = 12,
        ["A103"] = 8,
        ["A107"] = 14,
        ["A11"] = 2,
        ["A110"] = 180,
        ["A112"] = 37,
        ["A114"] = 143,
        ["A116"] = 78,
        ["A117"] = 138,
        ["A125"] = 4,
        ["A132"] = 7,
        ["A134"] = 4,
        ["A136"] = 5,
        ["A141"] = 12,
        ["A142"] = 322,
        ["A150"] = 2,
        ["A151"] = 2,
        ["A16"] = 2,
        ["A161"] = 26,
        ["A17"] = 2,
        ["A170"] = 2,
        ["A171"] = 4,
        ["A172"] = 2,
        ["A173"] = 6,
        ["A174"] = 2,
        ["A18"] = 20,
        ["A20"] = 3,
        ["A21"] = 4,
        ["A22"] = 7,
        ["A26"] = 9,
        ["A28"] = 5,
        ["A29"] = 10,
        ["A30"] = 2,
        ["A31"] = 8,
        ["A37"] = 6,
        ["A38"] = 5,
        ["A40"] = 4,
        ["A41"] = 2,
        ["A42"] = 4,
        ["A46"] = 16,
        ["A47"] = 8,
        ["A48"] = 12,
        ["A50"] = 2,
        ["A72"] = 9,
        ["A79"] = 2,
        ["A82"] = 2,
        ["A98"] = 4,
        ["EIP131"] = 2,
        ["EIP141"] = 4,
        ["EIP142"] = 6,
        ["EIP146"] = 2,
        ["EIP162"] = 8,
        ["EIP189"] = 2,
        ["EIP194"] = 3,
        ["EIP211"] = 2,
        ["EIP215"] = 12,
        ["EIP225"] = 8,
        ["EIP231"] = 2,
        ["EIP240"] = 8,
        ["EIP241"] = 2,
        ["EIP245"] = 8,
        ["EIP246"] = 12,
        ["EIP261"] = 4,
        ["EIP280"] = 8,
        ["EIP285"] = 2,
        ["EIP295"] = 2,
        ["EIP315"] = 8,
        ["EIP342"] = 2,
        ["EIP36"] = 5,
        ["EIP46"] = 8,
        ["EIP63"] = 8,
        ["EIP73"] = 2,
        ["EIP77"] = 2,
        ["EIP83"] = 2,
        ["EIP99"] = 2,
        ["F120"] = 3,
        ["F136"] = 14,
        ["F137"] = 6,
        ["F30"] = 3,
        ["F59"] = 6,
        ["F61"] = 4,
    };

    /// <summary>Uses original sources in stable catalogue order.</summary>
    /// <returns>The unchanged 81 reviewed declarations.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Reviewed.Contains(Scenario.Id)) yield return new TestCaseData(Scenario).SetName("CellBendReview_" + Scenario.Id);
        }
    }

    /// <summary>Compares completed routes in both modes and reports per-link bend deltas; known quality increases are measured rather than hidden as skipped tests.</summary>
    /// <param name="Scenario">Original source with its hints intact.</param>
    [TestCaseSource(nameof(Cases))]
    public void MeasuresBothFinishedLayouts(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new FlowchartRenderContext { UseCellRegions = false }, out var Off), Is.True);
        int OffBends = Off.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
        bool Completed = false;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (OnContract, On) =>
        {
            Completed = true;
            int OnBends = On.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
            Assert.That(OnBends, Is.LessThanOrEqualTo(CheckpointBends[Scenario.Id]),
                "Joint candidates must preserve every original-81 bend result at checkpoint 3d73b88.");
            Assert.That(FlowchartRouteRuleChecker.Check(OnContract, On)
                .Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
            Assert.That(On.RenderContext.MoveBeforeDetourBudget.CandidateMoves,
                Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            TestContext.Out.WriteLine($"CELL_FINISHED_BENDS|{Scenario.Id}|off={OffBends}|on={OnBends}|delta={OnBends - OffBends}|candidates={On.RenderContext.MoveBeforeDetourBudget.CandidateMoves}|accepted={On.RenderContext.MoveBeforeDetourBudget.AcceptedMoves}");
            foreach (var Route in On.EdgeRoutes)
            {
                var Previous = Off.EdgeRoutes.FirstOrDefault(Item => Item.EdgeId == Route.EdgeId);
                int Before = Previous == null ? 0 : FlowchartLayoutScore.CountCorners(Previous.Points);
                int After = FlowchartLayoutScore.CountCorners(Route.Points);
                if (Before != After) TestContext.Out.WriteLine($"CELL_FINISHED_LINK|{Scenario.Id}|{Route.EdgeId}|off={Before}|on={After}|sides={Previous?.ExitSide}/{Previous?.EntrySide}->{Route.ExitSide}/{Route.EntrySide}");
            }
        });
        Assert.That(Completed, Is.True);
    }
}