using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
public sealed partial class SettingsEditorFontTests
{
    [TestCase(9F)]
    [TestCase(14.5F)]
    public void AcceptingSettingsLeavesTheEditorGutterPaintable(float FontSize)
    {
        IntPtr PreviousContext = SetThreadDpiAwarenessContext(new IntPtr(-4));
        List<Exception> Failures = [];
        ThreadExceptionEventHandler Handler = (_, E) => Failures.Add(E.Exception);
        Application.ThreadException += Handler;
        try
        {
            SessionSettings Settings = new SessionSettings { EditorFontSize = 9F };
            using Form Host = new Form { ShowInTaskbar = false, ClientSize = new Size(800, 500) };
            using CodeEditor Editor = new CodeEditor { Dock = DockStyle.Fill, HighlightDelayMilliseconds = 5000 };
            float ReportedFontSize = 0F;
            Editor.FontSizeChanged += (_, Size) => ReportedFontSize = Size;
            Host.Controls.Add(Editor);
            Editor.ApplyFont(Settings.EditorFontSize, Settings.EditorWordWrap);
            LineNumberGutter Gutter = (LineNumberGutter)Editor.Controls.Find("LineNumberColumn", true)[0];
            Host.Show();
            Editor.Editor.Text = "enterprise\n  application-component shop \"Shop\"\n";
            Application.DoEvents();
            for (int Attempt = 0; Attempt < 3; Attempt++)
            {
                using SettingsForm Dialog = new SettingsForm(Settings);
                using System.Windows.Forms.Timer Accept = new() { Interval = 50 };
                Accept.Tick += (_, _) =>
                {
                    Accept.Stop();
                    ((NumericUpDown)Dialog.Controls.Find("FontSizeNumeric", true)[0]).Value = (decimal)FontSize;
                    ((Button)Dialog.AcceptButton!).PerformClick();
                };
                Accept.Start();
                Assert.That(Dialog.ShowDialog(Host), Is.EqualTo(DialogResult.OK));
                Dialog.ApplyTo(Settings);
                Editor.ApplyFont(Settings.EditorFontSize, Settings.EditorWordWrap);
                Editor.ApplyEditorColours();
                using Bitmap Picture = new Bitmap(Gutter.Width, 100);
                Gutter.DrawToBitmap(Picture, new Rectangle(Point.Empty, Picture.Size));
                Host.Refresh();
                Application.DoEvents();
                Assert.That(Settings.EditorFontSize, Is.EqualTo(FontSize));
                using Graphics Canvas = Graphics.FromImage(Picture);
                Assert.That(Editor.Editor.Font.GetHeight(Canvas), Is.GreaterThan(0));
                Editor.ZoomIn();
                Assert.That(ReportedFontSize, Is.EqualTo(FontSize + 1F));
                Editor.ResetZoom();
                Assert.That(ReportedFontSize, Is.EqualTo(FontSize));
                Assert.That(Editor.Editor.Font.GetHeight(Canvas), Is.GreaterThan(0));
            }
            Assert.That(Failures, Is.Empty, string.Join(Environment.NewLine, Failures));
        }
        finally
        {
            Application.ThreadException -= Handler;
            SetThreadDpiAwarenessContext(PreviousContext);
        }
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    private static partial IntPtr SetThreadDpiAwarenessContext(IntPtr Context);
}
