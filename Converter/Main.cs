namespace Converter

{

    using System.IO;

    using System.Threading.Tasks;

    using System.Windows.Forms;

    using MDConverter;

    using MDConverter.Options;



    public partial class Main : Form

    {

        private bool _IsJobRunning;



        /// <summary>

        /// Creates a new instance of the main window.

        /// </summary>

        public Main()

        {

            InitializeComponent();

            InitialiseInteraction();

        }



        /// <summary>

        /// Creates the main window with optional paths pre-filled from the command line.

        /// </summary>

        public Main(string? InputPath, string? OutputPath)

        {

            InitializeComponent();

            InitialiseInteraction();



            if (!string.IsNullOrWhiteSpace(InputPath))

            {

                Input.Text = InputPath;

            }



            if (!string.IsNullOrWhiteSpace(OutputPath))

            {

                Output.Text = OutputPath;

            }

        }



        private void InitialiseInteraction()

        {

            AllowDrop = true;

            DragEnter += Main_DragEnter;

            DragDrop += Main_DragDrop;

            Start.Click += Start_Click;

        }



        /// <summary>

        /// Updates the progress bar and status label on the UI thread.

        /// </summary>

        internal void UpdateProgress(int PercentComplete, string Message)

        {

            int ClampedPercent = PercentComplete;

            if (ClampedPercent < 0)

            {

                ClampedPercent = 0;

            }



            if (ClampedPercent > 100)

            {

                ClampedPercent = 100;

            }



            JobProgress.Value = ClampedPercent;

            ProgressLabel.Text = Message;

        }



        private void SetJobRunning(bool IsRunning)

        {

            _IsJobRunning = IsRunning;

            Start.Enabled = !IsRunning;

            Input.Enabled = !IsRunning;

            Output.Enabled = !IsRunning;

            JobProgress.Visible = IsRunning;

            ProgressLabel.Visible = IsRunning;



            if (IsRunning)

            {

                JobProgress.Value = 0;

                ProgressLabel.Text = "Starting…";

            }

            else

            {

                JobProgress.Value = 0;

                ProgressLabel.Text = "Ready";

            }

        }



        private async void Start_Click(object? Sender, EventArgs E)

        {

            if (_IsJobRunning)

            {

                return;

            }



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



            SetJobRunning(true);

            GuiConversionProgressReporter ProgressReporter = new GuiConversionProgressReporter(this);



            try

            {

                MarkdownToDocxOptions Options = new MarkdownToDocxOptions();

                MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options, ProgressReporter);

                await Task.Run(() => { Converter.ConvertFile(InputPath, OutputPath); }).ConfigureAwait(true);



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

            finally

            {

                SetJobRunning(false);

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

