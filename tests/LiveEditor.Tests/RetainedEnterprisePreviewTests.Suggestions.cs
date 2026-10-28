using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

public sealed partial class RetainedEnterprisePreviewTests
{
    private const string SuggestionSource = "enterprise\napplication-component a \"Item\"\napplication-component b \"Item\"\na -flow-> b\n";
    private static readonly DiagramLayoutOptions SuggestionOptions = new() { EnableRouteAwareConstruction = true };

    [Test]
    public void APreflightedPlacementLeavesTheAcceptedSourceGeometryLabelsAndIdentityUntouched()
    {
        var Host = BentAcceptedScene();
        var Before = Host.CopyInteractionSnapshot()!;
        var State = DiagramPlacementStateKey.From(Before.Layout);
        string Id = Before.Model.Edges.Single().Id!;
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        PlacementSuggestionPreview? Proposal;
        try { Proposal = Host.PreparePlacementSuggestion(Before.SourceText, Id, SuggestionOptions, CancellationToken.None, out var Reason);
            Assert.That(Proposal, Is.Not.Null, Reason); }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
        Assert.Multiple(() =>
        {
            Assert.That(Placements, Is.Zero);
            Assert.That(Proposal!.Comparison.IsImprovement, Is.True);
            Assert.That(Proposal.Png.Length, Is.GreaterThan(100));
            Assert.That(Proposal.Edit.Apply(Before.SourceText), Does.Contain(" at "));
            Assert.That(Host.CopyInteractionSnapshot()!.IsStale, Is.False);
            Assert.That(Host.CopyInteractionSnapshot()!.SourceText, Is.EqualTo(Before.SourceText));
            Assert.That(DiagramPlacementStateKey.From(Host.CopyPlacementLayout()!), Is.EqualTo(State));
            Assert.That(Proposal.Origin, Is.EqualTo(new DiagramEvaluationOrigin(Before.DocumentId, Before.SourceRevision, Before.Revision)));
        });
        var Repeat = Host.PreparePlacementSuggestion(Before.SourceText, Id, SuggestionOptions, CancellationToken.None, out _)!;
        Assert.That(Repeat.Svg, Is.EqualTo(Proposal!.Svg));
        Assert.That(Repeat.Edit, Is.EqualTo(Proposal.Edit));
        string Changed = Proposal.Edit.Apply(Before.SourceText);
        Host.Observe(Before.SourceRevision + 1);
        var Applied = Host.Prepare(Changed, Before.SourceRevision + 1, EnterpriseValidator.Analyze(Changed), SuggestionOptions);
        Assert.That(Applied.Svg, Is.EqualTo(Proposal.Svg));
        Assert.That(Host.TryCommit(Applied), Is.True);
        Assert.That(DiagramPlacementStateKey.From(Host.CopyPlacementLayout()!), Is.EqualTo(DiagramPlacementStateKey.From(Proposal.Comparison.Trial.CopyCandidate()!.Layout)));
        Host.Observe(Before.SourceRevision + 2);
        var Undo = Host.Prepare(Before.SourceText, Before.SourceRevision + 2, EnterpriseValidator.Analyze(Before.SourceText), SuggestionOptions);
        Assert.That(Host.TryCommit(Undo), Is.True);
        Assert.That(DiagramPlacementStateKey.From(Host.CopyPlacementLayout()!), Is.EqualTo(State));
    }

    [Test]
    public void ASourceEditDuringSuggestionPreparationCannotChangeTheAcceptedScene()
    {
        var Host = BentAcceptedScene();
        var Before = Host.CopyInteractionSnapshot()!;
        Host.Observe(Before.SourceRevision + 1);
        Assert.Throws<InvalidOperationException>(() => Host.PreparePlacementSuggestion(Before.SourceText,
            Before.Model.Edges.Single().Id!, SuggestionOptions, CancellationToken.None, out _));
        Assert.That(DiagramPlacementStateKey.From(Host.CopyPlacementLayout()!), Is.EqualTo(DiagramPlacementStateKey.From(Before.Layout)));
    }

    [Test]
    public void DetachedPreflightCannotEnterInitialLayoutForAnUnsupportedChange()
    {
        var Host = BentAcceptedScene(); var Before = Host.CopyInteractionSnapshot()!;
        var Fork = Host.ForkForEvaluation();
        Fork.Observe(Before.SourceRevision + 1);
        string Changed = Before.SourceText + "direction LR\n";
        int Placements = 0;
        DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
        try
        {
            var Failure = Assert.Throws<InvalidOperationException>(() => Fork.Prepare(Changed, Before.SourceRevision + 1,
                EnterpriseValidator.Analyze(Changed), SuggestionOptions, AllowInitialLayout: false));
            Assert.That(Failure!.Message, Does.Contain("requires retained geometry"));
        }
        finally { DiagramGridLayout.LayoutStageObserver = null; }
        Assert.That(Placements, Is.Zero);
        Assert.That(Host.CopyInteractionSnapshot()!.IsStale, Is.False);
        Assert.That(DiagramPlacementStateKey.From(Host.CopyPlacementLayout()!), Is.EqualTo(DiagramPlacementStateKey.From(Before.Layout)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SuggestionReviewCancelsExactlyOrAppliesOneUndoableSourceChange(bool Apply)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = SuggestionForm();
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            string Before = Editor.Text, Svg = Preview.RenderedSvg!;
            byte[] Png = Preview.LastPreviewPngBytes!;
            PlacementSuggestionPreview? Captured = null;
            int TextChanges = 0, Placements = 0;
            string ObservedText = Editor.Text;
            Editor.TextChanged += (_, _) => { if (Editor.Text != ObservedText) { TextChanges++; ObservedText = Editor.Text; } };
            DiagramGridLayout.LayoutStageObserver = (_, _) => Placements++;
            Form.PlacementSuggestionReview = Proposal =>
            {
                Assert.That(Editor.Text, Is.EqualTo(Before));
                Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
                Assert.That(Preview.LastPreviewPngBytes, Is.SameAs(Png));
                Captured = Proposal; return Apply;
            };
            try
            {
                Assert.That(Form.SuggestPlacementImprovement(), Is.True);
                Wait(() => Captured != null || Form.LastPlacementSuggestionReason != null);
                Assert.That(Captured, Is.Not.Null, Form.LastPlacementSuggestionReason);
                if (!Apply)
                {
                    Assert.That(Editor.Text, Is.EqualTo(Before));
                    Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
                    Assert.That(Preview.LastPreviewPngBytes, Is.SameAs(Png));
                    Assert.That(TextChanges, Is.Zero);
                    Assert.That(Placements, Is.Zero);
                    return;
                }
                Wait(() => Preview.RenderedSvg == Captured!.Svg && Form.DiagramMetadata.PercentRendered == 100);
                Assert.That(TextChanges, Is.EqualTo(1));
                Assert.That(Editor.Text, Is.EqualTo(Captured!.Edit.Apply(Before)));
                Assert.That(Form.LastRetainedUpdateKind, Is.EqualTo("LocalRouting"));
                Assert.That(Form.LastRebuiltRouteIds, Is.EqualTo(Captured.Comparison.Trial.RebuiltRouteIds));
                Assert.That(Form.LastRouteReport!.Bends, Is.EqualTo(Captured.Comparison.After!.Routes.Bends));
                Editor.Undo();
                Wait(() => Preview.RenderedSvg == Svg);
                Assert.That(Editor.Text, Is.EqualTo(Before));
                Assert.That(TextChanges, Is.EqualTo(2));
                Editor.Redo();
                Wait(() => Preview.RenderedSvg == Captured.Svg);
                Assert.That(TextChanges, Is.EqualTo(3));
            }
            finally { DiagramGridLayout.LayoutStageObserver = null; }
            Assert.That(Placements, Is.Zero);
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void TheVisualReviewContainsBothAcceptedAndProposedDiagramsAndMeasuredTotals()
    {
        var Host = BentAcceptedScene(); var Before = Host.CopyInteractionSnapshot()!;
        var Proposal = Host.PreparePlacementSuggestion(Before.SourceText, Before.Model.Edges.Single().Id!, SuggestionOptions, CancellationToken.None, out _)!;
        Host.Observe(Before.SourceRevision + 1);
        var Redraw = Host.Prepare(Before.SourceText, Before.SourceRevision + 1, EnterpriseValidator.Analyze(Before.SourceText), SuggestionOptions);
        byte[] Png = MDConverter.MermaidPreviewRender.RasterizeAcceptedSvg(Redraw.Svg, out _)!;
        using var Dialog = new PlacementSuggestionDialog(Proposal, Redraw.Svg, Png);
        Dialog.Show(); Application.DoEvents();
        var Left = (PreviewCanvas)Dialog.Controls.Find("BeforeSuggestionPreview", true)[0];
        var Right = (PreviewCanvas)Dialog.Controls.Find("AfterSuggestionPreview", true)[0];
        Assert.That(Left.RenderedSvg, Is.EqualTo(Redraw.Svg));
        Assert.That(Right.RenderedSvg, Is.EqualTo(Proposal.Svg));
        Assert.That(Left.Image, Is.Not.Null); Assert.That(Right.Image, Is.Not.Null);
        string Text = ((Label)Dialog.Controls.Find("SuggestionMetrics", true)[0]).Text;
        Assert.That(Text, Does.Contain("Bends:")); Assert.That(Text, Does.Contain("Rendered width:")); Assert.That(Text, Does.Contain("Displacement:"));
        Assert.That(Dialog.AcceptButton, Is.EqualTo(Dialog.Controls.Find("ApplySuggestionButton", true)[0]));
        Assert.That(Dialog.CancelButton, Is.EqualTo(Dialog.Controls.Find("CancelSuggestionButton", true)[0]));
        if (Environment.GetEnvironmentVariable("DIAGRAM_SUGGESTION_SNAPSHOT") is { Length: > 0 } Path)
        {
            using var Image = new System.Drawing.Bitmap(Dialog.Width, Dialog.Height);
            Dialog.DrawToBitmap(Image, new System.Drawing.Rectangle(0, 0, Dialog.Width, Dialog.Height));
            Image.Save(Path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    [Test]
    public void AChangedSourceOrFlagCannotApplyAnOlderReviewedSuggestion()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = SuggestionForm();
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            string Original = Editor.Text;
            PlacementSuggestionPreview? Captured = null;
            Form.PlacementSuggestionReview = Proposal => { Captured = Proposal; Editor.AppendText("%% newer source\n"); return true; };
            Assert.That(Form.SuggestPlacementImprovement(), Is.True);
            Wait(() => Captured != null);
            Wait(() => Form.LastRouteReport != null && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Editor.Text, Is.EqualTo(Original + "%% newer source\n"));
            Assert.That(Form.ApplyPlacementSuggestion(Captured!), Is.False);
            LiveEditorFeatureGate.AG_RouteAwareDiagramConstruction = false;
            Assert.That(Form.SuggestPlacementImprovement(), Is.False);
            Assert.That(Form.ApplyPlacementSuggestion(Captured!), Is.False);
        }
        finally { Restore(Previous); }
    }

    [Test]
    public void TypingImmediatelyAfterRequestingASuggestionCancelsItsReview()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = SuggestionForm();
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            int Reviews = 0, Generation = Form.DiagramMetadata.Generation;
            Form.PlacementSuggestionReview = _ => { Reviews++; return true; };
            Assert.That(Form.SuggestPlacementImprovement(), Is.True);
            Editor.AppendText("%% cancel pending suggestion\n");
            Wait(() => Form.DiagramMetadata.Generation > Generation && Form.DiagramMetadata.PercentRendered == 100);
            Assert.That(Reviews, Is.Zero);
            Assert.That(Editor.Text, Does.Not.Contain(" at "));
            Assert.That(Form.LastPlacementSuggestion, Is.Null);
        }
        finally { Restore(Previous); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SuggestionCommandIsHiddenAndUnavailableWhenAgIsOff(bool Af)
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm(Af);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            Editor.Text = SuggestionSource;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            var Item = (ToolStripMenuItem)typeof(MainForm).GetField("_SuggestPlacementMenuItem", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Form)!;
            Assert.That(Item.Available, Is.False);
            Assert.That(Form.SuggestPlacementImprovement(), Is.False);
            Assert.That(Editor.Text, Is.EqualTo(SuggestionSource));
        }
        finally { Restore(Previous); }
    }

    private static MainForm SuggestionForm()
    {
        var Form = new MainForm(new SessionSettings { RouteAwareDiagramConstruction = true, RenderDelayMilliseconds = 50, HighlightDelayMilliseconds = 40 });
        Form.ShowInTaskbar = false; Form.ClientSize = new System.Drawing.Size(1200, 800); _ = Form.Handle;
        var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = SuggestionSource;
        Wait(() => Form.LastRouteReport != null);
        var Host = BentAcceptedScene();
        typeof(MainForm).GetField("_RetainedPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Form, Host);
        Editor.Text = Host.CopyInteractionSnapshot()!.SourceText;
        Wait(() => Form.LastRouteReport is { Bends: > 0 } && Form.DiagramMetadata.PercentRendered == 100);
        return Form;
    }

    // Exercise a valid completed scene with room for improvement, independently
    // of whichever arrangement the initial producer currently happens to choose.
    private static RetainedEnterprisePreview BentAcceptedScene()
    {
        var Host = new RetainedEnterprisePreview();
        Host.Observe(1);
        var Initial = Host.Prepare(SuggestionSource, 1, EnterpriseValidator.Analyze(SuggestionSource), SuggestionOptions);
        Assert.That(Host.TryCommit(Initial), Is.True);
        var Service = Initial.CandidateService;
        var Snapshot = Service.CopySnapshot();
        var Geometry = DiagramGridLayout.RetainedRoutingState.Capture(Snapshot.Model, Snapshot.Layout);
        var Old = Snapshot.Layout.CellNodes!.Single(Node => Node.Id == "b").CellNode!.Region;
        string? Failure = Geometry.TryMove(new System.Collections.Generic.Dictionary<string, FlowchartCellRegion>
            { ["b"] = new(Old.StartRow, Old.StartColumn + 2, Old.RowSpan, Old.ColumnSpan) }, FlowchartLayoutRules.RepairChainLines, out _, out _);
        Assert.That(Failure, Is.Null);
        Assert.That(FlowchartRouteRuleChecker.Check(Geometry.Contract, Geometry.Layout), Is.Empty);
        var Field = typeof(DiagramSourceUpdateService).GetField("Committed", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var Accepted = (DiagramSourceUpdateService.Accepted)Field.GetValue(Service)!;
        Field.SetValue(Service, Accepted with { Geometry = Geometry });
        Host.Observe(2);
        string Source = SuggestionSource + "%% completed arrangement\n";
        var Drawn = Host.Prepare(Source, 2, EnterpriseValidator.Analyze(Source), SuggestionOptions);
        Assert.That(Host.TryCommit(Drawn), Is.True);
        return Host;
    }
}
