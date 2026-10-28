using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveEditor.Editor;

/// <summary>
/// Read-only line numbers that stay beside the source editor and move with its vertical scroll.
/// The column is wide enough for four digits.
/// </summary>
internal sealed class LineNumberGutter : Panel
{
    private const int WM_MOUSEWHEEL = 0x020A;

    private const int HORIZONTAL_PADDING = 8;

    private RichTextBox? _Editor;

    private Font? _OwnedFont;

    private bool _MatchingFont;

    /// <summary>
    /// Creates the line-number column.
    /// </summary>
    public LineNumberGutter()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        TabStop = false;
        Cursor = Cursors.Default;
    }

    /// <summary>
    /// Follows the editor's lines, font, colours, and vertical scroll.
    /// </summary>
    /// <param name="Editor">The source editor.</param>
    public void Bind(RichTextBox Editor)
    {
        if (Editor == null)
        {
            throw new ArgumentNullException(nameof(Editor));
        }

        Detach();
        _Editor = Editor;
        _Editor.TextChanged += Editor_Changed;
        _Editor.VScroll += Editor_Changed;
        _Editor.Resize += Editor_Changed;
        _Editor.FontChanged += Editor_FontChanged;
        _Editor.BackColorChanged += Editor_ColourChanged;
        _Editor.ForeColorChanged += Editor_ColourChanged;
        _Editor.MouseWheel += Editor_Changed;
        _Editor.SelectionChanged += Editor_Changed;
        MatchEditorFont();
        MatchEditorColours();
    }

    /// <inheritdoc />
    protected override void Dispose(bool Disposing)
    {
        if (Disposing)
        {
            Detach();
            if (_OwnedFont != null)
            {
                _OwnedFont.Dispose();
                _OwnedFont = null;
            }
        }

        base.Dispose(Disposing);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs E)
    {
        base.OnMouseDown(E);
        if (_Editor == null || _Editor.IsDisposed || !_Editor.IsHandleCreated)
        {
            return;
        }

        int Index = _Editor.GetCharIndexFromPosition(new Point(1, E.Y));
        int Line = _Editor.GetLineFromCharIndex(Index);
        int Start = _Editor.GetFirstCharIndexFromLine(Line);
        if (Start < 0)
        {
            _Editor.Focus();
            return;
        }

        int Next = _Editor.GetFirstCharIndexFromLine(Line + 1);
        int End = Next < 0 ? _Editor.TextLength : Next;
        _Editor.Select(Start, Math.Max(0, End - Start));
        _Editor.Focus();
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs E)
    {
        base.OnMouseWheel(E);
        if (_Editor == null || _Editor.IsDisposed || !_Editor.IsHandleCreated)
        {
            return;
        }

        int Packed = E.Delta << 16;
        SendMessage(_Editor.Handle, WM_MOUSEWHEEL, (IntPtr)Packed, IntPtr.Zero);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs E)
    {
        base.OnPaint(E);
        E.Graphics.Clear(BackColor);
        if (_Editor == null || _Editor.IsDisposed || !_Editor.IsHandleCreated || _Editor.TextLength < 0)
        {
            return;
        }

        int FirstLine = VisibleLine(new Point(1, 1));
        int LastLine = VisibleLine(new Point(1, Math.Max(1, _Editor.ClientSize.Height - 1)));
        if (LastLine < FirstLine)
        {
            int Swap = FirstLine;
            FirstLine = LastLine;
            LastLine = Swap;
        }

        FirstLine = Math.Max(0, FirstLine - 1);
        int LineCount = _Editor.GetLineFromCharIndex(Math.Max(0, _Editor.TextLength)) + 1;
        LastLine = Math.Min(LineCount - 1, LastLine + 1);
        TextFormatFlags Flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        Rectangle Row = new Rectangle(HORIZONTAL_PADDING, 0, Math.Max(1, ClientSize.Width - (HORIZONTAL_PADDING * 2)), Font.Height);
        for (int Line = FirstLine; Line <= LastLine; Line++)
        {
            int Index = _Editor.GetFirstCharIndexFromLine(Line);
            if (Index < 0)
            {
                continue;
            }

            Point Position = _Editor.GetPositionFromCharIndex(Index);
            Row.Y = Position.Y;
            Row.Height = Math.Max(Font.Height, _Editor.Font.Height);
            bool Current = Line == _Editor.GetLineFromCharIndex(_Editor.SelectionStart);
            if (Current)
            {
                bool Dark = BackColor.R < 80 && BackColor.G < 80 && BackColor.B < 80;
                using SolidBrush Band = new SolidBrush(Dark ? Color.FromArgb(7, 48, 77) : Color.FromArgb(228, 242, 252));
                E.Graphics.FillRectangle(Band, 0, Row.Y, ClientSize.Width, Row.Height);
                using SolidBrush Bar = new SolidBrush(Color.FromArgb(0, 122, 204));
                E.Graphics.FillRectangle(Bar, 0, Row.Y, 3, Row.Height);
            }

            string Label = (Line + 1).ToString(CultureInfo.InvariantCulture);
            TextRenderer.DrawText(E.Graphics, Label, Font, Row, Current ? Color.FromArgb(0, 122, 204) : ForeColor, Flags);
        }
    }

    private int VisibleLine(Point Location)
    {
        int Index = _Editor!.GetCharIndexFromPosition(Location);
        return _Editor.GetLineFromCharIndex(Index);
    }

    private void Editor_Changed(object? Sender, EventArgs E)
    {
        Invalidate();
    }

    private void Editor_FontChanged(object? Sender, EventArgs E)
    {
        MatchEditorFont();
        Invalidate();
    }

    private void Editor_ColourChanged(object? Sender, EventArgs E)
    {
        MatchEditorColours();
        Invalidate();
    }

    private void MatchEditorFont()
    {
        if (_Editor == null || _MatchingFont)
        {
            return;
        }

        _MatchingFont = true;
        try
        {
            Font Next = new Font(_Editor.Font.FontFamily, _Editor.Font.Size, _Editor.Font.Style, GraphicsUnit.Point);
            // Control.Font retains its current instance when the new font is equal.
            // Keep that instance alive and discard only the unused replacement.
            if (Font.Equals(Next))
            {
                Next.Dispose();
            }
            else
            {
                Font? Previous = _OwnedFont;
                Font = Next;
                _OwnedFont = Next;
                Previous?.Dispose();
            }

            ResizeToFourDigits();
        }
        finally
        {
            _MatchingFont = false;
        }
    }

    private void MatchEditorColours()
    {
        if (_Editor == null)
        {
            return;
        }

        BackColor = _Editor.BackColor;
        ForeColor = Quiet(_Editor.ForeColor, _Editor.BackColor);
    }

    private void ResizeToFourDigits()
    {
        Size Measured = TextRenderer.MeasureText("9999", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Width = Measured.Width + (HORIZONTAL_PADDING * 2);
    }

    private void Detach()
    {
        if (_Editor == null)
        {
            return;
        }

        _Editor.TextChanged -= Editor_Changed;
        _Editor.VScroll -= Editor_Changed;
        _Editor.Resize -= Editor_Changed;
        _Editor.FontChanged -= Editor_FontChanged;
        _Editor.BackColorChanged -= Editor_ColourChanged;
        _Editor.ForeColorChanged -= Editor_ColourChanged;
        _Editor.MouseWheel -= Editor_Changed;
        _Editor.SelectionChanged -= Editor_Changed;
        _Editor = null;
    }

    private static Color Quiet(Color Ink, Color Paper)
    {
        return Color.FromArgb((Ink.R + Paper.R) / 2, (Ink.G + Paper.G) / 2, (Ink.B + Paper.B) / 2);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr Handle, int Message, IntPtr WParam, IntPtr LParam);
}
