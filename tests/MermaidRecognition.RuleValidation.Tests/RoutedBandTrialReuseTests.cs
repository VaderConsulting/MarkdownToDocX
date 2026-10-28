using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Checks that completed-trial reuse preserves width order, exact geometry, hints and findings.</summary>
[TestFixture, NonParallelizable]
public sealed class RoutedBandTrialReuseTests
{
    /// <summary>Different wrap widths of a solid frame still get scored, while identical complete inputs reuse routing.</summary>
    /// <param name="WithHint">Whether a same-band peer hint must survive every discarded and reused trial.</param>
    [TestCase(false), TestCase(true)]
    public void IdenticalWrapInputsKeepEveryCandidateAndExactOutput(bool WithHint)
    {
        string Source = "enterprise\ndirection TB\ngroup frame \"Frame\"\n"
            + "business-actor b \"Business\" in frame\n"
            + "business-actor peer \"Peer\" in frame" + (WithHint ? " right-of b +1" : "") + "\n"
            + "application-component a \"Application\" in frame\nnode t \"Technology\" in frame\n"
            + "b -association-> a\na -association-> t\npeer -association-> a";
        long Hits = 0;
        Action<string, long>? Previous = FlowchartRenderContext.OperationCountObserver;
        Action<int, FlowchartLayoutResult, FlowchartLayoutScore.ComparisonCost, bool>? PreviousWrap = DiagramGridLayout.BandWrapCandidateObserver;
        try
        {
            (string Snapshot, string[] Widths) Reference = Capture(Source, false);
            FlowchartRenderContext.OperationCountObserver = (Name, Count) =>
            {
                if (Name == "completed-band-trial-cache-hits")
                {
                    Hits += Count;
                }
            };
            (string Snapshot, string[] Widths) Cached = Capture(Source, true);
            Assert.Multiple(() =>
            {
                Assert.That(Hits, Is.GreaterThan(0), "The fixture must actually reuse a completed trial.");
                Assert.That(Cached.Widths, Is.EqualTo(Reference.Widths), "No wrap candidate or score comparison may disappear.");
                Assert.That(Cached.Snapshot, Is.EqualTo(Reference.Snapshot), "Reuse must preserve ports, guides, hints and all findings.");
            });
        }
        finally
        {
            FlowchartRenderContext.OperationCountObserver = Previous;
            DiagramGridLayout.BandWrapCandidateObserver = PreviousWrap;
        }

    }

    /// <summary>Incomplete Dagre group seeds are completed normally instead of being serialized into a cache key.</summary>
    [Test]
    public void IncompleteA167GroupSeedKeepsReferenceGeometryAndHints()
    {
        string Source = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == "A167").Source;
        Action<int, FlowchartLayoutResult, FlowchartLayoutScore.ComparisonCost, bool>? Previous = DiagramGridLayout.BandWrapCandidateObserver;
        try
        {
            (string Snapshot, string[] Widths) Reference = Capture(Source, false);
            (string Snapshot, string[] Widths) Cached = Capture(Source, true);
            Assert.Multiple(() =>
            {
                Assert.That(Cached.Widths, Is.EqualTo(Reference.Widths));
                Assert.That(Cached.Snapshot, Is.EqualTo(Reference.Snapshot));
            });
        }
        finally { DiagramGridLayout.BandWrapCandidateObserver = Previous; }
    }

    /// <summary>Completes a fresh contract and records every candidate score, published route, hint and finding.</summary>
    /// <param name="Source">Unchanged Enterprise input.</param>
    /// <param name="Reuse">Whether production completed-trial reuse is enabled.</param>
    /// <returns>Exact finished state and ordered width comparisons.</returns>
    private static (string Snapshot, string[] Widths) Capture(string Source, bool Reuse)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        Assert.That(Analysis.HasErrors, Is.False, string.Join("; ", Analysis.Diagnostics.Select(Item => Item.Message)));
        DiagramContract Contract = EnterpriseFlowchartContractMapper.Build(Analysis.Document);
        List<string> Widths = [];
        DiagramGridLayout.BandWrapCandidateObserver = (Width, Layout, Cost, Legal) =>
            Widths.Add(Width + "|" + Cost + "|" + Legal);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract,
            new() { UseCellRegions = true, DisableBandTrialReuseForTests = !Reuse }, out FlowchartLayoutResult? Layout), Is.True);
        return (CellProductionParityTests.Snapshot(Contract, Layout), Widths.ToArray());
    }
}
