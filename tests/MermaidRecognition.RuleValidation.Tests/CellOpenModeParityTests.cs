using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <remarks>Deliberately compares the cells-off compatibility path with cells-on state; this is not a production-only quality baseline.</remarks>
/// <summary>Checks that experimental cell activation leaves all existing open-mode sources on the unchanged unbound path.</summary>
[TestFixture]
[NonParallelizable]
public sealed class CellOpenModeParityTests
{
    /// <summary>Selects the six original open-mode sources from the catalogue.</summary>
    /// <returns>Unmodified open-mode scenarios.</returns>
    public static IEnumerable<TestCaseData> Cases()
    {
        HashSet<string> Open = ["F145", "F147", "F149", "A144", "A146", "A148"];
        foreach (TestCaseData Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            RouteRuleCatalogueAuditTests.Scenario Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (Open.Contains(Scenario.Id))
            {
                yield return new TestCaseData(Scenario).SetName("CellOpenParity_" + Scenario.Id);
            }
        }
    }

    /// <summary>Compares exact node/frame dimensions, route coordinates and port sides without tolerances.</summary>
    /// <param name="Scenario">Original open-mode diagram.</param>
    [TestCaseSource(nameof(Cases))]
    public void OpenLayoutStaysUnboundAndIdentical(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new FlowchartRenderContext { UseCellRegions = false }, out FlowchartLayoutResult? Before), Is.True);
        string? Actual = null;
        CellGuardCatalogueProbeTests.RunBoundCatalogue(Scenario, true, (_, Layout) =>
        {
            Assert.That(Layout.CellWriteCoverage?.BoundNodes ?? 0, Is.Zero);
            Actual = Geometry(Layout);
        });
        Assert.That(Actual, Is.EqualTo(Geometry(Before)));
    }

    /// <summary>Serializes exact model doubles and declared port sides in stable identity order.</summary>
    /// <param name="Layout">Completed model in its unchanged coordinate convention.</param>
    /// <returns>Exact geometry snapshot, without rounding or runtime timing fields.</returns>
    private static string Geometry(FlowchartLayoutResult Layout) => JsonSerializer.Serialize(new
    {
        Bounds = Layout.NodeBounds.Concat(Layout.ClusterBounds).OrderBy(Box => Box.NodeId, StringComparer.Ordinal)
            .Select(Box => new { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }),
        Routes = Layout.EdgeRoutes.OrderBy(Route => Route.EdgeId, StringComparer.Ordinal)
            .Select(Route => new { Route.EdgeId, Route.ExitSide, Route.EntrySide,
                Points = Route.Points.Select(Point => new { Point.X, Point.Y }) })
    });
}
