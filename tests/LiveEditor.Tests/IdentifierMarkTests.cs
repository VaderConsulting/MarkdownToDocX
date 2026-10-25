using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Double-clicking an identifier keeps the Windows selection and marks every other copy.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class IdentifierMarkTests
{
    [Test]
    public void DoubleClickKeepsTheSelectionAndMarksOtherCopies()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        string Gap = new string('\n', 12);
        string Source = "enterprise\n  application-component shop \"Web Shop\"" + Gap + "  application-component shopFloor \"Floor\"\n  shop -association-> shopFloor\n";
        Editor.Text = Source;
        Application.DoEvents();
        string Text = Editor.Text;
        int First = Text.IndexOf(" shop", StringComparison.Ordinal);
        Assert.That(First, Is.GreaterThanOrEqualTo(0));
        First += 1;
        int Second = Text.IndexOf("\n  shop ", StringComparison.Ordinal);
        Assert.That(Second, Is.GreaterThan(First));
        Second = Text.IndexOf("shop", Second, StringComparison.Ordinal);
        int Floor = Text.IndexOf("shopFloor", StringComparison.Ordinal);
        Editor.Select(First, "shop".Length);
        RaiseDoubleClick(Editor);
        Application.DoEvents();

        Assert.That(Editor.SelectedText, Is.EqualTo("shop"));
        Assert.That(Editor.SelectionStart, Is.EqualTo(First));
        Assert.That(Editor.SelectionLength, Is.EqualTo("shop".Length));
        Assert.That(Form.AssistPopupVisible, Is.True);
        Point Word = Editor.PointToScreen(Editor.GetPositionFromCharIndex(First));
        Point Other = Editor.PointToScreen(Editor.GetPositionFromCharIndex(Second));
        Point Popup = Form.AssistPopupLocation;
        int DistanceToWord = Math.Abs(Popup.X - Word.X) + Math.Abs(Popup.Y - Word.Y);
        int DistanceToOther = Math.Abs(Popup.X - Other.X) + Math.Abs(Popup.Y - Other.Y);
        Assert.That(DistanceToWord, Is.LessThan(DistanceToOther));
        Assert.That(Math.Abs(Popup.X - Word.X), Is.LessThan(120));
        Assert.That(Popup.Y, Is.GreaterThan(Word.Y - 4));
        Assert.That(Popup.Y - Word.Y, Is.LessThan(Editor.Font.Height + 48));

        Color Mark = MermaidSyntaxHighlighter.OtherOccurrenceColour(false);
        Assert.That(Form.TextBackColorAt(First).ToArgb(), Is.Not.EqualTo(Mark.ToArgb()));
        Assert.That(Form.TextBackColorAt(Second).ToArgb(), Is.EqualTo(Mark.ToArgb()));
        Assert.That(Form.TextBackColorAt(Floor).ToArgb(), Is.Not.EqualTo(Mark.ToArgb()));
        Assert.That(Editor.Focused, Is.True);

        Editor.Select(0, 0);
        Application.DoEvents();
        Assert.That(Editor.SelectionLength, Is.EqualTo(0));
        Assert.That(Form.HasMarkedIdentifier, Is.False);
        Assert.That(Form.TextBackColorAt(Second).ToArgb(), Is.Not.EqualTo(Mark.ToArgb()));
        Assert.That(Form.TextBackColorAt(First).ToArgb(), Is.Not.EqualTo(Mark.ToArgb()));
    }

    private static void RaiseDoubleClick(RichTextBox Editor)
    {
        MethodInfo? OnDoubleClick = typeof(Control).GetMethod("OnMouseDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic);
        if (OnDoubleClick == null)
        {
            throw new InvalidOperationException("OnMouseDoubleClick is missing.");
        }

        OnDoubleClick.Invoke(Editor, new object[] { new MouseEventArgs(MouseButtons.Left, 2, 1, 1, 0) });
    }

    private static MainForm OpenEditor()
    {
        SessionSettings Settings = new SessionSettings
        {
            DiagnosticsVisible = false,
            HighlightDelayMilliseconds = 5000,
            RenderDelayMilliseconds = 60000,
            WideToolbar = true,
            EditorOnLeft = true,
            VerticalSplit = true
        };
        MainForm Form = new MainForm(Settings)
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(30, 30),
            ClientSize = new Size(1000, 700)
        };
        Form.Show();
        Application.DoEvents();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        Editor.Focus();
        Application.DoEvents();
        return Form;
    }

    private static T Require<T>(Control Root, string Name) where T : Control
    {
        Control[] Matches = Root.Controls.Find(Name, true);
        if (Matches.Length != 1 || Matches[0] is not T Typed)
        {
            throw new InvalidOperationException("Missing " + Name);
        }

        return Typed;
    }
}
