using System.Reflection;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks scoring of unresolved intermediate paths during complete cell-route candidate evaluation.</summary>
[TestFixture]
public sealed class FlowchartCellFinalRouteCostTests
{
    /// <summary>An unresolved candidate contributes no segment length and does not throw while being compared or rejected.</summary>
    /// <param name="Count">Stored points in the intermediate candidate.</param>
    /// <param name="Expected">Total Manhattan segment length used by the existing score.</param>
    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 7)]
    public void ScoresIncompleteTrialPaths(int Count, double Expected)
    {
        var Points = new[] { new FlowchartLayoutPoint { X = 0, Y = 0 }, new FlowchartLayoutPoint { X = 3, Y = 4 } }.Take(Count).ToList();
        var Actual = typeof(FlowchartPortSideRouteRepair).GetMethod("Length", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Points]);
        Assert.That(Actual, Is.EqualTo(Expected));
    }
}
