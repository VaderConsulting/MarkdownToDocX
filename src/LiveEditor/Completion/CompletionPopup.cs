using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using LiveEditor.Editor;

namespace LiveEditor.Completion;

/// <summary>
/// Which job the single editor popup is doing.
/// </summary>
internal enum AssistSurface
{
    /// <summary>The popup is closed.</summary>
    None,

    /// <summary>List Members, with optional parameter signature and notes.</summary>
    Members,

    /// <summary>Parameter Info only.</summary>
    Parameter,

    /// <summary>Quick Info for one word.</summary>
    QuickInfo
}

/// <summary>
/// One caret-anchored popup for List Members, Parameter Info, and Quick Info.
/// It does not take focus, so a click in the editor moves the caret.
/// </summary>
internal sealed class CompletionPopup : IDisposable, IMessageFilter
{
    private const int WM_MOUSEACTIVATE = 0x0021;

    private const int MA_NOACTIVATE = 3;

    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_KEYDOWN = 0x0100;

    private readonly AssistWindow _Window;

    private readonly RichTextBox _Signature;

    private readonly ListBox _List;

    private readonly TextBox _Detail;

    private Form? _HookedOwner;

    private RichTextBox? _HookedEditor;

    private CompletionContext? _Context;

    private TextBoxBase? _Target;

    private int _AnchorIndex;

    private bool _PlaceAbove;

    private bool _IsDisposed;

    private bool _IsPlacing;

    private bool _UpdatingList;

    private bool _FilterInstalled;

    private string _ContentSignature = string.Empty;

    /// <summary>Widest Parameter Info box in logical pixels, like the Visual Studio tooltip. Longer text wraps.</summary>
    private const int PARAMETER_INFO_WIDTH = 560;

    /// <summary>The editor's code font, used for every part of the popup as in Visual Studio.</summary>
    private readonly Font _CodeFont = EditorFonts.Monospace(9F);

    /// <summary>Same font for the Parameter Info description. Kept separate so the description can change on its own.</summary>
    private readonly Font _ProseFont = EditorFonts.Monospace(9F);

    /// <summary>Bold for the active parameter in the signature. Cascadia Mono has a true bold weight that stands out clearly.</summary>
    private readonly Font _SignatureBoldFont = EditorFonts.Monospace(9F, FontStyle.Bold);

    /// <summary>Font for the suggestion list.</summary>
    private readonly Font _ListFont = EditorFonts.Monospace(10F);

    /// <summary>Creates the popup.</summary>
    public CompletionPopup()
    {
        _Signature = new RichTextBox
        {
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.None,
            DetectUrls = false,
            TabStop = false,
            Font = _ProseFont,
            WordWrap = true,
            BackColor = SystemColors.Info,
            ForeColor = SystemColors.InfoText
        };
        _List = new ListBox
        {
            BorderStyle = BorderStyle.None,
            Font = _ListFont,
            IntegralHeight = false,
            HorizontalScrollbar = false,
            ItemHeight = 22,
            DrawMode = DrawMode.Normal,
            TabStop = false,
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.WindowText
        };
        _Detail = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = _CodeFont,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.None,
            TabStop = false,
            BackColor = SystemColors.Info,
            ForeColor = SystemColors.InfoText
        };
        _List.Click += List_Click;
        _List.SelectedIndexChanged += List_SelectedIndexChanged;
        _Detail.MouseDown += DismissFromPointer;
        _Signature.MouseDown += DismissFromPointer;
        _Window = new AssistWindow();
        _Window.Controls.Add(_Signature);
        _Window.Controls.Add(_List);
        _Window.Controls.Add(_Detail);

        // Create the window handles now, while hidden, so the signature can be measured before the first show.
        _ = _Window.Handle;
        _ = _Signature.Handle;
    }

    /// <summary>Raised when the user accepts a suggestion.</summary>
    public event EventHandler<CompletionItem>? ItemAccepted;

    /// <summary>Gets whether the popup is visible.</summary>
    public bool IsVisible
    {
        get
        {
            return _Window.Visible;
        }
    }

    /// <summary>Gets whether the popup itself has keyboard focus.</summary>
    public bool ContainsFocus
    {
        get
        {
            return _Window.ContainsFocus;
        }
    }

    /// <summary>Gets the job the popup is doing.</summary>
    public AssistSurface Surface { get; private set; } = AssistSurface.None;

    /// <summary>Gets the labels currently shown in the list.</summary>
    internal IReadOnlyList<string> MemberLabels
    {
        get
        {
            List<string> Labels = new List<string>();
            foreach (object Entry in _List.Items)
            {
                Labels.Add(Entry == null ? string.Empty : Entry.ToString() ?? string.Empty);
            }

            return Labels;
        }
    }

    /// <summary>Gets the label of the highlighted row.</summary>
    internal string SelectedMemberLabel
    {
        get
        {
            if (_List.SelectedItem == null)
            {
                return string.Empty;
            }

            return _List.SelectedItem.ToString() ?? string.Empty;
        }
    }

    /// <summary>Gets the notes beside the list, or the Quick Info or Parameter Info body.</summary>
    internal string DetailText
    {
        get
        {
            return _Detail.Text;
        }
    }

    /// <summary>Gets the signature header.</summary>
    internal string SignatureText
    {
        get
        {
            return _Signature.Text.Replace('\u2011', '-');
        }
    }

    /// <summary>Gets the height of the signature header in pixels.</summary>
    internal int SignatureHeight
    {
        get
        {
            return _Signature.Height;
        }
    }

    /// <summary>Gets the popup size in pixels.</summary>
    internal Size WindowSize
    {
        get
        {
            return _Window.Size;
        }
    }

    /// <summary>Gets the popup location in screen pixels.</summary>
    internal Point WindowLocation
    {
        get
        {
            return _Window.Location;
        }
    }

    /// <summary>Gets whether any part of the popup is showing a scrollbar.</summary>
    internal bool ShowsScrollBar
    {
        get
        {
            if (_Detail.ScrollBars != ScrollBars.None)
            {
                return true;
            }

            if (_Signature.ScrollBars != RichTextBoxScrollBars.None)
            {
                return true;
            }

            if (!_List.Visible)
            {
                return false;
            }

            int Needed = _List.Items.Count * _List.ItemHeight;
            return Needed > _List.ClientSize.Height + 1;
        }
    }

    /// <summary>Shows List Members under the caret. The signature, when present, is the header of this same window.</summary>
    public void ShowMembers(TextBoxBase Target, CompletionContext Context, ParameterHelp? Parameter)
    {
        if (Target == null || Context == null || Context.Items.Count == 0)
        {
            Hide();
            return;
        }

        string Signature = BuildContentSignature(Context, Parameter);
        bool KeepEditorFocus = Target.Focused;
        _Target = Target;
        _Context = Context;
        _AnchorIndex = Target.SelectionStart;
        _PlaceAbove = false;
        if (_Window.Visible && Surface == AssistSurface.Members && string.Equals(_ContentSignature, Signature, StringComparison.Ordinal))
        {
            Reposition();
            RestoreEditorFocus(Target, KeepEditorFocus);
            return;
        }

        UseCodeNotes();
        string? SelectedInsert = (_List.SelectedItem as CompletionItem)?.InsertText;
        _UpdatingList = true;
        try
        {
            _List.BeginUpdate();
            _List.Items.Clear();
            foreach (CompletionItem Item in Context.Items)
            {
                _List.Items.Add(Item);
            }

            int SelectedIndex = FindItemIndex(SelectedInsert);
            _List.SelectedIndex = SelectedIndex >= 0 ? SelectedIndex : 0;
            _List.EndUpdate();
        }
        finally
        {
            _UpdatingList = false;
        }
        _ContentSignature = Signature;
        Surface = AssistSurface.Members;
        ApplySignature(Parameter);
        ShowSelectedDocumentation();
        LayoutMembers(Target);
        Present(Target, KeepEditorFocus);
        _List.Refresh();
    }

    /// <summary>Shows Parameter Info in the same window.</summary>
    public void ShowParameter(TextBoxBase Target, int AnchorIndex, ParameterHelp Help)
    {
        if (Target == null || Help == null)
        {
            Hide();
            return;
        }

        bool KeepEditorFocus = Target.Focused;
        _Target = Target;
        _Context = null;
        _AnchorIndex = AnchorIndex;
        _PlaceAbove = true;
        Surface = AssistSurface.Parameter;
        _ContentSignature = Help.Signature + "\u001F" + Help.Summary + "\u001F" + Help.Heading + "\u001F" + Help.Body;
        ApplySignature(Help);

        // Like Visual Studio: the signature on top, then the active parameter's name and description,
        // in a box of limited width where long text wraps instead of running off the editor.
        _Detail.Font = _ProseFont;
        _Detail.WordWrap = true;
        _Detail.Text = WithWindowsLineBreaks(ParameterText(Help));
        _List.Visible = false;
        _Detail.Visible = true;
        _Signature.Visible = true;
        int Limit = Math.Min(AvailableWidth(Target), Target.LogicalToDeviceUnits(PARAMETER_INFO_WIDTH));
        int Natural = Math.Max(MeasureSignatureWidth(), MeasureWrapped(_Detail.Text, _Detail.Font, int.MaxValue).Width + 16);
        int Width = Math.Min(Natural, Limit);
        int Header = MeasureSignatureHeight(Width);
        int BodyHeight = MeasureWrapped(_Detail.Text, _Detail.Font, Width - 16).Height + 10;
        _Signature.SetBounds(1, 1, Width, Header);
        _Detail.SetBounds(1, Header + 1, Width, BodyHeight);
        _Window.ClientSize = new Size(Width + 2, Header + BodyHeight + 2);
        Present(Target, KeepEditorFocus);
    }

    /// <summary>Shows Quick Info for one word in the same window.</summary>
    public void ShowQuickInfo(TextBoxBase Target, int AnchorIndex, string Text)
    {
        if (Target == null || string.IsNullOrEmpty(Text))
        {
            Hide();
            return;
        }

        bool KeepEditorFocus = Target.Focused;
        _Target = Target;
        _Context = null;
        _AnchorIndex = AnchorIndex;
        _PlaceAbove = false;
        Surface = AssistSurface.QuickInfo;
        _ContentSignature = Text;
        _Signature.Visible = false;
        _List.Visible = false;
        _Detail.Visible = true;
        UseCodeNotes();
        _Detail.Text = WithWindowsLineBreaks(Text);
        Size Body = MeasureBlock(Text, _Detail.Font, AvailableWidth(Target));
        _Detail.SetBounds(1, 1, Body.Width, Body.Height);
        _Window.ClientSize = new Size(Body.Width + 2, Body.Height + 2);
        Present(Target, KeepEditorFocus);
    }

    /// <summary>Moves the open popup so it stays with the caret.</summary>
    public void Reposition()
    {
        if (_IsPlacing || !_Window.Visible || _Target == null || _Target.IsDisposed)
        {
            return;
        }

        if (Surface == AssistSurface.Members || Surface == AssistSurface.Parameter)
        {
            _AnchorIndex = _Target.SelectionStart;
        }

        Place(_Target);
    }

    /// <summary>Hides the popup.</summary>
    public void Hide()
    {
        Surface = AssistSurface.None;
        _Context = null;
        _Target = null;
        _ContentSignature = string.Empty;
        UnhookOwner();
        UnhookEditor();
        RemoveFilter();
        if (_Window.Visible)
        {
            _Window.Hide();
        }
    }

    /// <summary>Escape dismisses the popup even when it is the window under the pointer.</summary>
    public bool PreFilterMessage(ref Message Message)
    {
        if (!_Window.Visible || Message.Msg != WM_KEYDOWN || Message.WParam != (IntPtr)Keys.Escape)
        {
            return false;
        }

        Hide();
        return true;
    }

    /// <summary>Routes list keys. Other keys are left for the editor.</summary>
    public bool TryHandleKey(Keys KeyData)
    {
        if (!_Window.Visible)
        {
            return false;
        }

        if (KeyData == Keys.Escape)
        {
            Hide();
            return true;
        }

        if (Surface != AssistSurface.Members || _List.Items.Count == 0)
        {
            return false;
        }

        if (KeyData == Keys.Up)
        {
            if (_List.SelectedIndex > 0)
            {
                _List.SelectedIndex--;
            }

            return true;
        }

        if (KeyData == Keys.Down)
        {
            if (_List.SelectedIndex < _List.Items.Count - 1)
            {
                _List.SelectedIndex++;
            }

            return true;
        }

        if (KeyData == Keys.PageUp)
        {
            _List.SelectedIndex = Math.Max(0, _List.SelectedIndex - 8);
            return true;
        }

        if (KeyData == Keys.PageDown)
        {
            _List.SelectedIndex = Math.Min(_List.Items.Count - 1, _List.SelectedIndex + 8);
            return true;
        }

        if (KeyData == Keys.Enter || KeyData == Keys.Tab)
        {
            AcceptSelected();
            return true;
        }

        return false;
    }

    /// <summary>Inserts the selected item.</summary>
    public void AcceptSelected()
    {
        if (_Target == null || _Context == null || _List.SelectedItem is not CompletionItem Item)
        {
            Hide();
            return;
        }

        TextBoxBase Target = _Target;
        CompletionContext Context = _Context;
        int Start = Math.Max(0, Math.Min(Context.ReplaceStart, Target.TextLength));
        int End = Math.Max(Start, Math.Min(Context.ReplaceEnd, Target.TextLength));
        Target.Select(Start, End - Start);
        Target.SelectedText = Item.InsertText;
        Hide();
        ItemAccepted?.Invoke(this, Item);
        Target.Focus();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_IsDisposed)
        {
            return;
        }

        _IsDisposed = true;
        Hide();
        _List.Click -= List_Click;
        _List.SelectedIndexChanged -= List_SelectedIndexChanged;
        _Window.Dispose();
        _CodeFont.Dispose();
        _ProseFont.Dispose();
        _SignatureBoldFont.Dispose();
        _ListFont.Dispose();
    }

    private void LayoutMembers(TextBoxBase Target)
    {
        int ListHeight = Math.Max(_List.ItemHeight, (_List.Items.Count * _List.ItemHeight) + 8);
        int ListWidth = MeasureListWidth(Target);
        bool ShowNotes = HasDocumentation();
        Size Notes = Size.Empty;
        if (ShowNotes)
        {
            int NotesLimit = Math.Max(160, AvailableWidth(Target) - ListWidth);
            Notes = MeasureBlock(_Detail.Text, _Detail.Font, NotesLimit);
        }

        int Width = ListWidth + Notes.Width;
        int Header = MeasureSignatureHeight(Width);
        int BodyHeight = Math.Max(ListHeight, Notes.Height);
        _Signature.Visible = Header > 0;
        _Signature.SetBounds(1, 1, Width, Header);
        _List.Visible = true;
        _List.SetBounds(1, Header + 1, ListWidth, ListHeight);
        _Detail.Visible = ShowNotes;
        if (ShowNotes)
        {
            _Detail.SetBounds(1 + ListWidth, Header + 1, Notes.Width, Notes.Height);
        }

        _Window.ClientSize = new Size(Width + 2, Header + BodyHeight + 2);
    }

    private void Present(TextBoxBase Target, bool KeepEditorFocus)
    {
        Form? Owner = Target.FindForm();
        if (Owner != null && !ReferenceEquals(_Window.Owner, Owner))
        {
            _Window.Owner = Owner;
        }

        HookOwner(Owner);
        HookEditor(Target);
        InstallFilter();
        Place(Target);
        if (!_Window.Visible)
        {
            _Window.Show();
        }

        RestoreEditorFocus(Target, KeepEditorFocus);
        _Window.Invalidate(true);
    }

    private static void RestoreEditorFocus(TextBoxBase Target, bool KeepEditorFocus)
    {
        if (!KeepEditorFocus || Target.IsDisposed || Target.Focused)
        {
            return;
        }

        Target.Focus();
    }

    private void Place(TextBoxBase Target)
    {
        if (_IsPlacing || Target.IsDisposed)
        {
            return;
        }

        _IsPlacing = true;
        try
        {
            PlaceCore(Target);
        }
        finally
        {
            _IsPlacing = false;
        }
    }

    private void PlaceCore(TextBoxBase Target)
    {
        Point Caret = Target.PointToScreen(GetCaretClientPoint(Target, _AnchorIndex));
        Rectangle Working = Screen.FromControl(Target).WorkingArea;
        int BelowTop = Caret.Y + Target.Font.Height + 2;
        int AboveTop = Caret.Y - _Window.Height - 4;
        bool FitsBelow = BelowTop + _Window.Height <= Working.Bottom;
        bool FitsAbove = AboveTop >= Working.Top;
        bool MoreRoomAbove = Caret.Y - Working.Top > Working.Bottom - BelowTop;

        // Keep the popup against the caret line. Use the preferred side when it fits,
        // otherwise the other side, otherwise whichever side has more room.
        bool Above = _PlaceAbove
            ? FitsAbove || (!FitsBelow && MoreRoomAbove)
            : !FitsBelow && (FitsAbove || MoreRoomAbove);

        Point ScreenPoint = new Point(Caret.X, Above ? AboveTop : BelowTop);
        if (ScreenPoint.X + _Window.Width > Working.Right)
        {
            ScreenPoint.X = Math.Max(Working.Left, Working.Right - _Window.Width);
        }

        if (ScreenPoint.Y + _Window.Height > Working.Bottom)
        {
            ScreenPoint.Y = Working.Bottom - _Window.Height;
        }

        if (ScreenPoint.Y < Working.Top)
        {
            ScreenPoint.Y = Working.Top;
        }

        if (_Window.Location != ScreenPoint)
        {
            _Window.Location = ScreenPoint;
        }
    }

    private void HookOwner(Form? Owner)
    {
        if (ReferenceEquals(_HookedOwner, Owner))
        {
            return;
        }

        UnhookOwner();
        _HookedOwner = Owner;
        if (Owner == null)
        {
            return;
        }

        Owner.LocationChanged += Owner_Changed;
        Owner.SizeChanged += Owner_Changed;
    }

    private void UnhookOwner()
    {
        if (_HookedOwner == null)
        {
            return;
        }

        _HookedOwner.LocationChanged -= Owner_Changed;
        _HookedOwner.SizeChanged -= Owner_Changed;
        _HookedOwner = null;
    }

    private void HookEditor(TextBoxBase Target)
    {
        RichTextBox? Editor = Target as RichTextBox;
        if (ReferenceEquals(_HookedEditor, Editor))
        {
            return;
        }

        UnhookEditor();
        _HookedEditor = Editor;
        if (Editor == null)
        {
            return;
        }

        Editor.VScroll += Editor_Scrolled;
        Editor.HScroll += Editor_Scrolled;
    }

    private void UnhookEditor()
    {
        if (_HookedEditor == null)
        {
            return;
        }

        _HookedEditor.VScroll -= Editor_Scrolled;
        _HookedEditor.HScroll -= Editor_Scrolled;
        _HookedEditor = null;
    }

    private void InstallFilter()
    {
        if (_FilterInstalled)
        {
            return;
        }

        Application.AddMessageFilter(this);
        _FilterInstalled = true;
    }

    private void RemoveFilter()
    {
        if (!_FilterInstalled)
        {
            return;
        }

        Application.RemoveMessageFilter(this);
        _FilterInstalled = false;
    }

    private void Editor_Scrolled(object? Sender, EventArgs E)
    {
        Reposition();
    }

    private void ApplySignature(ParameterHelp? Help)
    {
        if (Help == null || Help.Signature.Length == 0)
        {
            _Signature.Text = string.Empty;
            _Signature.Visible = false;
            return;
        }

        _Signature.Visible = true;

        // A non-breaking hyphen keeps words such as left-of whole when the signature wraps. It is the same length,
        // so the bold range still lines up.
        _Signature.Text = Help.Signature.Replace('-', '\u2011');
        _Signature.SelectAll();
        _Signature.SelectionFont = _Signature.Font;
        _Signature.SelectionColor = _Signature.ForeColor;
        _Signature.Select(0, 0);
        if (Help.BoldLength > 0 && Help.BoldStart >= 0 && Help.BoldStart + Help.BoldLength <= Help.Signature.Length)
        {
            _Signature.Select(Help.BoldStart, Help.BoldLength);
            _Signature.SelectionFont = _SignatureBoldFont;
            _Signature.Select(0, 0);
        }
    }

    private int MeasureSignatureWidth()
    {
        Size Measured = TextRenderer.MeasureText(_Signature.Text, _SignatureBoldFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        return Measured.Width + 16;
    }

    private int MeasureSignatureHeight(int Width)
    {
        // Visible reads false while the popup window itself is hidden, so the text decides instead.
        if (_Signature.Text.Length == 0)
        {
            return 0;
        }

        // Ask the box itself where its last character landed, because the bold parameter is wider
        // than plain text and a plain measurement can miss a wrapped line.
        _Signature.Width = Width;
        if (_Signature.IsHandleCreated)
        {
            int First = _Signature.GetPositionFromCharIndex(0).Y;
            int Last = _Signature.GetPositionFromCharIndex(_Signature.TextLength - 1).Y;
            return (Last - First) + _SignatureBoldFont.Height + 8;
        }

        Size Measured = MeasureWrapped(_Signature.Text, _SignatureBoldFont, Width - 16);
        return Math.Max(_Signature.Font.Height + 8, Measured.Height + 8);
    }

    /// <summary>Size of text wrapped at word breaks to the given width, the way a wrapping text box lays it out.</summary>
    private static Size MeasureWrapped(string Text, Font Font, int MaximumWidth)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(0, Font.Height);
        }

        TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
        return TextRenderer.MeasureText(Text, Font, new Size(Math.Max(40, MaximumWidth), int.MaxValue), Flags);
    }

    /// <summary>Visual Studio's layout under the signature: what the statement does, then "name: description" for the active parameter.</summary>
    private static string ParameterText(ParameterHelp Help)
    {
        List<string> Parts = new List<string>();
        if (Help.Summary.Length > 0)
        {
            Parts.Add(Help.Summary);
        }

        string Parameter = Help.Heading.Length == 0 ? Help.Body : Help.Heading + ": " + Help.Body;
        if (Parameter.Length > 0)
        {
            Parts.Add(Parameter);
        }

        return string.Join("\n", Parts);
    }

    /// <summary>The list notes and Quick Info are preformatted, so they use the code font and never wrap.</summary>
    private void UseCodeNotes()
    {
        _Detail.Font = _CodeFont;
        _Detail.WordWrap = false;
    }

    private int MeasureListWidth(TextBoxBase Target)
    {
        const int HORIZONTAL_PADDING = 36;
        int ContentWidth = 40;
        foreach (CompletionItem Item in _List.Items)
        {
            Size Measured = TextRenderer.MeasureText(Item.ToString(), _List.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            int Needed = Measured.Width + HORIZONTAL_PADDING;
            if (Needed > ContentWidth)
            {
                ContentWidth = Needed;
            }
        }

        int EditorLimit = AvailableWidth(Target);
        if (ContentWidth > EditorLimit)
        {
            return EditorLimit;
        }

        return ContentWidth;
    }

    private bool HasDocumentation()
    {
        foreach (object Entry in _List.Items)
        {
            if (Entry is CompletionItem Item && Item.Documentation.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ShowSelectedDocumentation()
    {
        if (_List.SelectedItem is CompletionItem Item)
        {
            _Detail.Text = WithWindowsLineBreaks(Item.Documentation);
        }
        else
        {
            _Detail.Text = string.Empty;
        }

    }

    /// <summary>
    /// Converts every line break to CR LF, because a multiline TextBox only starts a new line at CR LF.
    /// Help text built with a bare LF would otherwise run together on one line.
    /// </summary>
    private static string WithWindowsLineBreaks(string? Text)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return string.Empty;
        }

        return Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
    }
    /// <summary>Width and height of preformatted notes. Lines stay as written so columns remain aligned.</summary>
    private static Size MeasureBlock(string Text, Font Font, int MaximumWidth)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(40, Font.Height + 8);
        }

        string[] Lines = Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int Width = 40;
        int LineHeight = Font.Height;
        foreach (string Line in Lines)
        {
            Size Measured = TextRenderer.MeasureText(Line.Length == 0 ? " " : Line, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            if (Measured.Width + 16 > Width)
            {
                Width = Measured.Width + 16;
            }

            if (Measured.Height > LineHeight)
            {
                LineHeight = Measured.Height;
            }
        }

        int Limit = Math.Max(160, MaximumWidth);
        if (Width > Limit)
        {
            Width = Limit;
        }

        int Height = (LineHeight * Math.Max(1, Lines.Length)) + 10;
        return new Size(Width, Height);
    }

    /// <summary>How wide a popup may be before it leaves the screen.</summary>
    private static int AvailableWidth(Control Control)
    {
        Rectangle Working = Screen.FromControl(Control).WorkingArea;
        return Math.Max(200, Working.Width - 48);
    }

    private void List_SelectedIndexChanged(object? Sender, EventArgs E)
    {
        if (_UpdatingList || _IsPlacing || Surface != AssistSurface.Members)
        {
            return;
        }

        ShowSelectedDocumentation();
        if (_Target != null)
        {
            LayoutMembers(_Target);
            Reposition();
        }
    }

    private void DismissFromPointer(object? Sender, MouseEventArgs E)
    {
        TextBoxBase? Target = _Target;
        Hide();
        if (Target != null && !Target.IsDisposed)
        {
            Target.Focus();
        }
    }

    private void List_Click(object? Sender, EventArgs E)
    {
        Point Client = _List.PointToClient(Control.MousePosition);
        if (Client.X >= _List.ClientSize.Width || Client.Y >= _List.ClientSize.Height)
        {
            return;
        }

        AcceptSelected();
    }

    private void Owner_Changed(object? Sender, EventArgs E)
    {
        Reposition();
    }

    private int FindItemIndex(string? InsertText)
    {
        if (string.IsNullOrEmpty(InsertText))
        {
            return -1;
        }

        for (int Index = 0; Index < _List.Items.Count; Index++)
        {
            if (_List.Items[Index] is CompletionItem Item && string.Equals(Item.InsertText, InsertText, StringComparison.Ordinal))
            {
                return Index;
            }
        }

        return -1;
    }

    private static string BuildContentSignature(CompletionContext Context, ParameterHelp? Parameter)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append((int)Context.TokenKind);
        Builder.Append('|');
        Builder.Append(Context.ReplaceStart);
        Builder.Append('|');
        Builder.Append(Context.ReplaceEnd);
        Builder.Append('|');
        Builder.Append(Parameter?.Signature ?? string.Empty);
        foreach (CompletionItem Item in Context.Items)
        {
            Builder.Append('|');
            Builder.Append(Item.InsertText);
            Builder.Append('\u001F');
            Builder.Append(Item.DisplayText);
            Builder.Append('\u001F');
            Builder.Append(Item.Documentation);
        }

        return Builder.ToString();
    }

    private static Point GetCaretClientPoint(TextBoxBase Target, int CharIndex)
    {
        int Index = Math.Max(0, Math.Min(CharIndex, Target.TextLength));
        if (Index <= 0)
        {
            return new Point(2, 2);
        }

        return Target.GetPositionFromCharIndex(Index);
    }

    private sealed class AssistWindow : Form
    {
        public AssistWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ControlBox = false;
            ShowIcon = false;
            Padding = new Padding(1);
            BackColor = SystemColors.ControlDark;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams Value = base.CreateParams;
                Value.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return Value;
            }
        }

        protected override void WndProc(ref Message Message)
        {
            if (Message.Msg == WM_MOUSEACTIVATE)
            {
                Message.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }

            base.WndProc(ref Message);
        }
    }
}
