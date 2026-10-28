using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

public sealed partial class RetainedEnterprisePreviewTests
{
    [Test]
    public void LoopLabelGrowthExpandsMeasuredDependenciesAndUndoReplaysAcceptedGeometry()
    {
        string Script = Source.Replace("a -flow-> b", "a -flow-> a \"short\" [leave W@-1, enter W@1]")
            .Replace("x -flow-> y", "x -flow-> y \"independent\"");
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, Script, 1);
        string Changed = Script.Replace("\"short\"", "\"" + new string('W', 120) + "\"");
        var Edit = Accept(Session, Changed, 2);
        Assert.That(Edit.Rebuild, Is.False);
        Assert.That(Edit.RebuiltRouteIds, Has.Length.EqualTo(1));
        Assert.That(Edit.Labels.Issues, Is.Empty);
        Assert.That(Edge(Edit.Svg, "x"), Is.EqualTo(Edge(Initial.Svg, "x")));
        var Undo = Accept(Session, Script, 3);
        Assert.That(Undo.Svg, Is.EqualTo(Initial.Svg));
        Assert.That(Undo.RebuiltRouteIds, Is.Empty, "accepted scene replay copies paths without routing");
        Assert.That(Accept(Session, Changed, 4).Svg, Is.EqualTo(Edit.Svg));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RelationshipLabelEditsPreserveOtherLabelsAndUndoExactly(bool Loop)
    {
        string Script = Source.Replace("a -flow-> b", Loop ? "a -flow-> a \"short\"" : "a -flow-> b \"short\"")
            .Replace("x -flow-> y", "x -flow-> y \"independent\"");
        var Session = new RetainedEnterprisePreview();
        var First = Accept(Session, Script, 1);
        var Before = Session.CopyInteractionSnapshot()!;
        string Independent = Before.Model.Edges.Single(Edge => Edge.SourceId == "x").Id!;
        string Changed = Script.Replace("\"short\"", "\"A longer relationship label with several words\"");
        var Edit = Accept(Session, Changed, 2);
        Assert.That(Edit.Rebuild, Is.False);
        Assert.That(Edit.Labels.Issues, Is.Empty);
        Assert.That(Edit.Labels.Placements[Independent], Is.SameAs(First.Labels.Placements[Independent]));
        Assert.That(Edit.Scene.Routes.Routes[Independent], Is.SameAs(First.Scene.Routes.Routes[Independent]));
        foreach (string Id in new[] { "a", "b", "x", "y" })
            Assert.That(Node(Edit.Svg, Id), Is.EqualTo(Node(First.Svg, Id)));
        Assert.That(Edge(Edit.Svg, "x"), Is.EqualTo(Edge(First.Svg, "x")));
        Assert.That(Accept(Session, Script, 3).Svg, Is.EqualTo(First.Svg));
        Assert.That(Accept(Session, Changed, 4).Svg, Is.EqualTo(Edit.Svg));
    }

    [Test]
    public void UnplaceableRelationshipTextRejectsWithoutPublishingAndRecovers()
    {
        string Script = Source.Replace("a -flow-> b", "a -flow-> b \"short\" [leave S@0, enter N@0]");
        var Session = new RetainedEnterprisePreview();
        var First = Accept(Session, Script, 1);
        string Before = JsonSerializer.Serialize(Session.CopyInteractionSnapshot()!.Layout.EdgeRoutes);
        string Changed = Script.Replace("\"short\"", "\"" + new string('W', 2000) + "\"");
        Session.Observe(2);
        Assert.Throws<InvalidOperationException>(() => Session.Prepare(Changed, 2, EnterpriseValidator.Analyze(Changed), Options));
        Assert.That(Session.CopyInteractionSnapshot()!.SourceRevision, Is.EqualTo(1));
        Assert.That(JsonSerializer.Serialize(Session.CopyInteractionSnapshot()!.Layout.EdgeRoutes), Is.EqualTo(Before));
        Assert.That(Accept(Session, Script + "%% recover\n", 3).Svg, Is.EqualTo(First.Svg));
    }

    [Test]
    public void RelationshipFontSizeControlsMeasuredBoundsAndTextSpacing()
    {
        string Script = Source.Replace("a -flow-> b", "a -flow-> b \"several words for a label\"") + "linkStyle 0 font-size:21px\n";
        var Session = new RetainedEnterprisePreview();
        var First = Accept(Session, Script, 1);
        var Label = First.Labels.Placements.Single().Value;
        Assert.That(Label.Height, Is.EqualTo(21 + 26.25 * (Label.Lines.Length - 1)));
        Assert.That(Label.Width, Is.EqualTo(Label.Lines.Max(Line => Line.Length) * 21 * 0.512).Within(0.01));
        Assert.That(First.Svg, Does.Contain("font-size=\"21px\""));
        var Changed = Accept(Session, Script.Replace("several words for a label", "changed words for the label"), 2);
        Assert.That(Changed.Rebuild, Is.False);
        Assert.That(Changed.Labels.Issues, Is.Empty);
    }

    [Test]
    public void WorkerLabelFailurePreservesTypedTextAndExportThenUndoRecovers()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            string Script = Source.Replace("a -flow-> b", "a -flow-> b \"short\" [leave S@0, enter N@0]");
            Editor.Text = Script;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Svg = Preview.RenderedSvg!;
            byte[] Png = Preview.LastPreviewPngBytes!;
            Editor.Select(Editor.Text.IndexOf("\"short\"", StringComparison.Ordinal) + 1, 5);
            Editor.SelectedText = new string('W', 2000);
            Wait(() => Form.DiagramMetadata.RenderFailed);
            Assert.That(Editor.Text, Does.Contain(new string('W', 2000)));
            Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
            Assert.That(Preview.LastPreviewPngBytes, Is.SameAs(Png));
            Editor.Undo();
            Wait(() => Form.DiagramMetadata.PercentRendered == 100 && !Form.DiagramMetadata.RenderFailed);
            Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
        }
        finally { Restore(Previous); }
    }
}
