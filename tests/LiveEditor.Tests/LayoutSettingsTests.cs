using System;
using LiveEditor.Editor;

using MermaidRecognition.Layout;

using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable]
public sealed class LayoutSettingsTests
{
    [Test]
    public void OlderSessionsKeepAllExperimentsOffAndUseTheDefaultWidth()
    {
        SessionSettings Settings = SessionSettings.FromJson("{}");
        Assert.That(Settings.IncrementalDiagramRouting || Settings.RouteAwareDiagramConstruction || Settings.WidthConstrainedLayout, Is.False);
        Assert.That(Settings.DiagramTargetWidthPx, Is.EqualTo(1200));
    }

    [Test]
    public void LayoutChoicesRoundTripInTheSession()
    {
        SessionSettings Settings = new SessionSettings
        {
            IncrementalDiagramRouting = true, RouteAwareDiagramConstruction = true,
            WidthConstrainedLayout = true, DiagramTargetWidthPx = 900
        };
        SessionSettings Restored = SessionSettings.FromJson(Settings.ToJson());
        Assert.Multiple(() =>
        {
            Assert.That(Restored.IncrementalDiagramRouting, Is.True);
            Assert.That(Restored.RouteAwareDiagramConstruction, Is.True);
            Assert.That(Restored.WidthConstrainedLayout, Is.True);
            Assert.That(Restored.DiagramTargetWidthPx, Is.EqualTo(900));
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, true, true)]
    public void SavedChoicesReachTheNextPreviewSnapshot(bool Incremental, bool RouteAware, bool Width)
    {
        DiagramLayoutOptions Before = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            LiveEditorFeatureGate.ApplyLayoutSettings(new()
            {
                IncrementalDiagramRouting = Incremental, RouteAwareDiagramConstruction = RouteAware,
                WidthConstrainedLayout = Width, DiagramTargetWidthPx = 900
            });
            DiagramLayoutOptions After = LiveEditorFeatureGate.CaptureLayoutOptions();
            Assert.Multiple(() =>
            {
                Assert.That(After.EnableIncrementalRouting, Is.EqualTo(Incremental));
                Assert.That(After.EnableRouteAwareConstruction, Is.EqualTo(RouteAware));
                Assert.That(After.EnableWidthConstrainedLayout, Is.EqualTo(Width));
                Assert.That(After.TargetWidthPx, Is.EqualTo(900));
            });
        }
        finally
        {
            LiveEditorFeatureGate.ApplyLayoutSettings(new()
            {
                IncrementalDiagramRouting = Before.EnableIncrementalRouting,
                RouteAwareDiagramConstruction = Before.EnableRouteAwareConstruction,
                WidthConstrainedLayout = Before.EnableWidthConstrainedLayout,
                DiagramTargetWidthPx = (int)Before.TargetWidthPx
            });
        }
    }

    [TestCase(int.MinValue, SessionSettings.MINIMUM_DIAGRAM_TARGET_WIDTH_PX)]
    [TestCase(int.MaxValue, SessionSettings.MAXIMUM_DIAGRAM_TARGET_WIDTH_PX)]
    public void InvalidSavedWidthsAreClampedBeforeReachingLayout(int Saved, int Expected)
    {
        DiagramLayoutOptions Before = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            LiveEditorFeatureGate.ApplyLayoutSettings(new() { WidthConstrainedLayout = true, DiagramTargetWidthPx = Saved });
            Assert.That(LiveEditorFeatureGate.CaptureLayoutOptions().TargetWidthPx, Is.EqualTo(Expected));
        }
        finally
        {
            LiveEditorFeatureGate.ApplyLayoutSettings(new()
            {
                IncrementalDiagramRouting = Before.EnableIncrementalRouting,
                RouteAwareDiagramConstruction = Before.EnableRouteAwareConstruction,
                WidthConstrainedLayout = Before.EnableWidthConstrainedLayout,
                DiagramTargetWidthPx = (int)Before.TargetWidthPx
            });
        }
    }
}
