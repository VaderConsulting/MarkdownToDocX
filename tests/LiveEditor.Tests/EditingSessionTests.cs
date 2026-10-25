using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Creates and edits a diagram in the real editor window.
/// A syntax error is reported in place. It must not take focus, because the correction is typed into the source.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditingSessionTests
{
    [Test]
    public void CreatingADiagramByTypingKeepsTheEditorFocused()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ReplaceSource(Editor, "enterprise\r\n  ");
        Editor.SelectionStart = Editor.TextLength;
        Editor.SelectedText = "application-component shop \"Shop\"";
        Application.DoEvents();

        Assert.That(Editor.Text, Does.Contain("application-component shop \"Shop\""));
        Assert.That(Editor.Focused, Is.True);
        Assert.That(Form.Text, Does.StartWith("* "));

        int Length = Editor.TextLength;
        TypeCharacter(Editor, 'z');
        Assert.That(Editor.TextLength, Is.EqualTo(Length + 1));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void EditingAnExistingLabelKeepsTheEditorFocused()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ReplaceSource(Editor, "enterprise\r\n  application-component shop \"Web Shop\"\r\n");
        int Start = Editor.Text.IndexOf("Web Shop", StringComparison.Ordinal);
        Editor.Select(Start, "Web Shop".Length);
        Editor.SelectedText = "Shop";
        Application.DoEvents();

        Assert.That(Editor.Text, Does.Contain("\"Shop\""));
        Assert.That(Editor.Text, Does.Not.Contain("Web Shop"));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void SyntaxErrorStaysInTheEditorAndStillAcceptsKeystrokes()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        ToolStripStatusLabel Status = RequireStatus(Form, "StatusLabel");
        string Source = "enterprise\r\n  application-component shop \"Shop\"\r\n  not-a-statement\r\n";
        ReplaceSource(Editor, Source);
        int Token = Editor.Text.IndexOf("not-a-statement", StringComparison.Ordinal);
        Editor.SelectionStart = Token + "not-a-statement".Length;
        Editor.Focus();
        WaitUntil(() => DiagnosticText(Diagnostics).Contains("cannot parse", StringComparison.Ordinal), 2000);

        string Listed = DiagnosticText(Diagnostics);
        Assert.That(Listed, Does.Contain("cannot parse"));
        Assert.That(Status.Text, Does.Contain("cannot parse"));
        Assert.That(Editor.Focused, Is.True);

        int Length = Editor.TextLength;
        TypeCharacter(Editor, 'z');
        Assert.That(Editor.TextLength, Is.EqualTo(Length + 1));
        Assert.That(Editor.Text, Does.Contain("not-a-statementz"));
        Assert.That(Editor.Focused, Is.True);

        int BadStart = Editor.Text.IndexOf("not-a-statementz", StringComparison.Ordinal);
        int LineEnd = Editor.Text.IndexOf('\n', BadStart);
        if (LineEnd < 0)
        {
            LineEnd = Editor.TextLength;
        }

        Editor.Select(BadStart, LineEnd - BadStart);
        Editor.SelectedText = "application-service api \"Api\"";
        WaitUntil(() => !DiagnosticText(Diagnostics).Contains("Error:", StringComparison.Ordinal), 2000);
        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        Assert.That(Editor.Text, Does.Contain("application-service api \"Api\""));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void ATypeNameOnItsOwnIsNotAnError()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        string Broken = "enterprise\r\n  application component comp \r\n";
        ReplaceSource(Editor, Broken);
        WaitUntil(() => DiagnosticText(Diagnostics).Contains("cannot parse", StringComparison.Ordinal), 2000);
        Assert.That(DiagnosticText(Diagnostics), Does.Contain("cannot parse"));

        ReplaceSource(Editor, "enterprise\r\n  application-component");
        Editor.SelectionStart = Editor.TextLength;
        Editor.Focus();
        WaitUntil(() => !DiagnosticText(Diagnostics).Contains("cannot parse", StringComparison.Ordinal), 2000);

        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("cannot parse"));
        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("application-component"));
        Assert.That(Editor.Focused, Is.True);

        int Length = Editor.TextLength;
        TypeCharacter(Editor, ' ');
        Assert.That(Editor.TextLength, Is.EqualTo(Length + 1));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void EnterOnABrokenLineInsertsANewline()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        TextBox Prompt = Require<TextBox>(Form, "PromptLabelBox");
        string Source = "enterprise\r\n  not-a-statement";
        ReplaceSource(Editor, Source);
        Editor.SelectionStart = Editor.TextLength;
        Editor.Focus();
        DismissPopup(Editor);
        int Lines = Editor.Lines.Length;
        PressEnter(Editor);
        Application.DoEvents();

        Assert.That(Editor.Lines.Length, Is.GreaterThan(Lines));
        Assert.That(Prompt.Visible, Is.False);
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void PaletteInsertReturnsFocusToTheEditor()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ReplaceSource(Editor, "enterprise\r\n  application-component shop \"Shop\"\r\n");
        Click(RequireGlyph(Form, "Application Component"));
        Application.DoEvents();

        TextBox Label = Require<TextBox>(Form, "PromptLabelBox");
        TextBox Identifier = Require<TextBox>(Form, "PromptIdBox");
        Assert.That(Label.Visible, Is.True);
        Assert.That(Label.Focused, Is.True);
        Label.Text = "Till";
        Application.DoEvents();
        if (Identifier.TextLength == 0)
        {
            Identifier.Text = "till";
        }

        Button Insert = Require<Button>(Form, "InsertLineButton");
        Insert.PerformClick();
        Application.DoEvents();

        Assert.That(Editor.Text, Does.Contain("application-component"));
        Assert.That(Editor.Text, Does.Contain("\"Till\""));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void PaletteValidationShowsAnErrorAndKeepsThePrompt()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ReplaceSource(Editor, "enterprise\r\n");
        Click(RequireGlyph(Form, "Application Component"));
        Application.DoEvents();

        TextBox Label = Require<TextBox>(Form, "PromptLabelBox");
        TextBox Identifier = Require<TextBox>(Form, "PromptIdBox");
        Label Error = Require<Label>(Form, "PromptErrorLabel");
        Label.Text = "Till";
        Identifier.Text = "1bad";
        Application.DoEvents();
        Require<Button>(Form, "InsertLineButton").PerformClick();
        Application.DoEvents();

        Assert.That(Error.Visible, Is.True);
        Assert.That(Error.Text, Is.Not.Empty);
        Assert.That(Identifier.Focused, Is.True);
        Assert.That(Editor.Text, Does.Not.Contain("1bad"));
        Assert.That(Label.Visible, Is.True);
    }

    private static MainForm OpenEditor()
    {
        SessionSettings Settings = new SessionSettings
        {
            DiagnosticsVisible = true,
            HighlightDelayMilliseconds = 40,
            RenderDelayMilliseconds = 40,
            WideToolbar = true,
            EditorOnLeft = true,
            VerticalSplit = true
        };
        MainForm Form = new MainForm(Settings)
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(40, 40),
            ClientSize = new Size(1200, 800)
        };
        Form.Show();
        Application.DoEvents();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        Editor.Focus();
        Application.DoEvents();
        return Form;
    }

    private static void ReplaceSource(RichTextBox Editor, string Source)
    {
        Editor.Focus();
        Editor.Text = Source;
        Editor.SelectionStart = Editor.TextLength;
        Application.DoEvents();
        DismissPopup(Editor);
        Editor.Focus();
    }

    private static void DismissPopup(RichTextBox Editor)
    {
        SendMessage(Editor.Handle, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
        SendMessage(Editor.Handle, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);
        Application.DoEvents();
    }

    private static void TypeCharacter(RichTextBox Editor, char Character)
    {
        Assert.That(Editor.Focused, Is.True);
        SendMessage(Editor.Handle, WM_CHAR, (IntPtr)Character, IntPtr.Zero);
        Application.DoEvents();
    }

    private static void PressEnter(RichTextBox Editor)
    {
        Assert.That(Editor.Focused, Is.True);
        SendMessage(Editor.Handle, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
        SendMessage(Editor.Handle, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
    }

    private static void Click(Control Control)
    {
        MethodInfo? OnClick = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        if (OnClick == null)
        {
            throw new InvalidOperationException("Control.OnClick is missing.");
        }

        OnClick.Invoke(Control, new object[] { EventArgs.Empty });
    }

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_CHAR = 0x0102;
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr HWnd, int Msg, IntPtr WParam, IntPtr LParam);

    private static void WaitUntil(Func<bool> Ready, int Milliseconds)
    {
        DateTime End = DateTime.UtcNow.AddMilliseconds(Milliseconds);
        while (DateTime.UtcNow < End)
        {
            Application.DoEvents();
            if (Ready())
            {
                return;
            }

            Thread.Sleep(20);
        }
    }

    private static string DiagnosticText(ListBox Diagnostics)
    {
        string Text = string.Empty;
        foreach (object Item in Diagnostics.Items)
        {
            if (Text.Length > 0)
            {
                Text += "\n";
            }

            Text += Item.ToString();
        }

        return Text;
    }

    private static ToolStripStatusLabel RequireStatus(Form Form, string Name)
    {
        StatusStrip Strip = Require<StatusStrip>(Form, "StatusBar");
        foreach (ToolStripItem Item in Strip.Items)
        {
            ToolStripStatusLabel? Label = Item as ToolStripStatusLabel;
            if (Label != null && string.Equals(Label.Name, Name, StringComparison.Ordinal))
            {
                return Label;
            }
        }

        throw new InvalidOperationException("Missing " + Name);
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

    private static ElementGlyphButton RequireGlyph(Control Root, string AccessibleName)
    {
        ElementGlyphButton? Found = FindGlyph(Root, AccessibleName);
        if (Found == null)
        {
            throw new InvalidOperationException("Missing " + AccessibleName);
        }

        return Found;
    }

    private static ElementGlyphButton? FindGlyph(Control Root, string AccessibleName)
    {
        ElementGlyphButton? Button = Root as ElementGlyphButton;
        if (Button != null && string.Equals(Button.AccessibleName, AccessibleName, StringComparison.Ordinal))
        {
            return Button;
        }

        foreach (Control Child in Root.Controls)
        {
            ElementGlyphButton? Nested = FindGlyph(Child, AccessibleName);
            if (Nested != null)
            {
                return Nested;
            }
        }

        return null;
    }
}
