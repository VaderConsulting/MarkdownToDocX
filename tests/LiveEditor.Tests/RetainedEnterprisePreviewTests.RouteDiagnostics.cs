using System.Linq;
using System.Windows.Forms;
using LiveEditor.Editor;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

public sealed partial class RetainedEnterprisePreviewTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void RouteMeasurementsBelongToAgAndDoNotEnableAfGestures(bool Af, bool Ag)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = new MainForm(new SessionSettings { IncrementalDiagramRouting = Af, RouteAwareDiagramConstruction = Ag,
                RenderDelayMilliseconds = 50, HighlightDelayMilliseconds = 40 });
            Form.ShowInTaskbar = false;
            Form.ClientSize = new System.Drawing.Size(1200, 800);
            _ = Form.Handle;
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var List = (ListBox)Form.Controls.Find("DiagnosticsList", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100 || Form.DiagramMetadata.RenderFailed);
            Assert.That(Form.DiagramMetadata.RenderFailed, Is.False, ((PreviewCanvas)Form.Controls.Find("Preview", true)[0]).RenderFailure);
            Assert.That(Form.LastRouteReport != null, Is.EqualTo(Ag));
            Assert.That(List.Items.Cast<object>().Any(Item => Item.ToString()!.StartsWith("Routes:")), Is.EqualTo(Ag));
            if (Ag)
            {
                Assert.That(Form.LastRouteReport!.Routes, Has.Length.EqualTo(2));
                Assert.That(Form.LastRouteReport.Routes.All(Route => Route.Identity.StartsWith("relationship:")), Is.True);
                Assert.That(List.Items.Cast<object>().Any(Item => Item.ToString()!.Contains("detour")), Is.True);
                Assert.That(List.Items.Cast<object>().Any(Item => Item.ToString()!.Contains("line 6")), Is.True);
            }
            if (!Af) Assert.That(Form.MovePreviewNode("b", new(300, 300)), Is.False);
            var Identities = Form.LastRouteReport?.Routes.Select(Route => Route.Identity).ToArray();
            int Generation = Form.DiagramMetadata.Generation;
            Editor.Text = "%% comment\n" + Source;
            Assert.That(Form.LastRouteReport, Is.Null, "measurements from older text must disappear immediately");
            Wait(() => Form.DiagramMetadata.Generation > Generation && Form.DiagramMetadata.PercentRendered == 100);
            if (Ag)
            {
                Assert.That(Form.LastRouteReport!.Routes.Select(Route => Route.Identity), Is.EqualTo(Identities));
                Assert.That(List.Items.Cast<object>().Any(Item => Item.ToString()!.Contains("line 7")), Is.True);
            }
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void FailedSourceAndAgSwitchCannotPublishStaleRouteMeasurements()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = new MainForm(new SessionSettings { RouteAwareDiagramConstruction = true, RenderDelayMilliseconds = 50 });
            Form.ShowInTaskbar = false;
            Form.ClientSize = new System.Drawing.Size(1200, 800);
            _ = Form.Handle;
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            Editor.Text = Source;
            Wait(() => Form.LastRouteReport != null || Form.DiagramMetadata.RenderFailed);
            Assert.That(Form.DiagramMetadata.RenderFailed, Is.False, ((PreviewCanvas)Form.Controls.Find("Preview", true)[0]).RenderFailure);
            Editor.AppendText("invalid statement\n");
            Wait(() => Form.DiagramMetadata.RenderFailed);
            Assert.That(Form.LastRouteReport, Is.Null);
            LiveEditorFeatureGate.ApplyLayoutSettings(new SessionSettings());
            Editor.Text = Source;
            Wait(() => !Form.DiagramMetadata.RenderFailed && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRouteReport, Is.Null);
            var List = (ListBox)Form.Controls.Find("DiagnosticsList", true)[0];
            Assert.That(List.Items.Cast<object>().Any(Item => Item.ToString()!.StartsWith("Routes:")), Is.False);
        }
        finally { Restore(Previous); }
    }
}
