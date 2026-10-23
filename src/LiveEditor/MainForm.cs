using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveEditor.Archi;
using LiveEditor.Completion;
using LiveEditor.Editor;
using MDConverter;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Provides a live Mermaid editor with preview, completion, diagnostics, and Archi handoff.
/// </summary>
public partial class MainForm : Form
{
    private const double MINIMUM_PREVIEW_ZOOM = 0.10;
    private const double MAXIMUM_PREVIEW_ZOOM = 8.00;
    private const double PREVIEW_ZOOM_STEP = 1.20;
    private const string DEFAULT_SOURCE =
        "enterprise\r\n"
        + "  title LiveEditor preview\r\n"
        + "  application-component webShop \"Web Shop\"\r\n"
        + "  application-service orderApi \"Order API\"\r\n"
        + "  data-object order \"Order\"\r\n"
        + "  webShop -serving-> orderApi\r\n"
        + "  orderApi -access(write)-> order\r\n";

    private const string COMPLETION_DEMO_SOURCE =
        "enterprise\r\n"
        + "  title LiveEditor relationship suggestions\r\n"
        + "  application-component webShop \"Web Shop\"\r\n"
        + "  application-service orderApi \"Order API\"\r\n"
        + "  data-object order \"Order\"\r\n"
        + "  webShop -";

    private readonly EnterpriseCompletionEngine _CompletionEngine = new EnterpriseCompletionEngine();
    private readonly CompletionPopup _CompletionPopup = new CompletionPopup();
    private readonly bool _CapturePreviewOnShown;
    private readonly List<EnterpriseDiagnostic> _Diagnostics = new List<EnterpriseDiagnostic>();
    private readonly List<int> _SelectedOccurrences = new List<int>();
    private SessionSettings _Settings;
    private GhostTextOverlay? _GhostText;
    private CompletionContext? _GhostContext;
    private FileSystemWatcher? _Watcher;
    private CancellationTokenSource? _RenderCancellation;
    private Image? _LastGoodPreview;
    private byte[]? _LastPngBytes;
    private string? _LastSvg;
    private string? _CurrentSourcePath;
    private string? _CurrentCoArchiFolder;
    private string? _CoArchiDiskSnapshot;
    private bool _IsDirty;
    private bool _SuppressCompletion;
    private bool _SuppressWatcher;
    private int _RenderGeneration;
    private double _PreviewZoom = 1.0;
    private DateTime? _BrokenSince;

    /// <summary>
    /// Creates the live editor window.
    /// </summary>
    public MainForm()
        : this(CapturePreviewOnShown: false)
    {
    }

    /// <summary>
    /// Creates the live editor window.
    /// </summary>
    /// <param name="CapturePreviewOnShown">When true, captures the completion preview and closes the window.</param>
    public MainForm(bool CapturePreviewOnShown)
    {
        _CapturePreviewOnShown = CapturePreviewOnShown;
        _Settings = LiveEditorFeatureGate.T_PersistentSession ? SessionSettings.Load() : new SessionSettings();
        InitializeComponent();
        _GhostText = LiveEditorFeatureGate.F_GhostText ? new GhostTextOverlay(SourceEditor) : null;
        WireEvents();
        ApplySettings();
        BuildSamplesMenu();
        BuildRecentFilesMenu();
        RestoreSessionContent();
        ApplyTheme();
        UpdateFeatureAvailability();
        UpdateTitle();
        QueueHighlight();
        QueueRender();
        Load += MainForm_Load;
        Shown += MainForm_Shown;
    }

    private void MainForm_Load(object? Sender, EventArgs E)
    {
        BeginInvoke(EnsureEditorSplitLayout);
    }

    private void EnsureEditorSplitLayout()
    {
        int Span = EditorSplit.Orientation == Orientation.Vertical ? EditorSplit.Width : EditorSplit.Height;
        if (Span < 100)
        {
            return;
        }

        int MinimumPane = Math.Min(240, Math.Max(120, Span / 4));
        EditorSplit.Panel1MinSize = MinimumPane;
        EditorSplit.Panel2MinSize = MinimumPane;
        int Preferred = (int)Math.Round(Span * 0.45);
        int Maximum = Math.Max(MinimumPane, Span - MinimumPane - EditorSplit.SplitterWidth);
        EditorSplit.SplitterDistance = Math.Max(MinimumPane, Math.Min(Maximum, Preferred));
    }

    private async void MainForm_Shown(object? Sender, EventArgs E)
    {
        EnsureEditorSplitLayout();
        SourceEditor.Focus();
        SourceEditor.SelectionStart = Math.Min(_Settings.CaretIndex, SourceEditor.TextLength);
        RefreshCompletion(Force: true);
        if (!_CapturePreviewOnShown)
        {
            return;
        }

        _SuppressCompletion = true;
        try
        {
            SourceEditor.Text = COMPLETION_DEMO_SOURCE;
            SourceEditor.SelectionStart = SourceEditor.TextLength;
        }
        finally
        {
            _SuppressCompletion = false;
        }

        await Task.Delay(500).ConfigureAwait(true);
        RefreshCompletion(Force: true);
        await Task.Delay(300).ConfigureAwait(true);
        string OutputPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory, "liveeditor-completion-preview.png");
        try
        {
            WriteCompletionPreviewBitmap(OutputPath);
            Console.WriteLine($"Wrote preview: {OutputPath}");
        }
        catch (Exception Ex)
        {
            Console.Error.WriteLine($"Preview capture failed: {Ex.Message}");
        }

        Close();
    }

    private void WriteCompletionPreviewBitmap(string OutputPath)
    {
        CompletionContext Context = _CompletionEngine.Build(SourceEditor.Text, SourceEditor.SelectionStart);
        using Bitmap FormBitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(FormBitmap, new Rectangle(Point.Empty, ClientSize));
        int PopupWidth = 440;
        int RowHeight = 24;
        int VisibleRows = Math.Min(8, Math.Max(1, Context.Items.Count));
        int PopupHeight = 12 + (VisibleRows * RowHeight);
        Point Caret = SourceEditor.GetPositionFromCharIndex(Math.Max(0, SourceEditor.SelectionStart - 1));
        Point EditorOrigin = SourceEditor.PointToScreen(Point.Empty);
        Point FormOrigin = PointToScreen(Point.Empty);
        int PopupX = EditorOrigin.X - FormOrigin.X + Caret.X + 8;
        int PopupY = EditorOrigin.Y - FormOrigin.Y + Caret.Y + SourceEditor.Font.Height + 4;
        if (PopupX + PopupWidth > FormBitmap.Width - 8)
        {
            PopupX = FormBitmap.Width - PopupWidth - 8;
        }

        if (PopupY + PopupHeight > FormBitmap.Height - 8)
        {
            PopupY = Math.Max(8, FormBitmap.Height - PopupHeight - 8);
        }

        int CanvasWidth = Math.Max(FormBitmap.Width, PopupX + PopupWidth + 16);
        int CanvasHeight = Math.Max(FormBitmap.Height, PopupY + PopupHeight + 16);
        using Bitmap Canvas = new Bitmap(CanvasWidth, CanvasHeight);
        using Graphics Graphics = Graphics.FromImage(Canvas);
        Graphics.Clear(Color.WhiteSmoke);
        Graphics.DrawImage(FormBitmap, Point.Empty);
        Rectangle PopupBounds = new Rectangle(PopupX, PopupY, PopupWidth, PopupHeight);
        using SolidBrush Background = new SolidBrush(Color.White);
        using SolidBrush Selected = new SolidBrush(SystemColors.Highlight);
        using SolidBrush Primary = new SolidBrush(SystemColors.WindowText);
        using SolidBrush Secondary = new SolidBrush(SystemColors.GrayText);
        using SolidBrush SelectedText = new SolidBrush(SystemColors.HighlightText);
        using Pen Border = new Pen(Color.FromArgb(180, 180, 180));
        Graphics.FillRectangle(Background, PopupBounds);
        Graphics.DrawRectangle(Border, PopupBounds);
        using Font PopupFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        for (int Index = 0; Index < VisibleRows && Index < Context.Items.Count; Index++)
        {
            CompletionItem Item = Context.Items[Index];
            Rectangle Row = new Rectangle(PopupBounds.X + 1, PopupBounds.Y + 6 + (Index * RowHeight), PopupBounds.Width - 2, RowHeight);
            if (Index == 0)
            {
                Graphics.FillRectangle(Selected, Row);
            }

            Brush TextBrush = Index == 0 ? SelectedText : Primary;
            Brush DetailBrush = Index == 0 ? SelectedText : Secondary;
            Graphics.DrawString(Item.DisplayText, PopupFont, TextBrush, Row.X + 8, Row.Y + 2);
            if (!string.IsNullOrWhiteSpace(Item.Detail))
            {
                SizeF Size = Graphics.MeasureString(Item.DisplayText, PopupFont);
                Graphics.DrawString(Item.Detail, PopupFont, DetailBrush, Row.X + 12 + Size.Width, Row.Y + 2);
            }
        }

        Canvas.Save(OutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private void WireEvents()
    {
        SourceEditor.TextChanged += SourceEditor_TextChanged;
        SourceEditor.SelectionChanged += SourceEditor_SelectionChanged;
        SourceEditor.KeyDown += SourceEditor_KeyDown;
        SourceEditor.PreviewKeyDown += SourceEditor_PreviewKeyDown;
        SourceEditor.MouseDown += SourceEditor_MouseDown;
        SourceEditor.LostFocus += SourceEditor_LostFocus;
        PreviewCanvas.MouseWheel += PreviewCanvas_MouseWheel;
        PreviewCanvas.Resize += PreviewCanvas_Resize;
        DiagnosticsList.DoubleClick += DiagnosticsList_DoubleClick;
        RenderTimer.Tick += RenderTimer_Tick;
        HighlightTimer.Tick += HighlightTimer_Tick;
        OpenMenuItem.Click += OpenMenuItem_Click;
        SaveMenuItem.Click += SaveMenuItem_Click;
        SaveAsMenuItem.Click += SaveAsMenuItem_Click;
        ExitMenuItem.Click += ExitMenuItem_Click;
        CopySourceMenuItem.Click += CopySourceMenuItem_Click;
        RenameSymbolMenuItem.Click += RenameSymbolMenuItem_Click;
        GoToDefinitionMenuItem.Click += GoToDefinitionMenuItem_Click;
        FindReferencesMenuItem.Click += FindReferencesMenuItem_Click;
        CommandPaletteMenuItem.Click += CommandPaletteMenuItem_Click;
        SelectNextOccurrenceMenuItem.Click += SelectNextOccurrenceMenuItem_Click;
        InsertArchiSnippetMenuItem.Click += InsertArchiSnippetMenuItem_Click;
        ToggleThemeMenuItem.Click += ToggleThemeMenuItem_Click;
        ToggleSplitOrientationMenuItem.Click += ToggleSplitOrientationMenuItem_Click;
        ZoomInMenuItem.Click += ZoomInMenuItem_Click;
        ZoomOutMenuItem.Click += ZoomOutMenuItem_Click;
        ZoomResetMenuItem.Click += ZoomResetMenuItem_Click;
        FitPreviewMenuItem.Click += FitPreviewMenuItem_Click;
        ToggleDiagnosticsMenuItem.Click += ToggleDiagnosticsMenuItem_Click;
        ToggleMinimapMenuItem.Click += ToggleMinimapMenuItem_Click;
        ToggleLegacyPreviewMenuItem.Click += ToggleLegacyPreviewMenuItem_Click;
        ExportPngMenuItem.Click += ExportPngMenuItem_Click;
        ExportSvgMenuItem.Click += ExportSvgMenuItem_Click;
        ImportExchangeMenuItem.Click += ImportExchangeMenuItem_Click;
        ImportArchimateMenuItem.Click += ImportArchimateMenuItem_Click;
        ExportExchangeMenuItem.Click += ExportExchangeMenuItem_Click;
        ExportArchimateMenuItem.Click += ExportArchimateMenuItem_Click;
        OpenCoArchiFolderMenuItem.Click += OpenCoArchiFolderMenuItem_Click;
        SaveCoArchiFolderMenuItem.Click += SaveCoArchiFolderMenuItem_Click;
        FormClosing += MainForm_FormClosing;
        _CompletionPopup.ItemAccepted += CompletionPopup_ItemAccepted;
    }

    private void UpdateFeatureAvailability()
    {
        CopySourceMenuItem.Enabled = LiveEditorFeatureGate.N_CopySource;
        RenameSymbolMenuItem.Enabled = LiveEditorFeatureGate.I_RenameSymbol;
        GoToDefinitionMenuItem.Enabled = LiveEditorFeatureGate.H_GoToDefinition;
        CommandPaletteMenuItem.Enabled = LiveEditorFeatureGate.Z_CommandPalette;
        SelectNextOccurrenceMenuItem.Enabled = LiveEditorFeatureGate.AA_SelectNextOccurrence;
        InsertArchiSnippetMenuItem.Enabled = LiveEditorFeatureGate.AD_InsertFromArchi;
        ToggleThemeMenuItem.Enabled = LiveEditorFeatureGate.O_ThemeToggle;
        ToggleSplitOrientationMenuItem.Enabled = LiveEditorFeatureGate.P_SplitOrientation;
        ZoomInMenuItem.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        ZoomOutMenuItem.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        ZoomResetMenuItem.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        FitPreviewMenuItem.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        ToggleDiagnosticsMenuItem.Enabled = LiveEditorFeatureGate.X_DiagnosticsPanel;
        ToggleMinimapMenuItem.Enabled = LiveEditorFeatureGate.Y_Minimap;
        ToggleLegacyPreviewMenuItem.Enabled = LiveEditorFeatureGate.S_LegacyPreview;
        ExportPngMenuItem.Enabled = LiveEditorFeatureGate.M_ExportPngSvg;
        ExportSvgMenuItem.Enabled = LiveEditorFeatureGate.M_ExportPngSvg;
        RecentFilesMenuItem.Enabled = LiveEditorFeatureGate.U_RecentFiles;
        SamplesMenu.Enabled = LiveEditorFeatureGate.K_SampleGallery;
        if (LiveEditorFeatureGate.K_SampleGallery && SamplesMenu.DropDownItems.Count == 0)
        {
            BuildSamplesMenu();
        }
    }

    private void ApplySettings()
    {
        _PreviewZoom = ClampZoom(_Settings.PreviewZoom);
        BodySplit.Panel2Collapsed = !LiveEditorFeatureGate.X_DiagnosticsPanel || !_Settings.DiagnosticsVisible;
        MinimapBox.Visible = LiveEditorFeatureGate.Y_Minimap && _Settings.MinimapVisible;
        Orientation DesiredOrientation = _Settings.VerticalSplit ? Orientation.Vertical : Orientation.Horizontal;
        if (EditorSplit.Orientation != DesiredOrientation)
        {
            EditorSplit.Orientation = DesiredOrientation;
        }

        ToggleLegacyPreviewMenuItem.Checked = LiveEditorFeatureGate.S_LegacyPreview && _Settings.LegacyPreview;
        ToggleDiagnosticsMenuItem.Checked = !BodySplit.Panel2Collapsed;
        ToggleMinimapMenuItem.Checked = MinimapBox.Visible;
        ToggleThemeMenuItem.Checked = _Settings.DarkTheme;
    }

    private void RestoreSessionContent()
    {
        if (LiveEditorFeatureGate.T_PersistentSession && !string.IsNullOrWhiteSpace(_Settings.LastFilePath) && File.Exists(_Settings.LastFilePath))
        {
            try
            {
                LoadSourceFile(_Settings.LastFilePath, Announce: false);
                return;
            }
            catch
            {
            }
        }

        SourceEditor.Text = DEFAULT_SOURCE;
        SourceEditor.SelectionStart = SourceEditor.TextLength;
        _IsDirty = false;
    }

    private void SourceEditor_TextChanged(object? Sender, EventArgs E)
    {
        _IsDirty = true;
        _SelectedOccurrences.Clear();
        UpdateTitle();
        UpdateCoArchiDirtyBadge();
        QueueRender();
        QueueHighlight();
        if (!_SuppressCompletion)
        {
            RefreshCompletion();
        }
    }

    private void SourceEditor_SelectionChanged(object? Sender, EventArgs E)
    {
        UpdateSignatureHelp();
        UpdateCaretDiagnosticStatus();
        _CompletionPopup.Reposition();
        _GhostText?.Hide();
    }

    private void SourceEditor_PreviewKeyDown(object? Sender, PreviewKeyDownEventArgs E)
    {
        if ((_CompletionPopup.IsVisible || _GhostContext != null) && (E.KeyCode == Keys.Tab || E.KeyCode == Keys.Enter || E.KeyCode == Keys.Up || E.KeyCode == Keys.Down || E.KeyCode == Keys.Escape))
        {
            E.IsInputKey = true;
        }
    }

    private void SourceEditor_KeyDown(object? Sender, KeyEventArgs E)
    {
        if (E.Control && E.KeyCode == Keys.Space)
        {
            RefreshCompletion(Force: true);
            E.SuppressKeyPress = true;
            E.Handled = true;
            return;
        }

        if (_CompletionPopup.TryHandleKey(E.KeyData))
        {
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

        if (E.KeyCode == Keys.Escape)
        {
            HideCompletion();
        }
    }

    private void SourceEditor_MouseDown(object? Sender, MouseEventArgs E)
    {
        HideCompletion();
    }

    private void SourceEditor_LostFocus(object? Sender, EventArgs E)
    {
        _GhostText?.Hide();
    }

    private void CompletionPopup_ItemAccepted(object? Sender, CompletionItem Item)
    {
        _GhostContext = null;
        _GhostText?.Hide();
        _IsDirty = true;
        UpdateTitle();
        QueueRender();
        QueueHighlight();
        SetStatus($"Inserted {Item.DisplayText}");
        BeginInvoke(RefreshCompletion);
    }

    private void RefreshCompletion()
    {
        RefreshCompletion(Force: false);
    }

    private void RefreshCompletion(bool Force)
    {
        if (IsDisposed || (!SourceEditor.Focused && !Force))
        {
            return;
        }

        CompletionContext Context = _CompletionEngine.Build(SourceEditor.Text, SourceEditor.SelectionStart);
        UpdateSignatureHelp();
        if (Context.TokenKind == CompletionTokenKind.None)
        {
            HideCompletion();
            if (Force && !string.IsNullOrWhiteSpace(Context.StatusMessage))
            {
                SetStatus(Context.StatusMessage);
            }

            return;
        }

        if (Context.Items.Count == 0)
        {
            HideCompletion();
            if (!string.IsNullOrWhiteSpace(Context.StatusMessage))
            {
                SetStatus(TruncateStatus(Context.StatusMessage));
            }

            return;
        }

        string? Ghost = LiveEditorFeatureGate.F_GhostText ? _CompletionEngine.TryGetGhostText(Context) : null;
        if (!string.IsNullOrEmpty(Ghost))
        {
            _CompletionPopup.Hide();
            _GhostContext = Context;
            Point Caret = SourceEditor.GetPositionFromCharIndex(SourceEditor.SelectionStart);
            _GhostText?.Show(new Point(Caret.X, Caret.Y + 1), Ghost);
            SetStatus("Tab to accept suggestion");
            return;
        }

        _GhostContext = null;
        _GhostText?.Hide();
        _CompletionPopup.Show(SourceEditor, Context);
        SetStatus($"{Context.Items.Count.ToString(CultureInfo.InvariantCulture)} suggestion(s) - Tab/Enter to accept, Esc to dismiss");
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
        _SuppressCompletion = true;
        try
        {
            SourceEditor.Select(Start, End - Start);
            SourceEditor.SelectedText = Item.InsertText;
        }
        finally
        {
            _SuppressCompletion = false;
        }

        _GhostContext = null;
        _GhostText?.Hide();
        SetStatus($"Inserted {Item.DisplayText}");
        BeginInvoke(RefreshCompletion);
        return true;
    }

    private void HideCompletion()
    {
        _CompletionPopup.Hide();
        _GhostContext = null;
        _GhostText?.Hide();
    }

    private void UpdateSignatureHelp()
    {
        if (!LiveEditorFeatureGate.G_SignatureHelp)
        {
            SignatureLabel.Text = string.Empty;
            return;
        }

        SignatureLabel.Text = _CompletionEngine.BuildSignatureHelp(SourceEditor.Text, SourceEditor.SelectionStart);
    }

    private void QueueHighlight()
    {
        HighlightTimer.Stop();
        HighlightTimer.Start();
    }

    private void HighlightTimer_Tick(object? Sender, EventArgs E)
    {
        HighlightTimer.Stop();
        if (LiveEditorFeatureGate.A_SyntaxHighlighting)
        {
            MermaidSyntaxHighlighter.Apply(SourceEditor, _Settings.DarkTheme);
        }

        RefreshDiagnostics();
        if (LiveEditorFeatureGate.Y_Minimap && MinimapBox.Visible)
        {
            MinimapBox.Text = SourceEditor.Text;
            MinimapBox.BackColor = SourceEditor.BackColor;
            MinimapBox.ForeColor = SourceEditor.ForeColor;
        }
    }

    private void RefreshDiagnostics()
    {
        _Diagnostics.Clear();
        DiagnosticsList.Items.Clear();
        if (EnterpriseParser.IsEnterpriseSource(SourceEditor.Text))
        {
            EnterpriseParseResult Result = EnterpriseParser.Parse(SourceEditor.Text);
            foreach (EnterpriseDiagnostic Diagnostic in Result.Diagnostics)
            {
                _Diagnostics.Add(Diagnostic);
                DiagnosticsList.Items.Add(new DiagnosticListItem(Diagnostic));
            }
        }

        if (LiveEditorFeatureGate.E_InlineDiagnostics)
        {
            TintDiagnosticLines();
        }

        UpdateCaretDiagnosticStatus();
    }

    private void TintDiagnosticLines()
    {
        int Caret = SourceEditor.SelectionStart;
        int Length = SourceEditor.SelectionLength;
        Color ErrorColour = _Settings.DarkTheme ? Color.FromArgb(90, 55, 55) : Color.MistyRose;
        foreach (EnterpriseDiagnostic Diagnostic in _Diagnostics)
        {
            if (Diagnostic.Severity != MermaidDiagnosticSeverity.Error || Diagnostic.LineNumber <= 0)
            {
                continue;
            }

            int LineIndex = Diagnostic.LineNumber - 1;
            if (LineIndex >= SourceEditor.Lines.Length)
            {
                continue;
            }

            int Start = SourceEditor.GetFirstCharIndexFromLine(LineIndex);
            if (Start < 0)
            {
                continue;
            }

            int LineLength = SourceEditor.Lines[LineIndex].Length;
            SourceEditor.Select(Start, Math.Max(1, Math.Min(LineLength, SourceEditor.TextLength - Start)));
            SourceEditor.SelectionBackColor = ErrorColour;
        }

        SourceEditor.Select(Math.Min(Caret, SourceEditor.TextLength), Math.Min(Length, Math.Max(0, SourceEditor.TextLength - Caret)));
    }

    private void UpdateCaretDiagnosticStatus()
    {
        if (!LiveEditorFeatureGate.E_InlineDiagnostics || _Diagnostics.Count == 0)
        {
            return;
        }

        int LineNumber = SourceEditor.GetLineFromCharIndex(SourceEditor.SelectionStart) + 1;
        foreach (EnterpriseDiagnostic Diagnostic in _Diagnostics)
        {
            if (Diagnostic.LineNumber == LineNumber)
            {
                SetStatus(TruncateStatus(Diagnostic.Message));
                return;
            }
        }
    }

    private void DiagnosticsList_DoubleClick(object? Sender, EventArgs E)
    {
        if (DiagnosticsList.SelectedItem is not DiagnosticListItem Item || Item.Diagnostic.LineNumber <= 0)
        {
            return;
        }

        JumpToLine(Item.Diagnostic.LineNumber);
    }

    private void QueueRender()
    {
        RenderTimer.Stop();
        RenderTimer.Start();
    }

    private void RenderTimer_Tick(object? Sender, EventArgs E)
    {
        RenderTimer.Stop();
        StartRender(SourceEditor.Text);
    }

    private void StartRender(string Source)
    {
        _RenderCancellation?.Cancel();
        _RenderCancellation?.Dispose();
        _RenderCancellation = new CancellationTokenSource();
        CancellationToken Token = _RenderCancellation.Token;
        int Generation = Interlocked.Increment(ref _RenderGeneration);
        if (string.IsNullOrWhiteSpace(Source) || IsCommentOnly(Source))
        {
            SetStatus("Idle");
            return;
        }

        SetStatus("Rendering...");
        bool LegacyPreview = LiveEditorFeatureGate.S_LegacyPreview && _Settings.LegacyPreview;
        Task.Run(() => RenderWorker(Source, Generation, LegacyPreview, Token), Token);
    }

    private void RenderWorker(string Source, int Generation, bool LegacyPreview, CancellationToken Token)
    {
        Stopwatch Timer = Stopwatch.StartNew();
        try
        {
            Token.ThrowIfCancellationRequested();
            string? Svg = null;
            byte[]? PngBytes;
            if (LegacyPreview)
            {
                Svg = MermaidDiagramExport.RenderSvg(Source);
                Token.ThrowIfCancellationRequested();
                PngBytes = MermaidDiagramExport.TryRasterizeSvg(Svg);
            }
            else
            {
                PngBytes = MermaidDiagramExport.TryRenderPng(Source);
            }

            Token.ThrowIfCancellationRequested();
            Timer.Stop();
            if (PngBytes == null || PngBytes.Length == 0)
            {
                BeginInvoke(() => ApplyRenderFailure(Generation, "Incomplete diagram", Timer.ElapsedMilliseconds));
                return;
            }

            BeginInvoke(() => ApplyRenderSuccess(Generation, PngBytes, Svg, Timer.ElapsedMilliseconds, LegacyPreview));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception Ex)
        {
            Timer.Stop();
            BeginInvoke(() => ApplyRenderFailure(Generation, Ex.Message, Timer.ElapsedMilliseconds));
        }
    }

    private void ApplyRenderSuccess(int Generation, byte[] PngBytes, string? Svg, long ElapsedMilliseconds, bool LegacyPreview)
    {
        if (Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        try
        {
            using MemoryStream Stream = new MemoryStream(PngBytes);
            using Image Loaded = Image.FromStream(Stream);
            Image NewImage = new Bitmap(Loaded);
            Image? Previous = PreviewImage.Image;
            PreviewImage.Image = NewImage;
            if (Previous != null && !ReferenceEquals(Previous, _LastGoodPreview))
            {
                Previous.Dispose();
            }

            if (_LastGoodPreview != null && !ReferenceEquals(_LastGoodPreview, NewImage))
            {
                _LastGoodPreview.Dispose();
            }

            _LastGoodPreview = NewImage;
            _LastPngBytes = (byte[])PngBytes.Clone();
            _LastSvg = Svg;
            _BrokenSince = null;
            PreviewBanner.Visible = LegacyPreview;
            PreviewBanner.Height = LegacyPreview ? 28 : 0;
            PreviewBanner.Text = LegacyPreview ? "Legacy Naiad preview" : string.Empty;
            ApplyPreviewZoom();
            SetStatus("OK");
            UpdateRenderTiming(ElapsedMilliseconds);
        }
        catch (Exception Ex)
        {
            ApplyRenderFailure(Generation, Ex.Message, ElapsedMilliseconds);
        }
    }

    private void ApplyRenderFailure(int Generation, string Message, long ElapsedMilliseconds)
    {
        if (Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        _BrokenSince ??= DateTime.Now;
        if (!LiveEditorFeatureGate.Q_KeepLastGoodPreview)
        {
            Image? Previous = PreviewImage.Image;
            PreviewImage.Image = null;
            if (Previous != null && !ReferenceEquals(Previous, _LastGoodPreview))
            {
                Previous.Dispose();
            }
        }

        PreviewBanner.Visible = true;
        PreviewBanner.Height = 32;
        string Time = _BrokenSince.Value.ToString("T", CultureInfo.CurrentCulture);
        PreviewBanner.Text = $"Broken since {Time}: {TruncateStatus(Message)}";
        SetStatus(TruncateStatus(Message));
        UpdateRenderTiming(ElapsedMilliseconds);
    }

    private void UpdateRenderTiming(long ElapsedMilliseconds)
    {
        TimingLabel.Text = LiveEditorFeatureGate.R_RenderTiming ? $"{ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty;
    }

    private void PreviewCanvas_MouseWheel(object? Sender, MouseEventArgs E)
    {
        if (!LiveEditorFeatureGate.L_ZoomPanPreview || (ModifierKeys & Keys.Control) != Keys.Control)
        {
            return;
        }

        SetPreviewZoom(E.Delta > 0 ? _PreviewZoom * PREVIEW_ZOOM_STEP : _PreviewZoom / PREVIEW_ZOOM_STEP);
    }

    private void PreviewCanvas_Resize(object? Sender, EventArgs E)
    {
        if (PreviewImage.Image != null)
        {
            ApplyPreviewZoom();
        }
    }

    private void SetPreviewZoom(double Zoom)
    {
        _PreviewZoom = ClampZoom(Zoom);
        _Settings.PreviewZoom = _PreviewZoom;
        ApplyPreviewZoom();
        SetStatus($"Preview zoom {Math.Round(_PreviewZoom * 100.0).ToString(CultureInfo.InvariantCulture)}%");
    }

    private static double ClampZoom(double Zoom)
    {
        return Math.Max(MINIMUM_PREVIEW_ZOOM, Math.Min(MAXIMUM_PREVIEW_ZOOM, Zoom));
    }

    private void ApplyPreviewZoom()
    {
        if (PreviewImage.Image == null)
        {
            return;
        }

        int Width = Math.Max(1, (int)Math.Round(PreviewImage.Image.Width * _PreviewZoom));
        int Height = Math.Max(1, (int)Math.Round(PreviewImage.Image.Height * _PreviewZoom));
        PreviewImage.Size = new Size(Width, Height);
        PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
    }

    private void ZoomInMenuItem_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.L_ZoomPanPreview)
        {
            SetPreviewZoom(_PreviewZoom * PREVIEW_ZOOM_STEP);
        }
    }

    private void ZoomOutMenuItem_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.L_ZoomPanPreview)
        {
            SetPreviewZoom(_PreviewZoom / PREVIEW_ZOOM_STEP);
        }
    }

    private void ZoomResetMenuItem_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.L_ZoomPanPreview)
        {
            SetPreviewZoom(1.0);
        }
    }

    private void FitPreviewMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.L_ZoomPanPreview || PreviewImage.Image == null)
        {
            return;
        }

        double WidthScale = Math.Max(1, PreviewCanvas.ClientSize.Width - 8) / (double)PreviewImage.Image.Width;
        double HeightScale = Math.Max(1, PreviewCanvas.ClientSize.Height - 8) / (double)PreviewImage.Image.Height;
        SetPreviewZoom(Math.Min(WidthScale, HeightScale));
    }

    private void ExportPngMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.M_ExportPngSvg)
        {
            return;
        }

        byte[]? Bytes = _LastPngBytes;
        if (Bytes == null || Bytes.Length == 0)
        {
            MessageBox.Show(this, "No rendered PNG is available.", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using SaveFileDialog Dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            FileName = "diagram.png",
            Title = "Export PNG"
        };
        if (Dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllBytes(Dialog.FileName, Bytes);
            SetStatus($"Exported {Path.GetFileName(Dialog.FileName)}");
        }
    }

    private void ExportSvgMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.M_ExportPngSvg)
        {
            return;
        }

        try
        {
            string Svg = _LastSvg ?? MermaidDiagramExport.RenderSvg(SourceEditor.Text);
            _LastSvg = Svg;
            using SaveFileDialog Dialog = new SaveFileDialog
            {
                Filter = "SVG image (*.svg)|*.svg",
                FileName = "diagram.svg",
                Title = "Export SVG"
            };
            if (Dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllText(Dialog.FileName, Svg);
                SetStatus($"Exported {Path.GetFileName(Dialog.FileName)}");
            }
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Export SVG failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleThemeMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.O_ThemeToggle)
        {
            return;
        }

        _Settings.DarkTheme = !_Settings.DarkTheme;
        ToggleThemeMenuItem.Checked = _Settings.DarkTheme;
        ApplyTheme();
        QueueHighlight();
    }

    private void ApplyTheme()
    {
        Color Background = _Settings.DarkTheme ? Color.FromArgb(30, 30, 30) : Color.White;
        Color Foreground = _Settings.DarkTheme ? Color.Gainsboro : Color.Black;
        Color Panel = _Settings.DarkTheme ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
        SourceEditor.BackColor = Background;
        SourceEditor.ForeColor = Foreground;
        MinimapBox.BackColor = Background;
        MinimapBox.ForeColor = Foreground;
        DiagnosticsList.BackColor = Background;
        DiagnosticsList.ForeColor = Foreground;
        PreviewCanvas.BackColor = Background;
        PreviewImage.BackColor = Background;
        PreviewHost.BackColor = Panel;
        PreviewBanner.BackColor = _Settings.DarkTheme ? Color.FromArgb(100, 70, 20) : Color.LemonChiffon;
        PreviewBanner.ForeColor = _Settings.DarkTheme ? Color.White : Color.Black;
    }

    private void ToggleSplitOrientationMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.P_SplitOrientation)
        {
            return;
        }

        EditorSplit.Orientation = EditorSplit.Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        _Settings.VerticalSplit = EditorSplit.Orientation == Orientation.Vertical;
        EnsureEditorSplitLayout();
    }

    private void ToggleDiagnosticsMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.X_DiagnosticsPanel)
        {
            return;
        }

        BodySplit.Panel2Collapsed = !BodySplit.Panel2Collapsed;
        _Settings.DiagnosticsVisible = !BodySplit.Panel2Collapsed;
        ToggleDiagnosticsMenuItem.Checked = _Settings.DiagnosticsVisible;
    }

    private void ToggleMinimapMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.Y_Minimap)
        {
            return;
        }

        MinimapBox.Visible = !MinimapBox.Visible;
        _Settings.MinimapVisible = MinimapBox.Visible;
        ToggleMinimapMenuItem.Checked = MinimapBox.Visible;
        if (MinimapBox.Visible)
        {
            MinimapBox.Text = SourceEditor.Text;
        }
    }

    private void ToggleLegacyPreviewMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.S_LegacyPreview)
        {
            return;
        }

        _Settings.LegacyPreview = !_Settings.LegacyPreview;
        ToggleLegacyPreviewMenuItem.Checked = _Settings.LegacyPreview;
        QueueRender();
    }

    private void CopySourceMenuItem_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.N_CopySource && !string.IsNullOrEmpty(SourceEditor.Text))
        {
            Clipboard.SetText(SourceEditor.Text);
            SetStatus("Mermaid source copied");
        }
    }

    private void GoToDefinitionMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.H_GoToDefinition)
        {
            return;
        }

        string? Id = SymbolNavigator.IdAt(SourceEditor.Text, SourceEditor.SelectionStart);
        if (string.IsNullOrEmpty(Id))
        {
            SetStatus("Place the caret on a symbol");
            return;
        }

        int Index = SymbolNavigator.FindDefinition(SourceEditor.Text, Id);
        if (Index < 0)
        {
            SetStatus($"Definition for '{Id}' was not found");
            return;
        }

        JumpToIndex(Index, Id.Length);
    }

    private void RenameSymbolMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.I_RenameSymbol)
        {
            return;
        }

        string? OldId = SymbolNavigator.IdAt(SourceEditor.Text, SourceEditor.SelectionStart);
        if (string.IsNullOrEmpty(OldId))
        {
            SetStatus("Place the caret on a symbol");
            return;
        }

        string NewId = Microsoft.VisualBasic.Interaction.InputBox($"Rename '{OldId}' to:", "Rename symbol", OldId);
        if (string.IsNullOrWhiteSpace(NewId) || string.Equals(NewId, OldId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            int Caret = SourceEditor.SelectionStart;
            SourceEditor.Text = SymbolNavigator.Rename(SourceEditor.Text, OldId, NewId.Trim());
            SourceEditor.SelectionStart = Math.Min(Caret, SourceEditor.TextLength);
            SetStatus($"Renamed '{OldId}' to '{NewId.Trim()}'");
        }
        catch (ArgumentException Ex)
        {
            MessageBox.Show(this, Ex.Message, "Rename failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void FindReferencesMenuItem_Click(object? Sender, EventArgs E)
    {
        string? Id = SymbolNavigator.IdAt(SourceEditor.Text, SourceEditor.SelectionStart);
        if (string.IsNullOrEmpty(Id))
        {
            SetStatus("Place the caret on a symbol");
            return;
        }

        List<int> References = SymbolNavigator.FindReferences(SourceEditor.Text, Id);
        if (References.Count == 0)
        {
            SetStatus($"No references to '{Id}'");
            return;
        }

        JumpToIndex(References[0], Id.Length);
        SetStatus($"{References.Count.ToString(CultureInfo.InvariantCulture)} reference(s) to '{Id}'");
    }

    private void SelectNextOccurrenceMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.AA_SelectNextOccurrence)
        {
            return;
        }

        string Token = SourceEditor.SelectedText;
        if (string.IsNullOrEmpty(Token))
        {
            string? Id = SymbolNavigator.IdAt(SourceEditor.Text, SourceEditor.SelectionStart);
            if (string.IsNullOrEmpty(Id))
            {
                SetStatus("Select text or place the caret on a symbol");
                return;
            }

            Token = Id;
        }

        int FromIndex = SourceEditor.SelectionStart + Math.Max(1, SourceEditor.SelectionLength);
        int Found = SymbolNavigator.FindNextOccurrence(SourceEditor.Text, Token, FromIndex);
        if (Found < 0)
        {
            return;
        }

        _SelectedOccurrences.Add(Found);
        JumpToIndex(Found, Token.Length);
        SetStatus($"Occurrence {_SelectedOccurrences.Count.ToString(CultureInfo.InvariantCulture)} selected");
    }

    private void CommandPaletteMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.Z_CommandPalette)
        {
            return;
        }

        string Commands = "Open, Save, Copy source, Go to definition, Rename symbol, Toggle theme, Toggle diagnostics, Toggle minimap, Fit preview, Export PNG, Export SVG";
        string Command = Microsoft.VisualBasic.Interaction.InputBox($"Enter a command:\r\n{Commands}", "Command palette", string.Empty).Trim();
        if (Command.Length == 0)
        {
            return;
        }

        Dictionary<string, ToolStripMenuItem> Items = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["Open"] = OpenMenuItem,
            ["Save"] = SaveMenuItem,
            ["Copy source"] = CopySourceMenuItem,
            ["Go to definition"] = GoToDefinitionMenuItem,
            ["Rename symbol"] = RenameSymbolMenuItem,
            ["Toggle theme"] = ToggleThemeMenuItem,
            ["Toggle diagnostics"] = ToggleDiagnosticsMenuItem,
            ["Toggle minimap"] = ToggleMinimapMenuItem,
            ["Fit preview"] = FitPreviewMenuItem,
            ["Export PNG"] = ExportPngMenuItem,
            ["Export SVG"] = ExportSvgMenuItem
        };
        if (Items.TryGetValue(Command, out ToolStripMenuItem? Item) && Item.Enabled)
        {
            Item.PerformClick();
        }
        else
        {
            SetStatus($"Unknown command '{Command}'");
        }
    }

    private void BuildSamplesMenu()
    {
        SamplesMenu.DropDownItems.Clear();
        if (!LiveEditorFeatureGate.K_SampleGallery)
        {
            return;
        }

        foreach ((string Title, string Source) Sample in SampleCatalog.All)
        {
            ToolStripMenuItem Item = new ToolStripMenuItem(Sample.Title)
            {
                Tag = Sample.Source
            };
            Item.Click += SampleMenuItem_Click;
            SamplesMenu.DropDownItems.Add(Item);
        }
    }

    private void SampleMenuItem_Click(object? Sender, EventArgs E)
    {
        if (Sender is not ToolStripMenuItem Item || Item.Tag is not string Source || !ConfirmDiscardChanges())
        {
            return;
        }

        SetEditorContent(Source, null, null, IsDirty: true);
        SetStatus($"Loaded {Item.Text}");
    }

    private void BuildRecentFilesMenu()
    {
        RecentFilesMenuItem.DropDownItems.Clear();
        if (!LiveEditorFeatureGate.U_RecentFiles)
        {
            return;
        }

        foreach (string RecentPath in _Settings.RecentFiles)
        {
            ToolStripMenuItem Item = new ToolStripMenuItem(RecentPath)
            {
                Tag = RecentPath,
                Enabled = File.Exists(RecentPath)
            };
            Item.Click += RecentFileMenuItem_Click;
            RecentFilesMenuItem.DropDownItems.Add(Item);
        }

        if (RecentFilesMenuItem.DropDownItems.Count == 0)
        {
            RecentFilesMenuItem.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
        }
    }

    private void RecentFileMenuItem_Click(object? Sender, EventArgs E)
    {
        if (Sender is not ToolStripMenuItem Item || Item.Tag is not string SourcePath || !ConfirmDiscardChanges())
        {
            return;
        }

        try
        {
            LoadSourceFile(SourcePath, Announce: true);
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using OpenFileDialog Dialog = new OpenFileDialog
        {
            Filter = "Mermaid source (*.mmd;*.md;*.txt)|*.mmd;*.md;*.txt|All files (*.*)|*.*",
            Title = "Open Mermaid source"
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            LoadSourceFile(Dialog.FileName, Announce: true);
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadSourceFile(string SourcePath, bool Announce)
    {
        string Source = File.ReadAllText(SourcePath);
        SetEditorContent(Source, SourcePath, null, IsDirty: false);
        _Settings.RememberFile(SourcePath);
        BuildRecentFilesMenu();
        ConfigureWatcher();
        if (Announce)
        {
            SetStatus($"Opened {Path.GetFileName(SourcePath)}");
        }
    }

    private void SaveMenuItem_Click(object? Sender, EventArgs E)
    {
        if (string.IsNullOrWhiteSpace(_CurrentSourcePath))
        {
            SaveAsMenuItem_Click(Sender, E);
            return;
        }

        TrySaveSource(_CurrentSourcePath);
    }

    private void SaveAsMenuItem_Click(object? Sender, EventArgs E)
    {
        using SaveFileDialog Dialog = new SaveFileDialog
        {
            Filter = "Mermaid source (*.mmd)|*.mmd|Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Save Mermaid source",
            FileName = string.IsNullOrWhiteSpace(_CurrentSourcePath) ? "diagram.mmd" : Path.GetFileName(_CurrentSourcePath)
        };
        if (Dialog.ShowDialog(this) == DialogResult.OK)
        {
            TrySaveSource(Dialog.FileName);
        }
    }

    private void TrySaveSource(string SourcePath)
    {
        try
        {
            _SuppressWatcher = true;
            File.WriteAllText(SourcePath, SourceEditor.Text);
            _CurrentSourcePath = SourcePath;
            _CurrentCoArchiFolder = null;
            _IsDirty = false;
            _Settings.RememberFile(SourcePath);
            BuildRecentFilesMenu();
            ConfigureWatcher();
            UpdateTitle();
            SetStatus($"Saved {Path.GetFileName(SourcePath)}");
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            BeginInvoke(() => _SuppressWatcher = false);
        }
    }

    private void ConfigureWatcher()
    {
        DisposeWatcher();
        if (!LiveEditorFeatureGate.V_WatchFolder)
        {
            return;
        }

        string? DirectoryPath;
        string FileName;
        if (!string.IsNullOrWhiteSpace(_CurrentSourcePath))
        {
            DirectoryPath = Path.GetDirectoryName(_CurrentSourcePath);
            FileName = Path.GetFileName(_CurrentSourcePath);
        }
        else if (!string.IsNullOrWhiteSpace(_CurrentCoArchiFolder))
        {
            DirectoryPath = _CurrentCoArchiFolder;
            FileName = "model.archimate";
        }
        else
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DirectoryPath) || string.IsNullOrWhiteSpace(FileName) || !Directory.Exists(DirectoryPath))
        {
            return;
        }

        _Watcher = new FileSystemWatcher(DirectoryPath, FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _Watcher.Changed += Watcher_Changed;
        _Watcher.Renamed += Watcher_Changed;
    }

    private void Watcher_Changed(object Sender, FileSystemEventArgs E)
    {
        if (_SuppressWatcher || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(HandleExternalFileChange);
    }

    private void HandleExternalFileChange()
    {
        if (_SuppressWatcher)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_CurrentSourcePath))
        {
            RefreshCoArchiDiskSnapshot();
            return;
        }

        if (!File.Exists(_CurrentSourcePath))
        {
            return;
        }

        bool Reload = !_IsDirty;
        if (_IsDirty)
        {
            DialogResult Result = MessageBox.Show(this, "The source file changed on disk. Reload it and discard local changes?", "File changed", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            Reload = Result == DialogResult.Yes;
        }

        if (!Reload)
        {
            return;
        }

        try
        {
            _SuppressWatcher = true;
            string Source = File.ReadAllText(_CurrentSourcePath);
            SetEditorContent(Source, _CurrentSourcePath, null, IsDirty: false);
            SetStatus("Reloaded changed file");
        }
        catch (IOException)
        {
        }
        finally
        {
            _SuppressWatcher = false;
        }
    }

    private void RefreshCoArchiDiskSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_CurrentCoArchiFolder))
        {
            return;
        }

        if (ArchiInterop.TryImportCoArchiFolder(_CurrentCoArchiFolder, out string MermaidSource, out _, out _))
        {
            _CoArchiDiskSnapshot = MermaidSource;
            UpdateCoArchiDirtyBadge();
        }
    }

    private void DisposeWatcher()
    {
        if (_Watcher == null)
        {
            return;
        }

        _Watcher.EnableRaisingEvents = false;
        _Watcher.Changed -= Watcher_Changed;
        _Watcher.Renamed -= Watcher_Changed;
        _Watcher.Dispose();
        _Watcher = null;
    }

    private void ExitMenuItem_Click(object? Sender, EventArgs E)
    {
        Close();
    }

    private void MainForm_FormClosing(object? Sender, FormClosingEventArgs E)
    {
        if (!ConfirmDiscardChanges())
        {
            E.Cancel = true;
            return;
        }

        SaveSession();
        _RenderCancellation?.Cancel();
        _RenderCancellation?.Dispose();
        _RenderCancellation = null;
        RenderTimer.Stop();
        HighlightTimer.Stop();
        DisposeWatcher();
        _CompletionPopup.Dispose();
        _GhostText?.Dispose();
        _GhostText = null;
        Image? Preview = PreviewImage.Image;
        PreviewImage.Image = null;
        Preview?.Dispose();
        if (_LastGoodPreview != null && !ReferenceEquals(_LastGoodPreview, Preview))
        {
            _LastGoodPreview.Dispose();
        }

        _LastGoodPreview = null;
    }

    private void SaveSession()
    {
        if (!LiveEditorFeatureGate.T_PersistentSession)
        {
            return;
        }

        _Settings.CaretIndex = SourceEditor.SelectionStart;
        _Settings.PreviewZoom = _PreviewZoom;
        _Settings.LastFilePath = _CurrentSourcePath;
        _Settings.LastCoArchiFolder = _CurrentCoArchiFolder;
        _Settings.VerticalSplit = EditorSplit.Orientation == Orientation.Vertical;
        _Settings.DiagnosticsVisible = !BodySplit.Panel2Collapsed;
        _Settings.MinimapVisible = MinimapBox.Visible;
        try
        {
            _Settings.Save();
        }
        catch (Exception Ex)
        {
            Debug.WriteLine($"Could not save LiveEditor settings: {Ex.Message}");
        }
    }

    private void ImportExchangeMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using OpenFileDialog Dialog = new OpenFileDialog
        {
            Filter = "Model Exchange XML (*.xml)|*.xml|All files (*.*)|*.*",
            Title = "Import Model Exchange"
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            string Xml = File.ReadAllText(Dialog.FileName);
            if (!ArchiInterop.TryImportExchangeXml(Xml, out string MermaidSource, out string WarningSummary, out string ErrorMessage))
            {
                MessageBox.Show(this, ErrorMessage, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ApplyImportedSource(MermaidSource, null, null, WarningSummary, "Imported Model Exchange");
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void InsertArchiSnippetMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.AD_InsertFromArchi)
        {
            return;
        }

        string Xml = string.Empty;
        using OpenFileDialog Dialog = new OpenFileDialog
        {
            Filter = "Model Exchange XML (*.xml)|*.xml|All files (*.*)|*.*",
            Title = "Choose Model Exchange XML, or cancel to paste XML"
        };
        if (Dialog.ShowDialog(this) == DialogResult.OK)
        {
            Xml = File.ReadAllText(Dialog.FileName);
        }
        else
        {
            Xml = Microsoft.VisualBasic.Interaction.InputBox("Paste Model Exchange XML:", "Insert from Archi", string.Empty);
        }

        if (string.IsNullOrWhiteSpace(Xml))
        {
            return;
        }

        if (!ArchiInterop.TryImportExchangeXml(Xml, out string MermaidSource, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Insert failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int InsertAt = SourceEditor.SelectionStart;
        SourceEditor.SelectedText = MermaidSource;
        SourceEditor.SelectionStart = Math.Min(SourceEditor.TextLength, InsertAt + MermaidSource.Length);
        SetStatus(string.IsNullOrWhiteSpace(WarningSummary) ? "Inserted Archi source" : TruncateStatus(WarningSummary));
    }

    private void ImportArchimateMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using OpenFileDialog Dialog = new OpenFileDialog
        {
            Filter = "ArchiMate model (*.archimate)|*.archimate|All files (*.*)|*.*",
            Title = "Import ArchiMate file"
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!ArchiInterop.TryImportArchimateFile(Dialog.FileName, out string MermaidSource, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ApplyImportedSource(MermaidSource, null, null, WarningSummary, "Imported ArchiMate file");
    }

    private void ExportExchangeMenuItem_Click(object? Sender, EventArgs E)
    {
        using SaveFileDialog Dialog = new SaveFileDialog
        {
            Filter = "Model Exchange XML (*.xml)|*.xml|All files (*.*)|*.*",
            Title = "Export Model Exchange",
            FileName = "model-exchange.xml"
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!ArchiInterop.TryExportExchangeXml(SourceEditor.Text, Dialog.FileName, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetStatus(string.IsNullOrEmpty(WarningSummary) ? "Exported Model Exchange" : TruncateStatus(WarningSummary));
    }

    private void ExportArchimateMenuItem_Click(object? Sender, EventArgs E)
    {
        using SaveFileDialog Dialog = new SaveFileDialog
        {
            Filter = "ArchiMate model (*.archimate)|*.archimate|All files (*.*)|*.*",
            Title = "Export ArchiMate file",
            FileName = "model.archimate"
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!ArchiInterop.TryExportArchimateFile(SourceEditor.Text, Dialog.FileName, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetStatus(string.IsNullOrEmpty(WarningSummary) ? "Exported ArchiMate file" : TruncateStatus(WarningSummary));
    }

    private void OpenCoArchiFolderMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using FolderBrowserDialog Dialog = new FolderBrowserDialog
        {
            Description = "Select a coArchi working-copy folder containing model.archimate and .git",
            UseDescriptionForTitle = true
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!ArchiInterop.TryImportCoArchiFolder(Dialog.SelectedPath, out string MermaidSource, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Open coArchi folder failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ApplyImportedSource(MermaidSource, null, Dialog.SelectedPath, WarningSummary, "Opened coArchi folder");
        _CoArchiDiskSnapshot = MermaidSource;
        _Settings.LastCoArchiFolder = Dialog.SelectedPath;
        UpdateCoArchiDirtyBadge();
    }

    private void SaveCoArchiFolderMenuItem_Click(object? Sender, EventArgs E)
    {
        string? FolderPath = _CurrentCoArchiFolder;
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            using FolderBrowserDialog Dialog = new FolderBrowserDialog
            {
                Description = "Select an existing coArchi working-copy folder to overwrite model.archimate",
                UseDescriptionForTitle = true
            };
            if (Dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            FolderPath = Dialog.SelectedPath;
        }

        if (!ArchiInterop.TrySaveCoArchiFolder(SourceEditor.Text, FolderPath, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Save to coArchi folder failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _CurrentCoArchiFolder = FolderPath;
        _CoArchiDiskSnapshot = SourceEditor.Text;
        _Settings.LastCoArchiFolder = FolderPath;
        UpdateCoArchiDirtyBadge();
        UpdateTitle();
        SetStatus(string.IsNullOrEmpty(WarningSummary) ? "Saved coArchi model.archimate" : TruncateStatus(WarningSummary));
    }

    private void UpdateCoArchiDirtyBadge()
    {
        if (!LiveEditorFeatureGate.AC_CoArchiDirtyBadge || string.IsNullOrWhiteSpace(_CurrentCoArchiFolder))
        {
            CoArchiDirtyLabel.Text = string.Empty;
            return;
        }

        bool Changed = !string.Equals(SourceEditor.Text, _CoArchiDiskSnapshot, StringComparison.Ordinal);
        CoArchiDirtyLabel.Text = Changed ? "coArchi modified" : "coArchi current";
        CoArchiDirtyLabel.ForeColor = Changed ? Color.DarkOrange : Color.ForestGreen;
    }

    private void ApplyImportedSource(string MermaidSource, string? SourcePath, string? CoArchiFolder, string WarningSummary, string SuccessStatus)
    {
        SetEditorContent(MermaidSource, SourcePath, CoArchiFolder, IsDirty: true);
        if (string.IsNullOrEmpty(WarningSummary))
        {
            SetStatus(SuccessStatus);
        }
        else
        {
            SetStatus(TruncateStatus($"{SuccessStatus} - {WarningSummary}"));
        }
    }

    private void SetEditorContent(string Source, string? SourcePath, string? CoArchiFolder, bool IsDirty)
    {
        _SuppressCompletion = true;
        try
        {
            SourceEditor.Text = Source;
            SourceEditor.SelectionStart = SourceEditor.TextLength;
        }
        finally
        {
            _SuppressCompletion = false;
        }

        _CurrentSourcePath = SourcePath;
        _CurrentCoArchiFolder = CoArchiFolder;
        _CoArchiDiskSnapshot = CoArchiFolder == null ? null : Source;
        _IsDirty = IsDirty;
        UpdateTitle();
        UpdateCoArchiDirtyBadge();
        ConfigureWatcher();
        QueueHighlight();
        QueueRender();
    }

    private void JumpToIndex(int Index, int Length)
    {
        SourceEditor.Focus();
        SourceEditor.Select(Math.Max(0, Math.Min(Index, SourceEditor.TextLength)), Math.Max(0, Math.Min(Length, SourceEditor.TextLength - Index)));
        SourceEditor.ScrollToCaret();
    }

    private void JumpToLine(int LineNumber)
    {
        int LineIndex = Math.Max(0, LineNumber - 1);
        int Index = SourceEditor.GetFirstCharIndexFromLine(LineIndex);
        if (Index >= 0)
        {
            int Length = LineIndex < SourceEditor.Lines.Length ? SourceEditor.Lines[LineIndex].Length : 0;
            JumpToIndex(Index, Length);
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_IsDirty)
        {
            return true;
        }

        DialogResult Result = MessageBox.Show(this, "Discard unsaved changes?", "LiveEditor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        return Result == DialogResult.Yes;
    }

    private void UpdateTitle()
    {
        string Name = string.IsNullOrWhiteSpace(_CurrentSourcePath) ? "Untitled" : Path.GetFileName(_CurrentSourcePath);
        if (!string.IsNullOrWhiteSpace(_CurrentCoArchiFolder))
        {
            Name = $"coArchi: {Path.GetFileName(_CurrentCoArchiFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}";
        }

        Text = $"{(_IsDirty ? "* " : string.Empty)}{Name} - LiveEditor";
    }

    private void SetStatus(string Message)
    {
        StatusLabel.Text = Message;
    }

    private static string TruncateStatus(string Message)
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            return "Error";
        }

        string SingleLine = Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return SingleLine.Length <= 200 ? SingleLine : $"{SingleLine.Substring(0, 197)}...";
    }

    private static bool IsCommentOnly(string Source)
    {
        using StringReader Reader = new StringReader(Source);
        string? Line;
        while ((Line = Reader.ReadLine()) != null)
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length > 0 && !Trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class DiagnosticListItem
    {
        private readonly EnterpriseDiagnostic _Diagnostic;

        public DiagnosticListItem(EnterpriseDiagnostic Diagnostic)
        {
            _Diagnostic = Diagnostic;
        }

        public EnterpriseDiagnostic Diagnostic
        {
            get
            {
                return _Diagnostic;
            }
        }

        public override string ToString()
        {
            string Location = _Diagnostic.LineNumber > 0 ? $"Line {_Diagnostic.LineNumber.ToString(CultureInfo.InvariantCulture)}" : "Diagram";
            return $"{_Diagnostic.Severity}: {Location}: {_Diagnostic.Message}";
        }
    }
}
