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
    public void PackedArtifactContainerRetainsCellsThroughPaintAndMetadataEdits()
    {
        string Script = "enterprise\nnode container \"Files\"\n"
            + string.Concat(Enumerable.Range(0, 10).Select(Index => $"artifact n{Index} \"File {Index}\" in container\ncontainer -assignment-> n{Index}\n"));
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, Script, 1);
        var Before = Session.CopyInteractionSnapshot()!;
        Assert.That(Before.Layout.NodeBounds.Select(Node => Node.CenterX).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(Before.Layout.NodeBounds.Select(Node => Node.CenterY).Distinct().Count(), Is.GreaterThan(1));
        var Comment = Accept(Session, Script + "%% same placement\n", 2);
        Assert.That(Comment.Svg, Is.EqualTo(Initial.Svg));
        var Paint = Accept(Session, Script + "style n0 fill:#ff0000\n", 3);
        Assert.That(Paint.Rebuild, Is.False);
        Assert.That(Paint.RebuiltRouteIds, Is.Empty);
        Assert.That(Paint.Svg, Does.Contain("#ff0000"));
        Assert.That(Session.CopyInteractionSnapshot()!.Layout.CellNodes!.Select(Node => Node.CellNode!.Region),
            Is.EqualTo(Before.Layout.CellNodes!.Select(Node => Node.CellNode!.Region)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MeasuredNodeLabelChangesStayLocalWithWidthOptions(bool Width)
    {
        const string Script = "enterprise\nbusiness-service business \"Business\" at 0 0\n"
            + "application-service app \"App\" at 4 0\ntechnology-service host \"Host\" at 8 0\n";
        var Options = new MermaidRecognition.Layout.DiagramLayoutOptions { EnableIncrementalRouting = true,
            EnableWidthConstrainedLayout = Width, TargetWidthPx = 700 };
        var Session = new RetainedEnterprisePreview();
        RetainedEnterprisePreview.Prepared Apply(string Text, long Revision)
        {
            Session.Observe(Revision);
            var Candidate = Session.Prepare(Text, Revision, MermaidRecognition.Kinds.Enterprise.EnterpriseValidator.Analyze(Text), Options);
            Assert.That(Session.TryCommit(Candidate), Is.True); return Candidate;
        }
        var Initial = Apply(Script, 1);
        var Before = Session.CopyInteractionSnapshot()!;
        string Label = string.Join(" ", Enumerable.Repeat("Moderately longer label with enough words to occupy several separate lines", 3));
        string Changed = Script.Replace("\"App\"", "\"" + Label + "\"");
        var Resized = Apply(Changed, 2);
        Assert.That(Resized.Rebuild, Is.False);
        Assert.That(Resized.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Resized.RebuiltRouteIds, Is.Empty);
        Assert.That(Node(Resized.Svg, "business"), Is.EqualTo(Node(Initial.Svg, "business")));
        Assert.That(Node(Resized.Svg, "host"), Is.EqualTo(Node(Initial.Svg, "host")));
        Assert.That(Session.CopyInteractionSnapshot()!.Layout.CellNodes!.Single(Node => Node.Id == "app").CellNode!.Region.RowSpan,
            Is.GreaterThan(Before.Layout.CellNodes!.Single(Node => Node.Id == "app").CellNode!.Region.RowSpan));
        Assert.That(Apply(Script, 3).Svg, Is.EqualTo(Initial.Svg));
        Assert.That(Apply(Changed, 4).Svg, Is.EqualTo(Resized.Svg));
    }
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

    [TestCase(false)]
    [TestCase(true)]
    public void UndoAfterDeletionStillRemembersTheFirstPlacementOrigin(bool Coalesced)
    {
        var Session = new RetainedEnterprisePreview();
        string Original = Accept(Session, Source, 1).Svg;
        string Label = string.Join(" ", Enumerable.Repeat("A larger label needing several measured lines", 3));
        string MovedSource = Source.Replace("application-component a \"A\"", "application-component a \"" + Label + "\" at -2 0");
        string Moved = Accept(Session, MovedSource, 2).Svg;
        Accept(Session, "enterprise\n", 3);
        if (!Coalesced) Assert.That(Accept(Session, MovedSource, 4).Svg, Is.EqualTo(Moved));
        Assert.That(Accept(Session, Source, Coalesced ? 4 : 5).Svg, Is.EqualTo(Original));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WorkerNodeLabelSizingIsOneUndoableLocalUpdate(bool Grows)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source; Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Original = Preview.RenderedSvg!;
            int Generation = Form.DiagramMetadata.Generation;
            int Start = Editor.Text.IndexOf("\"B\"", StringComparison.Ordinal);
            Editor.Select(Start + 1, 1);
            Editor.SelectedText = Grows ? string.Join(" ", Enumerable.Repeat("A larger label needing several measured lines", 3)) : "Brief";
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation + 1));
            Assert.That(Form.LastRebuiltRouteIds.Length, Is.EqualTo(Grows ? 1 : 0));
            string Changed = Preview.RenderedSvg!;
            foreach (string Id in new[] { "a", "x", "y" }) Assert.That(Node(Changed, Id), Is.EqualTo(Node(Original, Id)));
            Editor.Undo(); Wait(() => Preview.RenderedSvg == Original);
            Editor.Redo(); Wait(() => Preview.RenderedSvg == Changed);
        }
        finally { Restore(Previous); }
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
