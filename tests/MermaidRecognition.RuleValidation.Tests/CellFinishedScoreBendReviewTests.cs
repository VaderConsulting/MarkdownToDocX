using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

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
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new FlowchartRenderContext(), out var Off), Is.True);
        int OffBends = Off.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
        bool Completed = false;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (OnContract, On) =>
        {
            Completed = true;
            int OnBends = On.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points));
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