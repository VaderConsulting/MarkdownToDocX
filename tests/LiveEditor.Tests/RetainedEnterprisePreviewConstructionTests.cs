using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

public sealed partial class RetainedEnterprisePreviewTests
{
    [Test]
    public void SingleNodeRelationshipMenuCreatesOneUndoableSelfRelationship()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Initial = Preview.RenderedSvg!;
            Preview.SelectedItems = [Preview.Items.Single(Item => Item.Id == "a" && Item.Kind == PreviewObjectKind.Node)];
            var Choice = Preview.RelationshipAdds!().Single(Item => Item.Name == "flow to itself");
            int Generation = Form.DiagramMetadata.Generation;
            Choice.Execute();
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation + 1));
            Assert.That(Editor.Text, Does.Contain("a -flow-> a [leave"));
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo("a"));
            string Added = Preview.RenderedSvg!;
            Editor.Undo(); Wait(() => Preview.RenderedSvg == Initial);
            Editor.Redo(); Wait(() => Preview.RenderedSvg == Added);
        }
        finally { Restore(Previous); }
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ConstructionCanStartEmptyRemoveEverythingAndUndo(bool InitiallyEmpty)
    {
        const string Empty = "enterprise\n";
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, InitiallyEmpty ? Empty : Source, 1);
        var Filled = InitiallyEmpty ? Accept(Session, Source, 2) : Initial;
        long Revision = InitiallyEmpty ? 3 : 2;
        var Removed = Accept(Session, Empty, Revision);
        Assert.That(Removed.Rebuild, Is.False);
        Assert.That(Session.CopyInteractionSnapshot()!.Layout.NodeBounds, Is.Empty);
        var Undo = Accept(Session, Source, Revision + 1);
        Assert.That(Undo.Rebuild, Is.False);
        Assert.That(Undo.Svg, Is.EqualTo(Filled.Svg));
        var Redo = Accept(Session, Empty, Revision + 2);
        Assert.That(Redo.Svg, Is.EqualTo(Removed.Svg));
    }

    [Test]
    public void WorkerConstructionUndoRestoresNodeAndAcceptedPictureWithoutRebuilding()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string OriginalSvg = Preview.RenderedSvg!;
            Editor.Select(Editor.TextLength, 0);
            Editor.SelectedText = "application-component extra\n";
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting");
            string AddedSvg = Preview.RenderedSvg!;
            Editor.Undo();
            Wait(() => Preview.RenderedSvg == OriginalSvg);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
            Editor.Redo();
            Wait(() => Preview.RenderedSvg == AddedSvg);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void UndoAfterDeletionStillRemembersTheFirstPlacementOrigin()
    {
        var Session = new RetainedEnterprisePreview();
        string Original = Accept(Session, Source, 1).Svg;
        string MovedSource = Source.Replace("application-component a \"A\"", "application-component a \"A\" at -2 0");
        string Moved = Accept(Session, MovedSource, 2).Svg;
        Accept(Session, "enterprise\n", 3);
        Assert.That(Accept(Session, MovedSource, 4).Svg, Is.EqualTo(Moved));
        Assert.That(Accept(Session, Source, 5).Svg, Is.EqualTo(Original));
    }
    [TestCase("application-component extra right-of b +3\n")]
    [TestCase("node extra\n")]
    [TestCase("business-actor extra\n")]
    [TestCase("application-component p NE b +2 +3\napplication-component q below p +2\np -flow-> q\n")]
    public void SourceConstructionAddsNodesWithoutInitialPlacement(string AddedSource)
    {
        var Previous = EnterpriseFlowchartEdgeRouter.StageTimingObserver;
        try
        {
            var Session = new RetainedEnterprisePreview();
            var Initial = Accept(Session, Source, 1);
            var Nodes = new[] { "a", "b", "x", "y" }.ToDictionary(Id => Id, Id => Node(Initial.Svg, Id));
            List<string> Stages = [];
            EnterpriseFlowchartEdgeRouter.StageTimingObserver = (Stage, Time) => { if (Time < 0) Stages.Add(Stage); };
            var Added = Accept(Session, Source + AddedSource, 2);
            Assert.That(Added.Rebuild, Is.False, Added.RebuildReason);
            Assert.That(Added.Kind, Is.EqualTo("LocalRouting"));
            Assert.That(Stages, Does.Not.Contain("enterprise-shared-flowchart-layout").And.Not.Contain("enterprise-contract-map"));
            foreach (var Pair in Nodes) Assert.That(Node(Added.Svg, Pair.Key), Is.EqualTo(Pair.Value));
            Assert.That(Session.CopyInteractionSnapshot()!.Layout.CellNodes!.Count, Is.GreaterThan(4));
        }
        finally { EnterpriseFlowchartEdgeRouter.StageTimingObserver = Previous; }
    }
}
