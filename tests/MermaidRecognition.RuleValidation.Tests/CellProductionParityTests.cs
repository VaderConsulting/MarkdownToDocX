using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Compares the public production pipeline with the previously audited cells-on pipeline on every source.</summary>
[TestFixture, NonParallelizable, Explicit("Replays every catalogue scenario in the reference and public cell pipelines.")]
public sealed class CellProductionParityTests
{
    /// <summary>Uses all catalogue sources, including open-mode and native-ineligible cases.</summary>
    /// <returns>One independently reported case per source.</returns>
    public static IEnumerable<TestCaseData> Cases() => RouteRuleCatalogueAuditTests.Catalogue()
        .Select(Case => new TestCaseData(Case.Arguments[0]).SetName("CellProductionParity_" +
            ((RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Id));

    /// <summary>Requires exact completed geometry, route bends, findings and original hints after production activation.</summary>
    /// <param name="Scenario">Unchanged source and catalogue identity.</param>
    [TestCaseSource(nameof(Cases))]
    public void ProductionMatchesAuditedCells(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract? ReferenceContract = null;
        FlowchartLayoutResult? Reference = null;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, false, (Contract, Layout) =>
        { ReferenceContract = Contract; Reference = Layout; });
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true };
        bool Complete = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out FlowchartLayoutResult? Production);
        Assert.That(Complete, Is.EqualTo(Reference != null));
        if (!Complete)
        {
            return;
        }

        Assert.That(Snapshot(Contract, Production), Is.EqualTo(Snapshot(ReferenceContract!, Reference!)),
            "Production activation must not alter the last audited cell layout.");
        Assert.That(Context.RejectedCellPixelWrites, Is.Empty);
        if (Production.CellNodes != null)
        {
            Assert.That(CellGridAudit.Faults(Contract, Production, Production.CellNodes), Is.Empty);
            Assert.That(FlowchartPlacementHints.CellHintsMatch(Contract, [.. Production.CellNodes],
                FlowchartCellRepair.Capture(Production.CellNodes).Regions), Is.True);
        }
        TestContext.Out.WriteLine($"CELL_PRODUCTION_PARITY|{Scenario.Id}|bends={Production.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points))}|equal=True");
    }

    /// <summary>Serializes only published geometry and findings, excluding mutable caches and timing diagnostics.</summary>
    /// <param name="Contract">Parsed original hint and shape model.</param>
    /// <param name="Layout">Completed published model.</param>
    /// <returns>An ordinal geometry and finding signature.</returns>
    internal static string Snapshot(DiagramContract Contract, FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new
    {
        Layout.NodeBounds, Layout.ClusterBounds, Layout.EdgeRoutes,
        Guides = Layout.ResolvedEdgeGuides?.Select(Guide => new
        {
            Guide.Edge.Id, Guide.Edge.SourceId, Guide.Edge.TargetId, Guide.RouteKind,
            Points = Guide.Points.Select(Point => new[] { Point.X, Point.Y })
        }),
        Findings = FlowchartRouteRuleChecker.Check(Contract, Layout),
        Diagnostics = Layout.RenderContext.LayoutStageViolations,
        Hints = Contract.Nodes.Select(Node => Node.PlacementHint).Concat(Contract.Groups.Select(Group => Group.PlacementHint)),
        RouteHints = Contract.Edges.Select(Edge => Edge.RoutingHint)
    });
}
