using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests;

[TestFixture, NonParallelizable]
public sealed class WidthConstrainedLayoutTests
{
    [Test]
    public void SingleLayerFreeNodesUseWidthToAvoidAnUnnecessarilyTallColumn()
    {
        string Source = "enterprise\n" + string.Join("\n", Enumerable.Range(0, 12).Select(I => " application-component a" + I));
        FlowchartLayoutResult Before = Layout(Source, false, 1200);
        FlowchartLayoutResult After = Layout(Source, true, 1200);
        Assert.Multiple(() =>
        {
            Assert.That(After.NodeBounds.Count, Is.EqualTo(12));
            Assert.That(FlowchartLayoutScore.Measure(Contract(Source), After, false).Height,
                Is.LessThan(FlowchartLayoutScore.Measure(Contract(Source), Before, false).Height));
            Assert.That(FlowchartLayoutScore.PlacementWidth(After), Is.LessThanOrEqualTo(1200));
            Assert.That(FlowchartRouteRuleChecker.Check(Contract(Source), After).Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
        });
    }

    [Test]
    public void NarrowTargetFoldsVerticalFanPeersAndRetainsAllLegalRoutes()
    {
        string Source = "enterprise\n application-component hub\n" + string.Join("\n", Enumerable.Range(0, 8)
            .Select(I => " application-component a" + I + "\n hub -flow-> a" + I));
        FlowchartLayoutResult Before = Layout(Source, false, 700);
        FlowchartLayoutResult After = Layout(Source, true, 700);
        Assert.That(After.EdgeRoutes.Count, Is.EqualTo(8));
        Assert.That(After.EdgeRoutes.All(Route => Route.Points.Count >= 2), Is.True);
        Assert.That(FlowchartLayoutScore.PlacementWidth(After), Is.LessThan(FlowchartLayoutScore.PlacementWidth(Before)));
        Assert.That(FlowchartRouteRuleChecker.Check(Contract(Source), After).Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
    }

    [Test]
    public void DisabledWidthPreferenceDoesNotChangeGeometry()
    {
        const string Source = "enterprise\n application-component a\n application-component b\n a -flow-> b";
        string First = System.Text.Json.JsonSerializer.Serialize(Layout(Source, false, 300).NodeBounds);
        string Second = System.Text.Json.JsonSerializer.Serialize(Layout(Source, false, 1200).NodeBounds);
        Assert.That(Second, Is.EqualTo(First));
    }

    [Test]
    public void ExplicitHorizontalChainPreservesItsDirectionEvenWhenItExceedsTheTarget()
    {
        string Source = "enterprise\n direction LR\n" + string.Join("\n", Enumerable.Range(0, 5).Select(I => " application-component a" + I))
            + "\n" + string.Join("\n", Enumerable.Range(0, 4).Select(I => " a" + I + " -flow-> a" + (I + 1)));
        FlowchartLayoutResult Result = Layout(Source, true, 300);
        Dictionary<string, FlowchartLayoutNodeBounds> Nodes = Result.NodeBounds.ToDictionary(Node => Node.NodeId);
        Assert.That(Enumerable.Range(0, 4).All(I => Nodes["a" + I].CenterX < Nodes["a" + (I + 1)].CenterX), Is.True);
        Assert.That(FlowchartLayoutScore.PlacementWidth(Result), Is.GreaterThan(300));
    }

    [Test]
    public void ExplicitPlacementHintRemainsActive()
    {
        const string Source = "enterprise\n application-component a\n application-component b right-of a";
        DiagramContract Graph = Contract(Source);
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true,
            LayoutOptions = new() { EnableWidthConstrainedLayout = true, TargetWidthPx = 300 } };
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Graph, Context, out _), Is.True);
        Assert.That(Graph.Nodes.Single(Node => Node.Id == "b").PlacementHint!.IsActive, Is.True);
    }

    [Test]
    public void MixedMeasuredNodeSpansRetainDisjointCellsAndLegalRoutes()
    {
        string Source = "enterprise\n application-component hub \"A component with a longer measured label\"\n"
            + string.Join("\n", Enumerable.Range(0, 5).Select(I => " application-component a" + I
                + " \"" + (I == 2 ? "This component has enough text to require additional measured space and several wrapped lines" : "Peer " + I)
                + "\"\n hub -flow-> a" + I));
        DiagramContract Graph = Contract(Source);
        FlowchartLayoutResult Result = Layout(Source, true, 700);
        Assert.That(Result.NodeBounds.Count, Is.EqualTo(6));
        Assert.That(FlowchartLayoutScore.Measure(Graph, Result, false).Overlaps, Is.Zero);
        Assert.That(Result.EdgeRoutes.Count, Is.EqualTo(5));
        Assert.That(FlowchartRouteRuleChecker.Check(Graph, Result).Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
    }

    [Test]
    public void AlternatingDefaultAndWidthRendersDoNotShareGeometry()
    {
        const string Source = "enterprise\n application-component one\n application-component two\n application-component three\n application-component four";
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out bool Previous);
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
        try
        {
            string Render(DiagramLayoutOptions Options) => EnterpriseSvgRenderer.Render(EnterpriseValidator.Analyze(Source).Document, Options);
            DiagramLayoutOptions Enabled = new DiagramLayoutOptions { EnableWidthConstrainedLayout = true, TargetWidthPx = 700 };
            string Before = Render(DiagramLayoutOptions.Default);
            string After = Render(Enabled);
            Assert.That(After, Is.Not.EqualTo(Before));
            Assert.That(Render(DiagramLayoutOptions.Default), Is.EqualTo(Before));
            Assert.That(Render(Enabled), Is.EqualTo(After));
        }
        finally { AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, Previous); }
    }

    private static MermaidRecognition.Contract.DiagramContract Contract(string Source)
        => EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Source).Document);

    private static FlowchartLayoutResult Layout(string Source, bool Enabled, double Width)
    {
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true,
            LayoutOptions = new() { EnableWidthConstrainedLayout = Enabled, TargetWidthPx = Width } };
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract(Source), Context, out FlowchartLayoutResult? Result), Is.True);
        return Result;
    }
}
