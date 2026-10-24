using System.Globalization;
using System.Text;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Exports per-case node centres and port sides for baseline layout comparison scripts.</summary>
[TestFixture, NonParallelizable, Explicit("Run from scripts/compare-layout-baseline.sh")]
public sealed class CatalogueLayoutFingerprintTests
{
    [Test]
    public void ExportAllLayoutFingerprints()
    {
        StringBuilder Builder = new StringBuilder();
        foreach (TestCaseData Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            if (!TryLayout(Scenario, out FlowchartLayoutResult? Layout))
            {
                Builder.AppendLine(Scenario.Id + "\tSKIP\tlayout-failed");
                continue;
            }

            foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.OrderBy(B => B.NodeId, StringComparer.Ordinal))
            {
                Builder.AppendLine(string.Join('\t',
                    Scenario.Id,
                    "node",
                    Box.NodeId,
                    Box.CenterX.ToString("F2", CultureInfo.InvariantCulture),
                    Box.CenterY.ToString("F2", CultureInfo.InvariantCulture)));
            }

            foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.OrderBy(R => R.EdgeId, StringComparer.Ordinal))
            {
                Builder.AppendLine(string.Join('\t',
                    Scenario.Id,
                    "port",
                    Route.EdgeId,
                    Route.SourceId,
                    Route.TargetId,
                    Route.ExitSide,
                    Route.EntrySide));
            }
        }

        foreach (string Line in Builder.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            TestContext.Out.WriteLine(Line);
        }

        Assert.Pass("Fingerprint export written to test output.");
    }

    private static bool TryLayout(RouteRuleCatalogueAuditTests.Scenario Scenario, out FlowchartLayoutResult? Layout)
    {
        Layout = null;
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        return FlowchartSugiyamaLayoutEngine.TryLayout(
            Contract,
            new FlowchartRenderContext { UseCellRegions = true },
            out Layout);
    }
}
