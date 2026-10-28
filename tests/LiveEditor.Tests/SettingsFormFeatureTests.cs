using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
public sealed class SettingsFormFeatureTests
{
    private DiagramLayoutOptions _Previous = null!;

    [SetUp]
    public void RememberRuntimeOptions() => _Previous = LiveEditorFeatureGate.CaptureLayoutOptions();

    [TearDown]
    public void RestoreRuntimeOptions() => LiveEditorFeatureGate.ApplyLayoutSettings(new()
    {
        IncrementalDiagramRouting = _Previous.EnableIncrementalRouting,
        RouteAwareDiagramConstruction = _Previous.EnableRouteAwareConstruction,
        WidthConstrainedLayout = _Previous.EnableWidthConstrainedLayout,
        DiagramTargetWidthPx = (int)_Previous.TargetWidthPx
    });

    [Test]
    public void DialogLoadsAndAppliesLayoutChoicesWithoutChangingTheSessionWhileEditing()
    {
        var Settings = new SessionSettings
        {
            IncrementalDiagramRouting = true, RouteAwareDiagramConstruction = true,
            WidthConstrainedLayout = true, DiagramTargetWidthPx = 900
        };
        string Original = Settings.ToJson();
        using var Dialog = new SettingsForm(Settings);
        var Tabs = Require<TabControl>(Dialog, "SettingsTabs");
        Assert.That(Tabs.TabPages.Count, Is.EqualTo(2));
        Assert.That(Tabs.TabPages[0].Text, Is.EqualTo("General"));
        Assert.That(Tabs.TabPages[1].Text, Is.EqualTo("Diagram layout"));
        Assert.That(Require<CheckBox>(Dialog, "IncrementalRoutingCheckBox").Checked, Is.True);
        Assert.That(Require<CheckBox>(Dialog, "RouteAwareConstructionCheckBox").Checked, Is.True);
        var Width = Require<CheckBox>(Dialog, "WidthConstrainedLayoutCheckBox");
        var Target = Require<NumericUpDown>(Dialog, "TargetWidthNumeric");
        Assert.That(Width.Checked, Is.True);
        Assert.That(Target.Value, Is.EqualTo(900));
        Width.Checked = false;
        Assert.That(Target.Enabled, Is.False);
        Assert.That(Target.Value, Is.EqualTo(900));
        Width.Checked = true;
        Assert.That(Target.Enabled, Is.True);
        Target.Value = 700;
        Require<CheckBox>(Dialog, "IncrementalRoutingCheckBox").Checked = false;
        Assert.That(Settings.ToJson(), Is.EqualTo(Original));
        Assert.That(LiveEditorFeatureGate.CaptureLayoutOptions(), Is.EqualTo(_Previous));
        Dialog.ApplyTo(Settings);
        var Restored = SessionSettings.FromJson(Settings.ToJson());
        Assert.Multiple(() =>
        {
            Assert.That(Restored.IncrementalDiagramRouting, Is.False);
            Assert.That(Restored.RouteAwareDiagramConstruction, Is.True);
            Assert.That(Restored.WidthConstrainedLayout, Is.True);
            Assert.That(Restored.DiagramTargetWidthPx, Is.EqualTo(700));
        });
    }

    [Test]
    public void CancelLeavesTheSessionAndRunningSwitchesUnchanged()
    {
        var Settings = new SessionSettings();
        string Original = Settings.ToJson();
        using var Dialog = new SettingsForm(Settings);
        Dialog.StartPosition = FormStartPosition.Manual;
        Dialog.Location = new Point(-20000, -20000);
        Dialog.Show();
        Require<TabControl>(Dialog, "SettingsTabs").SelectedIndex = 1;
        Require<CheckBox>(Dialog, "WidthConstrainedLayoutCheckBox").Checked = true;
        Require<NumericUpDown>(Dialog, "TargetWidthNumeric").Value = 700;
        Require<Button>(Dialog, "CancelSettingsButton").PerformClick();
        Assert.That(Dialog.DialogResult, Is.EqualTo(DialogResult.Cancel));
        Assert.That(Settings.ToJson(), Is.EqualTo(Original));
        Assert.That(LiveEditorFeatureGate.CaptureLayoutOptions(), Is.EqualTo(_Previous));
    }

    [Test]
    public void ButtonsRemainOutsideTheTabsAndOlderSessionsUseDisabledDefaults()
    {
        using var Dialog = new SettingsForm(SessionSettings.FromJson("{}"));
        var Root = Require<TableLayoutPanel>(Dialog, "RootLayout");
        var Actions = Require<FlowLayoutPanel>(Dialog, "ButtonPanel");
        Assert.That(Actions.Parent, Is.SameAs(Root));
        Assert.That(Dialog.AcceptButton, Is.SameAs(Require<Button>(Dialog, "OkSettingsButton")));
        Assert.That(Dialog.CancelButton, Is.SameAs(Require<Button>(Dialog, "CancelSettingsButton")));
        Assert.That(Require<CheckBox>(Dialog, "IncrementalRoutingCheckBox").Checked, Is.False);
        Assert.That(Require<CheckBox>(Dialog, "RouteAwareConstructionCheckBox").Checked, Is.False);
        Assert.That(Require<CheckBox>(Dialog, "WidthConstrainedLayoutCheckBox").Checked, Is.False);
        Assert.That(Require<NumericUpDown>(Dialog, "TargetWidthNumeric").Value, Is.EqualTo(1200));
        Assert.That(Require<NumericUpDown>(Dialog, "TargetWidthNumeric").Enabled, Is.False);
    }

    [Test]
    public void EditorStartupAndReapplyingPreferencesUseTheSavedSwitches()
    {
        var Settings = SessionSettings.FromJson(new SessionSettings
        {
            IncrementalDiagramRouting = true, RouteAwareDiagramConstruction = true,
            WidthConstrainedLayout = true, DiagramTargetWidthPx = 700
        }.ToJson());
        using var Editor = new MainForm(Settings);
        var Before = LiveEditorFeatureGate.CaptureLayoutOptions();
        Assert.That(Before.EnableIncrementalRouting && Before.EnableRouteAwareConstruction && Before.EnableWidthConstrainedLayout, Is.True);
        Assert.That(Before.TargetWidthPx, Is.EqualTo(700));
        using var Dialog = new SettingsForm(Settings);
        Require<CheckBox>(Dialog, "WidthConstrainedLayoutCheckBox").Checked = false;
        Require<NumericUpDown>(Dialog, "TargetWidthNumeric").Value = 900;
        Dialog.ApplyTo(Settings);
        typeof(MainForm).GetMethod("ApplyEditorPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Editor, null);
        var After = LiveEditorFeatureGate.CaptureLayoutOptions();
        Assert.That(After.EnableWidthConstrainedLayout, Is.False);
        Assert.That(After.TargetWidthPx, Is.EqualTo(900));
        Assert.That(Before.EnableWidthConstrainedLayout, Is.True, "Already captured requests retain their options.");
        Assert.That(Before.TargetWidthPx, Is.EqualTo(700));
    }

    private static T Require<T>(Control Root, string Name) where T : Control
    {
        Control[] Found = Root.Controls.Find(Name, true);
        Assert.That(Found, Has.Length.EqualTo(1), Name);
        return (T)Found[0];
    }
}
