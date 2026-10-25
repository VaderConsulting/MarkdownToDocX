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

    private int _LineHeight = 16;

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
        int LineHeight = MeasureLine(Index, Origin.Y);
        using Graphics Canvas = CreateGraphics();
        using Pen Bar = new Pen(Color.FromArgb(0, 122, 204), 2F);
        Canvas.DrawLine(Bar, 1, Origin.Y, 1, Origin.Y + LineHeight);
    }

    /// <summary>
    /// Returns the caret line's height from the following line.
    /// The text box font is not used: while the control is painting, that font can be invalid and its height throws.
    /// </summary>
    private int MeasureLine(int Index, int OriginY)
    {
        int Line = GetLineFromCharIndex(Index);
        int Next = GetFirstCharIndexFromLine(Line + 1);
        if (Next > Index)
        {
            int Height = GetPositionFromCharIndex(Next).Y - OriginY;
            if (Height > 0)
            {
                _LineHeight = Height;
                return Height;
            }
        }

        return _LineHeight;
    }
}
