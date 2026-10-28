using System.Reflection;
using System.Text.Json;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Exercises exact rejection rollback on the original sources that exposed empty completed trial paths.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellCompletedCandidateRollbackTests
{
    /// <summary>Selects the two named failed-run cases and the same empty-path fault exposed by the full census.</summary>
    /// <returns>Original catalogue declarations, with no hint or source edits.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (TestCaseData Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            RouteRuleCatalogueAuditTests.Scenario Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Scenario.Id is "A58" or "A138" or "EIP84")
            {
                yield return new TestCaseData(Scenario).SetName("CellCompletedRollback_" + Scenario.Id);
            }
        }
    }

    /// <summary>Rejects a real fully completed trial and compares all regions, gaps, ports, routes, frames and painter caches exactly.</summary>
    /// <param name="Scenario">Unchanged original scenario.</param>
    [TestCaseSource(nameof(Cases))]
    public void RejectedCompletedCandidateRestoresTheWholeLayout(RouteRuleCatalogueAuditTests.Scenario Scenario)
        => VerifyRejectedCandidate(Scenario, false);

    /// <summary>Recovery rechecks an already charged assignment without extending the sixteen distinct-candidate budget or leaking its completed route.</summary>
    /// <param name="Scenario">Original source with unchanged hints.</param>
    [TestCaseSource(nameof(Cases))]
    public void RejectedReferenceRecoveryRestoresTheWholeLayout(RouteRuleCatalogueAuditTests.Scenario Scenario)
        => VerifyRejectedCandidate(Scenario, true);

    /// <summary>Runs one forced completed rejection through either the ordinary search or exact recovery and compares all saved state.</summary>
    /// <param name="Scenario">Original catalogue source.</param>
    /// <param name="Recovery">Whether the assignment was already charged against the candidate limit.</param>
    private static void VerifyRejectedCandidate(RouteRuleCatalogueAuditTests.Scenario Scenario, bool Recovery)
    {
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, ObserveCompletedState: State =>
        {
            FlowchartLayoutResult Layout = (FlowchartLayoutResult)State.GetType().GetProperty("Copy")!.GetValue(State)!;
            // This is a fresh test invocation of the same bounded search, not extra
            // production budget. The original full pipeline has already completed.
            FlowchartRenderContext.RepairSearchBudget Budget = Layout.RenderContext.MoveBeforeDetourBudget;
            Budget.CandidateMoves = Recovery ? FlowchartLayoutRules.RepairCandidateMoves : 0; Budget.AcceptedMoves = 0; Budget.AttemptedMoves.Clear();
            State.GetType().GetProperty("CellFinalRoutingReady")!.SetValue(State, true);
            List<DiagramGridLayout.GridNode> Nodes = (List<DiagramGridLayout.GridNode>)State.GetType().GetProperty("Placed")!.GetValue(State)!;
            FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanMove(Nodes, 0, 1);
            Assert.That(Plan, Is.Not.Null);
            Type Repair = typeof(DiagramGridLayout).GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!;
            Type OptionType = Repair.GetNestedType("CellRepairOption", BindingFlags.NonPublic)!;
            var Option = Activator.CreateInstance(OptionType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, ["real-source-rejection", "complete diagram", Plan!], null)!;
            Array Options = Array.CreateInstance(OptionType, 1); Options.SetValue(Option, 0);
            string Before = Snapshot(State);
            Action<string, string, double, bool>? PreviousCandidate = DiagramGridLayout.RouteRepairCandidateObserver;
            DiagramGridLayout.RouteRepairCandidateObserver = (Kind, _, _, _) =>
            {
                if (Kind == "cell-move")
                {
                    throw new InvalidOperationException("reject-real-completed-candidate");
                }
            };
            try
            {
                TargetInvocationException? Failure = Assert.Throws<TargetInvocationException>(() => Repair
                    .GetMethod(Recovery ? "SearchCellAssignmentsCore" : "SearchCellAssignments", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, [ State, Options, FlowchartLayoutRules.RepairCandidateMoves, 1,
                        new HashSet<string>(), null, false,
                        .. Recovery ? [true] : Array.Empty<object?>(), ]));
                Assert.That(Failure!.InnerException!.Message, Is.EqualTo("reject-real-completed-candidate"));
                Assert.That(Snapshot(State), Is.EqualTo(Before));
                Assert.That(Budget.CandidateMoves, Is.LessThanOrEqualTo(FlowchartLayoutRules.RepairCandidateMoves));
            }
            finally { DiagramGridLayout.RouteRepairCandidateObserver = PreviousCandidate; }
        });
    }

    /// <summary>Serializes authoritative regions and every derived object whose geometry a rejected candidate could publish.</summary>
    /// <param name="State">Actual completed cell engine state.</param>
    /// <returns>Exact ordered values, independent of reference identity and the intentionally spent candidate budget.</returns>
    private static string Snapshot(object State)
    {
        Type Type = State.GetType();
        FlowchartLayoutResult Layout = (FlowchartLayoutResult)Type.GetProperty("Copy")!.GetValue(State)!;
        List<DiagramGridLayout.GridNode> Nodes = (List<DiagramGridLayout.GridNode>)Type.GetProperty("Placed")!.GetValue(State)!;
        List<DiagramGridLayout.GridFrame> Frames = (List<DiagramGridLayout.GridFrame>)Type.GetProperty("Frames")!.GetValue(State)!;
        FlowchartLogicalGrid.Snapshot Cells = FlowchartCellRepair.Capture(Nodes);
        return JsonSerializer.Serialize(new
        {
            Regions = Cells.Regions.Select(Pair => new { Pair.Key, Pair.Value.StartRow, Pair.Value.StartColumn, Pair.Value.RowSpan, Pair.Value.ColumnSpan }),
            Cells.ColumnGaps, Cells.RowGaps,
            Frames = Frames.Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom }),
            Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes,
            Guides = Layout.ResolvedEdgeGuides?.Select(Guide => new { Guide.Edge.Id, Points = Guide.Points.Select(Point => new[] { Point.X, Point.Y }), Guide.RouteKind }),
            CacheGeometry = Layout.RenderContext.CachedRouteGeometry,
            CacheGuides = Layout.RenderContext.CachedRouteGuides?.Select(Guide => new { Guide.Edge.Id, Points = Guide.Points.Select(Point => new[] { Point.X, Point.Y }), Guide.RouteKind }),
            Layout.RenderContext.LayoutStageTrace, Layout.RenderContext.LayoutStageViolations
        });
    }
}
