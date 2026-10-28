using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Layer tabs and element buttons. Choosing a type asks for a label, then raises an insert.
/// </summary>
internal sealed partial class ElementPalette : UserControl
{
    private const int MAXIMUM_GLYPH_ROWS = 2;

    private static readonly HashSet<string> STOP_WORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "the",
        "of",
        "and",
        "for",
        "to",
        "or"
    };

    private readonly List<ElementGlyphButton> _Buttons = [];

    private readonly Dictionary<EnterpriseLayer, TabPage> _Pages = [];

    private readonly List<TabPage> _StatementPages = [];

    private readonly List<Button> _StatementButtons = [];

    private string _DiagramKind = "enterprise";

    private string _Snippet = string.Empty;

    private Func<string>? _SourceText;

    private ElementGlyphButton? _LiveButton;

    private EnterpriseElementMetadata? _Chosen;

    private bool _IdEdited;

    private bool _IsUpdating;

    private bool _IsLayingOut;

    /// <summary>
    /// Creates the palette and fills it from the element catalogue.
    /// </summary>
    public ElementPalette()
    {
        InitializeComponent();
        BuildTabs();
    }

    /// <summary>Raised when the user confirms a label and identifier.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public event EventHandler<ElementInsertRequest>? InsertRequested;

    /// <summary>
    /// Supplies the editor text used to keep new identifiers unique.
    /// </summary>
    /// <param name="SourceText">Returns the current script.</param>
    public void BindSource(Func<string> SourceText)
    {
        _SourceText = SourceText;
    }

    /// <summary>Gets whether the current line matches an element type.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasLiveMatch
    {
        get
        {
            return _LiveButton != null;
        }
    }

    /// <summary>
    /// Highlights the element that matches the token at the caret. The other types stay on the palette.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <param name="CaretIndex">Caret index.</param>
    public void UpdateFromEditor(string Source, int CaretIndex)
    {
        if (_IsUpdating || PromptPanel.Visible)
        {
            return;
        }

        string Kind = DiagramKindCatalog.Detect(Source);
        if (!string.Equals(Kind, _DiagramKind, StringComparison.OrdinalIgnoreCase))
        {
            _DiagramKind = Kind;
            ShowPagesForKind();
        }

        if (!string.Equals(_DiagramKind, "enterprise", StringComparison.OrdinalIgnoreCase))
        {
            HighlightStatements(CurrentToken(Source, CaretIndex));
            return;
        }

        string Prefix = DeclarationType(Source, CaretIndex);
        if (Prefix.Length == 0)
        {
            Prefix = CurrentToken(Source, CaretIndex);
        }
        EnterpriseLayer? MatchedLayer = null;
        if (Prefix.Length > 0)
        {
            foreach (EnterpriseLayer Layer in _Pages.Keys)
            {
                if (CountMatches(Layer, Prefix) > 0)
                {
                    MatchedLayer = Layer;
                    break;
                }
            }
        }

        Control? Focused = FindFocused(FindForm());
        _IsUpdating = true;
        try
        {
            if (MatchedLayer.HasValue && LayerTabs.SelectedTab != _Pages[MatchedLayer.Value])
            {
                LayerTabs.SelectedTab = _Pages[MatchedLayer.Value];
            }

            ApplyFilter(Prefix);
        }
        finally
        {
            _IsUpdating = false;
            if (Focused != null && !Focused.IsDisposed && !Focused.Focused)
            {
                Focused.Focus();
            }
        }
    }

    /// <summary>
    /// Opens the label prompt when the current line is an element-type prefix.
    /// </summary>
    /// <param name="LineText">Text of the current line without the line break.</param>
    /// <returns>True when the prompt opened.</returns>
    public bool TryOpenPromptForLine(string LineText)
    {
        if (_LiveButton == null || _LiveButton.Metadata == null)
        {
            return false;
        }

        string Trimmed = (LineText ?? string.Empty).Trim();
        if (Trimmed.Length == 0 || Trimmed.IndexOf(' ') >= 0)
        {
            return false;
        }

        string Name = ElementShapePainter.DisplayName(_LiveButton.Metadata.KebabName);
        if (!_LiveButton.Metadata.KebabName.StartsWith(Trimmed, StringComparison.OrdinalIgnoreCase)
            && !Name.StartsWith(Trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        OpenPrompt(_LiveButton.Metadata);
        return true;
    }

    /// <summary>Raised when the label prompt closes, so editing can return to the source.</summary>
    public event EventHandler? PromptClosed;

    /// <summary>Closes the label prompt.</summary>
    public void ClosePrompt()
    {
        _Chosen = null;
        _Snippet = string.Empty;
        _IdEdited = false;
        PromptLabelBox.Text = string.Empty;
        PromptIdBox.Text = string.Empty;
        PromptErrorLabel.Visible = false;
        PromptErrorLabel.Text = string.Empty;
        PromptPanel.Visible = false;
        ApplyToolbarHeight();
        PromptGlyph.Invalidate();
        PromptClosed?.Invoke(this, EventArgs.Empty);
    }

    private void BuildTabs()
    {
        RegisterLayer(EnterpriseLayer.Motivation, MotivationPage, MotivationFlow);
        RegisterLayer(EnterpriseLayer.Strategy, StrategyPage, StrategyFlow);
        RegisterLayer(EnterpriseLayer.Business, BusinessPage, BusinessFlow);
        RegisterLayer(EnterpriseLayer.Application, ApplicationPage, ApplicationFlow);
        RegisterLayer(EnterpriseLayer.Technology, TechnologyPage, TechnologyFlow);
        RegisterLayer(EnterpriseLayer.Implementation, ImplementationPage, ImplementationFlow);
        RegisterLayer(EnterpriseLayer.Composite, CompositePage, CompositeFlow);
        RegisterLayer(EnterpriseLayer.Junction, JunctionPage, JunctionFlow);

        foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
        {
            ElementGlyphButton Button = new ElementGlyphButton
            {
                Metadata = Metadata
            };
            Button.Click += GlyphButton_Click;
            _Buttons.Add(Button);
            _Pages[Metadata.Layer].Controls[0].Controls.Add(Button);
        }

    }

    private void RegisterLayer(EnterpriseLayer Layer, TabPage Page, FlowLayoutPanel Flow)
    {
        _Pages[Layer] = Page;
    }

    private void ApplyFilter(string Prefix)
    {
        EnterpriseLayer Selected = SelectedLayer();
        ElementGlyphButton? Live = null;
        foreach (ElementGlyphButton Button in _Buttons)
        {
            if (Button.Metadata == null)
            {
                Button.IsLive = false;
                continue;
            }

            Button.Visible = true;
            if (Button.Metadata.Layer != Selected || Prefix.Length == 0)
            {
                Button.IsLive = false;
                continue;
            }

            bool Exact = string.Equals(Button.Metadata.KebabName, Prefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ElementShapePainter.DisplayName(Button.Metadata.KebabName), Prefix, StringComparison.OrdinalIgnoreCase);
            if (Exact)
            {
                Live = Button;
            }
        }

        if (Live == null && Prefix.Length > 0)
        {
            foreach (ElementGlyphButton Button in _Buttons)
            {
                if (Button.Metadata != null && Button.Metadata.Layer == Selected && Matches(Button.Metadata, Prefix))
                {
                    Live = Button;
                    break;
                }
            }
        }

        foreach (ElementGlyphButton Button in _Buttons)
        {
            Button.IsLive = ReferenceEquals(Button, Live);
        }

        _LiveButton = Live;
        LayoutGlyphs();
    }

    private int CountMatches(EnterpriseLayer Layer, string Prefix)
    {
        int Count = 0;
        foreach (ElementGlyphButton Button in _Buttons)
        {
            if (Button.Metadata != null && Button.Metadata.Layer == Layer && Matches(Button.Metadata, Prefix))
            {
                Count++;
            }
        }

        return Count;
    }

    private static bool Matches(EnterpriseElementMetadata Metadata, string Prefix)
    {
        if (Prefix.Length == 0)
        {
            return true;
        }

        return Metadata.KebabName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || ElementShapePainter.DisplayName(Metadata.KebabName).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    private EnterpriseLayer SelectedLayer()
    {
        foreach (KeyValuePair<EnterpriseLayer, TabPage> Pair in _Pages)
        {
            if (ReferenceEquals(Pair.Value, LayerTabs.SelectedTab))
            {
                return Pair.Key;
            }
        }

        return EnterpriseLayer.Application;
    }

    private static Control? FindFocused(Control? Root)
    {
        if (Root == null)
        {
            return null;
        }

        if (Root.Focused)
        {
            return Root;
        }

        foreach (Control Child in Root.Controls)
        {
            Control? Found = FindFocused(Child);
            if (Found != null)
            {
                return Found;
            }
        }

        return null;
    }

    private static string DeclarationType(string Source, int CaretIndex)
    {
        if (string.IsNullOrEmpty(Source))
        {
            return string.Empty;
        }

        if (CaretIndex < 0)
        {
            CaretIndex = 0;
        }

        if (CaretIndex > Source.Length)
        {
            CaretIndex = Source.Length;
        }

        int LineStart = CaretIndex;
        while (LineStart > 0 && Source[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        int LineEnd = CaretIndex;
        while (LineEnd < Source.Length && Source[LineEnd] != '\n' && Source[LineEnd] != '\r')
        {
            LineEnd++;
        }

        string Line = Source.Substring(LineStart, LineEnd - LineStart).Trim();
        int Space = Line.IndexOf(' ');
        if (Space <= 0)
        {
            return string.Empty;
        }

        string Token = Line.Substring(0, Space);
        if (!EnterpriseElementCatalog.TryResolveElementType(Token, out EnterpriseElementType ElementType))
        {
            return string.Empty;
        }

        return EnterpriseElementCatalog.GetKebabName(ElementType);
    }

    private static string CurrentToken(string Source, int CaretIndex)
    {
        if (string.IsNullOrEmpty(Source))
        {
            return string.Empty;
        }

        if (CaretIndex < 0)
        {
            CaretIndex = 0;
        }

        if (CaretIndex > Source.Length)
        {
            CaretIndex = Source.Length;
        }

        int LineStart = CaretIndex;
        while (LineStart > 0 && Source[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        int TokenStart = LineStart;
        while (TokenStart < CaretIndex && (Source[TokenStart] == ' ' || Source[TokenStart] == '\t'))
        {
            TokenStart++;
        }

        int TokenEnd = TokenStart;
        while (TokenEnd < CaretIndex && Source[TokenEnd] != ' ' && Source[TokenEnd] != '\t')
        {
            TokenEnd++;
        }

        if (TokenEnd < CaretIndex)
        {
            return string.Empty;
        }

        return Source.Substring(TokenStart, TokenEnd - TokenStart);
    }

    private void OpenPrompt(EnterpriseElementMetadata Metadata)
    {
        _Chosen = Metadata;
        _Snippet = string.Empty;
        _IdEdited = false;
        PromptTypeLabel.Text = ElementShapePainter.DisplayName(Metadata.KebabName);
        PromptLabelBox.Text = string.Empty;
        PromptIdBox.Text = string.Empty;
        PromptErrorLabel.Visible = false;
        PromptErrorLabel.Text = string.Empty;
        PromptPanel.Visible = true;
        ApplyToolbarHeight();
        PromptGlyph.Invalidate();
        PromptLabelBox.Focus();
    }

    private void GlyphButton_Click(object? Sender, EventArgs E)
    {
        if (Sender is ElementGlyphButton Button && Button.Metadata != null)
        {
            OpenPrompt(Button.Metadata);
        }
    }

    private void LayerTabs_SelectedIndexChanged(object? Sender, EventArgs E)
    {
        if (_IsUpdating)
        {
            return;
        }

        foreach (ElementGlyphButton Button in _Buttons)
        {
            Button.IsLive = false;
            Button.Visible = Button.Metadata != null && Button.Metadata.Layer == SelectedLayer();
        }

        _LiveButton = null;
        LayoutGlyphs();
    }

    private void Flow_Resize(object? Sender, EventArgs E)
    {
        LayoutGlyphs();
    }

    /// <summary>
    /// Widens the visible buttons, using a second row when that lets the type names fit.
    /// </summary>
    private void LayoutGlyphs()
    {
        if (_IsLayingOut || LayerTabs.SelectedTab == null || LayerTabs.SelectedTab.Controls.Count == 0)
        {
            return;
        }

        FlowLayoutPanel? Flow = LayerTabs.SelectedTab.Controls[0] as FlowLayoutPanel;
        if (Flow == null || Flow.Width < 80)
        {
            return;
        }

        List<ElementGlyphButton> VisibleButtons = [];
        foreach (Control Child in Flow.Controls)
        {
            ElementGlyphButton? Button = Child as ElementGlyphButton;
            if (Button != null && Button.Visible)
            {
                VisibleButtons.Add(Button);
            }
        }

        if (VisibleButtons.Count == 0)
        {
            ApplyToolbarHeight();
            return;
        }

        _IsLayingOut = true;
        try
        {
            for (int Index = 0; Index < VisibleButtons.Count; Index++)
            {
                VisibleButtons[Index].FitToFont();
            }

            int Available = Flow.Width - Flow.Padding.Horizontal;
        int[] Preferred = new int[VisibleButtons.Count];
        int[] Scaled = new int[VisibleButtons.Count];
        for (int Index = 0; Index < VisibleButtons.Count; Index++)
        {
            int Minimum = ElementGlyphButton.MINIMUM_WIDTH;
            int Wanted = VisibleButtons[Index].PreferredWidth();
            int RowLimit = Available - VisibleButtons[Index].Margin.Horizontal;
            if (RowLimit < Minimum)
            {
                RowLimit = Minimum;
            }

            if (Wanted > RowLimit)
            {
                Wanted = RowLimit;
            }

            if (Wanted < Minimum)
            {
                Wanted = Minimum;
            }

            Preferred[Index] = Wanted;
        }

        int[] Chosen = Preferred;
        int Rows = CountRows(VisibleButtons, Preferred, Available);
        if (Rows > MAXIMUM_GLYPH_ROWS)
        {
            double Low = 0;
            double High = 1;
            for (int Step = 0; Step < 14; Step++)
            {
                double Mid = (Low + High) / 2.0;
                FillScaledWidths(Preferred, Scaled, Mid);
                if (CountRows(VisibleButtons, Scaled, Available) <= MAXIMUM_GLYPH_ROWS)
                {
                    Low = Mid;
                }
                else
                {
                    High = Mid;
                }
            }

            FillScaledWidths(Preferred, Scaled, Low);
            if (CountRows(VisibleButtons, Scaled, Available) > MAXIMUM_GLYPH_ROWS)
            {
                for (int Index = 0; Index < Scaled.Length; Index++)
                {
                    Scaled[Index] = ElementGlyphButton.MINIMUM_WIDTH;
                }
            }

            Chosen = Scaled;
        }

        for (int Index = 0; Index < VisibleButtons.Count; Index++)
        {
            if (VisibleButtons[Index].Width != Chosen[Index])
            {
                VisibleButtons[Index].Width = Chosen[Index];
            }

            VisibleButtons[Index].FitToFont();
        }

        ApplyToolbarHeight();
        }
        finally
        {
            _IsLayingOut = false;
        }
    }

    private static void FillScaledWidths(int[] Preferred, int[] Widths, double Scale)
    {
        for (int Index = 0; Index < Preferred.Length; Index++)
        {
            int Extra = Preferred[Index] - ElementGlyphButton.MINIMUM_WIDTH;
            int Width = ElementGlyphButton.MINIMUM_WIDTH + (int)Math.Round(Extra * Scale);
            if (Width > Preferred[Index])
            {
                Width = Preferred[Index];
            }

            if (Width < ElementGlyphButton.MINIMUM_WIDTH)
            {
                Width = ElementGlyphButton.MINIMUM_WIDTH;
            }

            Widths[Index] = Width;
        }
    }

    private static int CountRows(List<ElementGlyphButton> Buttons, int[] Widths, int Available)
    {
        int Rows = 1;
        int Used = 0;
        for (int Index = 0; Index < Buttons.Count; Index++)
        {
            int Span = Widths[Index] + Buttons[Index].Margin.Horizontal;
            if (Used > 0 && Used + Span > Available)
            {
                Rows++;
                Used = 0;
            }

            Used += Span;
        }

        return Rows;
    }

    private List<ElementGlyphButton> ButtonsOnSelectedLayer()
    {
        List<ElementGlyphButton> Visible = [];
        if (LayerTabs.SelectedTab == null || LayerTabs.SelectedTab.Controls.Count == 0)
        {
            return Visible;
        }

        if (LayerTabs.SelectedTab.Controls[0] is not FlowLayoutPanel Flow)
        {
            return Visible;
        }

        foreach (Control Child in Flow.Controls)
        {
            if (Child is ElementGlyphButton Button && Button.Visible)
            {
                Visible.Add(Button);
            }
        }

        return Visible;
    }

    private void ApplyToolbarHeight()
    {
        if (PromptPanel.Visible)
        {
            LayerTabs.Visible = false;
            PromptPanel.PerformLayout();
            int PromptHeight = Math.Max(PromptPanel.Height, PromptPanel.PreferredSize.Height);
            if (Height != PromptHeight)
            {
                Height = PromptHeight;
            }

            return;
        }

        LayerTabs.Visible = true;
        if (LayerTabs.SelectedTab == null || LayerTabs.SelectedTab.Controls.Count == 0)
        {
            return;
        }

        if (LayerTabs.SelectedTab.Controls[0] is not FlowLayoutPanel Flow)
        {
            if (Height < 72)
            {
                Height = 72;
            }

            return;
        }

        Flow.PerformLayout();
        int ContentBottom = 0;
        foreach (Control Child in Flow.Controls)
        {
            if (!Child.Visible)
            {
                continue;
            }

            int Bottom = Child.Bottom + Child.Margin.Bottom;
            if (Bottom > ContentBottom)
            {
                ContentBottom = Bottom;
            }
        }

        if (ContentBottom <= 0)
        {
            return;
        }

        ContentBottom += Flow.Padding.Bottom;
        int Chrome = 28;
        if (LayerTabs.IsHandleCreated && LayerTabs.Height > LayerTabs.DisplayRectangle.Height)
        {
            Chrome = LayerTabs.Height - LayerTabs.DisplayRectangle.Height;
        }

        int Next = Chrome + ContentBottom;
        if (Height != Next)
        {
            Height = Next;
        }
    }

    private void PromptLabelBox_TextChanged(object? Sender, EventArgs E)
    {
        PromptErrorLabel.Visible = false;
        if (!_IdEdited)
        {
            PromptIdBox.Text = SuggestIdentifier(PromptLabelBox.Text, CollectUsedIdentifiers());
        }
    }

    private void PromptIdBox_TextChanged(object? Sender, EventArgs E)
    {
        if (PromptIdBox.Focused)
        {
            _IdEdited = true;
        }

        PromptErrorLabel.Visible = false;
    }

    private void InsertLineButton_Click(object? Sender, EventArgs E)
    {
        if (_Chosen == null && _Snippet.Length == 0)
        {
            return;
        }

        string Label = PromptLabelBox.Text.Trim();
        string Identifier = PromptIdBox.Text.Trim();
        if (Label.Length == 0)
        {
            ShowError("Enter a label.");
            PromptLabelBox.Focus();
            return;
        }

        if (Identifier.Length == 0 || !char.IsLetter(Identifier[0]))
        {
            ShowError("Use a short identifier that starts with a letter.");
            PromptIdBox.Focus();
            return;
        }

        for (int Index = 1; Index < Identifier.Length; Index++)
        {
            if (!char.IsLetterOrDigit(Identifier[Index]))
            {
                ShowError("Use a short identifier that starts with a letter.");
                PromptIdBox.Focus();
                return;
            }
        }

        if (CollectUsedIdentifiers().Contains(Identifier))
        {
            ShowError("That identifier is already used.");
            PromptIdBox.Focus();
            return;
        }

        if (_Snippet.Length > 0)
        {
            string SafeLabel = Label.Replace("\"", string.Empty, StringComparison.Ordinal);
            string Line = _Snippet.Replace("{id}", Identifier, StringComparison.Ordinal).Replace("{label}", SafeLabel, StringComparison.Ordinal);
            ClosePrompt();
            InsertRequested?.Invoke(this, new ElementInsertRequest(Line));
            return;
        }

        EnterpriseElementMetadata Chosen = _Chosen!;
        ClosePrompt();
        InsertRequested?.Invoke(this, new ElementInsertRequest(Chosen.KebabName, Identifier, Label));
    }

    private void CancelPromptButton_Click(object? Sender, EventArgs E)
    {
        ClosePrompt();
    }

    private void PromptGlyph_Paint(object? Sender, PaintEventArgs E)
    {
        if (_Chosen != null)
        {
            ElementShapePainter.Draw(E.Graphics, new Rectangle(0, 0, PromptGlyph.Width - 1, PromptGlyph.Height - 1), _Chosen);
        }
    }

    private void ShowError(string Message)
    {
        PromptErrorLabel.Text = Message;
        PromptErrorLabel.Visible = true;
        PromptPanel.PerformLayout();
        ApplyToolbarHeight();
    }

    private HashSet<string> CollectUsedIdentifiers()
    {
        string Source = _SourceText == null ? string.Empty : _SourceText();
        return Parser.CollectUsedIdentifiers(Source);
    }

    private static string SuggestIdentifier(string Label, HashSet<string> Used)
    {
        List<string> Words = [];
        string Current = string.Empty;
        foreach (char Character in Label.Trim())
        {
            if (char.IsLetterOrDigit(Character))
            {
                Current += Character;
            }
            else if (Current.Length > 0)
            {
                if (!STOP_WORDS.Contains(Current))
                {
                    Words.Add(Current);
                }

                Current = string.Empty;
            }
        }

        if (Current.Length > 0 && !STOP_WORDS.Contains(Current))
        {
            Words.Add(Current);
        }

        if (Words.Count == 0)
        {
            return string.Empty;
        }

        List<string> Candidates = [];
        if (Words.Count == 1)
        {
            string Word = Words[0].ToLower(CultureInfo.InvariantCulture);
            Candidates.Add(Word.Substring(0, Math.Min(2, Word.Length)));
            if (Word.Length >= 3)
            {
                Candidates.Add(Word.Substring(0, 3));
            }
        }
        else
        {
            string Initials = string.Empty;
            foreach (string Word in Words)
            {
                Initials += char.ToLower(Word[0], CultureInfo.InvariantCulture);
            }

            Candidates.Add(Initials.Substring(0, Math.Min(2, Initials.Length)));
            if (Initials.Length >= 3)
            {
                Candidates.Add(Initials.Substring(0, 3));
            }
        }

        foreach (string Candidate in Candidates)
        {
            if (!Used.Contains(Candidate))
            {
                return Candidate;
            }
        }

        string Base = Candidates[Candidates.Count - 1];
        int Suffix = 2;
        while (Used.Contains(Base + Suffix.ToString(CultureInfo.InvariantCulture)))
        {
            Suffix++;
        }

        return Base + Suffix.ToString(CultureInfo.InvariantCulture);
    }

    private void ShowPagesForKind()
    {
        bool PreviousUpdating = _IsUpdating;
        _IsUpdating = true;
        try
        {
            while (LayerTabs.TabPages.Count > 0)
            {
                LayerTabs.TabPages.RemoveAt(0);
            }

            DisposeStatementPages();
            if (string.Equals(_DiagramKind, "enterprise", StringComparison.OrdinalIgnoreCase))
            {
                foreach (EnterpriseLayer Layer in Enum.GetValues<EnterpriseLayer>())
                {
                    LayerTabs.TabPages.Add(_Pages[Layer]);
                }

                if (LayerTabs.TabPages.Count > 3)
                {
                    LayerTabs.SelectedIndex = 3;
                }

                return;
            }

            BuildStatementPages();
            for (int Index = 0; Index < _StatementPages.Count; Index++)
            {
                LayerTabs.TabPages.Add(_StatementPages[Index]);
            }
        }
        finally
        {
            _IsUpdating = PreviousUpdating;
        }

        LayoutGlyphs();
    }

    private void DisposeStatementPages()
    {
        for (int Index = 0; Index < _StatementPages.Count; Index++)
        {
            _StatementPages[Index].Dispose();
        }

        _StatementPages.Clear();
        _StatementButtons.Clear();
    }

    private void BuildStatementPages()
    {
        DiagramKindDefinition? Kind = DiagramKindCatalog.Find(_DiagramKind);
        if (Kind == null || Kind.Toolbar.Length == 0)
        {
            TabPage Missing = new TabPage(DiagramKindCatalog.TitleFor(_DiagramKind));
            Label Note = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "This diagram type has no toolbar yet."
            };
            Missing.Controls.Add(Note);
            _StatementPages.Add(Missing);
            return;
        }

        Dictionary<string, FlowLayoutPanel> Flows = new Dictionary<string, FlowLayoutPanel>(StringComparer.Ordinal);
        for (int Index = 0; Index < Kind.Toolbar.Length; Index++)
        {
            DiagramToolbarItem Item = Kind.Toolbar[Index];
            if (!Flows.TryGetValue(Item.Tab, out FlowLayoutPanel? Flow))
            {
                TabPage Page = new TabPage(Item.Tab)
                {
                    Padding = new Padding(0)
                };
                Flow = new FlowLayoutPanel
                {
                    AutoScroll = false,
                    Dock = DockStyle.Fill,
                    WrapContents = true,
                    Padding = new Padding(4, 2, 4, 2)
                };
                Flow.Resize += Flow_Resize;
                Page.Controls.Add(Flow);
                _StatementPages.Add(Page);
                Flows[Item.Tab] = Flow;
            }

            Button Choice = new Button
            {
                Name = "Tool_" + Item.Caption.Replace(" ", string.Empty, StringComparison.Ordinal),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(3),
                Text = Item.Caption,
                Tag = Item,
                UseVisualStyleBackColor = false
            };
            ToolTip Tip = new ToolTip();
            Tip.SetToolTip(Choice, Item.Tip);
            Choice.Click += StatementButton_Click;
            Flow.Controls.Add(Choice);
            _StatementButtons.Add(Choice);
        }
    }

    private void StatementButton_Click(object? Sender, EventArgs E)
    {
        Button? Choice = Sender as Button;
        DiagramToolbarItem? Item = Choice == null ? null : Choice.Tag as DiagramToolbarItem;
        if (Item == null)
        {
            return;
        }

        if (Item.AsksForName)
        {
            OpenSnippetPrompt(Item);
            return;
        }

        InsertRequested?.Invoke(this, new ElementInsertRequest(Item.Snippet));
    }

    private void OpenSnippetPrompt(DiagramToolbarItem Item)
    {
        _Chosen = null;
        _Snippet = Item.Snippet;
        _IdEdited = false;
        PromptTypeLabel.Text = Item.Caption;
        PromptLabelBox.Text = string.Empty;
        PromptIdBox.Text = string.Empty;
        PromptErrorLabel.Visible = false;
        PromptErrorLabel.Text = string.Empty;
        PromptPanel.Visible = true;
        ApplyToolbarHeight();
        PromptGlyph.Invalidate();
        PromptLabelBox.Focus();
    }

    private void HighlightStatements(string Prefix)
    {
        Button? Live = null;
        int BestLength = -1;
        for (int Index = 0; Index < _StatementButtons.Count; Index++)
        {
            DiagramToolbarItem? Item = _StatementButtons[Index].Tag as DiagramToolbarItem;
            if (Item == null || Prefix.Length == 0 || !Item.Caption.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Item.Caption.Length > BestLength)
            {
                BestLength = Item.Caption.Length;
                Live = _StatementButtons[Index];
            }
        }

        for (int Index = 0; Index < _StatementButtons.Count; Index++)
        {
            bool Selected = ReferenceEquals(_StatementButtons[Index], Live);
            _StatementButtons[Index].BackColor = Selected ? Color.FromArgb(255, 230, 150) : SystemColors.Control;
        }
    }

}
