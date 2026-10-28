using System;
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
    public void RebindingWhileDpiFontIsActiveDoesNotDisposeTheExplicitFont()
    {
        IntPtr PreviousContext = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            using Font EditorFont = new Font("Consolas", 11F);
            using Font ScaledFont = new Font("Consolas", 16.5F);
            using RichTextBox Editor = new RichTextBox { Font = EditorFont, Text = "one\ntwo" };
            using LineNumberGutter Gutter = new LineNumberGutter();
            Editor.CreateControl();
            Gutter.Bind(Editor);
            var ScaledFontField = typeof(Control).GetField("_scaledControlFont",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            try
            {
                ScaledFontField.SetValue(Gutter, ScaledFont);
                Assert.That(Gutter.Font, Is.SameAs(ScaledFont));
                Gutter.Bind(Editor);
            }
            finally
            {
                ScaledFontField.SetValue(Gutter, null);
            }
            Assert.That(Gutter.Font.Height, Is.GreaterThan(0));
            using Bitmap Picture = new Bitmap(Gutter.Width, 100);
            Gutter.DrawToBitmap(Picture, new Rectangle(Point.Empty, Picture.Size));
        }
        finally
        {
            SetThreadDpiAwarenessContext(PreviousContext);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr Context);

    [Test]
    public void PaintingDoesNotUseARetiredDpiFont()
    {
        IntPtr PreviousContext = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            using Font EditorFont = new Font("Consolas", 9F);
            using Font ScaledFont = new Font("Consolas", 13.5F);
            using RichTextBox Editor = new RichTextBox { Font = EditorFont, Text = "one\ntwo" };
            using LineNumberGutter Gutter = new LineNumberGutter();
            Editor.CreateControl();
            Gutter.Bind(Editor);
            var ScaledFontField = typeof(Control).GetField("_scaledControlFont",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            try
            {
                ScaledFontField.SetValue(Gutter, ScaledFont);
                ScaledFont.Dispose();
                using Bitmap Picture = new Bitmap(Gutter.Width, 100);
                using Graphics Canvas = Graphics.FromImage(Picture);
                using PaintEventArgs Paint = new PaintEventArgs(Canvas, new Rectangle(Point.Empty, Picture.Size));
                typeof(LineNumberGutter).GetMethod("OnPaint",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(Gutter, new object[] { Paint });
            }
            finally
            {
                ScaledFontField.SetValue(Gutter, null);
            }
        }
        finally
        {
            SetThreadDpiAwarenessContext(PreviousContext);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RebindingAnEditorWithTheSameFontKeepsThePaintFontAlive(bool DifferentEditor)
    {
        using Font EditorFont = new Font("Consolas", 11F);
        using RichTextBox Editor = new RichTextBox { Font = EditorFont, Text = "one\ntwo" };
        using RichTextBox OtherEditor = new RichTextBox { Font = EditorFont, Text = "three\nfour" };
        using LineNumberGutter Gutter = new LineNumberGutter();
        Editor.CreateControl();
        OtherEditor.CreateControl();
        Gutter.Bind(Editor);

        for (int Attempt = 0; Attempt < 3; Attempt++)
        {
            Gutter.Bind(DifferentEditor ? OtherEditor : Editor);
            Assert.That(Gutter.Font.Height, Is.GreaterThan(0));
            using Bitmap Picture = new Bitmap(Gutter.Width, 100);
            Gutter.DrawToBitmap(Picture, new Rectangle(Point.Empty, Picture.Size));
        }
    }

    [Test]
    public void FontChangesAndRepeatedNotificationsKeepTheGutterPaintable()
    {
        using Font SmallFont = new Font("Consolas", 8F);
        using Font MediumFont = new Font("Consolas", 12F);
        using Font LargeFont = new Font("Consolas", 32F);
        using Font SessionFont = new Font("Consolas", 11F);
        using RichTextBox Editor = new RichTextBox { Text = "one\ntwo" };
        using LineNumberGutter Gutter = new LineNumberGutter();
        Editor.CreateControl();
        Gutter.Bind(Editor);
        var FontChanged = typeof(Control).GetMethod("OnFontChanged",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        foreach (Font EditorFont in new[] { SmallFont, MediumFont, LargeFont, SessionFont })
        {
            Editor.Font = EditorFont;
            FontChanged.Invoke(Editor, new object[] { System.EventArgs.Empty });
            Assert.That(Gutter.Font.Size, Is.EqualTo(EditorFont.Size));
            Assert.That(Gutter.Font.Height, Is.GreaterThan(0));
            Size FourDigits = TextRenderer.MeasureText("9999", Editor.Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            Assert.That(Gutter.Width, Is.EqualTo(FourDigits.Width + 16));
            using Bitmap Picture = new Bitmap(Gutter.Width, 100);
            Gutter.DrawToBitmap(Picture, new Rectangle(Point.Empty, Picture.Size));
        }
    }

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
