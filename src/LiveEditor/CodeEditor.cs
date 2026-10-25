using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using LiveEditor.Completion;
using LiveEditor.Editor;

namespace LiveEditor;

/// <summary>
/// Source editor with line numbers, syntax colour, identifier marks, and completion.
/// </summary>
internal sealed partial class CodeEditor : UserControl
{
    private readonly EditorIntelliSenseController _IntelliSense;

    private readonly GhostTextOverlay? _GhostText;

    private CompletionContext? _GhostContext;

    private bool _SuppressCaretUi;

    private bool _IsApplyingEditorFormatting;

    private bool _DarkTheme;

    private string? _MarkedId;

    private int _MarkedStart = -1;

    private int _MarkedLength;

    private readonly List<int> _MarkedStarts = new List<int>();

    /// <summary>
    /// Creates the editor and the line-number column beside it.
    /// </summary>
    public CodeEditor()
    {
        InitializeComponent();
        _SessionFontSize = SourceEditor.Font.Size;
        PlaceBesideLineNumbers();
        LineNumberColumn.Bind(SourceEditor);
        _IntelliSense = new EditorIntelliSenseController(SourceEditor);
        _IntelliSense.Attach();
        _IntelliSense.ListOpened += IntelliSense_ListOpened;
        _IntelliSense.Accepted += IntelliSense_Accepted;
        _GhostText = LiveEditorFeatureGate.F_GhostText ? new GhostTextOverlay(SourceEditor) : null;
        SourceEditor.TextChanged += SourceEditor_TextChanged;
        SourceEditor.SelectionChanged += SourceEditor_SelectionChanged;
        SourceEditor.MouseDoubleClick += SourceEditor_MouseDoubleClick;
        SourceEditor.KeyDown += SourceEditor_KeyDown;
        SourceEditor.PreviewKeyDown += SourceEditor_PreviewKeyDown;
        SourceEditor.LostFocus += SourceEditor_LostFocus;
        HighlightTimer.Tick += HighlightTimer_Tick;
        Disposed += CodeEditor_Disposed;
        FindNextButton.Click += FindNextButton_Click;
        FindPreviousButton.Click += FindPreviousButton_Click;
        ReplaceButton.Click += ReplaceButton_Click;
        ReplaceAllButton.Click += ReplaceAllButton_Click;
        CloseFindButton.Click += CloseFindButton_Click;
        QueryBox.KeyDown += QueryBox_KeyDown;
        QueryBox.TextChanged += QueryBox_TextChanged;
        MatchCaseCheckBox.CheckedChanged += QueryBox_TextChanged;
        ReplaceBox.KeyDown += QueryBox_KeyDown;
        ScrollMarks.LineChosen += ScrollMarks_LineChosen;
        SourceEditor.VScroll += SourceEditor_ScrollChanged;
        _EditorWheel = new EditorMouseWheel(this);
        SourceEditor.HandleCreated += SourceEditor_HandleCreated;
        SourceEditor.HandleDestroyed += SourceEditor_HandleDestroyed;
        if (SourceEditor.IsHandleCreated)
        {
            _EditorWheel.AssignHandle(SourceEditor.Handle);
        }
    }

    private float _SessionFontSize = 11F;

    private int _FindAnchor = -1;

    private int _FindLength;

    private bool _FindReplaces;

    private bool _GoToLineMode;

    private bool _CommentChord;

    private bool _DuplicateChord;

    private readonly List<int> _DiagnosticLines = new List<int>();

    private readonly List<int> _FindLines = new List<int>();

    private readonly EditorMouseWheel _EditorWheel;

    /// <summary>
    /// Raised while colours are applied so the host can refresh diagnostics and supply error lines.
    /// </summary>
    internal event EventHandler? ColoursRefreshing;

    /// <summary>
    /// Raised when the editor wants a short status message, such as after accepting ghost text.
    /// </summary>
    internal event EventHandler<string>? StatusRequested;

    /// <summary>
    /// Raised when the editor font size changes so the host can keep the session.
    /// </summary>
    internal event EventHandler<float>? FontSizeChanged;

    /// <summary>
    /// The text box that holds the diagram source.
    /// </summary>
    internal RichTextBox Editor
    {
        get
        {
            return SourceEditor;
        }
    }

    /// <summary>
    /// The small copy of the source shown at the side.
    /// </summary>
    internal RichTextBox Minimap
    {
        get
        {
            return MinimapBox;
        }
    }

    /// <summary>
    /// Completion for this editor.
    /// </summary>
    internal EditorIntelliSenseController IntelliSense
    {
        get
        {
            return _IntelliSense;
        }
    }

    /// <summary>
    /// True while syntax colours or identifier marks are being painted.
    /// </summary>
    internal bool IsApplyingFormatting
    {
        get
        {
            return _IsApplyingEditorFormatting;
        }
    }

    /// <summary>
    /// True while a caret move is caused by painting rather than the user.
    /// </summary>
    internal bool SuppressCaretNotifications
    {
        get
        {
            return _SuppressCaretUi;
        }
    }

    private bool _AutoIndent = true;

    private bool _InsertSpaces = true;

    private int _IndentSize = 2;

    /// <summary>
    /// When true, Enter repeats the indent of the current line.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool AutoIndent
    {
        get
        {
            return _AutoIndent;
        }
        set
        {
            _AutoIndent = value;
        }
    }

    /// <summary>
    /// When true, Tab inserts spaces.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool InsertSpaces
    {
        get
        {
            return _InsertSpaces;
        }
        set
        {
            _InsertSpaces = value;
        }
    }

    /// <summary>
    /// How many spaces Tab inserts.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int IndentSize
    {
        get
        {
            return _IndentSize;
        }
        set
        {
            _IndentSize = value;
        }
    }

    /// <summary>
    /// How long to wait after a change before colouring the text.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int HighlightDelayMilliseconds
    {
        get
        {
            return HighlightTimer.Interval;
        }
        set
        {
            HighlightTimer.Interval = Math.Max(50, Math.Min(5000, value));
        }
    }

    /// <summary>
    /// Shows or hides the minimap.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool MinimapVisible
    {
        get
        {
            return MinimapBox.Visible;
        }
        set
        {
            MinimapBox.Visible = value && LiveEditorFeatureGate.Y_Minimap;
            if (MinimapBox.Visible)
            {
                MinimapBox.Text = SourceEditor.Text;
                MinimapBox.BackColor = SourceEditor.BackColor;
                MinimapBox.ForeColor = SourceEditor.ForeColor;
            }
        }
    }

    /// <summary>
    /// True while a double-clicked identifier is still the selection.
    /// </summary>
    internal bool HasMarkedIdentifier
    {
        get
        {
            return _MarkedId != null;
        }
    }

    /// <summary>
    /// Screen position of the completion popup.
    /// </summary>
    internal Point AssistPopupLocation
    {
        get
        {
            return _IntelliSense.Popup.WindowLocation;
        }
    }

    /// <summary>
    /// True when the completion popup is open.
    /// </summary>
    internal bool AssistPopupVisible
    {
        get
        {
            return _IntelliSense.Popup.IsVisible;
        }
    }

    /// <summary>
    /// Applies the editor font, wrapping, and scroll bars.
    /// </summary>
    /// <param name="FontSize">Point size, limited to 8-32.</param>
    /// <param name="WordWrap">When true, long lines wrap and the horizontal scrollbar is hidden.</param>
    internal void ApplyFont(float FontSize, bool WordWrap)
    {
        float Size = Math.Max(8F, Math.Min(32F, FontSize));
        _SessionFontSize = Size;
        bool OtherFamily = !string.Equals(SourceEditor.Font.Name, EditorFonts.MonospaceFamily, StringComparison.OrdinalIgnoreCase);
        if (OtherFamily || Math.Abs(SourceEditor.Font.Size - Size) > 0.01F)
        {
            Font PreviousFont = SourceEditor.Font;
            SourceEditor.Font = EditorFonts.Monospace(Size);
            PreviousFont.Dispose();
        }

        SourceEditor.WordWrap = WordWrap;
        SourceEditor.ScrollBars = WordWrap ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
    }

    /// <summary>
    /// Sets the editor and minimap colours for the light or dark theme.
    /// </summary>
    /// <param name="DarkTheme">When true, uses the dark background.</param>
    internal void ApplyChrome(bool DarkTheme)
    {
        _DarkTheme = DarkTheme;
        Color Background = DarkTheme ? Color.FromArgb(30, 30, 30) : Color.White;
        Color Foreground = DarkTheme ? Color.Gainsboro : Color.Black;
        SourceEditor.BackColor = Background;
        SourceEditor.ForeColor = Foreground;
        MinimapBox.BackColor = Background;
        MinimapBox.ForeColor = Foreground;
        ScrollMarks.UseDarkChrome(DarkTheme);
    }

    /// <summary>
    /// Puts keyboard focus in the text box.
    /// </summary>
    internal void FocusEditor()
    {
        if (!IsDisposed && !SourceEditor.IsDisposed)
        {
            SourceEditor.Focus();
        }
    }

    /// <summary>
    /// Reads the background of one character without moving the visible selection.
    /// </summary>
    /// <param name="Index">Character index.</param>
    /// <returns>Background colour at that character.</returns>
    internal Color TextBackColorAt(int Index)
    {
        _SuppressCaretUi = true;
        try
        {
            int Start = SourceEditor.SelectionStart;
            int Length = SourceEditor.SelectionLength;
            SourceEditor.Select(Math.Max(0, Index), 1);
            Color Colour = SourceEditor.SelectionBackColor;
            SourceEditor.Select(Start, Length);
            return Colour;
        }
        finally
        {
            _SuppressCaretUi = false;
        }
    }

    /// <summary>
    /// Colours the source after the highlight delay.
    /// </summary>
    internal void QueueHighlight()
    {
        HighlightTimer.Stop();
        HighlightTimer.Start();
    }

    /// <summary>
    /// Stops a pending colour update. Used while the window is closing.
    /// </summary>
    internal void StopHighlight()
    {
        HighlightTimer.Stop();
    }

    /// <summary>
    /// Tints diagnostic lines after the host has parsed the source.
    /// </summary>
    /// <param name="LineNumbers">1-based line numbers.</param>
    internal void TintErrorLines(IReadOnlyList<int> LineNumbers)
    {
        _DiagnosticLines.Clear();
        if (LineNumbers != null)
        {
            _DiagnosticLines.AddRange(LineNumbers);
        }

        MermaidSyntaxHighlighter.TintLines(SourceEditor, LineNumbers, _DarkTheme);
        RefreshScrollMarks();
    }

    /// <summary>
    /// Loads source, indents a trailing blank line, and leaves the caret on the first line.
    /// </summary>
    /// <param name="Source">File or sample text.</param>
    internal void SetSource(string Source)
    {
        string Text = EditorEditing.IndentTrailingBlankLine(Source ?? string.Empty);
        _IntelliSense.Suppress = true;
        try
        {
            SourceEditor.Text = Text;
            SourceEditor.Select(0, 0);
            SourceEditor.ScrollToCaret();
        }
        finally
        {
            _IntelliSense.Suppress = false;
        }

        RefreshFindLines();
        RefreshScrollMarks();
    }

    private void SourceEditor_ScrollChanged(object? Sender, EventArgs E)
    {
        RefreshScrollMarks();
    }

    private void QueryBox_TextChanged(object? Sender, EventArgs E)
    {
        RefreshFindLines();
        RefreshScrollMarks();
    }

    private void ScrollMarks_LineChosen(object? Sender, int Line)
    {
        int Start = SourceEditor.GetFirstCharIndexFromLine(Line);
        if (Start < 0)
        {
            return;
        }

        SourceEditor.Select(Start, 0);
        SourceEditor.ScrollToCaret();
        SourceEditor.Focus();
    }

    private void RefreshFindLines()
    {
        _FindLines.Clear();
        if (!FindBar.Visible || _GoToLineMode || string.IsNullOrEmpty(QueryBox.Text))
        {
            return;
        }

        string Text = SourceText();
        string Query = QueryBox.Text;
        StringComparison Comparison = MatchCaseCheckBox.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int From = 0;
        while (From < Text.Length)
        {
            int Found = Text.IndexOf(Query, From, Comparison);
            if (Found < 0)
            {
                break;
            }

            _FindLines.Add(LineIndexAt(LineStartsOf(Text), Found));
            From = Found + Math.Max(1, Query.Length);
        }
    }

    private void RefreshScrollMarks()
    {
        if (SourceEditor.IsDisposed || !SourceEditor.IsHandleCreated)
        {
            return;
        }

        string Text = SourceText();
        List<int> Starts = LineStartsOf(Text);
        int CaretLine = LineIndexAt(Starts, Math.Max(0, Math.Min(SourceEditor.SelectionStart, Text.Length)));
        int First = SourceEditor.GetLineFromCharIndex(SourceEditor.GetCharIndexFromPosition(new Point(1, 1)));
        int Last = SourceEditor.GetLineFromCharIndex(SourceEditor.GetCharIndexFromPosition(new Point(1, Math.Max(1, SourceEditor.ClientSize.Height - 1))));
        if (Last < First)
        {
            int Swap = First;
            First = Last;
            Last = Swap;
        }

        ScrollMarks.ShowMarks(Starts.Count, CaretLine, First, Math.Max(1, Last - First + 1), _FindLines, _DiagnosticLines);
    }

    /// <summary>
    /// Keeps the line-number column and minimap in front of the fill order so the text starts at their inside edge.
    /// </summary>
    private void PlaceBesideLineNumbers()
    {
        Controls.SetChildIndex(FindBar, Controls.Count - 1);
        Controls.SetChildIndex(LineNumberColumn, Controls.Count - 1);
        Controls.SetChildIndex(ScrollMarks, Controls.Count - 1);
        Controls.SetChildIndex(MinimapBox, Controls.Count - 1);
        Controls.SetChildIndex(SourceEditor, 0);
    }

    private void SourceEditor_TextChanged(object? Sender, EventArgs E)
    {
        if (_IsApplyingEditorFormatting)
        {
            return;
        }

        ClearMarkedId();
        QueueHighlight();
        RefreshFindLines();
        RefreshScrollMarks();
    }

    private void SourceEditor_SelectionChanged(object? Sender, EventArgs E)
    {
        if (_SuppressCaretUi)
        {
            return;
        }

        if (_MarkedId != null && !MarkedSelectionHeld())
        {
            List<int> Starts = new List<int>(_MarkedStarts);
            int Length = _MarkedLength;
            ClearMarkedId();
            bool PreviousPause = _IntelliSense.Pause;
            _IsApplyingEditorFormatting = true;
            _IntelliSense.Pause = true;
            try
            {
                MermaidSyntaxHighlighter.ClearOccurrenceMarks(SourceEditor, Starts, Length);
            }
            finally
            {
                _IsApplyingEditorFormatting = false;
                _IntelliSense.Pause = PreviousPause;
            }

            ApplyEditorColours();
        }

        RefreshScrollMarks();
    }

    private void SourceEditor_MouseDoubleClick(object? Sender, MouseEventArgs E)
    {
        string Selected = SourceEditor.SelectedText;
        if (Selected.Length == 0 || Selected.IndexOfAny(new char[] { '\r', '\n', ' ', '\t' }) >= 0)
        {
            return;
        }

        string? Id = SymbolNavigator.IdAt(SourceEditor.Text, SourceEditor.SelectionStart);
        if (!string.Equals(Selected, Id, StringComparison.Ordinal) || !SymbolNavigator.IsReferenceIdentifier(Selected))
        {
            return;
        }

        _MarkedId = Selected;
        _MarkedStart = SourceEditor.SelectionStart;
        _MarkedLength = SourceEditor.SelectionLength;
        ApplyIdMarks();
    }

    private bool MarkedSelectionHeld()
    {
        return _MarkedId != null
            && SourceEditor.SelectionStart == _MarkedStart
            && SourceEditor.SelectionLength == _MarkedLength
            && string.Equals(SourceEditor.SelectedText, _MarkedId, StringComparison.Ordinal);
    }

    private void ClearMarkedId()
    {
        _MarkedId = null;
        _MarkedStart = -1;
        _MarkedLength = 0;
        _MarkedStarts.Clear();
    }

    private void SourceEditor_PreviewKeyDown(object? Sender, PreviewKeyDownEventArgs E)
    {
        if (E.KeyCode == Keys.Tab || E.KeyCode == Keys.F3 || E.KeyCode == Keys.F8)
        {
            E.IsInputKey = true;
        }

        if ((_IntelliSense.Popup.IsVisible || _GhostContext != null) && (E.KeyCode == Keys.Tab || E.KeyCode == Keys.Enter || E.KeyCode == Keys.Up || E.KeyCode == Keys.Down || E.KeyCode == Keys.Escape))
        {
            E.IsInputKey = true;
        }
    }

    private void SourceEditor_KeyDown(object? Sender, KeyEventArgs E)
    {
        if (_CommentChord)
        {
            _CommentChord = false;
            if (!E.Handled && E.Control && E.KeyCode == Keys.C)
            {
                CommentLines();
                E.SuppressKeyPress = true;
                E.Handled = true;
                return;
            }

            if (!E.Handled && E.Control && E.KeyCode == Keys.U)
            {
                UncommentLines();
                E.SuppressKeyPress = true;
                E.Handled = true;
                return;
            }
        }

        if (_DuplicateChord)
        {
            _DuplicateChord = false;
            if (!E.Handled && E.KeyCode == Keys.V)
            {
                DuplicateLines();
                E.SuppressKeyPress = true;
                E.Handled = true;
                return;
            }
        }

        if (E.Control && !E.Alt && !E.Shift && E.KeyCode == Keys.K)
        {
            _CommentChord = true;
            _DuplicateChord = false;
            if (!E.Handled)
            {
                E.SuppressKeyPress = true;
                E.Handled = true;
            }

            return;
        }

        if (E.Control && !E.Alt && !E.Shift && E.KeyCode == Keys.E)
        {
            _DuplicateChord = true;
            _CommentChord = false;
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.Handled)
        {
            if (E.KeyCode == Keys.Escape)
            {
                _GhostContext = null;
                _GhostText?.Hide();
            }

            return;
        }

        if (TryMoveSelectedLines(E))
        {
            _GhostContext = null;
            _GhostText?.Hide();
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (TryHandleEditingKey(E))
        {
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.Tab && E.Shift)
        {
            OutdentLines();
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.Tab && SourceEditor.SelectionLength > 0)
        {
            IndentLines();
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.Tab && TryAcceptGhostText())
        {
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (TryInsertNewLineIndent(E) || TryInsertIndentUnit(E))
        {
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.Escape)
        {
            _IntelliSense.Popup.Hide();
            _GhostContext = null;
            _GhostText?.Hide();
        }
    }

    /// <summary>
    /// Moves the lines touched by the selection up or down by one line. Alt+Up and Alt+Down.
    /// </summary>
    /// <param name="E">Key that may request the move.</param>
    /// <returns>True when the shortcut was recognised, including when the block is already at the edge.</returns>
    private bool TryMoveSelectedLines(KeyEventArgs E)
    {
        if (!E.Alt || E.Control || (E.KeyCode != Keys.Up && E.KeyCode != Keys.Down))
        {
            return false;
        }

        string Text = SourceEditor.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        List<int> LineStarts = LineStartsOf(Text);
        int SelectionStart = Math.Max(0, Math.Min(SourceEditor.SelectionStart, Text.Length));
        int SelectionEnd = Math.Max(SelectionStart, Math.Min(SelectionStart + SourceEditor.SelectionLength, Text.Length));
        int StartLine = LineIndexAt(LineStarts, SelectionStart);
        int EndLine = LineIndexAt(LineStarts, SelectionEnd);
        bool EndAtFollowingLine = SourceEditor.SelectionLength > 0 && EndLine > StartLine && SelectionEnd == LineStarts[EndLine];
        if (EndAtFollowingLine)
        {
            EndLine--;
        }

        int Direction = E.KeyCode == Keys.Up ? -1 : 1;
        if (StartLine + Direction < 0 || EndLine + Direction >= LineStarts.Count)
        {
            return true;
        }

        int First = Direction < 0 ? StartLine - 1 : StartLine;
        int Last = Direction < 0 ? EndLine : EndLine + 1;
        List<string> Contents = new List<string>();
        for (int Line = First; Line <= Last; Line++)
        {
            Contents.Add(LineContent(Text, LineStarts, Line));
        }

        string Neighbour = Direction < 0 ? Contents[0] : Contents[Contents.Count - 1];
        List<string> Block = new List<string>();
        if (Direction < 0)
        {
            for (int Index = 1; Index < Contents.Count; Index++)
            {
                Block.Add(Contents[Index]);
            }
        }
        else
        {
            for (int Index = 0; Index < Contents.Count - 1; Index++)
            {
                Block.Add(Contents[Index]);
            }
        }

        List<string> Reordered = new List<string>();
        if (Direction < 0)
        {
            Reordered.AddRange(Block);
            Reordered.Add(Neighbour);
        }
        else
        {
            Reordered.Add(Neighbour);
            Reordered.AddRange(Block);
        }

        int SpanStart = LineStarts[First];
        int SpanEnd = Last + 1 < LineStarts.Count ? LineStarts[Last + 1] : Text.Length;
        bool SpanEndsWithNewline = Last < LineStarts.Count - 1;
        string NewSpan = string.Join("\n", Reordered);
        if (SpanEndsWithNewline)
        {
            NewSpan += "\n";
        }

        string NewText = Text.Substring(0, SpanStart) + NewSpan + Text.Substring(SpanEnd);
        int StartColumn = SelectionStart - LineStarts[StartLine];
        int EndColumn = EndAtFollowingLine ? 0 : SelectionEnd - LineStarts[EndLine];
        List<int> NewStarts = LineStartsOf(NewText);
        int NewStartLine = StartLine + Direction;
        int NewEndLine = EndLine + Direction;
        int NewSelectionStart = NewStarts[NewStartLine] + StartColumn;
        int NewSelectionEnd = EndAtFollowingLine
            ? (NewEndLine + 1 < NewStarts.Count ? NewStarts[NewEndLine + 1] : NewText.Length)
            : NewStarts[NewEndLine] + EndColumn;

        bool PopupWasOpen = _IntelliSense.Popup.IsVisible;
        _SuppressCaretUi = true;
        _IntelliSense.Suppress = true;
        try
        {
            SourceEditor.Select(SpanStart, SpanEnd - SpanStart);
            SourceEditor.SelectedText = NewSpan;
            SourceEditor.Select(NewSelectionStart, Math.Max(0, NewSelectionEnd - NewSelectionStart));
        }
        finally
        {
            _IntelliSense.Suppress = false;
            _SuppressCaretUi = false;
        }

        if (PopupWasOpen)
        {
            _IntelliSense.Refresh(Force: false);
        }

        return true;
    }

    private static List<int> LineStartsOf(string Text)
    {
        List<int> LineStarts = new List<int>();
        LineStarts.Add(0);
        for (int Index = 0; Index < Text.Length; Index++)
        {
            if (Text[Index] == '\n')
            {
                LineStarts.Add(Index + 1);
            }
        }

        return LineStarts;
    }

    private static int LineIndexAt(List<int> LineStarts, int Position)
    {
        int Line = 0;
        for (int Index = 1; Index < LineStarts.Count; Index++)
        {
            if (LineStarts[Index] > Position)
            {
                break;
            }

            Line = Index;
        }

        return Line;
    }

    private static string LineContent(string Text, List<int> LineStarts, int Line)
    {
        int From = LineStarts[Line];
        int To = Line + 1 < LineStarts.Count ? LineStarts[Line + 1] : Text.Length;
        if (To > From && Text[To - 1] == '\n')
        {
            To--;
        }

        return Text.Substring(From, To - From);
    }

    private bool TryInsertNewLineIndent(KeyEventArgs E)
    {
        if (!AutoIndent || E.KeyCode != Keys.Enter || E.Control || E.Alt)
        {
            return false;
        }

        int CaretIndex = SourceEditor.SelectionStart;
        int LineStart = CaretIndex;
        while (LineStart > 0 && SourceEditor.Text[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        int IndentEnd = LineStart;
        while (IndentEnd < CaretIndex && (SourceEditor.Text[IndentEnd] == ' ' || SourceEditor.Text[IndentEnd] == '\t'))
        {
            IndentEnd++;
        }

        string ExistingIndent = SourceEditor.Text.Substring(LineStart, IndentEnd - LineStart);
        SourceEditor.SelectedText = Environment.NewLine + ExistingIndent;
        return true;
    }

    private bool TryInsertIndentUnit(KeyEventArgs E)
    {
        if (E.KeyCode != Keys.Tab || E.Control || E.Alt || E.Shift || SourceEditor.SelectionLength != 0)
        {
            return false;
        }

        int Size = Math.Max(1, Math.Min(16, IndentSize));
        SourceEditor.SelectedText = InsertSpaces ? new string(' ', Size) : "\t";
        return true;
    }

    private void SourceEditor_LostFocus(object? Sender, EventArgs E)
    {
        _GhostText?.Hide();
    }

    private bool TryAcceptGhostText()
    {
        if (_GhostContext == null || _GhostContext.Items.Count != 1)
        {
            return false;
        }

        CompletionContext Context = _GhostContext;
        CompletionItem Item = Context.Items[0];
        int Start = Math.Max(0, Math.Min(Context.ReplaceStart, SourceEditor.TextLength));
        int End = Math.Max(Start, Math.Min(Context.ReplaceEnd, SourceEditor.TextLength));
        _IntelliSense.Suppress = true;
        try
        {
            SourceEditor.Select(Start, End - Start);
            SourceEditor.SelectedText = Item.InsertText;
        }
        finally
        {
            _IntelliSense.Suppress = false;
        }

        _GhostContext = null;
        _GhostText?.Hide();
        StatusRequested?.Invoke(this, "Inserted " + Item.DisplayText);
        if (IsHandleCreated)
        {
            BeginInvoke(new Action(RefreshMembers));
        }

        return true;
    }

    private void RefreshMembers()
    {
        _IntelliSense.Refresh(Force: false);
    }

    private void IntelliSense_ListOpened(object? Sender, EventArgs E)
    {
        _GhostContext = null;
        _GhostText?.Hide();
    }

    private void IntelliSense_Accepted(object? Sender, CompletionItem Item)
    {
        _GhostContext = null;
        _GhostText?.Hide();
    }

    private void HighlightTimer_Tick(object? Sender, EventArgs E)
    {
        HighlightTimer.Stop();
        ApplyEditorColours();
        if (_IntelliSense.Popup.IsVisible)
        {
            _IntelliSense.Popup.Reposition();
        }
    }

    internal void ApplyEditorColours()
    {
        if (SourceEditor.IsDisposed)
        {
            return;
        }

        _SuppressCaretUi = true;
        _IsApplyingEditorFormatting = true;
        _IntelliSense.Pause = true;
        using (RichTextBoxUndoSuspension.Suspend(SourceEditor))
        {
        try
        {
            if (LiveEditorFeatureGate.A_SyntaxHighlighting)
            {
                MermaidSyntaxHighlighter.Apply(SourceEditor, _DarkTheme);
            }

            ColoursRefreshing?.Invoke(this, EventArgs.Empty);
            ApplyIdMarks();
            if (LiveEditorFeatureGate.Y_Minimap && MinimapBox.Visible)
            {
                MinimapBox.Text = SourceEditor.Text;
                MinimapBox.BackColor = SourceEditor.BackColor;
                MinimapBox.ForeColor = SourceEditor.ForeColor;
            }
        }
        finally
        {
            _IsApplyingEditorFormatting = false;
            _SuppressCaretUi = false;
            _IntelliSense.Pause = false;
        }
        }
    }

    private void ApplyIdMarks()
    {
        if (!MarkedSelectionHeld())
        {
            return;
        }

        bool PreviousSuppress = _SuppressCaretUi;
        bool PreviousFormatting = _IsApplyingEditorFormatting;
        bool PreviousPause = _IntelliSense.Pause;
        _SuppressCaretUi = true;
        _IsApplyingEditorFormatting = true;
        _IntelliSense.Pause = true;
        try
        {
            List<int> Starts = SymbolNavigator.FindExactIds(SourceEditor.Text, _MarkedId ?? string.Empty);
            _MarkedStarts.Clear();
            _MarkedStarts.AddRange(Starts);
            MermaidSyntaxHighlighter.MarkOtherOccurrences(SourceEditor, Starts, _MarkedLength, _MarkedStart, _DarkTheme);
        }
        finally
        {
            _SuppressCaretUi = PreviousSuppress;
            _IsApplyingEditorFormatting = PreviousFormatting;
            _IntelliSense.Pause = PreviousPause;
        }

        if (!PreviousPause && _IntelliSense.Popup.IsVisible)
        {
            _IntelliSense.Popup.Reposition();
        }
    }

    /// <summary>Shows the find bar.</summary>
    internal void ShowFind()
    {
        OpenFindBar(false, false);
    }

    /// <summary>Shows the replace bar.</summary>
    internal void ShowReplace()
    {
        OpenFindBar(true, false);
    }

    /// <summary>Shows the go-to-line bar.</summary>
    internal void ShowGoToLine()
    {
        OpenFindBar(false, true);
    }

    /// <summary>Finds the next match.</summary>
    internal void FindNextMatch()
    {
        FindInDirection(true);
    }

    /// <summary>Finds the previous match.</summary>
    internal void FindPreviousMatch()
    {
        FindInDirection(false);
    }

    /// <summary>Replaces the selected match, then finds the next one.</summary>
    internal void ReplaceMatch()
    {
        ApplyTextChange(EditorEditing.Replace(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength, QueryBox.Text, ReplaceBox.Text, MatchCaseCheckBox.Checked));
    }

    /// <summary>Replaces every match.</summary>
    internal void ReplaceAllMatches()
    {
        ApplyTextChange(EditorEditing.ReplaceAll(SourceText(), QueryBox.Text, ReplaceBox.Text, MatchCaseCheckBox.Checked));
    }

    /// <summary>Moves the caret to a 1-based line.</summary>
    internal void GoToLineNumber(int LineNumber)
    {
        ApplyTextChange(EditorEditing.GoToLine(SourceText(), LineNumber));
    }

    /// <summary>Indents the selected lines.</summary>
    internal void IndentLines()
    {
        ApplyTextChange(EditorEditing.Indent(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength, IndentUnitText()));
    }

    /// <summary>Removes one indent from the selected lines.</summary>
    internal void OutdentLines()
    {
        ApplyTextChange(EditorEditing.Outdent(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength, IndentUnitText()));
    }

    /// <summary>Comments the selected lines.</summary>
    internal void CommentLines()
    {
        ApplyTextChange(EditorEditing.Comment(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    /// <summary>Removes a comment marker from the selected lines.</summary>
    internal void UncommentLines()
    {
        ApplyTextChange(EditorEditing.Uncomment(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    /// <summary>Duplicates the selected lines.</summary>
    internal void DuplicateLines()
    {
        ApplyTextChange(EditorEditing.Duplicate(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    /// <summary>Deletes the selected lines.</summary>
    internal void DeleteLines()
    {
        ApplyTextChange(EditorEditing.DeleteLines(SourceText(), SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    /// <summary>Moves to the next diagnostic line.</summary>
    internal void NextDiagnostic()
    {
        ApplyTextChange(EditorEditing.NextDiagnostic(SourceText(), SourceEditor.SelectionStart, _DiagnosticLines, true));
    }

    /// <summary>Moves to the previous diagnostic line.</summary>
    internal void PreviousDiagnostic()
    {
        ApplyTextChange(EditorEditing.NextDiagnostic(SourceText(), SourceEditor.SelectionStart, _DiagnosticLines, false));
    }

    /// <summary>Undoes the last text edit.</summary>
    internal void UndoEdit()
    {
        if (SourceEditor.CanUndo)
        {
            SourceEditor.Undo();
        }
    }

    /// <summary>Redoes the last undone edit.</summary>
    internal void RedoEdit()
    {
        if (SourceEditor.CanRedo)
        {
            SourceEditor.Redo();
        }
    }

    /// <summary>Cuts the selection.</summary>
    internal void CutSelection()
    {
        SourceEditor.Cut();
    }

    /// <summary>Copies the selection.</summary>
    internal void CopySelection()
    {
        SourceEditor.Copy();
    }

    /// <summary>Pastes the clipboard.</summary>
    internal void PasteClipboard()
    {
        SourceEditor.Paste();
    }

    /// <summary>Increases the editor font size by one point.</summary>
    internal void ZoomIn()
    {
        SetEditorFont(SourceEditor.Font.Size + 1F);
    }

    /// <summary>Decreases the editor font size by one point.</summary>
    internal void ZoomOut()
    {
        SetEditorFont(SourceEditor.Font.Size - 1F);
    }

    /// <summary>Restores the editor font size from the start of the session.</summary>
    internal void ResetZoom()
    {
        SetEditorFont(_SessionFontSize);
    }

    private void OpenFindBar(bool Replace, bool GoToLine)
    {
        _IntelliSense.Popup.Hide();
        _FindReplaces = Replace;
        _GoToLineMode = GoToLine;
        FindPromptLabel.Text = GoToLine ? "Line" : "Find";
        FindNextButton.Text = GoToLine ? "Go" : "Next";
        FindPreviousButton.Visible = !GoToLine;
        MatchCaseCheckBox.Visible = !GoToLine;
        ReplaceRow.Visible = Replace;
        FindBar.Height = Replace ? 60 : 32;
        FindBar.Visible = true;
        if (!GoToLine && SourceEditor.SelectionLength > 0 && SourceEditor.SelectedText.IndexOf('\n') < 0 && SourceEditor.SelectedText.IndexOf('\r') < 0)
        {
            QueryBox.Text = SourceEditor.SelectedText;
        }

        QueryBox.Focus();
        QueryBox.SelectAll();
        RefreshFindLines();
        RefreshScrollMarks();
    }

    private void FindInDirection(bool Forward)
    {
        if (_GoToLineMode)
        {
            int LineNumber;
            if (!int.TryParse(QueryBox.Text.Trim(), out LineNumber))
            {
                StatusRequested?.Invoke(this, "Enter a line number.");
                return;
            }

            GoToLineNumber(LineNumber);
            return;
        }

        int Start = SourceEditor.SelectionStart;
        int Length = SourceEditor.SelectionLength;
        bool CollapsedOnLastMatch = _FindAnchor >= 0 && Length == 0 && (Start == _FindAnchor || Start == _FindAnchor + _FindLength);
        if (_FindAnchor >= 0 && (!SourceEditor.Focused || CollapsedOnLastMatch))
        {
            Start = _FindAnchor;
            Length = _FindLength;
        }

        TextChange Found = EditorEditing.Find(SourceText(), Start, Length, QueryBox.Text, Forward, MatchCaseCheckBox.Checked);
        if (Found.Succeeded)
        {
            _FindAnchor = Found.SelectionStart;
            _FindLength = Found.SelectionLength;
        }

        ApplyTextChange(Found);
    }

    private void ApplyTextChange(TextChange Change)
    {
        if (Change == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(Change.Status))
        {
            StatusRequested?.Invoke(this, Change.Status);
        }

        if (Change.Text != null)
        {
            _IntelliSense.Suppress = true;
            _SuppressCaretUi = true;
            try
            {
                SourceEditor.Select(0, SourceEditor.TextLength);
                SourceEditor.SelectedText = Change.Text;
            }
            finally
            {
                _IntelliSense.Suppress = false;
                _SuppressCaretUi = false;
            }
        }
        else if (!Change.Succeeded)
        {
            return;
        }

        int Start = Math.Max(0, Math.Min(Change.SelectionStart, SourceEditor.TextLength));
        int Length = Math.Max(0, Math.Min(Change.SelectionLength, SourceEditor.TextLength - Start));
        SourceEditor.Select(Start, Length);
        SourceEditor.ScrollToCaret();
    }

    private bool TryHandleEditingKey(KeyEventArgs E)
    {
        if (E.Control && !E.Alt && !E.Shift && E.KeyCode == Keys.F)
        {
            ShowFind();
            return true;
        }

        if (E.Control && !E.Alt && !E.Shift && E.KeyCode == Keys.H)
        {
            ShowReplace();
            return true;
        }

        if (E.Control && !E.Alt && !E.Shift && E.KeyCode == Keys.G)
        {
            ShowGoToLine();
            return true;
        }

        if (!E.Control && !E.Alt && E.KeyCode == Keys.F3)
        {
            FindInDirection(!E.Shift);
            return true;
        }

        if (E.Control && E.Shift && !E.Alt && E.KeyCode == Keys.L)
        {
            DeleteLines();
            return true;
        }

        if (!E.Control && !E.Alt && E.KeyCode == Keys.F8)
        {
            if (E.Shift)
            {
                PreviousDiagnostic();
            }
            else
            {
                NextDiagnostic();
            }

            return true;
        }

        if (E.Control && !E.Alt && !E.Shift && (E.KeyCode == Keys.Oemplus || E.KeyCode == Keys.Add))
        {
            ZoomIn();
            return true;
        }

        if (E.Control && !E.Alt && !E.Shift && (E.KeyCode == Keys.OemMinus || E.KeyCode == Keys.Subtract))
        {
            ZoomOut();
            return true;
        }

        if (E.Control && !E.Alt && !E.Shift && (E.KeyCode == Keys.D0 || E.KeyCode == Keys.NumPad0))
        {
            ResetZoom();
            return true;
        }

        if (E.KeyCode == Keys.Escape && FindBar.Visible)
        {
            CloseFindBar();
            return true;
        }

        return false;
    }

    private void SetEditorFont(float Size)
    {
        float Limited = Math.Max(8F, Math.Min(32F, Size));
        if (Math.Abs(SourceEditor.Font.Size - Limited) <= 0.01F)
        {
            return;
        }

        Font PreviousFont = SourceEditor.Font;
        SourceEditor.Font = EditorFonts.Monospace(Limited);
        PreviousFont.Dispose();
        FontSizeChanged?.Invoke(this, Limited);
        StatusRequested?.Invoke(this, "Editor font " + Limited.ToString("0") + " pt.");
    }

    private string IndentUnitText()
    {
        int Size = Math.Max(1, Math.Min(16, IndentSize));
        if (InsertSpaces)
        {
            return new string(' ', Size);
        }

        return "\t";
    }

    private string SourceText()
    {
        return SourceEditor.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private void FindNextButton_Click(object? Sender, EventArgs E)
    {
        FindInDirection(true);
    }

    private void FindPreviousButton_Click(object? Sender, EventArgs E)
    {
        FindInDirection(false);
    }

    private void ReplaceButton_Click(object? Sender, EventArgs E)
    {
        ReplaceMatch();
    }

    private void ReplaceAllButton_Click(object? Sender, EventArgs E)
    {
        ReplaceAllMatches();
    }

    private void CloseFindButton_Click(object? Sender, EventArgs E)
    {
        CloseFindBar();
    }

    private void CloseFindBar()
    {
        FindBar.Visible = false;
        _GoToLineMode = false;
        _FindLines.Clear();
        RefreshScrollMarks();
        SourceEditor.Focus();
    }

    private void QueryBox_KeyDown(object? Sender, KeyEventArgs E)
    {
        if (E.KeyCode == Keys.Enter)
        {
            FindInDirection(!E.Shift);
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.Escape)
        {
            CloseFindBar();
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.F3)
        {
            FindInDirection(!E.Shift);
            E.SuppressKeyPress = true;
            E.Handled = true;
        }
    }

    private void SourceEditor_HandleCreated(object? Sender, EventArgs E)
    {
        if (_EditorWheel.Handle != SourceEditor.Handle)
        {
            _EditorWheel.ReleaseHandle();
            _EditorWheel.AssignHandle(SourceEditor.Handle);
        }
    }

    private void SourceEditor_HandleDestroyed(object? Sender, EventArgs E)
    {
        _EditorWheel.ReleaseHandle();
    }

    private void CodeEditor_Disposed(object? Sender, EventArgs E)
    {
        _EditorWheel.ReleaseHandle();
        _IntelliSense.Dispose();
        _GhostText?.Dispose();
    }

    /// <summary>
    /// Turns Ctrl plus the mouse wheel into editor font zoom.
    /// </summary>
    private sealed class EditorMouseWheel : NativeWindow
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        private readonly CodeEditor _Owner;

        public EditorMouseWheel(CodeEditor Owner)
        {
            _Owner = Owner;
        }

        protected override void WndProc(ref Message Message)
        {
            if (Message.Msg == WM_MOUSEWHEEL && (Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                int Delta = (short)((Message.WParam.ToInt64() >> 16) & 0xFFFF);
                if (Delta > 0)
                {
                    _Owner.ZoomIn();
                }
                else if (Delta < 0)
                {
                    _Owner.ZoomOut();
                }

                Message.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref Message);
        }
    }
}
