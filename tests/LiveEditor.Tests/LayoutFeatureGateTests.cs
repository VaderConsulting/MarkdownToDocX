using MermaidRecognition.Layout;

using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable]
public sealed class LayoutFeatureGateTests
{
    [Test]
    public void ExperimentalFlagsDefaultOffAndAreCapturedIndependently()
    {
        DiagramLayoutOptions Before = LiveEditorFeatureGate.CaptureLayoutOptions();
        Assert.That(Before.EnableIncrementalRouting || Before.EnableRouteAwareConstruction || Before.EnableWidthConstrainedLayout, Is.False);
        try
        {
            LiveEditorFeatureGate.AH_WidthConstrainedLayout = true;
            DiagramLayoutOptions After = LiveEditorFeatureGate.CaptureLayoutOptions();
            Assert.That(After.EnableWidthConstrainedLayout, Is.True);
            Assert.That(After.EnableIncrementalRouting || After.EnableRouteAwareConstruction, Is.False);
            Assert.That(Before.EnableWidthConstrainedLayout, Is.False);
        }
        finally { LiveEditorFeatureGate.AH_WidthConstrainedLayout = false; }
    }
}
