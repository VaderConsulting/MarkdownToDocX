using MermaidRecognition.Core;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks explicit production activation and compatibility opt-out at the public layout boundary.</summary>
[TestFixture]
public sealed class FlowchartCellProductionActivationTests
{
    /// <summary>Exercises actual node ownership rather than inferring activation from output pixels.</summary>
    /// <param name="Enabled">Whether this render uses authoritative cell regions.</param>
    [TestCase(true), TestCase(false)]
    public void PublicLayoutHonoursCellMode(bool Enabled)
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\n A[Start] --> B[Finish]");
        var Context = new FlowchartRenderContext { UseCellRegions = Enabled };
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out var Layout), Is.True);
        Assert.That(Layout.CellWriteCoverage!.BoundNodes, Is.EqualTo(Enabled ? 2 : 0));
        Assert.That(Layout.CellNodes != null, Is.EqualTo(Enabled));
        Assert.That(Context.RejectedCellPixelWrites, Is.Empty);
        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout)
            .Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
    }
}
