using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor.Editor;

/// <summary>
/// Source text box that paints a blue bar on the line that holds the caret.
/// </summary>
internal sealed class SourceTextBox : RichTextBox
{
    private const int WM_PAINT = 0x000F;

    /// <summary>
    /// Creates the editor box.
    /// </summary>
    public SourceTextBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    /// <inheritdoc />
    protected override void OnSelectionChanged(EventArgs E)
    {
        base.OnSelectionChanged(E);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnVScroll(EventArgs E)
    {
        base.OnVScroll(E);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message Message)
    {
        base.WndProc(ref Message);
        if (Message.Msg == WM_PAINT && IsHandleCreated && !IsDisposed)
        {
            DrawCurrentLineBar();
        }
    }

    private void DrawCurrentLineBar()
    {
        int Index = GetFirstCharIndexOfCurrentLine();
        if (Index < 0)
        {
            return;
        }

        Point Origin = GetPositionFromCharIndex(Index);
        int LineHeight = Math.Max(Font.Height, 1);
        using Graphics Canvas = Graphics.FromHwnd(Handle);
        using Pen Bar = new Pen(Color.FromArgb(0, 122, 204), 2F);
        Canvas.DrawLine(Bar, 1, Origin.Y, 1, Origin.Y + LineHeight);
    }
}
