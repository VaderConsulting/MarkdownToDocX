using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Alt+Up and Alt+Down move the selected lines, as in Visual Studio.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class LineMoveTests
{
    private Form _Host = null!;

    private CodeEditor _Editor = null!;

    private RichTextBox _Box = null!;

    [SetUp]
    public void SetUp()
    {
        _Host = new Form
        {
            ShowInTaskbar = false
        };
        _Editor = new CodeEditor
        {
            Dock = DockStyle.Fill
        };
        _Host.Controls.Add(_Editor);
        _Host.Show();
        _Box = _Editor.Editor;
        _Box.Focus();
    }

    [TearDown]
    public void TearDown()
    {
        _Host.Dispose();
        Application.DoEvents();
    }

    [Test]
    public void AltUpMovesTheCurrentLineAndKeepsTheColumn()
    {
        SetText("alpha\nbeta\ngamma");
        _Box.Select(7, 0);

        Press(Keys.Alt | Keys.Up);

        Assert.That(Normalised(_Box.Text), Is.EqualTo("beta\nalpha\ngamma"));
        Assert.That(_Box.SelectionStart, Is.EqualTo(1));
        Assert.That(_Box.SelectionLength, Is.EqualTo(0));
    }

    [Test]
    public void AltDownMovesTheCurrentLineAndKeepsTheColumn()
    {
        SetText("alpha\nbeta\ngamma");
        _Box.Select(7, 0);

        Press(Keys.Alt | Keys.Down);

        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\ngamma\nbeta"));
        Assert.That(_Box.SelectionStart, Is.EqualTo(13));
        Assert.That(_Box.SelectionLength, Is.EqualTo(0));
    }

    [Test]
    public void TheFirstAndLastLinesStayPut()
    {
        SetText("alpha\nbeta\ngamma");
        _Box.Select(1, 0);
        Press(Keys.Alt | Keys.Up);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\nbeta\ngamma"));
        Assert.That(_Box.SelectionStart, Is.EqualTo(1));

        _Box.Select(14, 0);
        Press(Keys.Alt | Keys.Down);
        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\nbeta\ngamma"));
        Assert.That(_Box.SelectionStart, Is.EqualTo(14));
    }

    [Test]
    public void ASelectionMovesEveryLineItTouches()
    {
        SetText("one\ntwo\nthree\nfour");
        _Box.Select(5, 6);

        Press(Keys.Alt | Keys.Up);

        Assert.That(Normalised(_Box.Text), Is.EqualTo("two\nthree\none\nfour"));
        Assert.That(_Box.SelectionStart, Is.EqualTo(1));
        Assert.That(_Box.SelectionLength, Is.EqualTo(6));
    }

    [Test]
    public void ASelectionThatEndsAtTheNextLineDoesNotTakeThatLine()
    {
        SetText("one\ntwo\nthree\nfour");
        _Box.Select(4, 10);

        Press(Keys.Alt | Keys.Down);

        Assert.That(Normalised(_Box.Text), Is.EqualTo("one\nfour\ntwo\nthree"));
        Assert.That(_Box.SelectedText.Replace("\r", string.Empty, StringComparison.Ordinal), Is.EqualTo("two\nthree"));
    }

    [Test]
    public void ABlankLineMovesWithTheShortcut()
    {
        SetText("alpha\n\nbeta");
        _Box.Select(6, 0);

        Press(Keys.Alt | Keys.Down);

        Assert.That(Normalised(_Box.Text), Is.EqualTo("alpha\nbeta\n"));
        Assert.That(_Box.SelectionStart, Is.EqualTo(11));
    }

    private void SetText(string Text)
    {
        _Box.Text = Text;
        Application.DoEvents();
    }

    private void Press(Keys KeyData)
    {
        MethodInfo? OnKeyDown = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(OnKeyDown, Is.Not.Null);
        OnKeyDown!.Invoke(_Box, new object[] { new KeyEventArgs(KeyData) });
        Application.DoEvents();
    }

    private static string Normalised(string Text)
    {
        return Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
