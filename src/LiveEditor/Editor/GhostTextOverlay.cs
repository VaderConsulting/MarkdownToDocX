using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor.Editor;

/// <summary>
/// Draws ghost-text completion beside the caret when exactly one suggestion remains.
/// </summary>
internal sealed class GhostTextOverlay : IDisposable
{
    private readonly Control _Host;

    private string _Text = string.Empty;

    private Point _Location;

    private bool _Visible;

    public GhostTextOverlay(Control Host)
    {
        _Host = Host ?? throw new ArgumentNullException(nameof(Host));
        _Host.Paint += Host_Paint;
    }

    public void Show(Point ClientLocation, string Text)
    {
        _Location = ClientLocation;
        _Text = Text ?? string.Empty;
        _Visible = !string.IsNullOrEmpty(_Text);
        _Host.Invalidate();
    }

    public void Hide()
    {
        if (!_Visible)
        {
            return;
        }

        _Visible = false;
        _Text = string.Empty;
        _Host.Invalidate();
    }

    public void Dispose()
    {
        _Host.Paint -= Host_Paint;
    }

    private void Host_Paint(object? Sender, PaintEventArgs E)
    {
        if (!_Visible || string.IsNullOrEmpty(_Text))
        {
            return;
        }

        using Font Font = EditorFonts.Monospace(11F, FontStyle.Italic);
        using SolidBrush Brush = new SolidBrush(Color.FromArgb(140, 140, 140));
        E.Graphics.DrawString(_Text, Font, Brush, _Location);
    }
}
