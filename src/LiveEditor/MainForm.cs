using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveEditor.Archi;
using MDConverter;

namespace LiveEditor;

/// <summary>
/// Live Mermaid editor with native preview and Archi / coArchi file handoff.
/// </summary>
public partial class MainForm : Form
{
    private const string DEFAULT_SOURCE =
        "flowchart TD" + "\r\n"
        + "  Start([Start]) --> Decision{Ready?}" + "\r\n"
        + "  Decision -->|Yes| Work[Do work]" + "\r\n"
        + "  Decision -->|No| Wait[Wait]" + "\r\n"
        + "  Work --> Done([Done])" + "\r\n"
        + "  Wait --> Decision" + "\r\n";

    private string? _CurrentSourcePath;

    private string? _CurrentCoArchiFolder;

    private bool _IsDirty;

    private int _RenderGeneration;

    private CancellationTokenSource? _RenderCancellation;

    private Image? _LastGoodPreview;

    /// <summary>
    /// Creates the live editor window.
    /// </summary>
    public MainForm()
    {
        InitializeComponent();
        WireEvents();
        SourceEditor.Text = DEFAULT_SOURCE;
        _IsDirty = false;
        UpdateTitle();
        QueueRender();
    }

    private void WireEvents()
    {
        SourceEditor.TextChanged += SourceEditor_TextChanged;
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
    }

    private void SourceEditor_TextChanged(object? Sender, EventArgs E)
    {
        _IsDirty = true;
        UpdateTitle();
        QueueRender();
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
