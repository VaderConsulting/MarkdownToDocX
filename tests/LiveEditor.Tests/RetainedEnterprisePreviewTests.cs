using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using LiveEditor.Editor;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed partial class RetainedEnterprisePreviewTests
{
    private const string Source = "enterprise\n"
        + "application-component a \"A\"\napplication-component b \"B\" below a +2\n"
        + "application-component x \"X\" right-of a +5\napplication-component y \"Y\" below x +2\n"
        + "a -flow-> b\nx -flow-> y\n";
    private static readonly DiagramLayoutOptions Options = new() { EnableIncrementalRouting = true };

    [TestCase(true)]
    [TestCase(false)]
    public void AcceptedRedrawPreservesTheCameraAcrossExtentAndRasterScaleChanges(bool Fitted)
    {
        using var Host = new Form { ClientSize = new Size(400, 400) };
        using var Preview = new PreviewCanvas { Dock = DockStyle.Fill };
        Host.Controls.Add(Preview); Host.Show(); Application.DoEvents();
        const string Script = "enterprise\napplication-component a \"A\"\n";
        static string Svg(int Height, int MinX) => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"{Height}\" viewBox=\"{MinX} 0 600 {Height}\"><g transform=\"translate(0,0)\"><g class=\"element application-component\" data-id=\"a\"><rect x=\"100\" y=\"100\" width=\"100\" height=\"50\"/></g></g></svg>";
        static byte[] Png(int Width, int Height)
        {
            using var Bitmap = new Bitmap(Width, Height); using var Stream = new System.IO.MemoryStream();
            Bitmap.Save(Stream, System.Drawing.Imaging.ImageFormat.Png); return Stream.ToArray();
        }
        Preview.LoadAccepted(Script, Svg(600, 0), Png(600, 600), null, false);
        Preview.FitToWindow = Fitted; Preview.Zoom = 2; Preview.ApplyView();
        var Viewport = (Panel)Preview.Controls.Find("Viewport", true)[0];
        var Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        if (!Fitted) Viewport.AutoScrollPosition = new Point(100, 120);
        var Before = Preview.Index!.FindObject("node\u001fa")!;
        double BeforeX = Picture.Left + Before.Left * Picture.Width / Preview.Index.Width;
        double BeforeY = Picture.Top + Before.Top * Picture.Height / Preview.Index.Height;
        Preview.LoadAccepted(Script, Svg(900, -50), Png(300, 450), null, true);
        var After = Preview.Index!.FindObject("node\u001fa")!;
        Assert.That(Picture.Left + After.Left * Picture.Width / Preview.Index.Width, Is.EqualTo(BeforeX).Within(1));
        Assert.That(Picture.Top + After.Top * Picture.Height / Preview.Index.Height, Is.EqualTo(BeforeY).Within(1));
    }

    [Test]
    public void CommentAndColourEditsDrawTheAcceptedGeometryWithoutRebuildingRoutes()
    {
        var Session = new RetainedEnterprisePreview();
        var First = Accept(Session, Source, 1);
        var Comment = Accept(Session, "%% comment\n" + Source, 2);
        Assert.That(Comment.Kind, Is.EqualTo("MetadataOnly"));
        Assert.That(Comment.RebuiltRouteIds, Is.Empty);
        Assert.That(Comment.Svg, Is.EqualTo(First.Svg));
        var Colour = Accept(Session, Source + "style a fill:#ff0000\n", 3);
        Assert.That(Colour.Kind, Is.EqualTo("Redraw"));
        Assert.That(Colour.RebuiltRouteIds, Is.Empty);
        Assert.That(Colour.Svg, Does.Contain("#ff0000"));
        Assert.That(Paths(Colour.Svg), Is.EqualTo(Paths(First.Svg)));
    }

    [Test]
    public void TypedHintPreservesTheIndependentComponentAndReportsItsRouteClosure()
    {
        var Session = new RetainedEnterprisePreview();
        var First = Accept(Session, Source, 1);
        var Move = Accept(Session, Source.Replace("below a +2", "below a +3"), 2);
        Assert.That(Move.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Move.RebuiltRouteIds, Is.EqualTo(Move.AffectedRouteIds));
        Assert.That(Move.RebuiltRouteIds, Has.Length.EqualTo(1));
        foreach (string Id in new[] { "x", "y" })
            Assert.That(Node(Move.Svg, Id), Is.EqualTo(Node(First.Svg, Id)), Id);
        Assert.That(Edge(Move.Svg, "x"), Is.EqualTo(Edge(First.Svg, "x")));
        Assert.That(Node(Move.Svg, "b"), Is.Not.EqualTo(Node(First.Svg, "b")));
    }

    [Test]
    public void FailedLocalMoveAndInvalidTextKeepTheAcceptedBaseForRecovery()
    {
        var Session = new RetainedEnterprisePreview();
        var First = Accept(Session, Source, 1);
        Session.Observe(2);
        Assert.Throws<InvalidOperationException>(() => Session.Prepare(Source + "bad statement\n", 2,
            EnterpriseValidator.Analyze(Source + "bad statement\n"), Options));
        string Collision = Source.Replace("below a +2", "right-of a +5");
        Session.Observe(3);
        Assert.Throws<InvalidOperationException>(() => Session.Prepare(Collision, 3, EnterpriseValidator.Analyze(Collision), Options));
        var Recovery = Accept(Session, Source + "%% valid again\n", 4);
        Assert.That(Recovery.Kind, Is.EqualTo("MetadataOnly"));
        Assert.That(Recovery.Svg, Is.EqualTo(First.Svg));
    }

    [Test]
    public void SupersededInitialAndLocalCandidatesCannotPublish()
    {
        var Session = new RetainedEnterprisePreview();
        Session.Observe(1);
        var Old = Session.Prepare(Source, 1, EnterpriseValidator.Analyze(Source), Options);
        Session.Observe(2);
        Assert.That(Session.TryCommit(Old), Is.False);
        Accept(Session, Source, 2);
        Session.Observe(3);
        var Local = Session.Prepare(Source + "%% old\n", 3, EnterpriseValidator.Analyze(Source), Options);
        Session.Observe(4);
        Assert.That(Session.TryCommit(Local), Is.False);
        Session.Invalidate("Document switched.");
        Assert.Throws<OperationCanceledException>(() => Session.Prepare(Source, 4, EnterpriseValidator.Analyze(Source), Options));
    }

    [Test]
    public void CompetingInitialCandidatesAndDuplicateCommitsAreRejected()
    {
        var Session = new RetainedEnterprisePreview(); Session.Observe(1);
        var First = Session.Prepare(Source, 1, EnterpriseValidator.Analyze(Source), Options);
        var Second = Session.Prepare(Source, 1, EnterpriseValidator.Analyze(Source), Options);
        Assert.That(Session.TryCommit(First), Is.True);
        Assert.That(Session.TryCommit(First), Is.False);
        Assert.That(Session.TryCommit(Second), Is.False);
    }

    [TestCase("title New title\n")]
    [TestCase("style a font-size:24px\n")]
    [TestCase("application-component extra \"Extra\"\n")]
    public void UnsupportedGeometryEditsIdentifyTheirRebuild(string Addition)
    {
        var Session = new RetainedEnterprisePreview(); Accept(Session, Source, 1);
        var Changed = Accept(Session, Source + Addition, 2);
        Assert.That(Changed.Rebuild, Is.True);
        Assert.That(Changed.RebuildReason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void OptionChangesRecreateTheSessionAndAbandonedDrawingsDoNotPublish()
    {
        var Session = new RetainedEnterprisePreview(); Accept(Session, Source, 1);
        Session.Observe(2);
        var Abandoned = Session.Prepare(Source + "title Abandoned\n", 2,
            EnterpriseValidator.Analyze(Source + "title Abandoned\n"), Options);
        Session.Observe(3);
        var Next = Session.Prepare(Source, 3, EnterpriseValidator.Analyze(Source), Options with { EnableWidthConstrainedLayout = true });
        Assert.That(Next.RebuildReason, Does.Contain("options"));
        Assert.That(Session.TryCommit(Next), Is.True);
        Assert.That(Session.TryCommit(Abandoned), Is.False);
    }

    [Test]
    public void WorkerKeepsTypedTextAndExportImageOnFailureThenRecoversThroughTheSameSession()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        bool KeepLastGood = LiveEditorFeatureGate.Q_KeepLastGoodPreview;
        try
        {
            LiveEditorFeatureGate.Q_KeepLastGoodPreview = false;

            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100 && Form.LastRetainedUpdateKind == "InitialLayout");

            string? AcceptedSvg = Preview.RenderedSvg;
            byte[]? AcceptedPng = Preview.LastPreviewPngBytes;
            Editor.Text = Source + "bad statement\n";

            Wait(() => Form.DiagramMetadata.RenderFailed);
            Assert.That(Editor.Text, Does.EndWith("bad statement\n"));
            Assert.That(Preview.RenderedSvg, Is.EqualTo(AcceptedSvg));
            Assert.That(Preview.LastPreviewPngBytes, Is.SameAs(AcceptedPng));
            Editor.Text = Source + "%% recovered\n";
            Wait(() => Form.DiagramMetadata.PercentRendered == 100 && Form.LastRetainedUpdateKind == "MetadataOnly");
            Assert.That(Form.LastRebuiltRouteIds, Is.Empty);
            Assert.That(Preview.RenderedSvg, Is.EqualTo(AcceptedSvg));
            Form.AutoLayout();
            Wait(() => Form.LastRetainedUpdateKind == "InitialLayout");
        }
        finally { Restore(Previous); LiveEditorFeatureGate.Q_KeepLastGoodPreview = KeepLastGood; }
    }

    [Test]
    public void WorkerPublishesTheLatestRapidEditAndFlagOffKeepsTheOrdinaryPath()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(false);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.Null);
            MDConverter.MermaidPreviewRender.TryRender(Source, out string Svg, out _, out _);
            Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
            for (int Index = 0; Index < 15; Index++) Editor.Text = Source + "title Revision " + Index + "\n";
            Wait(() => Form.DiagramMetadata.PercentRendered == 100 && Preview.RenderedSvg!.Contains("Revision 14"));
            Assert.That(Preview.RenderedSvg, Does.Not.Contain("Revision 13"));
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void WorkerRoutesTypedHintsUndoAndRedoThroughTheSameSourcePath()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string? Initial = Preview.RenderedSvg;
            Editor.Select(Editor.Text.IndexOf("below a +2", StringComparison.Ordinal) + "below a +".Length, 1);
            Editor.SelectedText = "3";
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting");
            string? Moved = Preview.RenderedSvg;
            Assert.That(Form.LastRebuiltRouteIds, Has.Length.EqualTo(1));
            Assert.That(Moved, Is.Not.EqualTo(Initial));
            Editor.Undo();
            Wait(() => Preview.RenderedSvg == Initial);
            Editor.Redo();
            Wait(() => Preview.RenderedSvg == Moved);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void DocumentAndFlagChangesStartFreshWorkerSessions()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            int Generation = Form.DiagramMetadata.Generation;
            typeof(MainForm).GetMethod("SetEditorContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(Form, [Source + "%% another document\n", null, null, false]);
            Wait(() => Form.DiagramMetadata.Generation > Generation && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("InitialLayout"));
            Generation = Form.DiagramMetadata.Generation;
            LiveEditorFeatureGate.AF_IncrementalDiagramRouting = false;
            Editor.AppendText("%% flag off\n");
            Wait(() => Form.DiagramMetadata.Generation > Generation && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.Null);
            Generation = Form.DiagramMetadata.Generation;
            LiveEditorFeatureGate.AF_IncrementalDiagramRouting = true;
            Editor.AppendText("%% flag on\n");
            Wait(() => Form.DiagramMetadata.Generation > Generation && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("InitialLayout"));
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void ImageDecodeFailureCannotPublishTheEngineCandidate()
    {
        using var Preview = new PreviewCanvas();
        bool Published = false;
        Assert.Throws<ArgumentException>(() => Preview.LoadAccepted(Source,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"/>",
            [1, 2, 3], null, true, () => { Published = true; return true; }));
        Assert.That(Published, Is.False);
        Assert.That(Preview.Image, Is.Null);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void RetainedEditsWorkWithIndependentConstructionAndWidthOptions(bool Construction, bool Width)
    {
        var Session = new RetainedEnterprisePreview();
        var Settings = Options with { EnableRouteAwareConstruction = Construction, EnableWidthConstrainedLayout = Width };
        Session.Observe(1);
        Assert.That(Session.TryCommit(Session.Prepare(Source, 1, EnterpriseValidator.Analyze(Source), Settings)), Is.True);
        Session.Observe(2);
        var Update = Session.Prepare(Source + "%% edit\n", 2, EnterpriseValidator.Analyze(Source), Settings);
        Assert.That(Update.Kind, Is.EqualTo("MetadataOnly"));
        Assert.That(Update.RebuiltRouteIds, Is.Empty);
        Assert.That(Session.TryCommit(Update), Is.True);
    }

    [Test]
    public void OpenLayoutKeepsItsOrdinaryPreviewAndReportsTheRetainedModeLimit()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = "enterprise\n%%{init: {\"layout\":\"open\"}}%%\napplication-component a \"A\"\n";
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.Null);
            Assert.That(Preview.RenderedSvg, Is.Not.Empty);
            Assert.That(Preview.PreviewAdvisory, Does.Contain("open-layout"));
        }
        finally { Restore(Previous); }
    }

    private static MainForm CreateForm(bool Incremental)
    {
        var Form = new MainForm(new SessionSettings { IncrementalDiagramRouting = Incremental,
            RenderDelayMilliseconds = 50, HighlightDelayMilliseconds = 40 });
        Form.ShowInTaskbar = false; Form.Location = new Point(40, 40); Form.ClientSize = new Size(1200, 800);
        _ = Form.Handle;
        return Form;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GuiRelationshipAddAndKeyboardDeleteEachSubmitOneUndoableSourceCommand(bool Styled)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            string Script = Styled ? PreviewRelationshipCommandsTests.StyledSource : PreviewRelationshipCommandsTests.Source;
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Script; Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Initial = Preview.RenderedSvg!;
            Preview.SelectedItems = [Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == "a"),
                Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == "c")];
            var Choices = Preview.RelationshipAdds!();
            Assert.That(Choices.Any(Choice => Choice.Name == "flow"), Is.True);
            int Generation = Form.DiagramMetadata.Generation;
            Choices.Single(Choice => Choice.Name == "flow").Execute();
            string Added = Editor.Text;
            Assert.That(Added, Is.Not.EqualTo(Script));
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            string AddedSvg = Preview.RenderedSvg!;
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation + 1));
            Assert.That(Preview.SelectedItems!.Single().SourceId, Is.EqualTo("a"));
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo("c"));
            Editor.Undo(); Assert.That(Editor.Text, Is.EqualTo(Script));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
            Editor.Redo(); Assert.That(Editor.Text, Is.EqualTo(Added));
            Wait(() => Preview.RenderedSvg == AddedSvg && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo("c"));
            Generation = Form.DiagramMetadata.Generation;
            var FocusSink = (PreviewFocusSink)Preview.Controls.Find("PreviewFocusSink", true).FirstOrDefault()!
                ?? (PreviewFocusSink)Preview.Controls.Find("FocusSink", true).Single();
            Assert.That(FocusSink.HandleKey(Keys.Control | Keys.Delete), Is.False);
            Assert.That(Editor.Text, Is.EqualTo(Added));
            Assert.That(FocusSink.HandleKey(Keys.Delete), Is.True);
            Assert.That(Editor.Text, Is.EqualTo(Script));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation + 1));
            Assert.That(Form.LastRebuiltRouteIds, Is.Empty);
            Assert.That(Preview.SelectedItems, Is.Null.Or.Empty);
            Editor.Undo(); Assert.That(Editor.Text, Is.EqualTo(Added));
            Wait(() => Preview.RenderedSvg == AddedSvg && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo("c"));
            Editor.Redo(); Assert.That(Editor.Text, Is.EqualTo(Script));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void GuiDeletePreservesIndexedStylesAndRestoresSelectionOnUndo()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = PreviewRelationshipCommandsTests.StyledSource;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Initial = Preview.RenderedSvg!;
            Form.SelectPreviewObject(Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Relationship && Item.SourceId == "a"));
            Assert.That(Form.DeletePreviewRelationship(), Is.True);
            string Removed = Editor.Text;
            Assert.That(Removed, Does.Contain("linkStyle 0 stroke:#00d,font-size:21px").And.Not.Contain("#d00"));
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRebuiltRouteIds, Is.Empty);
            Editor.Undo(); Assert.That(Editor.Text, Is.EqualTo(PreviewRelationshipCommandsTests.StyledSource));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Preview.SelectedItems!.Single().SourceId, Is.EqualTo("a"));
            Editor.Redo(); Assert.That(Editor.Text, Is.EqualTo(Removed));
            Wait(() => Preview.Items.Count(Item => Item.Kind == PreviewObjectKind.Relationship) == 1 && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
        }
        finally { Restore(Previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RelationshipMenusRespectFeatureGuardsAndCanReopenWithoutStaleCommands(bool Incremental)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(Incremental);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = PreviewRelationshipCommandsTests.Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            Preview.SelectedItems = [Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == "a"),
                Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == "c")];
            var Opening = typeof(PreviewCanvas).GetMethod("DiagramMenu_Opening", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            Opening.Invoke(Preview, [null, new System.ComponentModel.CancelEventArgs()]);
            Opening.Invoke(Preview, [null, new System.ComponentModel.CancelEventArgs()]);
            var Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
            var Add = (ToolStripMenuItem)Picture.ContextMenuStrip!.Items.Find("AddRelationshipMenuItem", false).Single();
            Assert.That(Add.Available, Is.EqualTo(Incremental));
            var Choices = Preview.RelationshipAdds!();
            Assert.That(Choices.Length > 0, Is.EqualTo(Incremental));
            Editor.AppendText("%% superseded menu\n");
            string Current = Editor.Text;
            foreach (var Choice in Choices) Choice.Execute();
            Assert.That(Editor.Text, Is.EqualTo(Current));
        }
        finally { Restore(Previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TypedRelationshipRemovalAndUndoRedoUseRetainedGeometryWithoutRouting(bool RemoveAll)
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
            int Start = Source.IndexOf("a -flow-> b", StringComparison.Ordinal);
            int Length = RemoveAll ? Source.Length - Start : "a -flow-> b\n".Length;
            Editor.Select(Start, Length); Editor.SelectedText = string.Empty;
            string Removed = Editor.Text;
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRebuiltRouteIds, Is.Empty);
            Assert.That(Preview.Items.Count(Item => Item.Kind == PreviewObjectKind.Relationship), Is.EqualTo(RemoveAll ? 0 : 1));
            foreach (string Id in new[] { "a", "b", "x", "y" })
                Assert.That(Node(Preview.RenderedSvg!, Id), Is.EqualTo(Node(Initial, Id)));
            if (!RemoveAll) Assert.That(Edge(Preview.RenderedSvg!, "x"), Is.EqualTo(Edge(Initial, "x")));
            Editor.Undo(); Assert.That(Editor.Text, Is.EqualTo(Source));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
            Editor.Redo(); Assert.That(Editor.Text, Is.EqualTo(Removed));
            Wait(() => Preview.Items.Count(Item => Item.Kind == PreviewObjectKind.Relationship) == (RemoveAll ? 0 : 1)
                && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
            Assert.That(Form.LastRebuiltRouteIds, Is.Empty);
        }
        finally { Restore(Previous); }
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public void GuiEndpointCommandsHaveOneSubmissionAndKeepStableSelectionThroughUndoRedo(bool SourceEnd, bool Reconnect)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            string Script = SourceEnd ? PreviewRelationshipMoveTests.Source.Replace("right-of b +3", "left-of a +3") : PreviewRelationshipMoveTests.Source;
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Script;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Initial = Preview.RenderedSvg!;
            var Line = Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Relationship);
            Form.SelectPreviewObject(Line);
            var Move = Preview.CreateRelationshipMove!(Line);
            Assert.That(Move, Is.Not.Null);
            var Original = Move!.Original(SourceEnd);
            var Target = Move.Targets(SourceEnd).First(Point => Point.NodeId == (Reconnect ? "c" : Original.NodeId)
                && Point.Direction == Original.Direction && Point.Step == (Reconnect ? 0 : 1));
            int Generation = Form.DiagramMetadata.Generation;
            Assert.That(Form.MovePreviewRelationship(Move, SourceEnd, Original), Is.False);
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation));
            Assert.That(Form.MovePreviewRelationship(Move, SourceEnd, Target), Is.True);
            string MovedSource = Editor.Text;
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            string Moved = Preview.RenderedSvg!;
            Assert.That(Moved, Is.Not.EqualTo(Initial));
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation + 1));
            Assert.That(Form.LastRebuiltRouteIds, Is.EqualTo(new[] { Move.Identity }));
            Assert.That(Preview.SelectedItems!.Single().SourceId, Is.EqualTo(SourceEnd ? Target.NodeId : "a"));
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo(SourceEnd ? "b" : Target.NodeId));
            Editor.Undo(); Assert.That(Editor.Text, Is.EqualTo(Script));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Preview.SelectedItems!.Single().SourceId, Is.EqualTo("a"));
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo("b"));
            Editor.Redo(); Assert.That(Editor.Text, Is.EqualTo(MovedSource));
            Wait(() => Preview.RenderedSvg == Moved && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Preview.SelectedItems!.Single().SourceId, Is.EqualTo(SourceEnd ? Target.NodeId : "a"));
            Assert.That(Preview.SelectedItems!.Single().TargetId, Is.EqualTo(SourceEnd ? "b" : Target.NodeId));
            Editor.AppendText("%% superseded command\n");
            Assert.That(Form.MovePreviewRelationship(Move, SourceEnd, Target), Is.False);
            Assert.That(Editor.Text, Is.EqualTo(MovedSource + "%% superseded command\n"));
        }
        finally { Restore(Previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EndpointHandlesRequireAnAfEnabledGridPreview(bool Open)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(Open);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Open ? PreviewRelationshipMoveTests.Source.Replace("enterprise\n", "enterprise\n%%{init: {\"layout\":\"open\"}}%%\n")
                : PreviewRelationshipMoveTests.Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            var Line = Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Relationship);
            Assert.That(Preview.CreateRelationshipMove!(Line), Is.Null);
        }
        finally { Restore(Previous); }
    }

    [TestCase("webShop")]
    [TestCase("orderApi")]
    [TestCase("order")]
    public void DefaultWebShopNodesCanMoveThroughTheRetainedSourcePath(string Id)
    {
        string Script = PreviewNodePlacementTests.Source;
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, Script, 1);
        var Index = PreviewHoverIndex.Build(Initial.Svg, Script);
        var Node = Index.FindObject("node\u001f" + Id)!;
        Assert.That(Node.Kind, Is.EqualTo(PreviewObjectKind.Node));
        Assert.That(Index.HitObject((Node.Left + Node.Right) / 2, (Node.Top + Node.Bottom) / 2, 3), Is.SameAs(Node), "node body must be selectable");
        var Grid = new PreviewNodePlacement.GridSnap(Script, Index.Objects, Session.CopyPlacementLayout()!);
        var Drop = new PointF((float)((Node.Left + Node.Right) / 2 + 540), (float)((Node.Top + Node.Bottom) / 2 + 90));
        var Snap = Grid.Snap(Id, Drop);
        Assert.That(Snap, Is.Not.Null, "No eligible placement peer for " + Id);
        Assert.That(PreviewNodePlacement.TryCreate(Script, Id, Index.Objects, Drop, out var Edit, out var Reason, Grid), Is.True, Reason);
        var Moved = Accept(Session, Edit!.Apply(Script), 2);
        Assert.That(Moved.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Moved.Svg, Is.Not.EqualTo(Initial.Svg));
    }

    [Test]
    public void APlacementAnchorStillOffersDragFeedbackWhenAllItsPeersDependOnIt()
    {
        const string Script = "enterprise\napplication-component webShop \"Web Shop\" NW order +1 +2\n"
            + "application-service orderApi \"Order API\" above order +1\ndata-object order \"Order\"\n"
            + "webShop -serving-> orderApi\norderApi -access(write)-> order\n";
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, Script, 1);
        var Index = PreviewHoverIndex.Build(Initial.Svg, Script);
        var Node = Index.FindObject("node\u001forder")!;
        var Grid = new PreviewNodePlacement.GridSnap(Script, Index.Objects, Session.CopyPlacementLayout()!);
        var Drop = new PointF((float)((Node.Left + Node.Right) / 2 + 180), (float)((Node.Top + Node.Bottom) / 2));
        var Snap = Grid.Snap("order", Drop);
        Assert.That(Snap, Is.Not.Null);
        Assert.That(Snap!.Direction, Is.EqualTo("at"));
        Assert.That(PreviewNodePlacement.TryCreate(Script, "order", Index.Objects, Drop, out var Edit, out var Reason, Grid), Is.True, Reason);
        Assert.That(EnterpriseValidator.Analyze(Edit!.Apply(Script)).Document.Elements["order"].PlacementHint!.Kind,
            Is.EqualTo(MermaidRecognition.Contract.PlacementHintKind.At));
        var Moved = Accept(Session, Edit.Apply(Script), 2);
        Assert.That(Moved.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Moved.RebuiltRouteIds.Length, Is.EqualTo(2));
        Assert.That(Moved.Svg, Is.Not.EqualTo(Initial.Svg));
    }

    [TestCase("default")]
    [TestCase("anchor")]
    [TestCase("alone")]
    public void NodesWithoutPlacementPeersCanStartAGuiMoveAndUndoTheirFirstHint(string Case)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            string Script = Case == "alone" ? "enterprise\ndata-object order \"Order\"\n" : PreviewNodePlacementTests.Source;
            if (Case == "anchor") Script = "enterprise\napplication-component webShop \"Web Shop\" NW order +1 +2\n"
                + "application-service orderApi \"Order API\" above order +1\ndata-object order \"Order\"\n"
                + "webShop -serving-> orderApi\norderApi -access(write)-> order\n";
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Script;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Initial = Preview.RenderedSvg!;
            var Order = Preview.Index!.FindObject("node\u001forder")!;
            Form.SelectPreviewObject(Order);
            Assert.That(Preview.CanMoveNode!(Order), Is.True);
            Assert.That(Preview.CreateNodeMoveSnap!(Order), Is.Not.Null, "Order must be able to start drag feedback");
            Assert.That(Form.MovePreviewNode("order", new((float)((Order.Left + Order.Right) / 2 + 180),
                (float)((Order.Top + Order.Bottom) / 2 + 90))), Is.True);
            string MovedSource = Editor.Text;
            if (Case != "default") Assert.That(EnterpriseValidator.Analyze(MovedSource).Document.Elements["order"].PlacementHint!.Kind,
                Is.EqualTo(MermaidRecognition.Contract.PlacementHintKind.At));
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting" && Form.DiagramMetadata.PercentRendered == 100);
            string Moved = Preview.RenderedSvg!;
            Assert.That(Moved, Is.Not.EqualTo(Initial));
            Editor.Undo();
            Assert.That(Editor.Text, Is.EqualTo(Script));
            Wait(() => Preview.RenderedSvg == Initial && Form.DiagramMetadata.PercentRendered == 100);
            Editor.Redo();
            Assert.That(Editor.Text, Is.EqualTo(MovedSource));
            Wait(() => Preview.RenderedSvg == Moved && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
        }
        finally { Restore(Previous); }
    }

    [TestCase("Order")]
    [TestCase("Invoice")]
    [TestCase("")]
    public void AnchorMovementDependsOnIdentitiesAndHintsRatherThanDisplayLabels(string Label)
    {
        string Script = "enterprise\napplication-component active \"Order\" NW root +1 +2\n"
            + "application-service service \"Service\" above root +1\ndata-object root"
            + (Label.Length == 0 ? "" : " \"" + Label + "\"")
            + "\nactive -serving-> service\nservice -access(write)-> root\n";
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, Script, 1);
        var Index = PreviewHoverIndex.Build(Initial.Svg, Script);
        var Root = Index.FindObject("node\u001froot")!;
        var Grid = new PreviewNodePlacement.GridSnap(Script, Index.Objects, Session.CopyPlacementLayout()!);
        var Drop = new PointF((float)((Root.Left + Root.Right) / 2 + 180), (float)((Root.Top + Root.Bottom) / 2 + 90));
        Assert.That(Grid.Snap(Root.Id, Drop), Is.Not.Null);
        Assert.That(PreviewNodePlacement.TryCreate(Script, Root.Id, Index.Objects, Drop, out var Edit, out var Reason, Grid), Is.True, Reason);
        var Changed = EnterpriseValidator.Analyze(Edit!.Apply(Script)).Document;
        Assert.That(Changed.Elements[Root.Id].PlacementHint!.Kind, Is.EqualTo(MermaidRecognition.Contract.PlacementHintKind.At));
        Assert.That(Changed.Elements[Root.Id].Label, Is.EqualTo(EnterpriseValidator.Analyze(Script).Document.Elements[Root.Id].Label));
        Assert.That(Accept(Session, Edit.Apply(Script), 2).Kind, Is.EqualTo("LocalRouting"));
    }

    [Test]
    public void FirstTypedPlacementHintCanBeUndoneEvenWhenItsAcceptedRevisionIncludesAComment()
    {
        const string Script = "enterprise\napplication-component a\napplication-component b\na -flow-> b\n";
        var Session = new RetainedEnterprisePreview();
        var Initial = Accept(Session, Script, 1);
        var Move = Accept(Session, Script.Replace("application-component b\n", "application-component b below a +3\n") + "%% another edit before debounce\n", 2);
        Assert.That(Move.Svg, Is.Not.EqualTo(Initial.Svg));
        var Undo = Accept(Session, Script, 3);
        Assert.That(Undo.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Undo.Svg, Is.EqualTo(Initial.Svg));
    }

    [TestCase("below a +3")]
    [TestCase("NW a +4 +2")]
    [TestCase("NE a +4 +2")]
    [TestCase("SE a +4 +2")]
    [TestCase("SW a +4 +2")]
    public void SnappedGuiMoveMatchesTypedHintPreservesSelectionAndIsOneUndoableSourceEdit(string Hint)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        var PreviousObserver = MermaidRecognition.Layout.Enterprise.EnterpriseFlowchartEdgeRouter.StageTimingObserver;
        try
        {
            var Reference = new RetainedEnterprisePreview();
            Accept(Reference, Source, 1);
            string Typed = Source.Replace("below a +2", Hint);
            var Expected = Accept(Reference, Typed, 2);
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Initial = Preview.RenderedSvg!;
            Form.SelectPreviewObject(Preview.Index!.FindObject("node\u001fb"));
            int Generation = Form.DiagramMetadata.Generation;
            var Stages = new System.Collections.Concurrent.ConcurrentBag<string>();
            MermaidRecognition.Layout.Enterprise.EnterpriseFlowchartEdgeRouter.StageTimingObserver =
                (Stage, Time) => { if (Time < 0) Stages.Add(Stage); };
            // Express the expected centre in the currently displayed frame,
            // including negative extents, then add an off-grid displacement.
            var ExpectedIndex = PreviewHoverIndex.Build(Expected.Svg, Typed);
            var Target = ExpectedIndex.FindObject("node\u001fb")!;
            var OldOrigin = PreviewHoverIndex.SvgOrigin(Initial);
            var NewOrigin = PreviewHoverIndex.SvgOrigin(Expected.Svg);
            Assert.That(Form.MovePreviewNode("b", new((float)((Target.Left + Target.Right) / 2 + NewOrigin.X - OldOrigin.X + 7),
                (float)((Target.Top + Target.Bottom) / 2 + NewOrigin.Y - OldOrigin.Y + 8))), Is.True);
            Assert.That(Editor.Text, Is.EqualTo(Typed));
            Wait(() => Preview.RenderedSvg == Expected.Svg && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation + 1));
            Assert.That(Form.LastRebuiltRouteIds, Is.EqualTo(Expected.RebuiltRouteIds));
            Assert.That(Preview.SelectedItems!.Single().Id, Is.EqualTo("b"));
            foreach (string Id in new[] { "x", "y" })
                Assert.That(Node(Preview.RenderedSvg!, Id), Is.EqualTo(Node(Initial, Id)));
            Assert.That(Edge(Preview.RenderedSvg!, "x"), Is.EqualTo(Edge(Initial, "x")));
            Editor.Undo();
            Assert.That(Editor.Text, Is.EqualTo(Source));
            Wait(() => Preview.RenderedSvg == Initial);
            Editor.Redo();
            Assert.That(Editor.Text, Is.EqualTo(Typed));
            Wait(() => Preview.RenderedSvg == Expected.Svg);
            Assert.That(Stages, Does.Not.Contain("enterprise-shared-flowchart-layout"));
            Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
        }
        finally
        {
            Restore(Previous);
            MermaidRecognition.Layout.Enterprise.EnterpriseFlowchartEdgeRouter.StageTimingObserver = PreviousObserver;
        }
    }

    [Test]
    public void NoOpAndStaleGuiDropsDoNotRewriteSourceOrSubmitAnotherRender()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            var B = Preview.Index!.FindObject("node\u001fb")!;
            PointF Center = new((float)((B.Left + B.Right) / 2), (float)((B.Top + B.Bottom) / 2));
            int Generation = Form.DiagramMetadata.Generation;
            Assert.That(Form.MovePreviewNode("b", Center), Is.False);
            Assert.That(Editor.Text, Is.EqualTo(Source));
            Assert.That(Form.DiagramMetadata.Generation, Is.EqualTo(Generation));
            Editor.AppendText("%% newer source\n");
            Assert.That(Form.MovePreviewNode("b", new(Center.X, Center.Y + 100)), Is.False);
            Assert.That(Editor.Text, Is.EqualTo(Source + "%% newer source\n"));
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void RejectedGuiDropKeepsTypedHintAndAcceptedExportAndCanBeUndone()
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
            byte[] Png = Preview.LastPreviewPngBytes!;
            var X = Preview.Index!.FindObject("node\u001fx")!;
            Assert.That(Form.MovePreviewNode("b", new((float)((X.Left + X.Right) / 2),
                (float)((X.Top + X.Bottom) / 2))), Is.True);
            Wait(() => Form.DiagramMetadata.RenderFailed);
            Assert.That(Editor.Text, Does.Contain("right-of a +5"));
            Assert.That(Preview.RenderedSvg, Is.EqualTo(Initial));
            Assert.That(Preview.LastPreviewPngBytes, Is.SameAs(Png));
            Editor.Undo();
            Assert.That(Editor.Text, Is.EqualTo(Source));
            Wait(() => !Form.DiagramMetadata.RenderFailed && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Preview.RenderedSvg, Is.EqualTo(Initial));
            Assert.That(Form.LastRebuiltRouteIds, Is.Empty);
        }
        finally { Restore(Previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GuiMovementRequiresAnAcceptedRetainedGridPreview(bool OpenLayout)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(OpenLayout);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            Editor.Text = OpenLayout ? Source.Replace("enterprise\n", "enterprise\n%%{init: {\"layout\":\"open\"}}%%\n") : Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            string Before = Editor.Text;
            Assert.That(Form.MovePreviewNode("b", new(300, 300)), Is.False);
            Assert.That(Editor.Text, Is.EqualTo(Before));
        }
        finally { Restore(Previous); }
    }

    private static void Wait(Func<bool> Complete)
    {
        DateTime End = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < End)
        {
            Application.DoEvents(); if (Complete()) return; Thread.Sleep(20);
        }
        Assert.Fail("The render worker did not publish the expected current revision.");
    }

    private static void Restore(DiagramLayoutOptions Options)
        => LiveEditorFeatureGate.ApplyLayoutSettings(new SessionSettings { IncrementalDiagramRouting = Options.EnableIncrementalRouting,
            RouteAwareDiagramConstruction = Options.EnableRouteAwareConstruction, WidthConstrainedLayout = Options.EnableWidthConstrainedLayout,
            DiagramTargetWidthPx = (int)Options.TargetWidthPx });

    private static RetainedEnterprisePreview.Prepared Accept(RetainedEnterprisePreview Session, string Text, long Revision)
    {
        Session.Observe(Revision);
        var Candidate = Session.Prepare(Text, Revision, EnterpriseValidator.Analyze(Text), Options);
        Assert.That(Session.TryCommit(Candidate), Is.True); return Candidate;
    }
    private static string Node(string Svg, string Id) => XDocument.Parse(Svg).Descendants()
        .Single(Item => (string?)Item.Attribute("data-id") == Id).ToString();
    private static string Edge(string Svg, string SourceId) => XDocument.Parse(Svg).Descendants()
        .Single(Item => (string?)Item.Attribute("data-source") == SourceId).ToString();
    private static string[] Paths(string Svg) => XDocument.Parse(Svg).Descendants()
        .Where(Item => Item.Attribute("data-edge-source") != null).Select(Item => (string)Item.Attribute("d")!).ToArray();
}
