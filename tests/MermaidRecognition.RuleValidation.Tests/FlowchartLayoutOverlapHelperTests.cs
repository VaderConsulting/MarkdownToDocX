using MermaidRecognition.RuleValidation.Generation;
using MermaidRecognition.RuleValidation.Models;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>
/// Layout overlap detection tests.
/// </summary>
[TestFixture]
public sealed class FlowchartLayoutOverlapHelperTests
{
    [Test]
    public void NodeBoundsOverlap_DetectsIntersectingRectangles()
    {
        BoundsActualResult Left = new BoundsActualResult
        {
            X = 0,
            Y = 0,
            Width = 100,
            Height = 50
        };
        BoundsActualResult Right = new BoundsActualResult
        {
            X = 80,
            Y = 10,
            Width = 100,
            Height = 50
        };

        Assert.That(FlowchartLayoutOverlapHelper.NodeBoundsOverlap(Left, Right), Is.True);
    }

    [Test]
    public void NodeBoundsOverlap_IgnoresTouchingEdgesWithinTolerance()
    {
        BoundsActualResult Left = new BoundsActualResult
        {
            X = 0,
            Y = 0,
            Width = 100,
            Height = 50
        };
        BoundsActualResult Right = new BoundsActualResult
        {
            X = 100,
            Y = 0,
            Width = 100,
            Height = 50
        };

        Assert.That(FlowchartLayoutOverlapHelper.NodeBoundsOverlap(Left, Right), Is.False);
    }
}
