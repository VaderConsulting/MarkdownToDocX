using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using LiveEditor.Editor;
using MermaidRecognition.Layout;
using System.Windows.Forms;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class KeyboardShortcutTests
{
    private const string Script = "enterprise\n  application-component a \"A\"\n  application-service b \"B\" below a +2\n  a -serving-> b\n";

    private DiagramLayoutOptions _Saved = DiagramLayoutOptions.Default;

    [SetUp]
    public void Save() => _Saved = LiveEditorFeatureGate.CaptureLayoutOptions();

    [TearDown]
    public void Restore() => LiveEditorFeatureGate.ApplyLayoutSettings(new SessionSettings { IncrementalDiagramRouting = _Saved.EnableIncrementalRouting,
        RouteAwareDiagramConstruction = _Saved.EnableRouteAwareConstruction, WidthConstrainedLayout = _Saved.EnableWidthConstrainedLayout,
        DiagramTargetWidthPx = (int)_Saved.TargetWidthPx });

    private static MainForm CreateForm()
    {
        var Form = new MainForm(new SessionSettings { IncrementalDiagramRouting = true, RenderDelayMilliseconds = 50, HighlightDelayMilliseconds = 40 });
        Form.ShowInTaskbar = false; Form.StartPosition = FormStartPosition.Manual; Form.Location = new Point(40, 40); Form.ClientSize = new Size(1200, 800);
        Form.Show();
        return Form;
    }

    private static bool Send(MainForm Form, Keys Key)
    {
        Control? Focused = Form.ActiveControl;
        while (Focused is ContainerControl Container && Container.ActiveControl != null) Focused = Container.ActiveControl;
        var Message = new Message { HWnd = (Focused ?? Form).Handle, Msg = 0x0100, WParam = (IntPtr)(int)(Key & Keys.KeyCode) };
        return (bool)typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Form, [Message, Key])!;
    }

    private static void KeyDown(Control Target, Keys Key)
        => typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Target, [new KeyEventArgs(Key)]);

    private static void Pump(Func<bool> Done)
    {
        DateTime End = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < End) { Application.DoEvents(); if (Done()) return; Thread.Sleep(20); }
        Assert.Fail("Timed out.");
    }

    [Test]
    public void UndoRedoWorkFromThePreviewAfterADrag()
    {
        var Previous = LiveEditorFeatureGate.CaptureLayoutOptions();
        try
        {
            using var Form = CreateForm();
            var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
            var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
            Editor.Text = Script;
            Pump(() => Form.DiagramMetadata.PercentRendered == 100 && Form.PreviewIndexIsCurrent);
            var B = Preview.Index!.FindObject("node\u001fb")!;
            Assert.That(Form.MovePreviewNode("b", new((float)((B.Left + B.Right) / 2 + 300), (float)((B.Top + B.Bottom) / 2))), Is.True);
            string Moved = Editor.Text;
            Assert.That(Moved, Is.Not.EqualTo(Script));

            Preview.FocusForKeys();
            Assert.That(Editor.Focused, Is.False, "the preview, not the editor, has focus");
            Assert.That(Send(Form, Keys.Control | Keys.Z), Is.True);
            Assert.That(Editor.Text, Is.EqualTo(Script));
            Assert.That(Send(Form, Keys.Control | Keys.Y), Is.True);
            Assert.That(Editor.Text, Is.EqualTo(Moved));
            Assert.That(Send(Form, Keys.Control | Keys.Z), Is.True);
            Assert.That(Editor.Text, Is.EqualTo(Script));
            Assert.That(Send(Form, Keys.Control | Keys.Shift | Keys.Z), Is.True);
            Assert.That(Editor.Text, Is.EqualTo(Moved));
        }
        finally { LiveEditorFeatureGate.ApplyLayoutSettings(new SessionSettings { IncrementalDiagramRouting = Previous.EnableIncrementalRouting, RouteAwareDiagramConstruction = Previous.EnableRouteAwareConstruction, WidthConstrainedLayout = Previous.EnableWidthConstrainedLayout, DiagramTargetWidthPx = (int)Previous.TargetWidthPx }); }
    }

    [Test]
    public void MenuShortcutsStillWorkWhilePreviewHasFocus()
    {
        using var Form = CreateForm();
        var Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
        Preview.FocusForKeys();
        var FindBar = Form.Controls.Find("FindBar", true).Single();
        Assert.That(FindBar.Visible, Is.False);
        Assert.That(Send(Form, Keys.Control | Keys.F), Is.True);
        Assert.That(FindBar.Visible, Is.True);
    }

    [Test]
    public void TextBoxesKeepTheirOwnUndoAndTheEditorKeepsNativeHandling()
    {
        using var Form = CreateForm();
        var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = Script;
        Editor.Focus();
        Assert.That(Editor.Focused, Is.True);
        string Before = Editor.Text;
        Assert.That(Send(Form, Keys.Control | Keys.Z), Is.False, "left to the editor's native undo");
        Assert.That(Send(Form, Keys.Control | Keys.Y), Is.False);
        Assert.That(Editor.Text, Is.EqualTo(Before));
        Assert.That(Send(Form, Keys.Control | Keys.Shift | Keys.Z), Is.True, "redo alternative");
    }

    [Test]
    public void EditorChordsCommentAndDuplicateTheCurrentLine()
    {
        using var Form = CreateForm();
        var Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = Script;
        Editor.Focus();
        Editor.SelectionStart = Script.IndexOf("a -serving", StringComparison.Ordinal);
        Editor.SelectionLength = 0;
        KeyDown(Editor, Keys.Control | Keys.K); KeyDown(Editor, Keys.Control | Keys.C);
        Assert.That(Editor.Text, Does.Contain("%% a -serving-> b").Or.Contain("%%a -serving-> b"));
        KeyDown(Editor, Keys.Control | Keys.K); KeyDown(Editor, Keys.Control | Keys.U);
        Assert.That(Editor.Text, Is.EqualTo(Script));
        KeyDown(Editor, Keys.Control | Keys.E); KeyDown(Editor, Keys.V);
        Assert.That(Editor.Text.Split('\n').Count(Line => Line.Contains("a -serving-> b")), Is.EqualTo(2));
    }
}
