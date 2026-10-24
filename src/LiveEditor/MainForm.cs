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

    private readonly bool _CapturePreviewOnShown;
    private readonly bool _PersistSession;
    private readonly List<EnterpriseDiagnostic> _Diagnostics = new List<EnterpriseDiagnostic>();
    private readonly List<int> _SelectedOccurrences = new List<int>();
    private SessionSettings _Settings;
    private FileSystemWatcher? _Watcher;
    private CancellationTokenSource? _RenderCancellation;
    private Image? _LastGoodPreview;
    private byte[]? _LastPngBytes;
    private string? _LastSvg;
    private readonly PreviewHoverWindow _PreviewHoverWindow = new PreviewHoverWindow();
    private readonly PreviewHoverDismissFilter _PreviewHoverDismiss;
    private PreviewHoverIndex? _PreviewHover;
    private string? _PreviewHoverText;
    private string? _CurrentSourcePath;
    private string? _CurrentCoArchiFolder;
    private string? _CoArchiDiskSnapshot;
    private bool _IsDirty;
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
        : this(CapturePreviewOnShown, LiveEditorFeatureGate.T_PersistentSession ? SessionSettings.Load() : new SessionSettings(), PersistSession: true)
    {
    }

    /// <summary>
    /// Creates the editor from a session that is not written back to disk.
    /// </summary>
    /// <param name="Settings">Session used for this window only.</param>
    internal MainForm(SessionSettings Settings)
        : this(CapturePreviewOnShown: false, Settings, PersistSession: false)
    {
    }

    private MainForm(bool CapturePreviewOnShown, SessionSettings Settings, bool PersistSession)
    {
        _CapturePreviewOnShown = CapturePreviewOnShown;
        _PersistSession = PersistSession;
        _Settings = Settings;
        InitializeComponent();
        _PreviewHoverDismiss = new PreviewHoverDismissFilter(this);
        CodeView.ColoursRefreshing += CodeView_ColoursRefreshing;
        CodeView.StatusRequested += CodeView_StatusRequested;
        CodeView.FontSizeChanged += CodeView_FontSizeChanged;
        CodeView.IntelliSense.StatusChanged += IntelliSense_StatusChanged;
        CodeView.IntelliSense.Accepted += CompletionPopup_ItemAccepted;
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
        int PreferredFraction = _Settings.EditorOnLeft ? 42 : 58;
        int Preferred = (int)Math.Round(Span * PreferredFraction / 100.0);
        int Maximum = Math.Max(MinimumPane, Span - MinimumPane - EditorSplit.SplitterWidth);
        EditorSplit.SplitterDistance = Math.Max(MinimumPane, Math.Min(Maximum, Preferred));
    }

    private async void MainForm_Shown(object? Sender, EventArgs E)
    {
        EnsureEditorSplitLayout();
        CodeView.Editor.Focus();
        CodeView.Editor.SelectionStart = Math.Min(_Settings.CaretIndex, CodeView.Editor.TextLength);
        if (!_CapturePreviewOnShown)
        {
            return;
        }

        CodeView.IntelliSense.Suppress = true;
        try
        {
            CodeView.Editor.Text = COMPLETION_DEMO_SOURCE;
            CodeView.Editor.SelectionStart = CodeView.Editor.TextLength;
        }
        finally
        {
            CodeView.IntelliSense.Suppress = false;
        }

        await Task.Delay(500).ConfigureAwait(true);
        CodeView.IntelliSense.Refresh(Force: true);
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
        CompletionContext Context = CodeView.IntelliSense.Assist.BuildMembers(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
        using Bitmap FormBitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(FormBitmap, new Rectangle(Point.Empty, ClientSize));
        int PopupWidth = 440;
        int RowHeight = 24;
        int VisibleRows = Math.Min(8, Math.Max(1, Context.Items.Count));
        int PopupHeight = 12 + (VisibleRows * RowHeight);
        Point Caret = CodeView.Editor.GetPositionFromCharIndex(Math.Max(0, CodeView.Editor.SelectionStart - 1));
        Point EditorOrigin = CodeView.Editor.PointToScreen(Point.Empty);
        Point FormOrigin = PointToScreen(Point.Empty);
        int PopupX = EditorOrigin.X - FormOrigin.X + Caret.X + 8;
        int PopupY = EditorOrigin.Y - FormOrigin.Y + Caret.Y + CodeView.Editor.Font.Height + 4;
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

    private void IntelliSense_StatusChanged(object? Sender, string Message)
    {
        SetStatus(Message);
    }

    private void CompletionPopup_ItemAccepted(object? Sender, CompletionItem Item)
    {
        _IsDirty = true;
        UpdateTitle();
        QueueRender();
        QueueHighlight();
        SetStatus($"Inserted {Item.DisplayText}");
    }

    private void WireEvents()
    {
        CodeView.Editor.TextChanged += Editor_TextChanged;
        CodeView.Editor.SelectionChanged += Editor_SelectionChanged;
        PreviewCanvas.MouseWheel += PreviewCanvas_MouseWheel;
        PreviewCanvas.Resize += PreviewCanvas_Resize;
        PreviewImage.MouseMove += PreviewImage_MouseMove;
        PreviewImage.MouseLeave += PreviewImage_MouseLeave;
        PreviewCanvas.MouseLeave += PreviewImage_MouseLeave;
        Deactivate += MainForm_Deactivate;
        Application.AddMessageFilter(_PreviewHoverDismiss);
        PreviewHost.Resize += PreviewHost_Resize;
        PreviewZoomInButton.Click += PreviewZoomInButton_Click;
        PreviewZoomOutButton.Click += PreviewZoomOutButton_Click;
        PreviewFitButton.Click += FitPreviewMenuItem_Click;
        PreviewFitButton.Paint += PreviewFitButton_Paint;
        ElementToolbar.BindSource(CurrentEditorText);
        ElementToolbar.InsertRequested += ElementToolbar_InsertRequested;
        ElementToolbar.PromptClosed += ElementToolbar_PromptClosed;
        DiagnosticsList.DoubleClick += DiagnosticsList_DoubleClick;
        RenderTimer.Tick += RenderTimer_Tick;
        NewMenuItem.Click += NewMenuItem_Click;
        OpenMenuItem.Click += OpenMenuItem_Click;
        SaveMenuItem.Click += SaveMenuItem_Click;
        SaveAsMenuItem.Click += SaveAsMenuItem_Click;
        ExitMenuItem.Click += ExitMenuItem_Click;
        UndoMenuItem.Click += UndoMenuItem_Click;
        RedoMenuItem.Click += RedoMenuItem_Click;
        CutMenuItem.Click += CutMenuItem_Click;
        CopyMenuItem.Click += CopyMenuItem_Click;
        PasteMenuItem.Click += PasteMenuItem_Click;
        FindMenuItem.Click += FindMenuItem_Click;
        ReplaceMenuItem.Click += ReplaceMenuItem_Click;
        GoToLineMenuItem.Click += GoToLineMenuItem_Click;
        FindNextMenuItem.Click += FindNextMenuItem_Click;
        FindPreviousMenuItem.Click += FindPreviousMenuItem_Click;
        CommentMenuItem.Click += CommentMenuItem_Click;
        UncommentMenuItem.Click += UncommentMenuItem_Click;
        DuplicateLineMenuItem.Click += DuplicateLineMenuItem_Click;
        DeleteLineMenuItem.Click += DeleteLineMenuItem_Click;
        IndentMenuItem.Click += IndentMenuItem_Click;
        OutdentMenuItem.Click += OutdentMenuItem_Click;
        NextErrorMenuItem.Click += NextErrorMenuItem_Click;
        PreviousErrorMenuItem.Click += PreviousErrorMenuItem_Click;
        CopySourceMenuItem.Click += CopySourceMenuItem_Click;
        ListMembersMenuItem.Click += ListMembersMenuItem_Click;
        ParameterInfoMenuItem.Click += ParameterInfoMenuItem_Click;
        QuickInfoMenuItem.Click += QuickInfoMenuItem_Click;
        RenameSymbolMenuItem.Click += RenameSymbolMenuItem_Click;
        GoToDefinitionMenuItem.Click += GoToDefinitionMenuItem_Click;
        FindReferencesMenuItem.Click += FindReferencesMenuItem_Click;
        CommandPaletteMenuItem.Click += CommandPaletteMenuItem_Click;
        SelectNextOccurrenceMenuItem.Click += SelectNextOccurrenceMenuItem_Click;
        InsertArchiSnippetMenuItem.Click += InsertArchiSnippetMenuItem_Click;
        SettingsMenuItem.Click += SettingsMenuItem_Click;
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
        PreviewZoomInButton.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        PreviewZoomOutButton.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        PreviewFitButton.Enabled = LiveEditorFeatureGate.L_ZoomPanPreview;
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
        CodeView.MinimapVisible = LiveEditorFeatureGate.Y_Minimap && _Settings.MinimapVisible;
        Orientation DesiredOrientation = _Settings.VerticalSplit ? Orientation.Vertical : Orientation.Horizontal;
        if (EditorSplit.Orientation != DesiredOrientation)
        {
            EditorSplit.Orientation = DesiredOrientation;
        }

        ToggleLegacyPreviewMenuItem.Checked = LiveEditorFeatureGate.S_LegacyPreview && _Settings.LegacyPreview;
        ToggleDiagnosticsMenuItem.Checked = !BodySplit.Panel2Collapsed;
        ToggleMinimapMenuItem.Checked = CodeView.MinimapVisible;
        ToggleThemeMenuItem.Checked = _Settings.DarkTheme;
        ApplyEditorPreferences();
        ApplyWorkspaceLayout();
    }

    private void ApplyEditorPreferences()
    {
        CodeView.AutoIndent = _Settings.AutoIndent;
        CodeView.InsertSpaces = _Settings.InsertSpaces;
        CodeView.IndentSize = _Settings.IndentSize;
        CodeView.ApplyFont(_Settings.EditorFontSize, _Settings.EditorWordWrap);
        CodeView.HighlightDelayMilliseconds = _Settings.HighlightDelayMilliseconds;
        RenderTimer.Interval = Math.Max(50, Math.Min(5000, _Settings.RenderDelayMilliseconds));
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

        CodeView.Editor.Text = DEFAULT_SOURCE;
        CodeView.Editor.SelectionStart = CodeView.Editor.TextLength;
        _IsDirty = false;
    }

    private void Editor_TextChanged(object? Sender, EventArgs E)
    {
        if (CodeView.IsApplyingFormatting)
        {
            return;
        }

        _IsDirty = true;
        _SelectedOccurrences.Clear();
        UpdateTitle();
        UpdateCoArchiDirtyBadge();
        QueueRender();
        ElementToolbar.UpdateFromEditor(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
    }

    private void Editor_SelectionChanged(object? Sender, EventArgs E)
    {
        if (CodeView.SuppressCaretNotifications)
        {
            return;
        }

        UpdateSignatureHelp();
        UpdateCaretDiagnosticStatus();
        ElementToolbar.UpdateFromEditor(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
    }

    private void CodeView_ColoursRefreshing(object? Sender, EventArgs E)
    {
        RefreshDiagnostics();
    }

    private void CodeView_StatusRequested(object? Sender, string Message)
    {
        SetStatus(Message);
    }

    /// <summary>
    /// True while a double-clicked identifier is still the selection.
    /// </summary>
    internal bool HasMarkedIdentifier
    {
        get
        {
            return CodeView.HasMarkedIdentifier;
        }
    }

    /// <summary>
    /// Screen position of the editor popup.
    /// </summary>
    internal Point AssistPopupLocation
    {
        get
        {
            return CodeView.AssistPopupLocation;
        }
    }

    /// <summary>
    /// True when the editor popup is open.
    /// </summary>
    internal bool AssistPopupVisible
    {
        get
        {
            return CodeView.AssistPopupVisible;
        }
    }

    /// <summary>
    /// Reads the background of one character without moving the visible selection.
    /// </summary>
    /// <param name="Index">Character index.</param>
    /// <returns>Background colour at that character.</returns>
    internal Color TextBackColorAt(int Index)
    {
        return CodeView.TextBackColorAt(Index);
    }


    private void ShowQuickInfo(int CharIndex)
    {
        CodeView.IntelliSense.ShowQuickInfoAtCaret();
    }

    private void RefreshMembers()
    {
        CodeView.IntelliSense.Refresh(Force: false);
    }

    private void QueueHighlight()
    {
        CodeView.QueueHighlight();
    }

    private void UpdateSignatureHelp()
    {
        if (!LiveEditorFeatureGate.G_SignatureHelp)
        {
            SignatureLabel.Text = string.Empty;
            return;
        }

        SignatureLabel.Text = CodeView.IntelliSense.Assist.BuildSignatureHelp(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
    }


    private void RefreshDiagnostics()
    {
        _Diagnostics.Clear();
        DiagnosticsList.Items.Clear();
        if (EnterpriseParser.IsEnterpriseSource(CodeView.Editor.Text))
        {
            EnterpriseParseResult Result = EnterpriseParser.Parse(CodeView.Editor.Text);
            foreach (EnterpriseDiagnostic Diagnostic in Result.Diagnostics)
            {
                _Diagnostics.Add(Diagnostic);
                DiagnosticsList.Items.Add(new DiagnosticListItem(Diagnostic));
            }
        }

        if (LiveEditorFeatureGate.E_InlineDiagnostics)
        {
            List<int> ErrorLines = new List<int>();
            foreach (EnterpriseDiagnostic Diagnostic in _Diagnostics)
            {
                if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error && Diagnostic.LineNumber > 0)
                {
                    ErrorLines.Add(Diagnostic.LineNumber);
                }
            }

            CodeView.TintErrorLines(ErrorLines);
        }

        UpdateCaretDiagnosticStatus();
    }

    private void UpdateCaretDiagnosticStatus()
    {
        string Message = CaretStatusOr(string.Empty);
        if (Message.Length > 0)
        {
            SetStatus(Message);
        }
    }

    private string CaretStatusOr(string Fallback)
    {
        if (!LiveEditorFeatureGate.E_InlineDiagnostics || _Diagnostics.Count == 0)
        {
            return Fallback;
        }

        int LineNumber = CodeView.Editor.GetLineFromCharIndex(CodeView.Editor.SelectionStart) + 1;
        foreach (EnterpriseDiagnostic Diagnostic in _Diagnostics)
        {
            if (Diagnostic.LineNumber == LineNumber)
            {
                return TruncateStatus(Diagnostic.Message);
            }
        }

        return Fallback;
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
        StartRender(CodeView.Editor.Text);
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

        SetStatus(CaretStatusOr("Rendering..."));
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
            else if (MermaidDiagramExport.TryRenderPreviewSvg(Source, out string PreviewSvg))
            {
                Svg = PreviewSvg;
                Token.ThrowIfCancellationRequested();
                PngBytes = MermaidDiagramExport.TryRasterizeSvg(Svg);
            }
            else
            {
                PngBytes = null;
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
            _PreviewHover = PreviewHoverIndex.Build(Svg, CodeView.Editor.Text);
            _BrokenSince = null;
            PreviewBanner.Visible = LegacyPreview;
            PreviewBanner.Height = LegacyPreview ? 28 : 0;
            PreviewBanner.Text = LegacyPreview ? "Legacy Naiad preview" : string.Empty;
            ApplyPreviewZoom();
            SetStatus(CaretStatusOr("OK"));
            UpdateRenderTiming(ElapsedMilliseconds);
            UpdateCaretDiagnosticStatus();
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

            _PreviewHover = null;
            HidePreviewHover();
        }

        PreviewBanner.Visible = true;
        PreviewBanner.Height = 32;
        string Time = _BrokenSince.Value.ToString("T", CultureInfo.CurrentCulture);
        PreviewBanner.Text = $"Broken since {Time}: {TruncateStatus(Message)}";
        SetStatus(CaretStatusOr(TruncateStatus(Message)));
        UpdateRenderTiming(ElapsedMilliseconds);
        UpdateCaretDiagnosticStatus();
    }

    private void UpdateRenderTiming(long ElapsedMilliseconds)
    {
        TimingLabel.Text = LiveEditorFeatureGate.R_RenderTiming ? $"{ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty;
    }

    private void PreviewImage_MouseMove(object? Sender, MouseEventArgs E)
    {
        if (PreviewImage.Image == null || _PreviewHover == null)
        {
            HidePreviewHover();
            return;
        }

        double SvgX;
        double SvgY;
        bool Mapped = PreviewHoverIndex.TryMapClientToSvg(
            E.X,
            E.Y,
            PreviewImage.ClientSize.Width,
            PreviewImage.ClientSize.Height,
            PreviewImage.Image.Width,
            PreviewImage.Image.Height,
            _PreviewHover.Width,
            _PreviewHover.Height,
            out SvgX,
            out SvgY);
        if (!Mapped)
        {
            HidePreviewHover();
            return;
        }

        string? Text = _PreviewHover.Hit(SvgX, SvgY);
        if (string.IsNullOrEmpty(Text))
        {
            HidePreviewHover();
            return;
        }

        if (string.Equals(Text, _PreviewHoverText, StringComparison.Ordinal))
        {
            return;
        }

        _PreviewHoverText = Text;
        _PreviewHoverWindow.ShowNear(this, Text, PreviewImage.PointToScreen(E.Location));
    }

    private void PreviewImage_MouseLeave(object? Sender, EventArgs E)
    {
        HidePreviewHover();
    }

    private void MainForm_Deactivate(object? Sender, EventArgs E)
    {
        HidePreviewHover();
    }

    private void DismissPreviewHoverIfPointerLeft()
    {
        if (!_PreviewHoverWindow.Visible || PreviewImage.IsDisposed)
        {
            return;
        }

        Rectangle Area = PreviewImage.RectangleToScreen(PreviewImage.ClientRectangle);
        if (!Area.Contains(Cursor.Position))
        {
            HidePreviewHover();
        }
    }

    private void HidePreviewHover()
    {
        _PreviewHoverText = null;
        _PreviewHoverWindow.HideTip();
    }

    private sealed class PreviewHoverDismissFilter : IMessageFilter
    {
        private const int WM_MOUSEMOVE = 0x0200;

        private readonly MainForm _Host;

        public PreviewHoverDismissFilter(MainForm Host)
        {
            _Host = Host;
        }

        public bool PreFilterMessage(ref Message Message)
        {
            if (Message.Msg == WM_MOUSEMOVE)
            {
                _Host.DismissPreviewHoverIfPointerLeft();
            }

            return false;
        }
    }

    private void PreviewCanvas_MouseWheel(object? Sender, MouseEventArgs E)
    {
        if (!LiveEditorFeatureGate.L_ZoomPanPreview || (ModifierKeys & Keys.Control) != Keys.Control)
        {
            return;
        }

        double CurrentZoom = CurrentPreviewZoom();
        SetPreviewZoom(E.Delta > 0 ? CurrentZoom * PREVIEW_ZOOM_STEP : CurrentZoom / PREVIEW_ZOOM_STEP);
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
        _Settings.PreviewFitToWindow = false;
        ApplyPreviewZoom();
        SetStatus($"Preview zoom {Math.Round(_PreviewZoom * 100.0).ToString(CultureInfo.InvariantCulture)}%");
    }

    private static double ClampZoom(double Zoom)
    {
        return Math.Max(MINIMUM_PREVIEW_ZOOM, Math.Min(MAXIMUM_PREVIEW_ZOOM, Zoom));
    }

    /// <summary>
    /// Returns the zoom the preview is actually showing. Fit-to-window does not store that scale.
    /// </summary>
    private double CurrentPreviewZoom()
    {
        if (!_Settings.PreviewFitToWindow || PreviewImage.Image == null)
        {
            return _PreviewZoom;
        }

        int ImageWidth = PreviewImage.Image.Width;
        int ImageHeight = PreviewImage.Image.Height;
        Size Area = PreviewCanvas.ClientSize;
        if (ImageWidth <= 0 || ImageHeight <= 0 || Area.Width <= 0 || Area.Height <= 0)
        {
            return _PreviewZoom;
        }

        double Fitted = Math.Min(Area.Width / (double)ImageWidth, Area.Height / (double)ImageHeight);
        return ClampZoom(Fitted);
    }

    private void ApplyPreviewZoom()
    {
        if (PreviewImage.Image == null)
        {
            PreviewImage.Dock = DockStyle.Fill;
            PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewCanvas.AutoScrollMinSize = Size.Empty;
            return;
        }

        if (_Settings.PreviewFitToWindow)
        {
            PreviewCanvas.AutoScrollPosition = Point.Empty;
            PreviewCanvas.AutoScrollMinSize = Size.Empty;
            PreviewImage.Dock = DockStyle.Fill;
            PreviewImage.Location = Point.Empty;
            PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
        }
        else
        {
            int Width = Math.Max(1, (int)Math.Round(PreviewImage.Image.Width * _PreviewZoom));
            int Height = Math.Max(1, (int)Math.Round(PreviewImage.Image.Height * _PreviewZoom));
            PreviewImage.Dock = DockStyle.None;
            PreviewImage.Location = Point.Empty;
            PreviewImage.Size = new Size(Width, Height);
            PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewCanvas.AutoScrollMinSize = new Size(Width, Height);
        }

        PreviewImage.Visible = true;
        PreviewImage.BringToFront();
        PreviewImage.Invalidate();
        PreviewCanvas.Invalidate();
        PositionPreviewZoomButtons();
    }

    private void PreviewHost_Resize(object? Sender, EventArgs E)
    {
        PositionPreviewZoomButtons();
    }

    private void PositionPreviewZoomButtons()
    {
        const int BUTTON = 32;
        const int GAP = 4;
        const int MARGIN = 10;
        int X = Math.Max(0, PreviewHost.ClientSize.Width - MARGIN - BUTTON);
        int FitTop = Math.Max(0, PreviewHost.ClientSize.Height - MARGIN - BUTTON);
        int OutTop = Math.Max(0, FitTop - GAP - BUTTON);
        int InTop = Math.Max(0, OutTop - GAP - BUTTON);
        PreviewZoomInButton.SetBounds(X, InTop, BUTTON, BUTTON);
        PreviewZoomOutButton.SetBounds(X, OutTop, BUTTON, BUTTON);
        PreviewFitButton.SetBounds(X, FitTop, BUTTON, BUTTON);
        PreviewZoomInButton.BringToFront();
        PreviewZoomOutButton.BringToFront();
        PreviewFitButton.BringToFront();
    }

    /// <summary>

    private void PreviewFitButton_Paint(object? Sender, PaintEventArgs E)
    {
        Rectangle Box = new Rectangle(8, 8, 14, 14);
        using Pen Outline = new Pen(PreviewFitButton.ForeColor, 1.6F);
        E.Graphics.DrawRectangle(Outline, Box);
    }

    private void ApplyWorkspaceLayout()
    {
        ElementToolbar.Dock = DockStyle.Top;
        EditorSplit.Dock = DockStyle.Fill;
        PreviewHost.Dock = DockStyle.Fill;
        Control EditorParent = _Settings.EditorOnLeft ? EditorSplit.Panel1 : EditorSplit.Panel2;
        Control PreviewParent = _Settings.EditorOnLeft ? EditorSplit.Panel2 : EditorSplit.Panel1;
        if (!ReferenceEquals(CodeView.Parent, EditorParent))
        {
            EditorParent.Controls.Add(CodeView);
        }

        if (!ReferenceEquals(PreviewHost.Parent, PreviewParent))
        {
            PreviewParent.Controls.Add(PreviewHost);
        }

        if (_Settings.WideToolbar)
        {
            if (!ReferenceEquals(ElementToolbar.Parent, BodySplit.Panel1))
            {
                BodySplit.Panel1.Controls.Add(ElementToolbar);
            }

            BodySplit.Panel1.Controls.SetChildIndex(EditorSplit, 0);
            BodySplit.Panel1.Controls.SetChildIndex(ElementToolbar, BodySplit.Panel1.Controls.Count - 1);
        }
        else if (!ReferenceEquals(ElementToolbar.Parent, PreviewHost))
        {
            PreviewHost.Controls.Add(ElementToolbar);
        }

        ArrangePreviewChrome();
        EnsureEditorSplitLayout();
        PositionPreviewZoomButtons();
    }

    private void ArrangePreviewChrome()
    {
        List<Control> Order = new List<Control>();
        if (ReferenceEquals(ElementToolbar.Parent, PreviewHost))
        {
            Order.Add(ElementToolbar);
        }

        Order.Add(PreviewBanner);
        Order.Add(PreviewCanvas);
        Order.Add(PreviewFitButton);
        Order.Add(PreviewZoomOutButton);
        Order.Add(PreviewZoomInButton);
        for (int Index = Order.Count - 1; Index >= 0; Index--)
        {
            PreviewHost.Controls.SetChildIndex(Order[Index], PreviewHost.Controls.Count - 1);
        }
    }

    private string CurrentEditorText()
    {
        return CodeView.Editor.Text;
    }

    private void ElementToolbar_InsertRequested(object? Sender, ElementInsertRequest Request)
    {
        string Text = CodeView.Editor.Text;
        int Caret = Math.Max(0, Math.Min(CodeView.Editor.SelectionStart, Text.Length));
        int LineStart = Caret;
        while (LineStart > 0 && Text[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        int LineEnd = Caret;
        while (LineEnd < Text.Length && Text[LineEnd] != '\n' && Text[LineEnd] != '\r')
        {
            LineEnd++;
        }

        int IndentEnd = LineStart;
        while (IndentEnd < LineEnd && (Text[IndentEnd] == ' ' || Text[IndentEnd] == '\t'))
        {
            IndentEnd++;
        }

        string Indent = Text.Substring(LineStart, IndentEnd - LineStart);
        string Rest = Text.Substring(IndentEnd, LineEnd - IndentEnd).Trim();
        string Declaration = Request.Snippet;
        if (Declaration.Length == 0)
        {
            Declaration = Request.KebabName + " " + Request.Identifier + " \"" + Request.Label.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        bool ReplaceLine = Rest.Length == 0 || (Request.KebabName.Length > 0 && Request.KebabName.StartsWith(Rest, StringComparison.OrdinalIgnoreCase));
        CodeView.IntelliSense.Suppress = true;
        try
        {
            if (ReplaceLine)
            {
                CodeView.Editor.Select(LineStart, LineEnd - LineStart);
                CodeView.Editor.SelectedText = Indent + Declaration;
            }
            else
            {
                CodeView.Editor.Select(LineEnd, 0);
                CodeView.Editor.SelectedText = Environment.NewLine + Indent + Declaration;
            }
        }
        finally
        {
            CodeView.IntelliSense.Suppress = false;
        }

        _IsDirty = true;
        UpdateTitle();
        QueueRender();
        QueueHighlight();
        CodeView.IntelliSense.Refresh(Force: false);
        ElementToolbar.UpdateFromEditor(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
        SetStatus("Inserted " + Request.Identifier);
        BeginInvoke(new Action(FocusEditor));
    }

    private void ElementToolbar_PromptClosed(object? Sender, EventArgs E)
    {
        BeginInvoke(new Action(FocusEditor));
    }

    private void FocusEditor()
    {
        if (!IsDisposed && !CodeView.Editor.IsDisposed)
        {
            CodeView.Editor.Focus();
        }
    }

    private void ZoomInMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.ZoomIn();
    }

    private void ZoomOutMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.ZoomOut();
    }

    private void PreviewZoomInButton_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.L_ZoomPanPreview)
        {
            SetPreviewZoom(CurrentPreviewZoom() * PREVIEW_ZOOM_STEP);
        }
    }

    private void PreviewZoomOutButton_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.L_ZoomPanPreview)
        {
            SetPreviewZoom(CurrentPreviewZoom() / PREVIEW_ZOOM_STEP);
        }
    }

    private void ZoomResetMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.ResetZoom();
    }

    private void UndoMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.UndoEdit();
    }

    private void RedoMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.RedoEdit();
    }

    private void CutMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.CutSelection();
    }

    private void CopyMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.CopySelection();
    }

    private void PasteMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.PasteClipboard();
    }

    private void FindMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.ShowFind();
    }

    private void ReplaceMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.ShowReplace();
    }

    private void GoToLineMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.ShowGoToLine();
    }

    private void FindNextMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.FindNextMatch();
    }

    private void FindPreviousMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.FindPreviousMatch();
    }

    private void CommentMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.CommentLines();
    }

    private void UncommentMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.UncommentLines();
    }

    private void DuplicateLineMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.DuplicateLines();
    }

    private void DeleteLineMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.DeleteLines();
    }

    private void IndentMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.IndentLines();
    }

    private void OutdentMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.OutdentLines();
    }

    private void NextErrorMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.NextDiagnostic();
    }

    private void PreviousErrorMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.PreviousDiagnostic();
    }

    private void CodeView_FontSizeChanged(object? Sender, float Size)
    {
        _Settings.EditorFontSize = Size;
        SaveSession();
    }

    private void FitPreviewMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.L_ZoomPanPreview || PreviewImage.Image == null)
        {
            return;
        }

        _Settings.PreviewFitToWindow = true;
        ApplyPreviewZoom();
        SetStatus("Preview fitted to window");
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
            string Svg = _LastSvg ?? MermaidDiagramExport.RenderSvg(CodeView.Editor.Text);
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
        CodeView.ApplyChrome(_Settings.DarkTheme);
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

        CodeView.MinimapVisible = !CodeView.MinimapVisible;
        _Settings.MinimapVisible = CodeView.MinimapVisible;
        ToggleMinimapMenuItem.Checked = CodeView.MinimapVisible;
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

    private void ListMembersMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.Editor.Focus();
        CodeView.IntelliSense.Refresh(Force: true);
    }

    private void ParameterInfoMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.Editor.Focus();
        CodeView.IntelliSense.ShowParameter();
    }

    private void QuickInfoMenuItem_Click(object? Sender, EventArgs E)
    {
        CodeView.Editor.Focus();
        ShowQuickInfo(CodeView.Editor.SelectionStart);
    }

    private void CopySourceMenuItem_Click(object? Sender, EventArgs E)
    {
        if (LiveEditorFeatureGate.N_CopySource && !string.IsNullOrEmpty(CodeView.Editor.Text))
        {
            Clipboard.SetText(CodeView.Editor.Text);
            SetStatus("Mermaid source copied");
        }
    }

    private void GoToDefinitionMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.H_GoToDefinition)
        {
            return;
        }

        string? Id = SymbolNavigator.IdAt(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
        if (string.IsNullOrEmpty(Id))
        {
            SetStatus("Place the caret on a symbol");
            return;
        }

        int Index = SymbolNavigator.FindDefinition(CodeView.Editor.Text, Id);
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

        string? OldId = SymbolNavigator.IdAt(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
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
            int Caret = CodeView.Editor.SelectionStart;
            CodeView.Editor.Text = SymbolNavigator.Rename(CodeView.Editor.Text, OldId, NewId.Trim());
            CodeView.Editor.SelectionStart = Math.Min(Caret, CodeView.Editor.TextLength);
            SetStatus($"Renamed '{OldId}' to '{NewId.Trim()}'");
        }
        catch (ArgumentException Ex)
        {
            MessageBox.Show(this, Ex.Message, "Rename failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void FindReferencesMenuItem_Click(object? Sender, EventArgs E)
    {
        string? Id = SymbolNavigator.IdAt(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
        if (string.IsNullOrEmpty(Id))
        {
            SetStatus("Place the caret on a symbol");
            return;
        }

        List<int> References = SymbolNavigator.FindReferences(CodeView.Editor.Text, Id);
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

        string Token = CodeView.Editor.SelectedText;
        if (string.IsNullOrEmpty(Token))
        {
            string? Id = SymbolNavigator.IdAt(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
            if (string.IsNullOrEmpty(Id))
            {
                SetStatus("Select text or place the caret on a symbol");
                return;
            }

            Token = Id;
        }

        int FromIndex = CodeView.Editor.SelectionStart + Math.Max(1, CodeView.Editor.SelectionLength);
        int Found = SymbolNavigator.FindNextOccurrence(CodeView.Editor.Text, Token, FromIndex);
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

        string Commands = "New, Open, Save, Undo, Redo, Cut, Copy, Paste, Find, Replace, Go to line, Comment, Uncomment, Duplicate line, Delete line, Next error, Copy source, Go to definition, Rename symbol, Settings, Toggle theme, Toggle diagnostics, Toggle minimap, Fit preview, Export PNG, Export SVG";
        string Command = Microsoft.VisualBasic.Interaction.InputBox($"Enter a command:\r\n{Commands}", "Command palette", string.Empty).Trim();
        if (Command.Length == 0)
        {
            return;
        }

        Dictionary<string, ToolStripMenuItem> Items = new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["New"] = NewMenuItem,
            ["Open"] = OpenMenuItem,
            ["Save"] = SaveMenuItem,
            ["Undo"] = UndoMenuItem,
            ["Redo"] = RedoMenuItem,
            ["Cut"] = CutMenuItem,
            ["Copy"] = CopyMenuItem,
            ["Paste"] = PasteMenuItem,
            ["Find"] = FindMenuItem,
            ["Replace"] = ReplaceMenuItem,
            ["Go to line"] = GoToLineMenuItem,
            ["Comment"] = CommentMenuItem,
            ["Uncomment"] = UncommentMenuItem,
            ["Duplicate line"] = DuplicateLineMenuItem,
            ["Delete line"] = DeleteLineMenuItem,
            ["Next error"] = NextErrorMenuItem,
            ["Copy source"] = CopySourceMenuItem,
            ["Go to definition"] = GoToDefinitionMenuItem,
            ["Rename symbol"] = RenameSymbolMenuItem,
            ["Settings"] = SettingsMenuItem,
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

    private void SettingsMenuItem_Click(object? Sender, EventArgs E)
    {
        using SettingsForm Dialog = new SettingsForm(_Settings);
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Dialog.ApplyTo(_Settings);
        ApplyEditorPreferences();
        ApplyWorkspaceLayout();
        _Settings.Save();
        QueueHighlight();
        QueueRender();
        CodeView.Editor.Focus();
        SetStatus("Settings saved");
    }

    private void BuildSamplesMenu()
    {
        SamplesMenu.DropDownItems.Clear();
        if (!LiveEditorFeatureGate.K_SampleGallery)
        {
            return;
        }

        for (int Index = 0; Index < SampleCatalog.Groups.Count; Index++)
        {
            DiagramSampleGroup Group = SampleCatalog.Groups[Index];
            ToolStripMenuItem Submenu = new ToolStripMenuItem(Group.MenuTitle);
            Submenu.DropDownItems.Add(CreateSampleItem(Group.SimpleTitle, Group.SimpleSource));
            Submenu.DropDownItems.Add(CreateSampleItem(Group.CompleteTitle, Group.FullSource));
            SamplesMenu.DropDownItems.Add(Submenu);
        }
    }

    private ToolStripMenuItem CreateSampleItem(string Title, string Source)
    {
        ToolStripMenuItem Item = new ToolStripMenuItem(Title);
        Item.Tag = Source;
        Item.Click += SampleMenuItem_Click;
        return Item;
    }

    private void SampleMenuItem_Click(object? Sender, EventArgs E)
    {
        if (Sender is not ToolStripMenuItem Item || Item.Tag is not string Source || !ConfirmDiscardChanges())
        {
            return;
        }

        SetEditorContent(Source, null, null, IsDirty: true);
        string GroupTitle = string.Empty;
        if (Item.OwnerItem is ToolStripMenuItem Parent)
        {
            GroupTitle = Parent.Text + " - ";
        }

        SetStatus("Loaded " + GroupTitle + Item.Text);
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

    private void NewMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using NewDiagramForm Dialog = new NewDiagramForm();
        if (Dialog.ShowDialog(this) != DialogResult.OK || Dialog.SelectedKind == null)
        {
            return;
        }

        SetEditorContent(Dialog.SelectedKind.Starter, null, null, false);
        ElementToolbar.UpdateFromEditor(CodeView.Editor.Text, CodeView.Editor.SelectionStart);
        FocusEditor();
        SetStatus("New " + Dialog.SelectedKind.Title);
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
            File.WriteAllText(SourcePath, CodeView.Editor.Text);
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
        CodeView.StopHighlight();
        DisposeWatcher();
        Application.RemoveMessageFilter(_PreviewHoverDismiss);
        HidePreviewHover();
        _PreviewHoverWindow.Dispose();
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
        if (!_PersistSession || !LiveEditorFeatureGate.T_PersistentSession)
        {
            return;
        }

        _Settings.CaretIndex = CodeView.Editor.SelectionStart;
        _Settings.PreviewZoom = _PreviewZoom;
        _Settings.LastFilePath = _CurrentSourcePath;
        _Settings.LastCoArchiFolder = _CurrentCoArchiFolder;
        _Settings.VerticalSplit = EditorSplit.Orientation == Orientation.Vertical;
        _Settings.DiagnosticsVisible = !BodySplit.Panel2Collapsed;
        _Settings.MinimapVisible = CodeView.MinimapVisible;
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

        int InsertAt = CodeView.Editor.SelectionStart;
        CodeView.Editor.SelectedText = MermaidSource;
        CodeView.Editor.SelectionStart = Math.Min(CodeView.Editor.TextLength, InsertAt + MermaidSource.Length);
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

        if (!ArchiInterop.TryExportExchangeXml(CodeView.Editor.Text, Dialog.FileName, out string WarningSummary, out string ErrorMessage))
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

        if (!ArchiInterop.TryExportArchimateFile(CodeView.Editor.Text, Dialog.FileName, out string WarningSummary, out string ErrorMessage))
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

        if (!ArchiInterop.TrySaveCoArchiFolder(CodeView.Editor.Text, FolderPath, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Save to coArchi folder failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _CurrentCoArchiFolder = FolderPath;
        _CoArchiDiskSnapshot = CodeView.Editor.Text;
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

        bool Changed = !string.Equals(CodeView.Editor.Text, _CoArchiDiskSnapshot, StringComparison.Ordinal);
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
        CodeView.IntelliSense.Suppress = true;
        try
        {
            CodeView.Editor.Text = Source;
            CodeView.Editor.SelectionStart = CodeView.Editor.TextLength;
        }
        finally
        {
            CodeView.IntelliSense.Suppress = false;
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
        CodeView.Editor.Focus();
        CodeView.Editor.Select(Math.Max(0, Math.Min(Index, CodeView.Editor.TextLength)), Math.Max(0, Math.Min(Length, CodeView.Editor.TextLength - Index)));
        CodeView.Editor.ScrollToCaret();
    }

    private void JumpToLine(int LineNumber)
    {
        int LineIndex = Math.Max(0, LineNumber - 1);
        int Index = CodeView.Editor.GetFirstCharIndexFromLine(LineIndex);
        if (Index >= 0)
        {
            int Length = LineIndex < CodeView.Editor.Lines.Length ? CodeView.Editor.Lines[LineIndex].Length : 0;
            JumpToIndex(Index, Length);
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_PersistSession || !_IsDirty)
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
