using System;
using System.IO;
using System.Windows.Forms;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

public sealed partial class RetainedEnterprisePreviewTests
{
    [Test]
    public void ImageExportAndCopyKeepAcceptedGeometryWhileSourceSaveKeepsCurrentText()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        var PreviousObserver = EnterpriseFlowchartEdgeRouter.StageTimingObserver;
        string Folder = Path.Combine(Path.GetTempPath(), "MarkdownToDocX-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        string SvgFile = Path.Combine(Folder, "diagram.svg"), PngFile = Path.Combine(Folder, "diagram.png"), SourceFile = Path.Combine(Folder, "diagram.mmd");
        try
        {
            using var Form = CreateForm(true);
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Assert.That(() => Form.ExportDiagramFile(SvgFile, true), Throws.InvalidOperationException);
            Assert.That(File.Exists(SvgFile), Is.False);
            Editor.Text = Source;
            Wait(() => Form.DiagramMetadata.PercentRendered == 100);
            Editor.Text = Source.Replace("a -flow-> b", "a -flow-> b [leave S@1, enter N@0]");
            Wait(() => Form.LastRetainedUpdateKind == "LocalRouting");
            string AcceptedSource = Editor.Text;
            string Svg = Preview.RenderedSvg!;
            byte[] Png = Preview.LastPreviewPngBytes!;
            Assert.That(Preview.PngForCopy(), Is.EqualTo(Png));
            int LayoutStages = 0;
            EnterpriseFlowchartEdgeRouter.StageTimingObserver = (_, _) => LayoutStages++;
            Form.ExportDiagramFile(SvgFile, true); Form.ExportDiagramFile(PngFile, false);
            Form.TrySaveSource(SourceFile); Application.DoEvents();
            Assert.That(File.ReadAllText(SvgFile), Is.EqualTo(Svg));
            Assert.That(File.ReadAllBytes(PngFile), Is.EqualTo(Png));
            Assert.That(File.ReadAllText(SourceFile), Is.EqualTo(AcceptedSource));
            var Reload = EnterpriseValidator.Analyze(File.ReadAllText(SourceFile));
            Assert.That(Reload.Document.Relationships[0].RoutingHint!.LeavePoint, Is.EqualTo(1));
            Assert.That(LayoutStages, Is.Zero);
            Editor.Text += "invalid statement\n";
            Wait(() => Form.DiagramMetadata.RenderFailed);
            LayoutStages = 0;
            Form.ExportDiagramFile(SvgFile, true); Form.ExportDiagramFile(PngFile, false);
            Form.TrySaveSource(SourceFile); Application.DoEvents();
            Assert.That(File.ReadAllText(SvgFile), Is.EqualTo(Svg));
            Assert.That(File.ReadAllBytes(PngFile), Is.EqualTo(Png));
            Assert.That(Preview.PngForCopy(), Is.EqualTo(Png));
            Assert.That(File.ReadAllText(SourceFile), Is.EqualTo(Editor.Text).And.EndsWith("invalid statement\n"));
            Assert.That(LayoutStages, Is.Zero);
        }
        finally
        {
            EnterpriseFlowchartEdgeRouter.StageTimingObserver = PreviousObserver;
            Restore(Previous);
            foreach (string FilePath in new[] { SvgFile, PngFile, SourceFile }) File.Delete(FilePath);
            Directory.Delete(Folder);
        }
    }
}
