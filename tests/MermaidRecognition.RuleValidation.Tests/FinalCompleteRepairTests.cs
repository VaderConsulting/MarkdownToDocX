using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks final-only whole-layout repair decisions, including exact rollback and the accepted A115 region move.</summary>
[TestFixture, NonParallelizable]
public sealed class FinalCompleteRepairTests
{
    /// <summary>Requires cached reference routes to preserve the complete model and original acceptance order.</summary>
    /// <param name="Id">Catalogue case with ports, frame repair or an accepted region move.</param>
    [TestCase("A115"), TestCase("F60"), TestCase("A104"), TestCase("F152")]
    public void ReferenceReuse_PreservesExactLayoutAndAcceptedOrder(string Id)
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        int CandidateHits = 0;
        DiagramGridLayout.FinalRepairConfirmationObserver = (Key, Geometry, Input, Base, Reused, Ms) =>
        { if (!Base && Reused)
            {
                CandidateHits++;
            }
        };
        (string Model, string[] Accepted) Run(bool DisableReuse)
        {
            DiagramContract Contract = Scenario.Enterprise
                ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
                : MermaidRecognizer.Parse(Scenario.Source);
            List<string> Accepted = [];
            DiagramGridLayout.FinalRepairDisableReferenceReuseForTests = DisableReuse;
            DiagramGridLayout.FinalRepairDisableFixedFramePreflightForTests = DisableReuse;
            DiagramGridLayout.FinalRepairObserver = (Key, Reason, Length) => { if (Reason == "accepted") { Accepted.Add(Key); } };
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            return (CellProductionParityTests.Snapshot(Contract, Layout), Accepted.ToArray());
        }
        try
        {
            (string Model, string[] Accepted) Reference = Run(true);
            (string Model, string[] Accepted) Cached = Run(false);
            Assert.That(Cached.Model, Is.EqualTo(Reference.Model));
            Assert.That(Cached.Accepted, Is.EqualTo(Reference.Accepted));
            if (Id == "F152")
            {
                Assert.That(CandidateHits, Is.Positive, "Exercise repeated full candidate inputs, not just base-route reuse.");
            }
        }
        finally
        {
            DiagramGridLayout.FinalRepairDisableReferenceReuseForTests = false;
            DiagramGridLayout.FinalRepairDisableFixedFramePreflightForTests = false;
            DiagramGridLayout.FinalRepairObserver = null;
            DiagramGridLayout.FinalRepairConfirmationObserver = null;
        }
    }

    /// <summary>Rejects the complete F60 candidate whose rebuilt group envelopes overlap after routing.</summary>
    [Test]
    public void F60_CompletedFrameOverlap_IsRejected()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "F60");
        DiagramContract Contract = MermaidRecognizer.Parse(Scenario.Source);
        int Rejected = 0;
        List<string> Confirmed = [];
        DiagramGridLayout.FinalRepairConfirmationObserver = (Key, Geometry, Input, Base, Reused, Ms) => Confirmed.Add(Key);
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, Length) =>
        {
            if (Reason == "frame-overlap") { Rejected++; TestContext.Out.WriteLine($"F60_FRAME_REJECT|{Key}"); }
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
            Assert.That(Rejected, Is.Positive, "Exercise the frame guard on the reproducing completed candidate.");
            Assert.That(Confirmed, Does.Not.Contain("move|I1|3,0"));
            Assert.That(Confirmed, Does.Not.Contain("move|P1|-3,0"));
        }
        finally { DiagramGridLayout.FinalRepairObserver = null; DiagramGridLayout.FinalRepairConfirmationObserver = null; }
    }

    /// <summary>Proves the reproducing fixed overlaps survive full reference completion with unchanged public envelopes.</summary>
    [Test]
    public void F60_ReferenceCompletion_LeavesFixedOverlappingFramesUnchanged()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "F60");
        DiagramContract Contract = MermaidRecognizer.Parse(Scenario.Source);
        string? CandidateFrames = null; int Checked = 0;
        DiagramGridLayout.FinalRepairDisableReferenceReuseForTests = true;
        DiagramGridLayout.FinalRepairDisableFixedFramePreflightForTests = true;
        DiagramGridLayout.FinalRepairStateObserver = (Stage, Layout, Nodes) =>
        { if (Stage == "candidate")
            {
                CandidateFrames = JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds });
            }
        };
        DiagramGridLayout.FinalRepairMeasurementObserver = (Key, Layout) =>
        {
            if (Key is not ("move|I1|3,0" or "move|P1|-3,0"))
            {
                return;
            }

            Assert.That(JsonSerializer.Serialize(new { Layout.NodeBounds, Layout.ClusterBounds }), Is.EqualTo(CandidateFrames));
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Not.Empty);
            Checked++;
        };
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            Assert.That(Checked, Is.EqualTo(2));
            Assert.That(RouteRuleCatalogueAuditTests.UnrelatedFrameOverlaps(Contract, Layout), Is.Empty);
        }
        finally
        {
            DiagramGridLayout.FinalRepairDisableReferenceReuseForTests = false;
            DiagramGridLayout.FinalRepairDisableFixedFramePreflightForTests = false;
            DiagramGridLayout.FinalRepairStateObserver = null;
            DiagramGridLayout.FinalRepairMeasurementObserver = null;
        }
    }
    /// <summary>Protects the accepted whole-cell A115 move and its completed production routes.</summary>
    [Test]
    public void A115_FinalRegionMove_RemovesTheCrossingWithExactRollback()
    {
        RouteRuleCatalogueAuditTests.Scenario Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A115");
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
        string? Saved = null; int Rejected = 0;
        DiagramGridLayout.FinalRepairStateObserver = (Stage, Layout, Nodes) =>
        {
            FlowchartLogicalGrid.Snapshot Cells = FlowchartCellRepair.Capture(Nodes);
            string Snapshot = JsonSerializer.Serialize(new { Model = CellProductionParityTests.Snapshot(Contract, Layout),
                Cells.Regions, Cells.ColumnGaps, Cells.RowGaps });
            if (Stage == "before")
            {
                Saved = Snapshot;
            }

            if (Stage == "rejected") { Assert.That(Snapshot, Is.EqualTo(Saved)); Rejected++; }
        };
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, Length) => TestContext.Out.WriteLine($"FINAL_OPTION|{Key}|{Reason}|length={Length:R}");
        try
        {
            Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true }, out FlowchartLayoutResult? Layout), Is.True);
            Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Any(Finding => Finding.Rule == "5.9"
                && Finding.Subject == "perthLan->perthApp:2 x shopImg1->shop:24"), Is.False);
            (int Bends, int Crossings, double Shared) Metrics = FlowchartStarFanNesting.Measure(Layout.EdgeRoutes);
            Assert.That(Metrics.Bends, Is.LessThanOrEqualTo(96));
            Assert.That(Metrics.Crossings, Is.LessThanOrEqualTo(131));
            IReadOnlyList<FlowchartRouteRuleChecker.Finding> Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
            Assert.That(Findings.Count(Finding => Finding.Rule == "5.1"), Is.LessThanOrEqualTo(13));
            Assert.That(Findings.Count, Is.LessThanOrEqualTo(113));
            Assert.That(Rejected, Is.Positive);
        }
        finally { DiagramGridLayout.FinalRepairObserver = null; DiagramGridLayout.FinalRepairStateObserver = null; }
    }
}
