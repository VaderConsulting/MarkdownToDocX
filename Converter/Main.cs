namespace Converter
{
    using System.IO;
    using System.Windows.Forms;
    using MDConverter;
    using MDConverter.Options;

    public partial class Main : Form
    {
        /// <summary>
        /// Creates a new instance of the main window.
        /// </summary>
        public Main()
        {
            InitializeComponent();

            AllowDrop = true;
            DragEnter += Main_DragEnter;
            DragDrop += Main_DragDrop;
            Start.Click += Start_Click;
        }

        private void Start_Click(object? Sender, EventArgs E)
        {
            string InputPath = Input.Text?.Trim() ?? string.Empty;
            string OutputPath = Output.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(InputPath))
            {
                MessageBox.Show(this, "Please provide an input Markdown filename.", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(InputPath))
            {
                MessageBox.Show(this, "Input file not found:\r\n" + InputPath, "Input not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                MessageBox.Show(this, "Please provide an output DOCX filename.", "Missing output", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                MarkdownToDocxOptions Options = new MarkdownToDocxOptions();
                MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options);
                Converter.ConvertFile(InputPath, OutputPath);

                MessageBox.Show(this, "Converted successfully:\r\n" + OutputPath, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (MarkdownToDocxException Ex)
            {
                MessageBox.Show(this, "Conversion error:\r\n" + Ex.Message, "Conversion failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception Ex)
            {
                MessageBox.Show(this, "Unexpected error:\r\n" + Ex.ToString(), "Conversion failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Main_DragEnter(object? Sender, DragEventArgs E)
        {
            if (E.Data == null)
            {
                E.Effect = DragDropEffects.None;
                return;
            }

            if (!E.Data.GetDataPresent(DataFormats.FileDrop))
            {
                E.Effect = DragDropEffects.None;
                return;
            }

            object? Data = E.Data.GetData(DataFormats.FileDrop);
            string[]? Files = Data as string[];
            if (Files == null || Files.Length == 0)
            {
                E.Effect = DragDropEffects.None;
                return;
            }

            bool HasMarkdownFile = false;
            for (int Index = 0; Index < Files.Length; Index++)
            {
                string FilePath = Files[Index];
                if (IsMarkdownFilePath(FilePath))
                {
                    HasMarkdownFile = true;
                    break;
                }
            }

            E.Effect = HasMarkdownFile ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Main_DragDrop(object? Sender, DragEventArgs E)
        {
            if (E.Data == null)
            {
                return;
            }

            object? Data = E.Data.GetData(DataFormats.FileDrop);
            string[]? Files = Data as string[];
            if (Files == null || Files.Length == 0)
            {
                return;
            }

            string? MarkdownFilePath = null;
            for (int Index = 0; Index < Files.Length; Index++)
            {
                string FilePath = Files[Index];
                if (IsMarkdownFilePath(FilePath))
                {
                    MarkdownFilePath = FilePath;
                    break;
                }
            }

            if (MarkdownFilePath == null)
            {
                return;
            }

            Input.Text = MarkdownFilePath;
            string OutputDocxPath = Path.ChangeExtension(MarkdownFilePath, ".docx");
            Output.Text = OutputDocxPath;
        }

        private static bool IsMarkdownFilePath(string FilePath)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                return false;
            }

            string Extension = Path.GetExtension(FilePath);
            if (string.Equals(Extension, ".md", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(Extension, ".markdown", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
