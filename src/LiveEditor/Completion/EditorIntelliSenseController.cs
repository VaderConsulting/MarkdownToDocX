using System;
using System.Windows.Forms;

namespace LiveEditor.Completion;

/// <summary>
/// Drives the one editor popup from the text box events.
/// The live editor and the tests both use this, so the popup can be exercised without the rest of the window.
/// </summary>
internal sealed class EditorIntelliSenseController : IDisposable
{
    private readonly RichTextBox _Editor;

    private readonly System.Windows.Forms.Timer _QuickInfoTimer = new System.Windows.Forms.Timer();

    private readonly EditorAssist _Assist = new EditorAssist();

    private readonly CompletionPopup _Popup = new CompletionPopup();

    private bool _Disposed;

    private bool _Attached;

    private bool _QuickInfoChord;

    private bool _TextChangePending;

    private int _RefreshDepth;

    private int _HoverIndex = -1;

    private bool _QuickInfoFromHover;

    /// <summary>
    /// Creates a controller for one editor.
    /// </summary>
    /// <param name="Editor">The source editor.</param>
    public EditorIntelliSenseController(RichTextBox Editor)
    {
        if (Editor == null)
        {
            throw new ArgumentNullException(nameof(Editor));
        }

        _Editor = Editor;
        _QuickInfoTimer.Interval = 450;
        _QuickInfoTimer.Tick += QuickInfoTimer_Tick;
        _Popup.ItemAccepted += Popup_ItemAccepted;
    }

    /// <summary>Raised when List Members opens, so ghost text can be cleared.</summary>
    public event EventHandler? ListOpened;

    /// <summary>Raised when the popup wants a status message.</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>Raised after a suggestion is inserted.</summary>
    public event EventHandler<CompletionItem>? Accepted;

    /// <summary>Gets the suggestion engine.</summary>
    public EditorAssist Assist
    {
        get
        {
            return _Assist;
        }
    }

    /// <summary>Gets the popup.</summary>
    public CompletionPopup Popup
    {
        get
        {
            return _Popup;
        }
    }

    /// <summary>Gets or sets whether programmatic edits should leave the popup alone.</summary>
    public bool Suppress { get; set; }

    /// <summary>Gets or sets whether editor events are ignored, such as while colours are applied.</summary>
    public bool Pause { get; set; }

    /// <summary>
    /// True when Up and Down should move the suggestion list instead of the caret.
    /// A line that already declares an item never takes those keys, so the caret can leave it.
    /// </summary>
    internal bool OwnsVerticalArrows()
    {
        if (_Editor.SelectionLength > 0)
        {
            return false;
        }

        if (_Popup.Surface != AssistSurface.Members)
        {
            return false;
        }

        if (_Assist.IsDeclaredLine(_Editor.Text, _Editor.SelectionStart))
        {
            return false;
        }

        CompletionContext Context = _Assist.BuildMembers(_Editor.Text, _Editor.SelectionStart);
        if (Context.TokenKind == CompletionTokenKind.RelationshipKind
            || Context.TokenKind == CompletionTokenKind.RelationshipTarget
            || Context.TokenKind == CompletionTokenKind.Qualifier)
        {
            return false;
        }

        if (Context.Items.Count == 0)
        {
            return false;
        }

        int Caret = _Editor.SelectionStart;
        if (Caret < Context.ReplaceStart || Caret > Context.ReplaceEnd)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Context.Prefix))
        {
            return true;
        }

        foreach (CompletionItem Item in Context.Items)
        {
            if (string.Equals(Item.InsertText.Trim(), Context.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets whether a hover Quick Info is waiting.</summary>
    public bool HoverPending
    {
        get
        {
            return _QuickInfoTimer.Enabled;
        }
    }

    /// <summary>Starts listening to the editor.</summary>
    public void Attach()
    {
        if (_Attached)
        {
            return;
        }

        _Attached = true;
        _Editor.TextChanged += Editor_TextChanged;
        _Editor.SelectionChanged += Editor_SelectionChanged;
        _Editor.KeyDown += Editor_KeyDown;
        _Editor.PreviewKeyDown += Editor_PreviewKeyDown;
        _Editor.MouseDown += Editor_MouseDown;
        _Editor.MouseUp += Editor_MouseUp;
        _Editor.MouseMove += Editor_MouseMove;
        _Editor.MouseLeave += Editor_MouseLeave;
        _Editor.LostFocus += Editor_LostFocus;
    }

    /// <summary>Shows List Members at the caret.</summary>
    /// <param name="Force">When true, the list stays even if the word is already complete.</param>
    public void Refresh(bool Force)
    {
        if (_Disposed || _RefreshDepth > 0)
        {
            return;
        }

        if (!_Editor.Focused && !Force && !_TextChangePending)
        {
            return;
        }

        _RefreshDepth++;
        try
        {
            RefreshCore(Force);
        }
        finally
        {
            _RefreshDepth--;
        }
    }

    /// <summary>Shows Parameter Info for the caret.</summary>
    public void ShowParameter()
    {
        _QuickInfoFromHover = false;
        _Popup.Hide();
        ParameterHelp? Help = _Assist.BuildParameter(_Editor.Text, _Editor.SelectionStart);
        if (Help == null)
        {
            ReportStatus("No parameter information here.");
            return;
        }

        _Popup.ShowParameter(_Editor, _Editor.SelectionStart, Help);
    }

    /// <summary>Shows Quick Info for the word at the caret or the selection.</summary>
    public void ShowQuickInfoAtCaret()
    {
        ShowQuickInfo(_Editor.SelectionStart, Announce: true);
    }

    /// <summary>Shows the hover Quick Info that the timer would show.</summary>
    public void ShowPendingHover()
    {
        _QuickInfoTimer.Stop();
        if (_HoverIndex < 0 || _Editor.SelectionLength > 0 || _Popup.Surface == AssistSurface.Members || _Popup.Surface == AssistSurface.Parameter)
        {
            return;
        }

        ShowQuickInfo(_HoverIndex, Announce: false);
        _QuickInfoFromHover = _Popup.Surface == AssistSurface.QuickInfo;
    }

    /// <summary>Hides the popup.</summary>
    public void Hide()
    {
        _QuickInfoFromHover = false;
        _Popup.Hide();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        if (_Attached)
        {
            _Editor.TextChanged -= Editor_TextChanged;
            _Editor.SelectionChanged -= Editor_SelectionChanged;
            _Editor.KeyDown -= Editor_KeyDown;
            _Editor.PreviewKeyDown -= Editor_PreviewKeyDown;
            _Editor.MouseDown -= Editor_MouseDown;
            _Editor.MouseUp -= Editor_MouseUp;
            _Editor.MouseMove -= Editor_MouseMove;
            _Editor.MouseLeave -= Editor_MouseLeave;
            _Editor.LostFocus -= Editor_LostFocus;
        }

        _QuickInfoTimer.Stop();
        _QuickInfoTimer.Tick -= QuickInfoTimer_Tick;
        _QuickInfoTimer.Dispose();
        _Popup.ItemAccepted -= Popup_ItemAccepted;
        _Popup.Dispose();
    }

    private void Editor_TextChanged(object? Sender, EventArgs E)
    {
        if (Pause || Suppress)
        {
            return;
        }

        _TextChangePending = true;
        Refresh(Force: false);
        if (_Editor.IsHandleCreated)
        {
            _Editor.BeginInvoke(FinishTextChange);
        }
        else
        {
            FinishTextChange();
        }
    }

    private void FinishTextChange()
    {
        _TextChangePending = false;
    }

    private void Editor_SelectionChanged(object? Sender, EventArgs E)
    {
        if (Pause)
        {
            return;
        }

        if (_TextChangePending)
        {
            if (_Popup.IsVisible)
            {
                _Popup.Reposition();
            }

            return;
        }

        if (IsSingleIdentifierSelection())
        {
            ShowSelectionInfo();
            return;
        }

        if (Control.MouseButtons != MouseButtons.None || !_Popup.IsVisible)
        {
            _Popup.Hide();
            return;
        }

        Refresh(Force: false);
    }

    private void Editor_MouseUp(object? Sender, MouseEventArgs E)
    {
        if (_TextChangePending || _Editor.SelectionLength > 0)
        {
            return;
        }

        _Popup.Hide();
    }

    private void Editor_MouseDown(object? Sender, MouseEventArgs E)
    {
        _QuickInfoTimer.Stop();
        _HoverIndex = -1;
    }

    private void Editor_MouseMove(object? Sender, MouseEventArgs E)
    {
        int Index = _Editor.GetCharIndexFromPosition(E.Location);
        if (_QuickInfoFromHover && _Popup.Surface == AssistSurface.QuickInfo)
        {
            if (!PointerIsOnWord(Index))
            {
                _QuickInfoFromHover = false;
                _HoverIndex = -1;
                _QuickInfoTimer.Stop();
                _Popup.Hide();
                return;
            }

            if (WordStart(Index) != WordStart(_HoverIndex))
            {
                _HoverIndex = Index;
                ShowQuickInfo(Index, Announce: false);
                _QuickInfoFromHover = _Popup.Surface == AssistSurface.QuickInfo;
            }

            return;
        }

        if (Index == _HoverIndex)
        {
            return;
        }

        _HoverIndex = Index;
        if (_Popup.IsVisible)
        {
            return;
        }

        _QuickInfoTimer.Stop();
        _QuickInfoTimer.Start();
    }

    private void Editor_MouseLeave(object? Sender, EventArgs E)
    {
        _HoverIndex = -1;
        _QuickInfoTimer.Stop();
        if (_QuickInfoFromHover && _Popup.Surface == AssistSurface.QuickInfo)
        {
            _QuickInfoFromHover = false;
            _Popup.Hide();
        }
    }

    private bool PointerIsOnWord(int Index)
    {
        if (Index < 0 || Index >= _Editor.TextLength)
        {
            return false;
        }

        return IsWordCharacter(_Editor.Text[Index]);
    }

    private int WordStart(int Index)
    {
        if (Index < 0 || Index >= _Editor.TextLength || !IsWordCharacter(_Editor.Text[Index]))
        {
            return -1;
        }

        int Start = Index;
        while (Start > 0 && IsWordCharacter(_Editor.Text[Start - 1]))
        {
            Start--;
        }

        return Start;
    }

    private static bool IsWordCharacter(char Character)
    {
        return char.IsLetterOrDigit(Character) || Character == '_' || Character == '-';
    }

    private void Editor_LostFocus(object? Sender, EventArgs E)
    {
        if (!_Popup.IsVisible || !_Editor.IsHandleCreated)
        {
            return;
        }

        _Editor.BeginInvoke(HideIfEditorIdle);
    }

    private void HideIfEditorIdle()
    {
        if (_Disposed || _Editor.Focused || _Popup.ContainsFocus)
        {
            return;
        }

        _Popup.Hide();
    }

    private void Editor_PreviewKeyDown(object? Sender, PreviewKeyDownEventArgs E)
    {
        bool ListOwnsArrows = OwnsVerticalArrows() && (E.KeyCode == Keys.Up || E.KeyCode == Keys.Down);
        bool AcceptOrDismiss = _Popup.IsVisible && (E.KeyCode == Keys.Tab || E.KeyCode == Keys.Enter || E.KeyCode == Keys.Escape);
        if (ListOwnsArrows || AcceptOrDismiss)
        {
            E.IsInputKey = true;
        }
    }

    private void Editor_KeyDown(object? Sender, KeyEventArgs E)
    {
        if (_QuickInfoChord)
        {
            _QuickInfoChord = false;
            if (E.Control && E.KeyCode == Keys.I)
            {
                ShowQuickInfoAtCaret();
                E.SuppressKeyPress = true;
                E.Handled = true;
                return;
            }
        }

        if (E.Control && !E.Shift && !E.Alt && E.KeyCode == Keys.K)
        {
            _QuickInfoChord = true;
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.Control && E.Shift && E.KeyCode == Keys.Space)
        {
            ShowParameter();
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if ((E.Control && E.KeyCode == Keys.Space) || (E.Control && !E.Shift && !E.Alt && E.KeyCode == Keys.J))
        {
            Refresh(Force: true);
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (E.KeyCode == Keys.Escape)
        {
            _Popup.Hide();
        }

        if ((E.KeyCode == Keys.Up || E.KeyCode == Keys.Down) && _Popup.Surface == AssistSurface.Members && !OwnsVerticalArrows())
        {
            _Popup.Hide();
            return;
        }

        if (_Popup.TryHandleKey(E.KeyData))
        {
            E.SuppressKeyPress = true;
            E.Handled = true;
        }
    }

    private void QuickInfoTimer_Tick(object? Sender, EventArgs E)
    {
        ShowPendingHover();
    }

    private void Popup_ItemAccepted(object? Sender, CompletionItem Item)
    {
        Accepted?.Invoke(this, Item);
        if (_Editor.IsHandleCreated)
        {
            _Editor.BeginInvoke(new Action(RefreshAfterAccept));
        }
    }

    private void RefreshAfterAccept()
    {
        Refresh(Force: false);
    }

    private void RefreshCore(bool Force)
    {
        _QuickInfoFromHover = false;
        if (!Force && _Editor.SelectionLength > 0)
        {
            if (IsSingleIdentifierSelection())
            {
                ShowSelectionInfo();
            }
            else
            {
                _Popup.Hide();
            }

            return;
        }

        CompletionContext Context = _Assist.BuildMembers(_Editor.Text, _Editor.SelectionStart);
        bool ShowList = Context.Items.Count > 0 && (Force || !IsCompletedToken(Context));
        if (!ShowList)
        {
            if (!Force)
            {
            ParameterHelp? Help = _Assist.BuildParameter(_Editor.Text, _Editor.SelectionStart);
            if (Help != null)
            {
                _Popup.ShowParameter(_Editor, _Editor.SelectionStart, Help);
                return;
            }
            }

            if (Force && !string.IsNullOrWhiteSpace(Context.StatusMessage))
            {
                ReportStatus(Context.StatusMessage);
            }

            _Popup.Hide();
            return;
        }

        ParameterHelp? Header = _Assist.BuildParameter(_Editor.Text, _Editor.SelectionStart);
        _Popup.ShowMembers(_Editor, Context, Header);
        ListOpened?.Invoke(this, EventArgs.Empty);
        ReportStatus(Context.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " suggestion(s) - Tab/Enter to accept, Esc to dismiss");
    }

    private void ShowSelectionInfo()
    {
        _QuickInfoFromHover = false;
        string? Text = _Assist.BuildQuickInfo(_Editor.Text, _Editor.SelectionStart);
        if (string.IsNullOrEmpty(Text))
        {
            _Popup.Hide();
            return;
        }

        _Popup.ShowQuickInfo(_Editor, _Editor.SelectionStart, Text);
    }

    private void ShowQuickInfo(int CharIndex, bool Announce)
    {
        string? Text = _Assist.BuildQuickInfo(_Editor.Text, CharIndex);
        if (string.IsNullOrEmpty(Text))
        {
            if (_Popup.Surface == AssistSurface.QuickInfo)
            {
                _Popup.Hide();
            }

            if (Announce)
            {
                ReportStatus("No information for this word.");
            }

            return;
        }

        _Popup.ShowQuickInfo(_Editor, CharIndex, Text);
    }

    private bool IsSingleIdentifierSelection()
    {
        if (_Editor.SelectionLength <= 0)
        {
            return false;
        }

        string Selected = _Editor.SelectedText;
        if (Selected.IndexOf('\n') >= 0 || Selected.IndexOf('\r') >= 0 || Selected.IndexOf(' ') >= 0 || Selected.IndexOf('\t') >= 0)
        {
            return false;
        }

        foreach (char Character in Selected)
        {
            if (!char.IsLetterOrDigit(Character) && Character != '_' && Character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompletedToken(CompletionContext Context)
    {
        if (string.IsNullOrEmpty(Context.Prefix))
        {
            return false;
        }

        foreach (CompletionItem Item in Context.Items)
        {
            string Candidate = Item.InsertText.Trim();
            if (Candidate.StartsWith(Context.Prefix, StringComparison.OrdinalIgnoreCase) && Candidate.Length > Context.Prefix.Length)
            {
                return false;
            }
        }

        return true;
    }

    private void ReportStatus(string Message)
    {
        StatusChanged?.Invoke(this, Message);
    }
}
