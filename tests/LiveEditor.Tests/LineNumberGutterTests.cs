using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Checks the read-only line-number column beside the editor.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class LineNumberGutterTests
{
    [Test]
    public void ColumnIsWideEnoughForFourDigits()
    {
        using Form Host = new Form();
        using RichTextBox Editor = new RichTextBox();
        using LineNumberGutter Gutter = new LineNumberGutter();
        Editor.Font = new Font("Consolas", 11F, FontStyle.Regular, GraphicsUnit.Point);
        Editor.Dock = DockStyle.Fill;
        Gutter.Dock = DockStyle.Left;
        Host.Controls.Add(Editor);
        Host.Controls.Add(Gutter);
        Gutter.Bind(Editor);
        Host.ClientSize = new Size(500, 300);
        Host.Show();
        Editor.Text = "one\r\ntwo\r\nthree\r\nfour";
        Application.DoEvents();

        Size FourDigits = TextRenderer.MeasureText("9999", Editor.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Assert.That(Gutter.TabStop, Is.False);
        Assert.That(Gutter.Width, Is.EqualTo(FourDigits.Width + 16));
        Assert.That(Gutter.BackColor.ToArgb(), Is.EqualTo(Editor.BackColor.ToArgb()));
        Assert.That(Editor.Left, Is.EqualTo(Gutter.Right));
        Assert.That(Editor.Width, Is.EqualTo(Host.ClientSize.Width - Gutter.Width));
    }
}
