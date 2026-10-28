using System.Reflection;
using LiveEditor.Editor;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Find, line edits, diagnostics, zoom, and undo on the code editor.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorEaseTests
{
    private Form _Host = null!;

    private CodeEditor _Editor = null!;

    private RichTextBox _Box = null!;

    [SetUp]
    public void SetUp()
    {
        _Host = new Form
        {
            ShowInTaskbar = false,
            ClientSize = new System.Drawing.Size(800, 500)
        };
        _Editor = new CodeEditor
        {
            Dock = DockStyle.Fill,
            HighlightDelayMilliseconds = 5000
        };
        _Host.Controls.Add(_Editor);
        _Host.Show();
        _Box = _Editor.Editor;
        _Box.Focus();
        Application.DoEvents();
    }

    [TearDown]
    public void TearDown()
    {
        _Host.Dispose();
        Application.DoEvents();
    }

    [Test]
    public void FindWrapsAndRespectsMatchCase()
    {
        SetText("Alpha beta alpha");
        _Box.Select(0, 0);
        _Editor.ShowFind();
        TextBox Query = (TextBox)_Editor.Controls.Find("QueryBox", true)[0];
        CheckBox MatchCase = (CheckBox)_Editor.Controls.Find("MatchCaseCheckBox", true)[0];
        Query.Text = "alpha";
        MatchCase.Checked = false;
        _Editor.FindNextMatch();
        Assert.That(_Box.SelectionStart, Is.EqualTo(0));
        _Editor.FindNextMatch();
        Assert.That(_Box.SelectionStart, Is.EqualTo(11));
        _Editor.FindNextMatch();
        Assert.That(_Box.SelectionStart, Is.EqualTo(0));

        MatchCase.Checked = true;
        _Editor.FindNextMatch();
        Assert.That(_Box.SelectionStart, Is.EqualTo(11));
    }

    [Test]
    public void ReplaceAllChangesEveryMatch()
    {
        SetText("one two one");
        _Editor.ShowReplace();
        TextBox Query = (TextBox)_Editor.Controls.Find("QueryBox", true)[0];
        TextBox Replacement = (TextBox)_Editor.Controls.Find("ReplaceBox", true)[0];
        Query.Text = "one";
        Replacement.Text = "1";
        _Editor.ReplaceAllMatches();
        Assert.That(Normalised(_Box.Text), Is.EqualTo("1 two 1"));
    }

    [Test]
    public void GoToLineMovesTheCaret()
    {
        SetText("alpha\nbeta\ngamma");
        _Editor.GoToLineNumber(2);
        Assert.That(_Box.SelectionStart, Is.EqualTo(6));
        Assert.That(_Box.SelectionLength, Is.EqualTo(0));
    }

    [Test]
    public void TabIndentsTheSelectionAndShiftTabRemovesIt()
    {
        SetText("alpha\nbeta");
        _Box.Select(0, _Box.TextLength);
        Press(Keys.Tab);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("  alpha\n  beta"));
        Press(Keys.Shift | Keys.Tab);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\nbeta"));
    }

    [Test]
    public void CommentChordAddsAndRemovesAMermaidComment()
    {
        SetText("  alpha\nbeta");
        _Box.Select(2, 1);
        Press(Keys.Control | Keys.K);
        Press(Keys.Control | Keys.C);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("  %% alpha\nbeta"));
        Press(Keys.Control | Keys.K);
        Press(Keys.Control | Keys.U);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("  alpha\nbeta"));
    }

    [Test]
    public void DuplicateAndDeleteLineUseTheirShortcuts()
    {
        SetText("alpha\nbeta\ngamma");
        _Box.Select(7, 0);
        Press(Keys.Control | Keys.E);
        Press(Keys.V);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\nbeta\nbeta\ngamma"));

        Press(Keys.Control | Keys.Shift | Keys.L);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\nbeta\ngamma"));
    }

    [Test]
    public void NextAndPreviousErrorWalkTheDiagnosticLines()
    {
        SetText("alpha\nbeta\ngamma\ndelta");
        _Editor.TintErrorLines(new int[] { 2, 4 });
        _Box.Select(0, 0);
        _Editor.NextDiagnostic();
        Assert.That(_Box.SelectionStart, Is.EqualTo(6));
        _Editor.NextDiagnostic();
        Assert.That(_Box.SelectionStart, Is.EqualTo(17));
        _Editor.NextDiagnostic();
        Assert.That(_Box.SelectionStart, Is.EqualTo(6));
        _Editor.PreviousDiagnostic();
        Assert.That(_Box.SelectionStart, Is.EqualTo(17));
    }

    [Test]
    public void ClickingALineNumberSelectsThatLine()
    {
        SetText("alpha\nbeta\ngamma");
        Application.DoEvents();
        LineNumberGutter? Gutter = null;
        foreach (Control Child in _Editor.Controls)
        {
            Gutter = Child as LineNumberGutter;
            if (Gutter != null)
            {
                break;
            }
        }

        Assert.That(Gutter, Is.Not.Null);
        System.Drawing.Point Position = _Box.GetPositionFromCharIndex(_Box.GetFirstCharIndexFromLine(1));
        MethodInfo? OnMouseDown = typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        OnMouseDown!.Invoke(Gutter, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 4, Position.Y + 2, 0) });
        Assert.That(Normalised(_Box.SelectedText), Is.EqualTo("beta\n"));
    }

    [Test]
    public void ZoomChangesTheEditorFontAndResetRestoresTheSessionSize()
    {
        // MainForm applies the session font before zooming; designer sizes may be below the zoom minimum.
        _Editor.ApplyFont(11F, false);
        float Started = _Box.Font.Size;
        _Editor.ZoomIn();
        Assert.That(_Box.Font.Size, Is.EqualTo(Started + 1F));
        _Editor.ResetZoom();
        Assert.That(_Box.Font.Size, Is.EqualTo(Started));
    }

    [Test]
    public void ColouringDoesNotConsumeTheTextUndo()
    {
        SetText("one");
        _Box.Select(3, 0);
        _Box.SelectedText = "X";
        Assert.That(Normalised(_Box.Text), Is.EqualTo("oneX"));
        _Editor.ApplyEditorColours();
        _Editor.UndoEdit();
        Assert.That(Normalised(_Box.Text), Is.EqualTo("one"));
    }

    private void SetText(string Text)
    {
        _Box.Text = Text;
        Application.DoEvents();
    }

    private void Press(Keys KeyData)
    {
        MethodInfo? OnKeyDown = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
        OnKeyDown!.Invoke(_Box, new object[] { new KeyEventArgs(KeyData) });
        Application.DoEvents();
    }

    private static string Normalised(string Text)
    {
        return Text.Replace("\r\n", "\n", System.StringComparison.Ordinal).Replace('\r', '\n');
    }
}
