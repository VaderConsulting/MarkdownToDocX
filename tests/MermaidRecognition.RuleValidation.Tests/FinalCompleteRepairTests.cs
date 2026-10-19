using System.Text.Json;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks final-only whole-layout repair decisions, including exact rollback and the A115 side reassignment.</summary>
[TestFixture, NonParallelizable]
public sealed class FinalCompleteRepairTests
{
    /// <summary>Rejects the complete F60 candidate whose rebuilt group envelopes overlap after routing.</summary>
    [Test]
    public void F60_CompletedFrameOverlap_IsRejected()
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "F60");
        var Contract = MermaidRecognizer.Parse(Scenario.Source);
        int Rejected = 0;
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, Length) =>
        {
            if (Reason == "frame-overlap") { Rejected++; TestContext.Out.WriteLine($"F60_FRAME_REJECT|{Key}"); }
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
            Assert.That(Rejected, Is.Positive, "Exercise the frame guard on the reproducing completed candidate.");
        }
        finally { DiagramGridLayout.FinalRepairObserver = null; }
    }
    /// <summary>Requires the previously demonstrated joint side alternative in the actual final production model.</summary>
    [Test]
    public void A115_FinalSideAssignment_RemovesTheCrossingWithExactRollback()
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        string? Saved = null; int Rejected = 0;
        DiagramGridLayout.FinalRepairStateObserver = (Stage, Layout, Nodes) =>
        {
            var Cells = FlowchartCellRepair.Capture(Nodes);
            string Snapshot = JsonSerializer.Serialize(new { Model = CellProductionParityTests.Snapshot(Contract, Layout),
                Cells.Regions, Cells.ColumnGaps, Cells.RowGaps });
            if (Stage == "before") Saved = Snapshot;
            if (Stage == "rejected") { Assert.That(Snapshot, Is.EqualTo(Saved)); Rejected++; }
        };
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, Length) => TestContext.Out.WriteLine($"FINAL_OPTION|{Key}|{Reason}|length={Length:R}");
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out var Layout), Is.True);
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Any(Finding => Finding.Rule == "5.9"
                && Finding.Subject == "perthLan->perthApp:2 x shopImg1->shop:24"), Is.False);
            var Metrics = FlowchartStarFanNesting.Measure(Layout.EdgeRoutes);
            Assert.That(Metrics.Bends, Is.LessThanOrEqualTo(96));
            Assert.That(Metrics.Crossings, Is.LessThanOrEqualTo(131));
            var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
            Assert.That(Findings.Count(Finding => Finding.Rule == "5.1"), Is.LessThanOrEqualTo(13));
            Assert.That(Findings.Count, Is.LessThanOrEqualTo(113));
            Assert.That(Rejected, Is.Positive);
        }
        finally { DiagramGridLayout.FinalRepairObserver = null; DiagramGridLayout.FinalRepairStateObserver = null; }
    }
}
