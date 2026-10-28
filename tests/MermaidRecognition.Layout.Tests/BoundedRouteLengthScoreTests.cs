using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks the approved whole-layout route-length term and its restricted use in candidate comparisons.</summary>
[TestFixture]
public sealed class BoundedRouteLengthScoreTests
{
    /// <summary>Requires finite lengths to remain below the canonical smaller bend/crossing cost.</summary>
    /// <param name="WeightedLength">A complete layout's weighted route length.</param>
    [TestCase(0.0), TestCase(1.0), TestCase(1000.0), TestCase(1e100)]
    public void LengthTerm_IsBounded(double WeightedLength)
    {
        double Value = FlowchartLayoutScore.BoundedLength(WeightedLength);
        Assert.That(Value, Is.GreaterThanOrEqualTo(0));
        Assert.That(Value, Is.LessThan(Math.Min(FlowchartLayoutRules.BendScore, FlowchartLayoutRules.CrossingScore)));
    }

    /// <summary>Checks that length cannot change a comparison when bend or crossing costs differ.</summary>
    [Test]
    public void DifferentRouteCosts_IgnoreLength()
    {
        FlowchartLayoutScore.ComparisonCost Longer = new FlowchartLayoutScore.ComparisonCost(100, 1000, 0, 0);
        Assert.That(new FlowchartLayoutScore.ComparisonCost(101, 0, 1, 0).CompareTo(Longer), Is.Positive);
        Assert.That(new FlowchartLayoutScore.ComparisonCost(101, 0, 0, 20).CompareTo(Longer), Is.Positive);
        Assert.That(new FlowchartLayoutScore.ComparisonCost(101, 0, 0, 0).CompareTo(Longer), Is.Negative);
    }

    /// <summary>Checks exact length improvements even when the bounded doubles saturate or an old primary-score epsilon is present.</summary>
    [Test]
    public void TiedRouteCosts_PreserveExactLengthImprovements()
    {
        FlowchartLayoutScore.ComparisonCost Before = new FlowchartLayoutScore.ComparisonCost(100, 1e100, 0, 0);
        FlowchartLayoutScore.ComparisonCost After = new FlowchartLayoutScore.ComparisonCost(100, Math.BitDecrement(1e100), 0, 0);
        Assert.That(After < Before - 0.001, Is.True);
        Assert.That(new FlowchartLayoutScore.ComparisonCost(100, 10, 0, 0)
            < new FlowchartLayoutScore.ComparisonCost(100, 10.000001, 0, 0) - 0.001, Is.True);
    }

    /// <summary>Checks one term for the whole route inventory, rather than one bounded charge per route.</summary>
    [Test]
    public void CompleteLayout_AddsLengthOnce()
    {
        FlowchartLayoutResult Layout = new FlowchartLayoutResult();
        DiagramContract Contract = new DiagramContract { Kind = "flowchart", Direction = "TB" };
        int Column = 0;
        foreach (string Id in new[] { "first", "second" })
        {
            double X = Column++ * 300;
            Contract.NodesMutable.Add(new() { Id = Id, Shape = "rect" });
            Contract.NodesMutable.Add(new() { Id = Id + "End", Shape = "rect" });
            Contract.EdgesMutable.Add(new() { Id = Id, SourceId = Id, TargetId = Id + "End" });
            Layout.NodeBoundsMutable.Add(new() { NodeId = Id, CenterX = X, CenterY = 300, Width = 150, Height = 90 });
            Layout.NodeBoundsMutable.Add(new() { NodeId = Id + "End", CenterX = X, CenterY = 90, Width = 150, Height = 90 });
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Id, SourceId = Id, TargetId = Id + "End" };
            Route.Points.Add(new() { X = X, Y = 255 }); Route.Points.Add(new() { X = X, Y = 135 });
            Layout.EdgeRoutesMutable.Add(Route);
        }
        FlowchartLayoutScore.Result Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
        double Weighted = 240 / DiagramGridMetrics.GRID_UNIT_PX * FlowchartLayoutRules.RouteLengthWeight;
        Assert.That(Score.Cost.WeightedLength, Is.EqualTo(Weighted));
        Assert.That(Score.OptimizationScore - Score.Cost.Primary,
            Is.EqualTo(FlowchartLayoutScore.BoundedLength(Weighted)).Within(1e-10));
    }
}
