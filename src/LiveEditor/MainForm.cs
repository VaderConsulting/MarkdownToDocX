using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveEditor.Archi;
using LiveEditor.Completion;
using MDConverter;

namespace LiveEditor;

/// <summary>
/// Live Mermaid editor with native preview and Archi / coArchi file handoff.
/// </summary>
public partial class MainForm : Form
{
    private const string DEFAULT_SOURCE =
        "enterprise" + "\r\n"
        + "  title LiveEditor relationship suggestions" + "\r\n"
        + "  application-component webShop \"Web Shop\"" + "\r\n"
        + "  application-service orderApi \"Order API\"" + "\r\n"
        +         "  data-object order \"Order\"" + "\r\n"
        + "  webShop -";

    private string? _CurrentSourcePath;

    private string? _CurrentCoArchiFolder;

    private bool _IsDirty;

    private int _RenderGeneration;

    private CancellationTokenSource? _RenderCancellation;

    private Image? _LastGoodPreview;

    private readonly EnterpriseCompletionEngine _CompletionEngine = new EnterpriseCompletionEngine();

    private readonly CompletionPopup _CompletionPopup = new CompletionPopup();

    private bool _SuppressCompletion;

    private readonly bool _CapturePreviewOnShown;

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
    /// <param name="CapturePreviewOnShown">When true, saves a screenshot after opening the completion popup, then closes.</param>
    public MainForm(bool CapturePreviewOnShown)
    {
        _CapturePreviewOnShown = CapturePreviewOnShown;
        InitializeComponent();
        WireEvents();
        SourceEditor.Text = DEFAULT_SOURCE;
        SourceEditor.SelectionStart = SourceEditor.Text.Length;
        _IsDirty = false;
        UpdateTitle();
        QueueRender();
        Shown += MainForm_Shown;
    }

    private async void MainForm_Shown(object? Sender, EventArgs E)
    {
        SourceEditor.Focus();
        SourceEditor.SelectionStart = SourceEditor.Text.Length;
        RefreshCompletion(Force: true);
        if (!_CapturePreviewOnShown)
        {
            return;
        }

        await Task.Delay(500).ConfigureAwait(true);
        RefreshCompletion(Force: true);
        await Task.Delay(300).ConfigureAwait(true);
        string OutputPath = Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory,
            "liveeditor-completion-preview.png");
        try
        {
            WriteCompletionPreviewBitmap(OutputPath);
            Console.WriteLine("Wrote preview: " + OutputPath);
        }
        catch (Exception Ex)
        {
            Console.Error.WriteLine("Preview capture failed: " + Ex.Message);
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
        SourceEditor.KeyDown += SourceEditor_KeyDown;
        SourceEditor.PreviewKeyDown += SourceEditor_PreviewKeyDown;
        SourceEditor.MouseDown += SourceEditor_MouseDown;
        SourceEditor.LostFocus += SourceEditor_LostFocus;
        RenderTimer.Tick += RenderTimer_Tick;
        OpenMenuItem.Click += OpenMenuItem_Click;
        SaveMenuItem.Click += SaveMenuItem_Click;
        SaveAsMenuItem.Click += SaveAsMenuItem_Click;
        ExitMenuItem.Click += ExitMenuItem_Click;
        ImportExchangeMenuItem.Click += ImportExchangeMenuItem_Click;
        ImportArchimateMenuItem.Click += ImportArchimateMenuItem_Click;
        ExportExchangeMenuItem.Click += ExportExchangeMenuItem_Click;
        ExportArchimateMenuItem.Click += ExportArchimateMenuItem_Click;
        OpenCoArchiFolderMenuItem.Click += OpenCoArchiFolderMenuItem_Click;
        SaveCoArchiFolderMenuItem.Click += SaveCoArchiFolderMenuItem_Click;
        FormClosing += MainForm_FormClosing;
        _CompletionPopup.ItemAccepted += CompletionPopup_ItemAccepted;
    }

    private void SourceEditor_TextChanged(object? Sender, EventArgs E)
    {
        _IsDirty = true;
        UpdateTitle();
        QueueRender();
        if (!_SuppressCompletion)
        {
            RefreshCompletion();
        }
    }

    private void SourceEditor_PreviewKeyDown(object? Sender, PreviewKeyDownEventArgs E)
    {
        if (_CompletionPopup.IsVisible
            && (E.KeyCode == Keys.Tab || E.KeyCode == Keys.Enter || E.KeyCode == Keys.Up || E.KeyCode == Keys.Down || E.KeyCode == Keys.Escape))
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
        }
    }

    private void SourceEditor_MouseDown(object? Sender, MouseEventArgs E)
    {
        _CompletionPopup.Hide();
    }

    private void SourceEditor_LostFocus(object? Sender, EventArgs E)
    {
        // Keep the caret popup open while it is visible; ToolStripDropDown can briefly steal focus.
    }

    private void CompletionPopup_ItemAccepted(object? Sender, CompletionItem Item)
    {
        _SuppressCompletion = true;
        try
        {
            _IsDirty = true;
            UpdateTitle();
            QueueRender();
        }
        finally
        {
            _SuppressCompletion = false;
        }

        SetStatus("Inserted " + Item.DisplayText);
        BeginInvoke(RefreshCompletion);
    }

    private void RefreshCompletion()
    {
        RefreshCompletion(Force: false);
    }

    private void RefreshCompletion(bool Force)
    {
        if (IsDisposed || !SourceEditor.Focused && !Force)
        {
            if (!Force)
            {
                return;
            }
        }

        CompletionContext Context = _CompletionEngine.Build(SourceEditor.Text, SourceEditor.SelectionStart);
        if (Context.TokenKind == CompletionTokenKind.None)
        {
            _CompletionPopup.Hide();
            if (Force && !string.IsNullOrWhiteSpace(Context.StatusMessage))
            {
                SetStatus(Context.StatusMessage);
            }

            return;
        }

        if (Context.Items.Count == 0)
        {
            _CompletionPopup.Hide();
            if (!string.IsNullOrWhiteSpace(Context.StatusMessage))
            {
                SetStatus(TruncateStatus(Context.StatusMessage));
            }

            return;
        }

        _CompletionPopup.Show(SourceEditor, Context);
        SetStatus(Context.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " suggestion(s) - Tab/Enter to accept, Esc to dismiss");
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
        Task.Run(() => RenderWorker(Source, Generation, Token), Token);
    }

    private void RenderWorker(string Source, int Generation, CancellationToken Token)
    {
        try
        {
            Token.ThrowIfCancellationRequested();
            byte[]? PngBytes = MermaidDiagramExport.TryRenderPng(Source);
            Token.ThrowIfCancellationRequested();

            if (PngBytes == null || PngBytes.Length == 0)
            {
                BeginInvoke(() => ApplyRenderFailure(Generation, "Incomplete diagram"));
                return;
            }

            BeginInvoke(() => ApplyRenderSuccess(Generation, PngBytes));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception Ex)
        {
            BeginInvoke(() => ApplyRenderFailure(Generation, Ex.Message));
        }
    }

    private void ApplyRenderSuccess(int Generation, byte[] PngBytes)
    {
        if (Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        try
        {
            using MemoryStream Stream = new MemoryStream(PngBytes);
            Image Loaded = Image.FromStream(Stream);
            Image? Previous = PreviewImage.Image;
            PreviewImage.Image = new Bitmap(Loaded);
            Loaded.Dispose();
            if (!ReferenceEquals(Previous, _LastGoodPreview))
            {
                Previous?.Dispose();
            }

            _LastGoodPreview?.Dispose();
            _LastGoodPreview = PreviewImage.Image;
            SetStatus("OK");
        }
        catch (Exception Ex)
        {
            ApplyRenderFailure(Generation, Ex.Message);
        }
    }

    private void ApplyRenderFailure(int Generation, string Message)
    {
        if (Generation != _RenderGeneration || IsDisposed)
        {
            return;
        }

        SetStatus(TruncateStatus(Message));
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
        if (SingleLine.Length <= 200)
        {
            return SingleLine;
        }

        return SingleLine.Substring(0, 197) + "...";
    }

    private static bool IsCommentOnly(string Source)
    {
        using StringReader Reader = new StringReader(Source);
        string? Line;
        while ((Line = Reader.ReadLine()) != null)
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            if (!Trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
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
            SourceEditor.Text = File.ReadAllText(Dialog.FileName);
            _CurrentSourcePath = Dialog.FileName;
            _CurrentCoArchiFolder = null;
            _IsDirty = false;
            UpdateTitle();
            SetStatus("Opened " + Path.GetFileName(Dialog.FileName));
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        if (Dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        TrySaveSource(Dialog.FileName);
    }

    private void TrySaveSource(string Path)
    {
        try
        {
            File.WriteAllText(Path, SourceEditor.Text);
            _CurrentSourcePath = Path;
            _IsDirty = false;
            UpdateTitle();
            SetStatus("Saved " + System.IO.Path.GetFileName(Path));
        }
        catch (Exception Ex)
        {
            MessageBox.Show(this, Ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

        _RenderCancellation?.Cancel();
        _RenderCancellation?.Dispose();
        RenderTimer.Stop();
        _CompletionPopup.Dispose();
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
            Description = "Select a coArchi working-copy folder (contains model.archimate and .git)",
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
        SetStatus(string.IsNullOrEmpty(WarningSummary) ? "Saved coArchi model.archimate" : TruncateStatus(WarningSummary));
    }

    private void ApplyImportedSource(
        string MermaidSource,
        string? SourcePath,
        string? CoArchiFolder,
        string WarningSummary,
        string SuccessStatus)
    {
        SourceEditor.TextChanged -= SourceEditor_TextChanged;
        SourceEditor.Text = MermaidSource;
        SourceEditor.TextChanged += SourceEditor_TextChanged;
        _CurrentSourcePath = SourcePath;
        _CurrentCoArchiFolder = CoArchiFolder;
        _IsDirty = true;
        UpdateTitle();
        QueueRender();
        if (string.IsNullOrEmpty(WarningSummary))
        {
            SetStatus(SuccessStatus);
        }
        else
        {
            SetStatus(TruncateStatus(SuccessStatus + " - " + WarningSummary));
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_IsDirty)
        {
            return true;
        }

        DialogResult Result = MessageBox.Show(
            this,
            "Discard unsaved changes?",
            "LiveEditor",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        return Result == DialogResult.Yes;
    }

    private void UpdateTitle()
    {
        string Name = string.IsNullOrWhiteSpace(_CurrentSourcePath)
            ? "Untitled"
            : Path.GetFileName(_CurrentSourcePath);
        if (!string.IsNullOrWhiteSpace(_CurrentCoArchiFolder))
        {
            Name = "coArchi: " + Path.GetFileName(_CurrentCoArchiFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        Text = (_IsDirty ? "* " : string.Empty) + Name + " - LiveEditor";
    }
}
