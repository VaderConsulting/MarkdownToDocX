using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveEditor.Archi;
using LiveEditor.Azure;
using LiveEditor.Completion;
using LiveEditor.Editor;
using MDConverter;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

/// <summary>
/// Provides a live Mermaid editor with preview, completion, diagnostics, and Archi handoff.
/// </summary>
public partial class MainForm : Form
{
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
    private readonly List<EnterpriseDiagnostic> _Diagnostics = [];
    private readonly List<int> _SelectedOccurrences = [];
    private SessionSettings _Settings;
    private FileSystemWatcher? _Watcher;
    private CancellationTokenSource? _RenderCancellation;

    private readonly object _RenderWorkerGate = new object();

    private bool _RenderWorkerRunning;

    private RenderRequest? _PendingRender;
    private RetainedEnterprisePreview _RetainedPreview = new();
    private DiagramLayoutOptions? _SessionLayoutOptions;
    private bool? _SessionLegacyPreview;
    internal string? LastRetainedUpdateKind { get; private set; }
    internal string[] LastRebuiltRouteIds { get; private set; } = [];
    private sealed record RenderRequest(string Source, int Generation, int TextVersion, bool LegacyPreview,
        DiagramLayoutOptions Options, RetainedEnterprisePreview Retained, CancellationToken Token);
    private byte[]? _LastPngBytes;
    private string? _LastSvg;
    private readonly PreviewHoverWindow _PreviewHoverWindow = new PreviewHoverWindow();
    private readonly PreviewHoverDismissFilter _PreviewHoverDismiss;
    private string? _PreviewHoverText;
    private bool _IgnorePreviewSelection;
    private bool _SyncingSelectionFromPreview;
    private bool _FollowPreviewFromCaret;
    private bool _ClearPreviewSelectionOnNextRender;
    private int _CaretAtPreviewClear = -1;
    private int _EditorTextVersion;
    private int _PreviewIndexTextVersion = -1;
    private int _RenderTextVersion;
    private EnterpriseDocument? _CaretDocument;
    private int _CaretDocumentVersion = -1;
    private string? _CurrentSourcePath;
    private string? _CurrentCoArchiFolder;
    private string? _CoArchiDiskSnapshot;
    private bool _IsDirty;
    private bool _SuppressWatcher;
    private int _RenderGeneration;
    private DiagramMetadata _DiagramMetadata = DiagramMetadata.Empty(0);
    private DateTime? _BrokenSince;

    private Control? _FloatingSelectionHost;

    private const int FLOATING_SELECTION_MARGIN_PX = 12;

    private const int FLOATING_SELECTION_WIDTH_PX = 600;

    private const int DEFAULT_EDITOR_PANE_PIXELS = 341;

    private const int MINIMUM_EDITOR_PANE_PIXELS = 240;

    private bool _EditorSplitDistanceApplied;

    private bool _LastEditorOnLeft = true;

    private Orientation _LastSplitOrientation = Orientation.Vertical;

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
        WireEvents();
        ApplySettings();
        AssignSampleSources();
        BuildRecentFilesMenu();
        RestoreSessionContent();
        ApplyTheme();
        UpdateFeatureAvailability();
        UpdateTitle();
        QueueHighlight();
        QueueRender();
    }

    private void MainForm_Load(object? Sender, EventArgs E)
    {
        BeginInvoke(() =>
        {
            EnsureEditorSplitLayout();
            LayoutFloatingSelectionPanel();
        });
    }

    private void EnsureEditorSplitLayout()
    {
        int Span = EditorSplit.Orientation == Orientation.Vertical ? EditorSplit.Width : EditorSplit.Height;
        if (Span < 100)
        {
            return;
        }

        EditorSplit.Panel1MinSize = MINIMUM_EDITOR_PANE_PIXELS;
        EditorSplit.Panel2MinSize = MINIMUM_EDITOR_PANE_PIXELS;
        EditorSplit.FixedPanel = _Settings.EditorOnLeft ? FixedPanel.Panel1 : FixedPanel.Panel2;
        bool NeedsSplitterDistance = !_EditorSplitDistanceApplied
            || _LastEditorOnLeft != _Settings.EditorOnLeft
            || _LastSplitOrientation != EditorSplit.Orientation;
        if (!NeedsSplitterDistance)
        {
            return;
        }

        int EditorPixels = _Settings.EditorPanePixels > 0 ? _Settings.EditorPanePixels : DEFAULT_EDITOR_PANE_PIXELS;
        int Maximum = Math.Max(MINIMUM_EDITOR_PANE_PIXELS, Span - MINIMUM_EDITOR_PANE_PIXELS - EditorSplit.SplitterWidth);
        int ClampedEditor = Math.Max(MINIMUM_EDITOR_PANE_PIXELS, Math.Min(Maximum, EditorPixels));
        if (_Settings.EditorOnLeft)
        {
            EditorSplit.SplitterDistance = ClampedEditor;
        }
        else
        {
            EditorSplit.SplitterDistance = Math.Max(MINIMUM_EDITOR_PANE_PIXELS, Span - ClampedEditor - EditorSplit.SplitterWidth);
        }

        _EditorSplitDistanceApplied = true;
        _LastEditorOnLeft = _Settings.EditorOnLeft;
        _LastSplitOrientation = EditorSplit.Orientation;
    }

    private void EditorSplit_SplitterMoved(object? Sender, SplitterEventArgs E)
    {
        if (!_PersistSession)
        {
            return;
        }

        _Settings.EditorPanePixels = GetEditorPanePixels();
    }

    private int GetEditorPanePixels()
    {
        Control EditorPane = _Settings.EditorOnLeft ? EditorSplit.Panel1 : EditorSplit.Panel2;
        if (EditorSplit.Orientation == Orientation.Vertical)
        {
            return EditorPane.Width;
        }

        return EditorPane.Height;
    }

    private async void MainForm_Shown(object? Sender, EventArgs E)
    {
        EnsureEditorSplitLayout();
        _IgnorePreviewSelection = true;
        try
        {
            CodeView.Editor.Focus();
            CodeView.Editor.SelectionStart = Math.Min(_Settings.CaretIndex, CodeView.Editor.TextLength);
        }
        finally
        {
            _IgnorePreviewSelection = false;
        }
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
        Preview.CanMoveNode = CanMovePreviewNode;
        Preview.CreateNodeMoveSnap = CreateNodeMoveSnap;
        Preview.NodeMoveRequested += (Id, Drop) => MovePreviewNode(Id, Drop);
        Preview.CreateRelationshipMove = CreateRelationshipMove;
        Preview.EndpointMoveRequested += (Move, SourceEnd, Target) => MovePreviewRelationship(Move, SourceEnd, Target);
        Preview.RelationshipCommandsVisible = () => LiveEditorFeatureGate.CaptureLayoutOptions().EnableIncrementalRouting;
        Preview.RelationshipAdds = RelationshipAddChoices;
        Preview.DeleteRelationshipAllowed = CanDeletePreviewRelationship;
        Preview.DeleteRelationshipRequested += () => DeletePreviewRelationship();
        Deactivate += (_, _) => Preview.CancelNodeMove();
        Application.AddMessageFilter(_PreviewHoverDismiss);
        ElementToolbar.BindSource(CurrentEditorText);
        CodeView.Editor.TextChanged += Editor_TextChanged;
        CodeView.Editor.SelectionChanged += Editor_SelectionChanged;
        CodeView.ColoursRefreshing += CodeView_ColoursRefreshing;
        CodeView.StatusRequested += CodeView_StatusRequested;
        CodeView.FontSizeChanged += CodeView_FontSizeChanged;
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
        Preview.NavigationEnabled = LiveEditorFeatureGate.L_ZoomPanPreview;
        ToggleDiagnosticsMenuItem.Enabled = LiveEditorFeatureGate.X_DiagnosticsPanel;
        ToggleMinimapMenuItem.Enabled = LiveEditorFeatureGate.Y_Minimap;
        ToggleLegacyPreviewMenuItem.Enabled = LiveEditorFeatureGate.S_LegacyPreview;
        ExportPngMenuItem.Enabled = LiveEditorFeatureGate.M_ExportPngSvg;
        ExportSvgMenuItem.Enabled = LiveEditorFeatureGate.M_ExportPngSvg;
        RecentFilesMenuItem.Enabled = LiveEditorFeatureGate.U_RecentFiles;
        AzureMenuItem.Enabled = LiveEditorFeatureGate.AE_AzureResourceGraphImport;
        SamplesMenu.Enabled = LiveEditorFeatureGate.K_SampleGallery;
        AutoLayoutMenuItem.Visible = LiveEditorFeatureGate.AF_IncrementalDiagramRouting;
    }

    private void ApplySettings()
    {
        Preview.Zoom = _Settings.PreviewZoom;
        Preview.FitToWindow = _Settings.PreviewFitToWindow;
        Preview.SelectionColour = SessionSettings.ParseColour(_Settings.SelectionColour);
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
        LiveEditorFeatureGate.ApplyLayoutSettings(_Settings);
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

        _EditorTextVersion++;
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
        SyncPreviewSelectionFromCaret();
    }

    private void Editor_MouseDoubleClick(object? Sender, MouseEventArgs E)
    {
        if (E.Button != MouseButtons.Left || _IgnorePreviewSelection || _SyncingSelectionFromPreview || CodeView.IsApplyingFormatting || Preview.Index == null || !PreviewIndexIsCurrent)
        {
            return;
        }

        PreviewObject? AtCaret = PreviewObjectAtCaret();
        if (AtCaret == null)
        {
            return;
        }

        _CaretAtPreviewClear = -1;
        _FollowPreviewFromCaret = true;
        if (!ReferenceEquals(AtCaret, CurrentPreviewItem()))
        {
            _IgnorePreviewSelection = true;
            try
            {
                Preview.SelectedItems = [AtCaret];
            }
            finally
            {
                _IgnorePreviewSelection = false;
            }
        }

        Preview.ScrollItemIntoView(AtCaret, Centre: true);
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
            EnterpriseParseResult Result = EnterpriseParser.ParseDocument(CodeView.Editor.Text);
            foreach (EnterpriseDiagnostic Diagnostic in Result.Diagnostics)
            {
                _Diagnostics.Add(Diagnostic);
                DiagnosticsList.Items.Add(new DiagnosticListItem(Diagnostic));
            }
        }

        if (LiveEditorFeatureGate.E_InlineDiagnostics)
        {
            List<int> ErrorLines = [];
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
        Preview.CancelNodeMove();
        int Revision = Interlocked.Increment(ref _RenderGeneration);
        _RenderCancellation?.Cancel();
        _RetainedPreview.Observe(Revision);
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
        bool LegacyPreview = LiveEditorFeatureGate.S_LegacyPreview && _Settings.LegacyPreview;
        DiagramLayoutOptions Options = LiveEditorFeatureGate.CaptureLayoutOptions();
        if (_SessionLayoutOptions != Options || _SessionLegacyPreview != LegacyPreview)
        {
            ResetRetainedPreview("Layout options or renderer changed.");
            _SessionLayoutOptions = Options; _SessionLegacyPreview = LegacyPreview;
        }
        _RenderCancellation?.Cancel();
        _RenderCancellation?.Dispose();
        _RenderCancellation = new CancellationTokenSource();
        var Request = new RenderRequest(Source, _RenderGeneration, _EditorTextVersion, LegacyPreview,
            Options, _RetainedPreview, _RenderCancellation.Token);
        if (!Options.EnableIncrementalRouting && (string.IsNullOrWhiteSpace(Source) || IsCommentOnly(Source)))
        {
            lock (_RenderWorkerGate) { _PendingRender = null; }
            _DiagramMetadata = DiagramMetadata.Empty(_RenderGeneration);
            _DiagramMetadata.ReportProgress(0, "Idle");
            SetStatus("Idle");
            return;
        }
        bool StartWorker;
        lock (_RenderWorkerGate)
        {
            _PendingRender = Request;
            StartWorker = !_RenderWorkerRunning;
            _RenderWorkerRunning = true;
        }
        SetStatus(CaretStatusOr("Rendering..."));
        if (StartWorker) Task.Run(RenderWorkerLoop);
    }

    private void ResetRetainedPreview(string Reason)
    {
        _RetainedPreview.Invalidate(Reason);
        _RetainedPreview = new();
        _RetainedPreview.Observe(_RenderGeneration);
    }

    internal void AutoLayout()
    {
        ResetRetainedPreview("Explicit Auto layout.");
        QueueRender();
    }

    private void AutoLayoutMenuItem_Click(object? Sender, EventArgs E) => AutoLayout();

    private void RenderWorkerLoop()
    {
        while (true)
        {
            RenderRequest Request;
            lock (_RenderWorkerGate)
            {
                if (_PendingRender == null) { _RenderWorkerRunning = false; return; }
                Request = _PendingRender; _PendingRender = null;
            }
            try { RenderWorkerPass(Request); }
            catch (OperationCanceledException) { }
        }
    }

    private void RenderWorkerPass(RenderRequest Request)
    {
        Stopwatch Timer = Stopwatch.StartNew();
        int Generation = Request.Generation;
        CancellationToken Token = Request.Token;
        bool Retained = Request.Options.EnableIncrementalRouting && !Request.LegacyPreview
            && EnterpriseParser.IsEnterpriseSource(Request.Source);
        try
        {
            Token.ThrowIfCancellationRequested();
            EnterpriseAnalysisResult? Analysis = EnterpriseParser.IsEnterpriseSource(Request.Source)
                ? EnterpriseValidator.Analyze(Request.Source) : null;
            Retained = Retained && Analysis?.Document.LayoutMode != MermaidRecognition.Config.DiagramLayoutMode.Open;
            DiagramMetadata Metadata = Analysis != null ? DiagramMetadata.BuildFromAnalysis(Analysis, Generation)
                : DiagramMetadata.Build(Request.Source, Generation);
            BeginInvoke(() => PublishDiagramMetadata(Metadata));
            Token.ThrowIfCancellationRequested();
            string? Svg, Failure = null, Advisory;
            byte[]? Png;
            RetainedEnterprisePreview.Prepared? Candidate = null;
            if (Retained)
            {
                Candidate = Request.Retained.Prepare(Request.Source, Generation, Analysis!, Request.Options);
                Svg = Candidate.Svg;
                Png = MermaidPreviewRender.RasterizeAcceptedSvg(Svg, out Advisory);
                if (Png == null || Png.Length == 0) Failure = "The retained drawing could not be rasterised.";
                if (Candidate.RebuildReason != null) Advisory = "Layout rebuilt: " + Candidate.RebuildReason;
            }
            else
            {
                Preview.PrepareImage(Request.Source, Analysis, Request.LegacyPreview, Request.Options,
                    out Svg, out Png, out Failure, out Advisory);
                if (Request.Options.EnableIncrementalRouting && !Request.LegacyPreview
                    && Analysis?.Document.LayoutMode == MermaidRecognition.Config.DiagramLayoutMode.Open && Failure == null)
                    Advisory = "Incremental routing requires grid layout; showing the open-layout preview.";
            }
            Token.ThrowIfCancellationRequested();
            if (Failure != null || Png == null || string.IsNullOrWhiteSpace(Svg))
                throw new InvalidOperationException(Failure ?? "Diagram preview failed.");
            // Publication, including the source index and export bytes, belongs to one
            // current UI revision. Debounced edits already supersede a running request.
            Invoke(() =>
            {
                if (Token.IsCancellationRequested || Generation != _RenderGeneration || IsDisposed
                    || Request.TextVersion != _EditorTextVersion || Request.Retained != _RetainedPreview) return;
                _IgnorePreviewSelection = true;
                try
                {
                    PublishDiagramMetadata(Metadata);
                    Preview.UseLegacyRenderer = Request.LegacyPreview;
                    if (!Preview.LoadAccepted(Request.Source, Svg, Png, Advisory, Candidate is { Rebuild: false },
                        () => Candidate == null || Request.Retained.TryCommit(Candidate))) return;
                    if (Candidate == null && Request.Options.EnableIncrementalRouting && !Request.LegacyPreview)
                        ResetRetainedPreview("Diagram kind or layout mode changed.");
                    LastRetainedUpdateKind = Candidate?.Kind;
                    LastRebuiltRouteIds = Candidate?.RebuiltRouteIds ?? [];
                    _RenderTextVersion = Request.TextVersion;
                    CompleteRender(Generation, Timer.ElapsedMilliseconds, Request.LegacyPreview);
                }
                finally { _IgnorePreviewSelection = false; }
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception Ex)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() =>
                {
                    if (!Token.IsCancellationRequested && Request.TextVersion == _EditorTextVersion)
                        ApplyRenderFailure(Generation, Request.Options.EnableIncrementalRouting && !Request.LegacyPreview
                                ? "Preview stale: " + Ex.Message : Ex.Message,
                            Timer.ElapsedMilliseconds, Request.Options.EnableIncrementalRouting && !Request.LegacyPreview);
                });
        }
    }

    private void CompleteRender(int Generation, long ElapsedMilliseconds, bool LegacyPreview)
    {
        if (Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        if (Preview.Image == null)
        {
            ApplyRenderFailure(Generation, string.IsNullOrWhiteSpace(Preview.RenderFailure) ? "Diagram preview failed with no detail." : Preview.RenderFailure, ElapsedMilliseconds);
            return;
        }

        try
        {
            _LastPngBytes = Preview.LastPreviewPngBytes ?? CopyPng(Preview.Image);
            _LastSvg = Preview.RenderedSvg;
            _PreviewIndexTextVersion = _RenderTextVersion;
            if (_ClearPreviewSelectionOnNextRender)
            {
                _ClearPreviewSelectionOnNextRender = false;
                _IgnorePreviewSelection = true;
                try
                {
                    Preview.SelectedItems = null;
                    _CaretAtPreviewClear = -1;
                    HidePreviewHover();
                    UpdateSelectionPanel();
                }
                finally
                {
                    _IgnorePreviewSelection = false;
                }
            }
            else
            {
                RestorePreviewSelection();
            }

            _BrokenSince = null;
            if (LegacyPreview)
            {
                Preview.ShowBanner("Legacy Naiad preview", 28);
            }
            else if (!string.IsNullOrWhiteSpace(Preview.PreviewAdvisory))
            {
                Preview.ShowBanner(Preview.PreviewAdvisory, 36);
            }
            else
            {
                Preview.HideBanner();
            }

            ReportRenderProgressNow(Generation, 100, "Done");
            SetStatus(CaretStatusOr("OK"));
            UpdateRenderTiming(ElapsedMilliseconds);
            UpdateCaretDiagnosticStatus();
        }
        catch (Exception Ex)
        {
            ApplyRenderFailure(Generation, Ex.Message, ElapsedMilliseconds);
        }
        finally
        {
            _IgnorePreviewSelection = false;
        }
    }

    private static byte[] CopyPng(Image Picture)
    {
        using MemoryStream Stream = new MemoryStream();
        using Bitmap Copy = new Bitmap(Picture);
        Copy.Save(Stream, ImageFormat.Png);
        return Stream.ToArray();
    }

    private void ApplyRenderFailure(int Generation, string Message, long ElapsedMilliseconds, bool RetainAccepted = false)
    {
        if (Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        if (_DiagramMetadata.Generation == Generation)
        {
            _DiagramMetadata.ReportFailure(TruncateStatus(Message));
        }

        _BrokenSince ??= DateTime.Now;
        if (!RetainAccepted && !LiveEditorFeatureGate.Q_KeepLastGoodPreview)
        {
            _IgnorePreviewSelection = true;
            try
            {
                Preview.Clear();
            }
            finally
            {
                _IgnorePreviewSelection = false;
            }

            HidePreviewHover();
            UpdateSelectionPanel();
        }

        string Time = _BrokenSince.Value.ToString("T", CultureInfo.CurrentCulture);
        bool SourceReady = Message.Contains("editor is complete", StringComparison.OrdinalIgnoreCase)
            || Message.Contains("Mermaid source in the editor", StringComparison.OrdinalIgnoreCase);
        string BannerText = SourceReady
            ? TruncateStatus(Message)
            : $"Broken since {Time}: {TruncateStatus(Message)}";
        Preview.ShowBanner(BannerText, SourceReady ? 40 : 32);
        SetStatus(CaretStatusOr(SourceReady ? "Source ready; preview could not be shown." : TruncateStatus(Message)));
        UpdateRenderTiming(ElapsedMilliseconds);
        UpdateCaretDiagnosticStatus();
    }

    /// <summary>
    /// Gets the metadata for the diagram in the editor: its type, nodes and relationships, and how far its render has got.
    /// </summary>
    internal DiagramMetadata DiagramMetadata
    {
        get
        {
            return _DiagramMetadata;
        }
    }

    /// <summary>Raised on the UI thread when new diagram metadata replaces the old.</summary>
    internal event EventHandler? DiagramMetadataChanged;

    private void PublishDiagramMetadata(DiagramMetadata Metadata)
    {
        if (Metadata.Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        _DiagramMetadata = Metadata;
        Metadata.ReportProgress(5, "Parsed");
        DiagramMetadataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReportRenderProgress(int Generation, int Percent, string Stage)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() => ReportRenderProgressNow(Generation, Percent, Stage));
    }

    private void ReportRenderProgressNow(int Generation, int Percent, string Stage)
    {
        if (Generation != _RenderGeneration || _DiagramMetadata.Generation != Generation || IsDisposed)
        {
            return;
        }

        _DiagramMetadata.ReportProgress(Percent, Stage);
    }

    private void UpdateRenderTiming(long ElapsedMilliseconds)
    {
        TimingLabel.Text = LiveEditorFeatureGate.R_RenderTiming ? $"{ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty;
    }

    private void Preview_PictureMouseMove(object? Sender, MouseEventArgs E)
    {
        if (Preview.IsMovingNode || Preview.IsMovingEndpoint) { HidePreviewHover(); return; }
        if (!Preview.HasPicture || Preview.Index == null)
        {
            HidePreviewHover();
            return;
        }

        string? Text = Preview.HitText(E.Location);
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
        _PreviewHoverWindow.ShowNear(this, Text, Preview.PictureToScreen(E.Location));
    }

    private void Preview_PictureMouseLeave(object? Sender, EventArgs E)
    {
        HidePreviewHover();
    }

    private void Preview_EscapePressed(object? Sender, EventArgs E)
    {
        SelectPreviewObject(null, SyncEditor: false);
    }

    private void Preview_LineStepRequested(object? Sender, KeyEventArgs E)
    {
        int Direction = E.KeyCode == Keys.Up ? -1 : 1;
        if (!TryStepEditorLine(Direction))
        {
            return;
        }

        SyncPreviewSelectionFromCaret(false);
    }

    /// <summary>
    /// Moves the editor caret one line. The column is kept, and a selected line is left from its end.
    /// </summary>
    /// <param name="Direction">-1 for the previous line, 1 for the next.</param>
    /// <returns>False when there is no further line.</returns>
    private bool TryStepEditorLine(int Direction)
    {
        RichTextBox Editor = CodeView.Editor;
        string Text = Editor.Text;
        if (Text.Length == 0)
        {
            return false;
        }

        int End = Editor.SelectionStart;
        if (Editor.SelectionLength > 0)
        {
            End = Editor.SelectionStart + Editor.SelectionLength;
            if (End > Editor.SelectionStart && End <= Text.Length && (Text[End - 1] == '\n' || Text[End - 1] == '\r'))
            {
                End--;
            }
        }

        int Probe = Math.Max(0, Math.Min(End, Text.Length - 1));
        int Line = Editor.GetLineFromCharIndex(Probe);
        int LineStart = Editor.GetFirstCharIndexFromLine(Line);
        int Column = Math.Max(0, End - LineStart);
        int Target = Line + Direction;
        if (Target < 0)
        {
            return false;
        }

        int TargetStart = Editor.GetFirstCharIndexFromLine(Target);
        if (TargetStart < 0)
        {
            return false;
        }

        int NextStart = Editor.GetFirstCharIndexFromLine(Target + 1);
        int TargetLength = NextStart < 0 ? Text.Length - TargetStart : NextStart - TargetStart;
        while (TargetLength > 0 && (Text[TargetStart + TargetLength - 1] == '\n' || Text[TargetStart + TargetLength - 1] == '\r'))
        {
            TargetLength--;
        }

        int NewIndex = TargetStart + Math.Min(Column, TargetLength);
        _SyncingSelectionFromPreview = true;
        try
        {
            Editor.Select(NewIndex, 0);
            Editor.ScrollToCaret();
        }
        finally
        {
            _SyncingSelectionFromPreview = false;
        }

        return true;
    }

    private void Preview_SelectedItemsChanged(object? Sender, EventArgs E)
    {
        UpdateSelectionPanel();
        PreviewObject? Selected = CurrentPreviewItem();
        RememberRelationshipSelection(Selected);
        if (_IgnorePreviewSelection || Selected == null || Selected.LineNumber < 1 || !PreviewIndexIsCurrent)
        {
            if (Selected == null)
            {
                _CaretAtPreviewClear = CodeView.Editor.SelectionStart;
            }

            return;
        }

        string Text = CodeView.Editor.Text;
        if (!PreviewSelection.TryGetLineSelection(Text, Selected.LineNumber, out int Start, out int Length))
        {
            return;
        }

        _SyncingSelectionFromPreview = true;
        try
        {
            CodeView.Editor.Select(Start, Length);
            CodeView.Editor.ScrollToCaret();
        }
        finally
        {
            _SyncingSelectionFromPreview = false;
        }
    }

    private void Preview_RefreshRequested(object? Sender, EventArgs E)
    {
        QueueRender();
    }

    private void Preview_ZoomChanged(object? Sender, EventArgs E)
    {
        _Settings.PreviewZoom = Preview.Zoom;
        _Settings.PreviewFitToWindow = Preview.FitToWindow;
        if (Preview.FitToWindow)
        {
            SetStatus("Preview fitted to window");
            return;
        }

        SetStatus($"Preview zoom {Math.Round(Preview.Zoom * 100.0).ToString(CultureInfo.InvariantCulture)}%");
    }

    /// <summary>Gets the selected preview object, or null when nothing is selected.</summary>
    internal PreviewObject? PreviewSelectionObject
    {
        get
        {
            return CurrentPreviewItem();
        }
    }

    /// <summary>Gets the hit index for the picture in the preview.</summary>
    internal PreviewHoverIndex? PreviewIndex
    {
        get
        {
            return Preview.Index;
        }
    }

    /// <summary>Gets whether the preview index was built from the text now in the editor.</summary>
    internal bool PreviewIndexIsCurrent
    {
        get
        {
            return Preview.Index != null && _PreviewIndexTextVersion == _EditorTextVersion;
        }
    }

    /// <summary>
    /// Selects a preview object and selects its declaration in the editor.
    /// </summary>
    /// <param name="Selected">Object to select, or null to clear the selection.</param>
    internal void SelectPreviewObject(PreviewObject? Selected)
    {
        SelectPreviewObject(Selected, SyncEditor: true);
    }

    /// <summary>
    /// Does what a left click at a point in the preview picture does.
    /// </summary>
    /// <param name="ClientPoint">Point in picture-box client pixels.</param>
    internal void ClickPreview(Point ClientPoint)
    {
        Preview.FocusForKeys();
        SelectPreviewObject(Preview.HitItem(ClientPoint), SyncEditor: true);
    }

    /// <summary>
    /// Does what Esc does while the preview has focus.
    /// </summary>
    internal void PressPreviewEscape()
    {
        Preview.PressEscape();
    }

    /// <summary>
    /// Returns the object under a point in the preview picture.
    /// </summary>
    /// <param name="ClientX">Pointer position in the picture box.</param>
    /// <param name="ClientY">Pointer position in the picture box.</param>
    /// <returns>The object, or null for empty space.</returns>
    internal PreviewObject? HitPreviewObject(int ClientX, int ClientY)
    {
        return Preview.HitItem(new Point(ClientX, ClientY));
    }

    private PreviewObject? CurrentPreviewItem()
    {
        IReadOnlyList<PreviewObject>? Items = Preview.SelectedItems;
        if (Items == null || Items.Count == 0)
        {
            return null;
        }

        return Items[Items.Count - 1];
    }

    private void SelectPreviewObject(PreviewObject? Selected, bool SyncEditor)
    {
        _IgnorePreviewSelection = !SyncEditor;
        try
        {
            if (Selected == null)
            {
                Preview.SelectedItems = null;
            }
            else
            {
                Preview.SelectedItems = [Selected];
            }
        }
        finally
        {
            _IgnorePreviewSelection = false;
        }
    }

    private PreviewObject? PreviewObjectAtCaret()
    {
        if (Preview.Index == null)
        {
            return null;
        }

        string Text = CodeView.Editor.Text;
        int Line = PreviewSelection.LineNumberAt(Text, CodeView.Editor.SelectionStart);
        PreviewObject? AtLine = Preview.Index.ObjectAtLine(Line);
        if (AtLine != null && PreviewIndexIsCurrent)
        {
            return AtLine;
        }

        PreviewObject? ByDeclaration = DrawnObjectForLine(Text, Line);
        if (ByDeclaration != null)
        {
            return ByDeclaration;
        }

        return AtLine;
    }

    private PreviewObject? DrawnObjectForLine(string Text, int Line)
    {
        if (Preview.Index == null || Line < 1 || !EnterpriseParser.IsEnterpriseSource(Text))
        {
            return null;
        }

        EnterpriseDocument Document = DocumentForCaret(Text);
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.LineNumber != Line || Relationship.HiddenByNesting)
            {
                continue;
            }

            string Kind = EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind);
            for (int Occurrence = 0; ; Occurrence++)
            {
                PreviewObject? Match = Preview.Index.FindObject("line\u001f" + Relationship.SourceId + "\u001f" + Relationship.TargetId + "\u001f" + Kind + "\u001f" + Occurrence.ToString(CultureInfo.InvariantCulture));
                if (Match == null)
                {
                    break;
                }

                if (Match.LineNumber == 0 || Match.LineNumber == Line || !PreviewIndexIsCurrent)
                {
                    return Match;
                }
            }
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (Element.LineNumber == Line)
            {
                return Preview.Index.FindObject("node\u001f" + Element.Id);
            }
        }

        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            if (Group.LineNumber == Line)
            {
                return Preview.Index.FindObject("node\u001f" + Group.Id);
            }
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            if (Note.LineNumber == Line)
            {
                return Preview.Index.FindObject("node\u001f" + Note.Id);
            }
        }

        return null;
    }

    private EnterpriseDocument DocumentForCaret(string Text)
    {
        if (_CaretDocument != null && _CaretDocumentVersion == _EditorTextVersion)
        {
            return _CaretDocument;
        }

        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(Text);
        _CaretDocument = Parsed.Document;
        _CaretDocumentVersion = _EditorTextVersion;
        return _CaretDocument;
    }

    private void SyncPreviewSelectionFromCaret()
    {
        SyncPreviewSelectionFromCaret(true);
    }

    private void SyncPreviewSelectionFromCaret(bool RequireEditorFocus)
    {
        if (_IgnorePreviewSelection || _SyncingSelectionFromPreview || (RequireEditorFocus && !CodeView.Editor.Focused) || CodeView.IsApplyingFormatting || Preview.Index == null || !PreviewIndexIsCurrent)
        {
            return;
        }

        if (_CaretAtPreviewClear >= 0 && CodeView.Editor.SelectionStart == _CaretAtPreviewClear)
        {
            return;
        }

        _CaretAtPreviewClear = -1;
        _FollowPreviewFromCaret = true;
        PreviewObject? AtCaret = PreviewObjectAtCaret();
        if (AtCaret == null)
        {
            return;
        }

        if (!ReferenceEquals(AtCaret, CurrentPreviewItem()))
        {
            _IgnorePreviewSelection = true;
            try
            {
                Preview.SelectedItems = [AtCaret];
            }
            finally
            {
                _IgnorePreviewSelection = false;
            }
        }

        Preview.ScrollItemIntoView(AtCaret, Centre: false);
    }

    private void ClearPreviewSelectionForNewContent()
    {
        ResetRetainedPreview("Document changed.");
        RenderTimer.Stop();
        _RenderCancellation?.Cancel();
        _RenderCancellation?.Dispose();
        _RenderCancellation = null;
        Interlocked.Increment(ref _RenderGeneration);
        _LastPngBytes = null;
        _LastSvg = null;
        _BrokenSince = null;
        _ClearPreviewSelectionOnNextRender = true;
        _FollowPreviewFromCaret = false;
        _CaretAtPreviewClear = -1;
        _SelectedOccurrences.Clear();
        _IgnorePreviewSelection = true;
        try
        {
            Preview.HideBanner();
            Preview.Clear();
            HidePreviewHover();
            UpdateSelectionPanel();
        }
        finally
        {
            _IgnorePreviewSelection = false;
        }
    }

    private void RestorePreviewSelection()
    {
        List<PreviewObject> Restored = [];
        if (Preview.SelectedItems != null)
        {
            foreach (PreviewObject Item in Preview.SelectedItems)
            {
                PreviewObject? Found = Preview.FindItem(Item.Key);
                if (Found != null)
                {
                    Restored.Add(Found);
                }
            }
        }

        if (RestoredRelationshipSelection() is { } Relationship)
        {
            Restored.Clear(); Restored.Add(Relationship);
        }

        if (_FollowPreviewFromCaret && CodeView.Editor.Focused && !HasRelationshipCommandSelection)
        {
            PreviewObject? AtCaret = PreviewObjectAtCaret();
            if (AtCaret != null)
            {
                Restored.Clear();
                Restored.Add(AtCaret);
            }
        }

        _IgnorePreviewSelection = true;
        try
        {
            Preview.SelectedItems = Restored;
        }
        finally
        {
            _IgnorePreviewSelection = false;
        }
    }

    private void UpdateSelectionPanel()
    {
        if (IsDisposed)
        {
            return;
        }

        PreviewObject? Selected = CurrentPreviewItem();
        if (Selected == null || Preview.Index == null)
        {
            SelectionDetails.ShowNone();
            EnsureFloatingSelectionVisible();
            return;
        }

        string Source = CodeView.Editor.Text;
        if (!EnterpriseParser.IsEnterpriseSource(Source))
        {
            SelectionDetails.ShowObject(Selected.Id.Length == 0 ? "Selection" : Selected.Id, "This diagram has no enterprise description.", CropSelection(Selected), Selected.LineNumber, ConnectionFacts(Selected, null), string.Empty);
            EnsureFloatingSelectionVisible();
            return;
        }

        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(Source);
        string Title;
        string Description;
        string Kebab = string.Empty;
        if (Selected.Kind == PreviewObjectKind.Relationship)
        {
            string KindName = string.Equals(Selected.RelationshipKind, "connection", StringComparison.Ordinal) ? string.Empty : Selected.RelationshipKind;
            Description = ElementStory.DescribeRelationship(Parsed.Document, Selected.SourceId, Selected.TargetId, KindName);
            Title = Description;
        }
        else if (Parsed.Document.Elements.TryGetValue(Selected.Id, out EnterpriseElement? Element))
        {
            Description = ElementStory.DescribeElement(Parsed.Document, Element);
            Title = string.IsNullOrWhiteSpace(Element.Label) ? Element.Id : Element.Label;
            Kebab = EnterpriseElementCatalog.GetSourceToken(Element);
        }
        else
        {
            Title = Selected.Id;
            Description = Selected.Id;
        }

        SelectionDetails.ShowObject(Title, Description, CropSelection(Selected), Selected.LineNumber, ConnectionFacts(Selected, Parsed.Document), Kebab);
        EnsureFloatingSelectionVisible();
    }

    /// <summary>
    /// Builds the short facts line under the editor line. Relationships name the two connected nodes; they do not describe geometric attach points.
    /// </summary>
    private string ConnectionFacts(PreviewObject Selected, EnterpriseDocument? Document)
    {
        if (Selected.Kind == PreviewObjectKind.Relationship)
        {
            string SourceName = NamedEndpoint(Document, Selected.SourceId);
            string TargetName = NamedEndpoint(Document, Selected.TargetId);
            return "Connects " + SourceName + " to " + TargetName;
        }

        int Count = PreviewSelection.ConnectionPoints(Selected).Count;
        string Points;
        if (Selected.IsJunction)
        {
            Points = "Junctions use angular connection points on the circle.";
        }
        else if (Count == 0)
        {
            Points = "No connection points.";
        }
        else
        {
            Points = Count.ToString(CultureInfo.InvariantCulture) + " connection points, spaced from the centre of each side.";
        }

        return Selected.Id + ". " + Points;
    }

    private static string NamedEndpoint(EnterpriseDocument? Document, string Id)
    {
        if (Document != null && Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            if (!string.IsNullOrWhiteSpace(Element.Label))
            {
                return Element.Label;
            }
        }

        return Id;
    }

    private Image? CropSelection(PreviewObject Selected)
    {
        Image? Picture = Preview.Picture;
        if (Picture == null || Preview.Index == null || Preview.Index.Width <= 0 || Preview.Index.Height <= 0)
        {
            return null;
        }

        if (Selected.Right <= Selected.Left || Selected.Bottom <= Selected.Top)
        {
            return null;
        }

        double ScaleX = Picture.Width / Preview.Index.Width;
        double ScaleY = Picture.Height / Preview.Index.Height;
        int Left = (int)Math.Floor((Selected.Left - 6) * ScaleX);
        int Top = (int)Math.Floor((Selected.Top - 6) * ScaleY);
        int Right = (int)Math.Ceiling((Selected.Right + 6) * ScaleX);
        int Bottom = (int)Math.Ceiling((Selected.Bottom + 6) * ScaleY);
        if (Left < 0)
        {
            Left = 0;
        }

        if (Top < 0)
        {
            Top = 0;
        }

        if (Right > Picture.Width)
        {
            Right = Picture.Width;
        }

        if (Bottom > Picture.Height)
        {
            Bottom = Picture.Height;
        }

        int Width = Right - Left;
        int Height = Bottom - Top;
        if (Width < 2 || Height < 2)
        {
            return null;
        }

        Bitmap Crop = new Bitmap(Width, Height);
        using (Graphics Canvas = Graphics.FromImage(Crop))
        {
            Canvas.DrawImage(Picture, new Rectangle(0, 0, Width, Height), new Rectangle(Left, Top, Width, Height), GraphicsUnit.Pixel);
        }

        return Crop;
    }

    /// <summary>
    /// Replaces the declaration line of an item. The item does not need to be selected.
    /// </summary>
    /// <param name="Item">Item whose declaration line is replaced.</param>
    /// <param name="Declaration">The new declaration line, without a line break.</param>
    /// <returns>False when the source is left unchanged.</returns>
    internal bool ReplacePreviewItem(PreviewObject? Item, string? Declaration)
    {
        if (Item == null)
        {
            return false;
        }

        return ApplyDiagramText(PreviewItemEdit.ReplaceDeclaration(CodeView.Editor.Text, Item, Declaration));
    }

    /// <summary>
    /// Removes the declaration line of an item. The item does not need to be selected.
    /// </summary>
    /// <param name="Item">Item to remove.</param>
    /// <returns>False when the source is left unchanged.</returns>
    internal bool RemovePreviewItem(PreviewObject? Item)
    {
        if (Item == null)
        {
            return false;
        }

        return ApplyDiagramText(PreviewItemEdit.Remove(CodeView.Editor.Text, Item));
    }

    /// <summary>
    /// Changes the type of an element, or the kind of a relationship. The item does not need to be selected.
    /// </summary>
    /// <param name="Item">Item to change.</param>
    /// <param name="NewKebab">New element token, such as application-service, or a relationship kind, such as serving.</param>
    /// <param name="RemoveDisallowedRelationships">When true, relationships the new element type does not allow are removed.</param>
    /// <returns>False when the source is left unchanged.</returns>
    internal bool ChangePreviewItemType(PreviewObject? Item, string? NewKebab, bool RemoveDisallowedRelationships)
    {
        if (Item == null)
        {
            return false;
        }

        return ApplyDiagramText(PreviewItemEdit.ChangeType(CodeView.Editor.Text, Item, NewKebab, RemoveDisallowedRelationships));
    }

    private bool ApplyDiagramText(string Updated)
    {
        if (string.Equals(Updated, CodeView.Editor.Text, StringComparison.Ordinal))
        {
            return false;
        }

        CodeView.IntelliSense.Suppress = true;
        _IgnorePreviewSelection = true;
        try
        {
            CodeView.Editor.Text = Updated;
        }
        finally
        {
            CodeView.IntelliSense.Suppress = false;
            _IgnorePreviewSelection = false;
        }

        _IsDirty = true;
        UpdateTitle();
        QueueHighlight();
        QueueRender();
        return true;
    }

    private void SelectionDetails_ChangeTypeClicked(object? Sender, EventArgs E)
    {
        PreviewObject? Selected = CurrentPreviewItem();
        if (Selected == null || Selected.Kind == PreviewObjectKind.Relationship || SelectionDetails.SelectedKebab.Length == 0)
        {
            return;
        }

        if (!ChangePreviewItemType(Selected, SelectionDetails.SelectedKebab, SelectionDetails.RemoveDisallowedRelationships))
        {
            SetStatus("The element is already that type, or its declaration could not be changed.");
        }
    }

    private void MainForm_Deactivate(object? Sender, EventArgs E)
    {
        HidePreviewHover();
    }

    private void DismissPreviewHoverIfPointerLeft()
    {
        if (!_PreviewHoverWindow.Visible || Preview.IsPictureDisposed)
        {
            return;
        }

        Rectangle Area = Preview.PictureScreenRectangle();
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
        private const int WM_MOUSEWHEEL = 0x020A;

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
                return false;
            }

            if (Message.Msg == WM_MOUSEWHEEL)
            {
                return _Host.Preview.TryHandleMouseWheel(Message);
            }

            return false;
        }
    }


    private void ApplyWorkspaceLayout()
    {
        ElementToolbar.Dock = DockStyle.Top;
        EditorSplit.Dock = DockStyle.Fill;
        Preview.Dock = DockStyle.Fill;
        CodeView.Dock = DockStyle.Fill;
        SelectionDetails.Dock = DockStyle.None;
        SelectionDetails.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Control EditorParent = _Settings.EditorOnLeft ? EditorSplit.Panel1 : EditorSplit.Panel2;
        Control PreviewParent = _Settings.EditorOnLeft ? EditorSplit.Panel2 : EditorSplit.Panel1;
        if (!ReferenceEquals(CodeView.Parent, EditorParent))
        {
            EditorParent.Controls.Add(CodeView);
        }

        if (!ReferenceEquals(Preview.Parent, PreviewParent))
        {
            PreviewParent.Controls.Add(Preview);
        }

        if (!ReferenceEquals(SelectionDetails.Parent, Preview))
        {
            Preview.Controls.Add(SelectionDetails);
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
        else if (!ReferenceEquals(ElementToolbar.Parent, PreviewParent))
        {
            PreviewParent.Controls.Add(ElementToolbar);
        }

        PreviewParent.Controls.SetChildIndex(Preview, 0);
        if (ReferenceEquals(ElementToolbar.Parent, PreviewParent))
        {
            PreviewParent.Controls.SetChildIndex(ElementToolbar, PreviewParent.Controls.Count - 1);
        }

        _FloatingSelectionHost?.Resize -= PreviewHost_Resize;

        _FloatingSelectionHost = Preview;
        _FloatingSelectionHost.Resize += PreviewHost_Resize;
        LayoutFloatingSelectionPanel();
        EnsureFloatingSelectionVisible();

        EditorParent.Controls.SetChildIndex(CodeView, 0);
        ApplyEditorSplitFixedPane();
    }

    private void ApplyEditorSplitFixedPane()
    {
        EditorSplit.Panel1MinSize = MINIMUM_EDITOR_PANE_PIXELS;
        EditorSplit.Panel2MinSize = MINIMUM_EDITOR_PANE_PIXELS;
        EditorSplit.FixedPanel = _Settings.EditorOnLeft ? FixedPanel.Panel1 : FixedPanel.Panel2;
    }

    private void PreviewHost_Resize(object? Sender, EventArgs E)
    {
        LayoutFloatingSelectionPanel();
    }

    private void LayoutFloatingSelectionPanel()
    {
        if (_FloatingSelectionHost == null || SelectionDetails.Parent != _FloatingSelectionHost)
        {
            return;
        }

        int HostWidth = _FloatingSelectionHost.ClientSize.Width;
        int HostHeight = _FloatingSelectionHost.ClientSize.Height;
        if (HostWidth <= 0 || HostHeight <= 0)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(LayoutFloatingSelectionPanel);
            }

            return;
        }

        int MaxWidth = HostWidth - (FLOATING_SELECTION_MARGIN_PX * 2);
        int Width = Math.Min(FLOATING_SELECTION_WIDTH_PX, MaxWidth);
        Width = Math.Max(Width, SelectionDetails.MinimumSize.Width);
        int Height = SelectionDetails.PreferredFloatingHeight;
        SelectionDetails.Size = new Size(Width, Height);
        int Y = HostHeight - Height - FLOATING_SELECTION_MARGIN_PX;
        if (Y < FLOATING_SELECTION_MARGIN_PX)
        {
            Y = FLOATING_SELECTION_MARGIN_PX;
        }

        SelectionDetails.Location = new Point(FLOATING_SELECTION_MARGIN_PX, Y);
        SyncSelectionFitObstacle();
    }

    private void SyncSelectionFitObstacle()
    {
        Rectangle Obstacle = Rectangle.Empty;
        if (ReferenceEquals(SelectionDetails.Parent, Preview))
        {
            int Left = Math.Max(0, SelectionDetails.Left - FLOATING_SELECTION_MARGIN_PX);
            int Top = Math.Max(0, SelectionDetails.Top - FLOATING_SELECTION_MARGIN_PX);
            int Right = SelectionDetails.Right + FLOATING_SELECTION_MARGIN_PX;
            int Bottom = SelectionDetails.Bottom + FLOATING_SELECTION_MARGIN_PX;
            Obstacle = Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        Preview.SetFitObstacle(Obstacle);
    }

    private void EnsureFloatingSelectionVisible()
    {
        LayoutFloatingSelectionPanel();
        SelectionDetails.BringToFront();
        Preview.EnsureZoomButtonsOnTop();
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
        Preview.Fit();
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
            ExportDiagramFile(Dialog.FileName, Svg: false);
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
            _ = SvgForExport();
            using SaveFileDialog Dialog = new SaveFileDialog
            {
                Filter = "SVG image (*.svg)|*.svg",
                FileName = "diagram.svg",
                Title = "Export SVG"
            };
            if (Dialog.ShowDialog(this) == DialogResult.OK)
            {
                ExportDiagramFile(Dialog.FileName, Svg: true);
                SetStatus($"Exported {Path.GetFileName(Dialog.FileName)}");
            }
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Export SVG failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string SvgForExport()
    {
        if (LiveEditorFeatureGate.CaptureLayoutOptions().EnableIncrementalRouting && _LastSvg == null)
            throw new InvalidOperationException("No accepted diagram is available to export.");
        return _LastSvg ??= MermaidDiagramExport.RenderSvg(CodeView.Editor.Text);
    }

    internal void ExportDiagramFile(string Path, bool Svg)
    {
        if (Svg) File.WriteAllText(Path, SvgForExport());
        else File.WriteAllBytes(Path, _LastPngBytes ?? throw new InvalidOperationException("No rendered PNG is available."));
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
        Preview.ApplyChrome(_Settings.DarkTheme, Background, Panel);
    }

    private void ToggleSplitOrientationMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.P_SplitOrientation)
        {
            return;
        }

        EditorSplit.Orientation = EditorSplit.Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        _Settings.VerticalSplit = EditorSplit.Orientation == Orientation.Vertical;
        _EditorSplitDistanceApplied = false;
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
        Preview.SelectionColour = SessionSettings.ParseColour(_Settings.SelectionColour);
        _EditorSplitDistanceApplied = false;
        ApplyEditorPreferences();
        UpdateFeatureAvailability();
        ApplyWorkspaceLayout();
        _Settings.Save();
        QueueHighlight();
        QueueRender();
        CodeView.Editor.Focus();
        SetStatus("Settings saved");
    }

    private void AssignSampleSources()
    {
        for (int Index = 0; Index < SamplesMenu.DropDownItems.Count; Index++)
        {
            if (SamplesMenu.DropDownItems[Index] is not ToolStripMenuItem GroupMenu)
            {
                continue;
            }

            DiagramSampleGroup? Group = null;
            for (int GroupIndex = 0; GroupIndex < SampleCatalog.Groups.Count; GroupIndex++)
            {
                if (string.Equals(SampleCatalog.Groups[GroupIndex].MenuTitle, GroupMenu.Text, StringComparison.Ordinal))
                {
                    Group = SampleCatalog.Groups[GroupIndex];
                    break;
                }
            }

            if (Group == null)
            {
                continue;
            }

            for (int ItemIndex = 0; ItemIndex < GroupMenu.DropDownItems.Count; ItemIndex++)
            {
                if (GroupMenu.DropDownItems[ItemIndex] is not ToolStripMenuItem Item)
                {
                    continue;
                }

                if (Item.Name != null && Item.Name.EndsWith("SimpleMenuItem", StringComparison.Ordinal))
                {
                    Item.Tag = Group.SimpleSource;
                }
                else
                {
                    Item.Tag = Group.FullSource;
                }
            }
        }
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
        ClearPreviewSelectionForNewContent();
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

    internal void TrySaveSource(string SourcePath)
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
        _RetainedPreview.Invalidate("Editor closed.");
        _RenderCancellation?.Cancel();
        _RenderCancellation?.Dispose();
        _RenderCancellation = null;
        RenderTimer.Stop();
        CodeView.StopHighlight();
        DisposeWatcher();
        Application.RemoveMessageFilter(_PreviewHoverDismiss);
        HidePreviewHover();
        _PreviewHoverWindow.Dispose();
        Preview.Clear();
    }

    private void SaveSession()
    {
        if (!_PersistSession || !LiveEditorFeatureGate.T_PersistentSession)
        {
            return;
        }

        _Settings.CaretIndex = CodeView.Editor.SelectionStart;
        _Settings.PreviewZoom = Preview.Zoom;
        _Settings.PreviewFitToWindow = Preview.FitToWindow;
        _Settings.LastFilePath = _CurrentSourcePath;
        _Settings.LastCoArchiFolder = _CurrentCoArchiFolder;
        _Settings.VerticalSplit = EditorSplit.Orientation == Orientation.Vertical;
        _Settings.EditorPanePixels = GetEditorPanePixels();
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

        ClearPreviewSelectionForNewContent();
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

    private void AzureResourceGraphMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.AE_AzureResourceGraphImport || !ConfirmDiscardChanges())
        {
            return;
        }

        using AzureResourceGraphConnectionForm Connection = new AzureResourceGraphConnectionForm(_Settings.AzureTenantId, _Settings.AzureClientId);
        if (Connection.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _Settings.AzureTenantId = Connection.TenantId;
        _Settings.AzureClientId = Connection.ClientId;
        if (!AzureResourceGraphImportForm.TryShow(this, Connection.Records, "Azure resources", out AzureResourceGraphImportOptions? Options) || Options == null)
        {
            return;
        }

        if (!AzureResourceGraphInterop.TryImportRecords(Connection.Records, Options, out string Source, out string Warnings, out string Error))
        {
            MessageBox.Show(this, Error, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ApplyImportedSource(Source, null, null, Warnings, $"Imported Azure Resource Graph ({Connection.Records.Count:N0} resources retrieved)");
    }

    private void ImportAzureResourceGraphMenuItem_Click(object? Sender, EventArgs E)
    {
        if (!LiveEditorFeatureGate.AE_AzureResourceGraphImport)
        {
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        using OpenFileDialog Dialog = new OpenFileDialog
        {
            Filter = "Azure Resource Graph CSV (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import Azure Resource Graph export"
        };
        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!AzureResourceGraphImportForm.TryShow(this, Dialog.FileName, out AzureResourceGraphImportOptions? ImportOptions) || ImportOptions == null)
        {
            return;
        }

        ClearPreviewSelectionForNewContent();
        if (!AzureResourceGraphInterop.TryImportFile(Dialog.FileName, ImportOptions, out string MermaidSource, out string WarningSummary, out string ErrorMessage))
        {
            MessageBox.Show(this, ErrorMessage, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ApplyImportedSource(MermaidSource, Dialog.FileName, null, WarningSummary, "Imported Azure Resource Graph");
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

        ClearPreviewSelectionForNewContent();
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

        ClearPreviewSelectionForNewContent();
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
        ClearPreviewSelectionForNewContent();
        ElementToolbar.ClosePrompt();
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
