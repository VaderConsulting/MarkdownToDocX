namespace MermaidRecognition.Layout.Tests;

/// <summary>Protects complete candidate selection from replacing a cheaper legal assignment with a more expensive fallback.</summary>
[TestFixture]
public sealed class CompleteCorridorAssignmentScoreTests
{
    /// <summary>The A116 score ordering keeps the allocated result instead of the worse ordinary fallback.</summary>
    [Test]
    public void CheaperAllocatedCandidateWinsAgainstOrdinaryFallback()
    {
        FlowchartLayoutScore.ComparisonCost Allocated = new FlowchartLayoutScore.ComparisonCost(3363, 0, 98, 1020);
        FlowchartLayoutScore.ComparisonCost Fallback = new FlowchartLayoutScore.ComparisonCost(3814, 0, 98, 1460);
        Assert.That(DiagramGridLayout.LowestCostCompleteAssignment([(true, Allocated), (true, Fallback)]), Is.Zero);
    }

    /// <summary>An available legal baseline participates in the same complete-score comparison.</summary>
    [Test]
    public void LowerScoringLegalBaselineIsKept()
    {
        FlowchartLayoutScore.ComparisonCost Allocated = new FlowchartLayoutScore.ComparisonCost(3363, 0, 98, 1020);
        FlowchartLayoutScore.ComparisonCost Fallback = new FlowchartLayoutScore.ComparisonCost(3814, 0, 98, 1460);
        FlowchartLayoutScore.ComparisonCost Baseline = new FlowchartLayoutScore.ComparisonCost(3320, 0, 97, 1000);
        Assert.That(DiagramGridLayout.LowestCostCompleteAssignment([(true, Allocated), (true, Fallback), (true, Baseline)]), Is.EqualTo(2));
    }

    /// <summary>A baseline that violates fixed geometry or spacing is excluded without displacing a cheaper legal allocated candidate.</summary>
    [Test]
    public void InvalidBaselineDoesNotForceMoreExpensiveFallback()
    {
        FlowchartLayoutScore.ComparisonCost Allocated = new FlowchartLayoutScore.ComparisonCost(3363, 0, 98, 1020);
        FlowchartLayoutScore.ComparisonCost Fallback = new FlowchartLayoutScore.ComparisonCost(3814, 0, 98, 1460);
        FlowchartLayoutScore.ComparisonCost Baseline = new FlowchartLayoutScore.ComparisonCost(3320, 0, 97, 1000);
        Assert.That(DiagramGridLayout.LowestCostCompleteAssignment([(true, Allocated), (true, Fallback), (false, Baseline)]), Is.Zero);
    }

    /// <summary>Equal legal scores retain deterministic input order, and a completely invalid inventory has no winner.</summary>
    [Test]
    public void CandidateOrderIsStableAndInvalidInventoryHasNoWinner()
    {
        FlowchartLayoutScore.ComparisonCost Cost = new FlowchartLayoutScore.ComparisonCost(3363, 0, 98, 1020);
        Assert.Multiple(() =>
        {
            Assert.That(DiagramGridLayout.LowestCostCompleteAssignment([(true, Cost), (true, Cost)]), Is.Zero);
            Assert.That(DiagramGridLayout.LowestCostCompleteAssignment([(false, Cost), (false, Cost)]), Is.EqualTo(-1));
        });
    }
}
