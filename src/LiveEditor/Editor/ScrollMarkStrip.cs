using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor.Editor;

/// <summary>
/// Narrow track beside the source scrollbar. It marks the caret, the visible page, find matches, and diagnostics.
/// </summary>
internal sealed class ScrollMarkStrip : Control
{
    private int _LineCount = 1;

    private int _CaretLine;

    private int _FirstVisible;

    private int _VisibleCount = 1;

    private readonly List<int> _FindLines = new List<int>();

    private readonly List<int> _DiagnosticLines = new List<int>();

    private bool _Dark;

    /// <summary>
    /// Matches the editor theme.
    /// </summary>
    /// <param name="Dark">When true, the track uses the dark background.</param>
    public void UseDarkChrome(bool Dark)
    {
        _Dark = Dark;
        Invalidate();
    }

    /// <summary>
    /// Creates the mark track.
    /// </summary>
    public ScrollMarkStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Width = 14;
        TabStop = false;
        Cursor = Cursors.Hand;
    }

    /// <summary>
    /// Raised when a mark is clicked. The argument is a zero-based line number.
    /// </summary>
    public event EventHandler<int>? LineChosen;

    /// <summary>
    /// Replaces the marks and redraws the track.
    /// </summary>
    /// <param name="LineCount">Lines in the document. At least one.</param>
    /// <param name="CaretLine">Zero-based line that holds the caret.</param>
    /// <param name="FirstVisible">Zero-based first visible line.</param>
    /// <param name="VisibleCount">How many lines fit in the editor.</param>
    /// <param name="FindLines">Zero-based lines that contain a find match.</param>
    /// <param name="DiagnosticLines">One-based diagnostic lines.</param>
    public void ShowMarks(int LineCount, int CaretLine, int FirstVisible, int VisibleCount, IReadOnlyList<int> FindLines, IReadOnlyList<int> DiagnosticLines)
    {
        _LineCount = Math.Max(1, LineCount);
        _CaretLine = Math.Max(0, CaretLine);
        _FirstVisible = Math.Max(0, FirstVisible);
        _VisibleCount = Math.Max(1, VisibleCount);
        _FindLines.Clear();
        if (FindLines != null)
        {
            _FindLines.AddRange(FindLines);
        }

        _DiagnosticLines.Clear();
        if (DiagnosticLines != null)
        {
            _DiagnosticLines.AddRange(DiagnosticLines);
        }

        Invalidate();
    }

    /// <summary>
    /// Maps a line onto the track. The last line sits on the bottom edge.
    /// </summary>
    /// <param name="Line">Zero-based line.</param>
    /// <param name="LineCount">Lines in the document.</param>
    /// <param name="Height">Track height in pixels.</param>
    /// <returns>Y coordinate of the mark.</returns>
    internal static int MarkTop(int Line, int LineCount, int Height)
    {
        int Span = Math.Max(1, LineCount - 1);
        int Usable = Math.Max(1, Height - 3);
        return (int)Math.Round(Math.Max(0, Line) * (Usable / (double)Span));
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs E)
    {
        base.OnPaint(E);
        Color Paper = Parent == null ? SystemColors.Control : Parent.BackColor;
        bool Dark = _Dark || (Paper.R < 80 && Paper.G < 80 && Paper.B < 80);
        E.Graphics.Clear(Dark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(245, 245, 245));
        int PageTop = MarkTop(_FirstVisible, _LineCount, Height);
        int PageBottom = MarkTop(Math.Min(_LineCount - 1, _FirstVisible + _VisibleCount), _LineCount, Height);
        using SolidBrush Page = new SolidBrush(Dark ? Color.FromArgb(70, 70, 74) : Color.FromArgb(210, 210, 210));
        E.Graphics.FillRectangle(Page, 2, PageTop, Math.Max(1, Width - 4), Math.Max(6, PageBottom - PageTop));
        using SolidBrush Find = new SolidBrush(Color.FromArgb(240, 160, 0));
        foreach (int Line in _FindLines)
        {
            E.Graphics.FillRectangle(Find, 1, MarkTop(Line, _LineCount, Height), Width - 2, 3);
        }

        using SolidBrush Problem = new SolidBrush(Color.FromArgb(229, 20, 0));
        foreach (int LineNumber in _DiagnosticLines)
        {
            E.Graphics.FillRectangle(Problem, 1, MarkTop(LineNumber - 1, _LineCount, Height), Width - 2, 3);
        }

        using SolidBrush Caret = new SolidBrush(Color.FromArgb(0, 122, 204));
        E.Graphics.FillRectangle(Caret, 0, MarkTop(_CaretLine, _LineCount, Height), Width, 3);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs E)
    {
        base.OnMouseDown(E);
        int Span = Math.Max(1, _LineCount - 1);
        int Usable = Math.Max(1, Height - 3);
        int Line = (int)Math.Round(E.Y * (Span / (double)Usable));
        Line = Math.Max(0, Math.Min(Span, Line));
        LineChosen?.Invoke(this, Line);
    }
}
