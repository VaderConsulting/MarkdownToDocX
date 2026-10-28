using System.Reflection;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Protects side assignment from incomplete trial routes exposed by A115 reference recovery.</summary>
[TestFixture, NonParallelizable]
public sealed class CellFacingAssignmentIncompleteRouteTests
{
    /// <summary>Replays A115 and leaves an incomplete candidate untouched for its owner to reject or recover.</summary>
    /// <param name="PointCount">Number of points retained in the discarded trial route.</param>
    [TestCase(0), TestCase(1)]
    public void A115IncompleteTrialCannotEnterFacingAssignment(int PointCount)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue()
            .Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!)
            .Single(Case => Case.Id == "A115");
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, ObserveCompletedState: State =>
        {
            FlowchartLayoutResult Layout = (FlowchartLayoutResult)State.GetType().GetProperty("Copy")!.GetValue(State)!;
            Assert.That(Layout.EdgeRoutes.All(Route => Route.Points.Count >= 2), Is.True,
                "The current production pipeline must complete A115 before the discarded-trial input is injected.");
            // Preserve a real side-repair opportunity while a different trial path is incomplete.
            FlowchartLayoutEdgeRoute Convergence = Layout.EdgeRoutes.GroupBy(Item => Item.TargetId).First(Group => Group.Count() > 1).First();
            Convergence.EntrySide = "invalid-trial-side";
            FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.First(Item => Item != Convergence);
            Route.Points.RemoveRange(PointCount, Route.Points.Count - PointCount);
            Assert.That(FlowchartRouteRuleChecker.Check(
                (MermaidRecognition.Contract.DiagramContract)State.GetType().GetProperty("Contract")!.GetValue(State)!, Layout)
                .Any(Finding => Finding.Rule == "4.11"), Is.True, "The guard must be exercised inside a nonempty side search.");
            string Before = System.Text.Json.JsonSerializer.Serialize(Layout);
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).ToDictionary(Box => Box.NodeId);
            Type Stage = typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!;
            MethodInfo Repair = Stage.GetMethod("RepairFinalFacingSides", BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.DoesNotThrow(() => Repair.Invoke(null, [State, Bounds, null]));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(Layout), Is.EqualTo(Before),
                "Side optimisation must not mutate an incomplete trial; the enclosing candidate owns its recovery.");
        });
    }
}
