using System.Drawing;
using System.Windows.Forms;
using LiveEditor.Editor;

namespace LiveEditor
{
    partial class CodeEditor
    {
        private System.ComponentModel.IContainer _Components = null;

        protected override void Dispose(bool Disposing)
        {
            if (Disposing && _Components != null)
            {
                _Components.Dispose();
            }

            base.Dispose(Disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SourceEditor = new SourceTextBox();
            LineNumberColumn = new LineNumberGutter();
            MinimapBox = new RichTextBox();
            ScrollMarks = new ScrollMarkStrip();
            HighlightTimer = new Timer(components);
            FindBar = new Panel();
            ReplaceRow = new FlowLayoutPanel();
            ReplacePromptLabel = new Label();
            ReplaceBox = new TextBox();
            ReplaceButton = new Button();
            ReplaceAllButton = new Button();
            FindRow = new FlowLayoutPanel();
            FindPromptLabel = new Label();
            QueryBox = new TextBox();
            FindNextButton = new Button();
            FindPreviousButton = new Button();
            MatchCaseCheckBox = new CheckBox();
            CloseFindButton = new Button();
            FindBar.SuspendLayout();
            ReplaceRow.SuspendLayout();
            FindRow.SuspendLayout();
            SuspendLayout();
            // 
            // SourceEditor
            // 
            SourceEditor.AcceptsTab = true;
            SourceEditor.AccessibleName = "Mermaid source editor";
            SourceEditor.DetectUrls = false;
            SourceEditor.Dock = DockStyle.Fill;
            SourceEditor.Font = new Font("Consolas", 7.04000044F);
            SourceEditor.HideSelection = false;
            SourceEditor.Location = new Point(43, 60);
            SourceEditor.Margin = new Padding(0);
            SourceEditor.Name = "SourceEditor";
            SourceEditor.TextChanged += SourceEditor_TextChanged;
            SourceEditor.SelectionChanged += SourceEditor_SelectionChanged;
            SourceEditor.MouseDoubleClick += SourceEditor_MouseDoubleClick;
            SourceEditor.KeyDown += SourceEditor_KeyDown;
            SourceEditor.PreviewKeyDown += SourceEditor_PreviewKeyDown;
            SourceEditor.LostFocus += SourceEditor_LostFocus;
            SourceEditor.VScroll += SourceEditor_ScrollChanged;
            SourceEditor.HandleCreated += SourceEditor_HandleCreated;
            SourceEditor.HandleDestroyed += SourceEditor_HandleDestroyed;
            SourceEditor.Size = new Size(378, 260);
            SourceEditor.TabIndex = 0;
            SourceEditor.Text = "";
            SourceEditor.WordWrap = false;
            // 
            // LineNumberColumn
            // 
            LineNumberColumn.AccessibleName = "Line numbers";
            LineNumberColumn.Dock = DockStyle.Left;
            LineNumberColumn.Location = new Point(0, 60);
            LineNumberColumn.Margin = new Padding(0);
            LineNumberColumn.Name = "LineNumberColumn";
            LineNumberColumn.Size = new Size(43, 260);
            LineNumberColumn.TabIndex = 1;
            // 
            // MinimapBox
            // 
            MinimapBox.AccessibleName = "Source minimap";
            MinimapBox.Dock = DockStyle.Right;
            MinimapBox.Font = new Font("Consolas", 2.048F);
            MinimapBox.Location = new Point(435, 60);
            MinimapBox.Margin = new Padding(0);
            MinimapBox.Name = "MinimapBox";
            MinimapBox.ReadOnly = true;
            MinimapBox.ScrollBars = RichTextBoxScrollBars.None;
            MinimapBox.Size = new Size(45, 260);
            MinimapBox.TabIndex = 1;
            MinimapBox.Text = "";
            MinimapBox.Visible = false;
            MinimapBox.WordWrap = false;
            // 
            // ScrollMarks
            // 
            ScrollMarks.AccessibleName = "Scrollbar marks";
            ScrollMarks.Dock = DockStyle.Right;
            ScrollMarks.Location = new Point(421, 60);
            ScrollMarks.Margin = new Padding(0);
            ScrollMarks.Name = "ScrollMarks";
            ScrollMarks.LineChosen += ScrollMarks_LineChosen;
            ScrollMarks.Size = new Size(14, 260);
            ScrollMarks.TabIndex = 2;
            ScrollMarks.TabStop = false;
            // 
            // HighlightTimer
            // 
            HighlightTimer.Interval = 250;
            HighlightTimer.Tick += HighlightTimer_Tick;
            // 
            // FindBar
            // 
            FindBar.Controls.Add(ReplaceRow);
            FindBar.Controls.Add(FindRow);
            FindBar.Dock = DockStyle.Top;
            FindBar.Location = new Point(0, 0);
            FindBar.Name = "FindBar";
            FindBar.Padding = new Padding(4, 4, 4, 0);
            FindBar.Size = new Size(480, 60);
            FindBar.TabIndex = 2;
            FindBar.Visible = false;
            // 
            // ReplaceRow
            // 
            ReplaceRow.Controls.Add(ReplacePromptLabel);
            ReplaceRow.Controls.Add(ReplaceBox);
            ReplaceRow.Controls.Add(ReplaceButton);
            ReplaceRow.Controls.Add(ReplaceAllButton);
            ReplaceRow.Dock = DockStyle.Top;
            ReplaceRow.Location = new Point(4, 32);
            ReplaceRow.Name = "ReplaceRow";
            ReplaceRow.Size = new Size(472, 28);
            ReplaceRow.TabIndex = 1;
            ReplaceRow.Visible = false;
            ReplaceRow.WrapContents = false;
            // 
            // ReplacePromptLabel
            // 
            ReplacePromptLabel.AutoSize = true;
            ReplacePromptLabel.Location = new Point(0, 6);
            ReplacePromptLabel.Margin = new Padding(0, 6, 6, 0);
            ReplacePromptLabel.Name = "ReplacePromptLabel";
            ReplacePromptLabel.Size = new Size(62, 20);
            ReplacePromptLabel.TabIndex = 0;
            ReplacePromptLabel.Text = "Replace";
            // 
            // ReplaceBox
            // 
            ReplaceBox.Location = new Point(68, 2);
            ReplaceBox.Margin = new Padding(0, 2, 6, 0);
            ReplaceBox.Name = "ReplaceBox";
            ReplaceBox.KeyDown += QueryBox_KeyDown;
            ReplaceBox.Size = new Size(180, 23);
            ReplaceBox.TabIndex = 0;
            // 
            // ReplaceButton
            // 
            ReplaceButton.AutoSize = true;
            ReplaceButton.Location = new Point(254, 1);
            ReplaceButton.Margin = new Padding(0, 1, 4, 0);
            ReplaceButton.Name = "ReplaceButton";
            ReplaceButton.Click += ReplaceButton_Click;
            ReplaceButton.Size = new Size(75, 30);
            ReplaceButton.TabIndex = 1;
            ReplaceButton.Text = "Replace";
            // 
            // ReplaceAllButton
            // 
            ReplaceAllButton.AutoSize = true;
            ReplaceAllButton.Location = new Point(333, 1);
            ReplaceAllButton.Margin = new Padding(0, 1, 4, 0);
            ReplaceAllButton.Name = "ReplaceAllButton";
            ReplaceAllButton.Click += ReplaceAllButton_Click;
            ReplaceAllButton.Size = new Size(92, 30);
            ReplaceAllButton.TabIndex = 2;
            ReplaceAllButton.Text = "Replace all";
            // 
            // FindRow
            // 
            FindRow.Controls.Add(FindPromptLabel);
            FindRow.Controls.Add(QueryBox);
            FindRow.Controls.Add(FindNextButton);
            FindRow.Controls.Add(FindPreviousButton);
            FindRow.Controls.Add(MatchCaseCheckBox);
            FindRow.Controls.Add(CloseFindButton);
            FindRow.Dock = DockStyle.Top;
            FindRow.Location = new Point(4, 4);
            FindRow.Name = "FindRow";
            FindRow.Size = new Size(472, 28);
            FindRow.TabIndex = 0;
            FindRow.WrapContents = false;
            // 
            // FindPromptLabel
            // 
            FindPromptLabel.AutoSize = true;
            FindPromptLabel.Location = new Point(0, 6);
            FindPromptLabel.Margin = new Padding(0, 6, 6, 0);
            FindPromptLabel.Name = "FindPromptLabel";
            FindPromptLabel.Size = new Size(37, 20);
            FindPromptLabel.TabIndex = 0;
            FindPromptLabel.Text = "Find";
            // 
            // QueryBox
            // 
            QueryBox.Location = new Point(43, 2);
            QueryBox.Margin = new Padding(0, 2, 6, 0);
            QueryBox.Name = "QueryBox";
            QueryBox.KeyDown += QueryBox_KeyDown;
            QueryBox.TextChanged += QueryBox_TextChanged;
            QueryBox.Size = new Size(180, 23);
            QueryBox.TabIndex = 0;
            // 
            // FindNextButton
            // 
            FindNextButton.AutoSize = true;
            FindNextButton.Location = new Point(229, 1);
            FindNextButton.Margin = new Padding(0, 1, 4, 0);
            FindNextButton.Name = "FindNextButton";
            FindNextButton.Click += FindNextButton_Click;
            FindNextButton.Size = new Size(75, 30);
            FindNextButton.TabIndex = 1;
            FindNextButton.Text = "Next";
            // 
            // FindPreviousButton
            // 
            FindPreviousButton.AutoSize = true;
            FindPreviousButton.Location = new Point(308, 1);
            FindPreviousButton.Margin = new Padding(0, 1, 4, 0);
            FindPreviousButton.Name = "FindPreviousButton";
            FindPreviousButton.Click += FindPreviousButton_Click;
            FindPreviousButton.Size = new Size(75, 30);
            FindPreviousButton.TabIndex = 2;
            FindPreviousButton.Text = "Previous";
            // 
            // MatchCaseCheckBox
            // 
            MatchCaseCheckBox.AutoSize = true;
            MatchCaseCheckBox.Location = new Point(395, 5);
            MatchCaseCheckBox.Margin = new Padding(8, 5, 4, 0);
            MatchCaseCheckBox.Name = "MatchCaseCheckBox";
            MatchCaseCheckBox.CheckedChanged += QueryBox_TextChanged;
            MatchCaseCheckBox.Size = new Size(93, 21);
            MatchCaseCheckBox.TabIndex = 3;
            MatchCaseCheckBox.Text = "Match case";
            // 
            // CloseFindButton
            // 
            CloseFindButton.AutoSize = true;
            CloseFindButton.Location = new Point(500, 1);
            CloseFindButton.Margin = new Padding(8, 1, 0, 0);
            CloseFindButton.Name = "CloseFindButton";
            CloseFindButton.Click += CloseFindButton_Click;
            CloseFindButton.Size = new Size(75, 30);
            CloseFindButton.TabIndex = 4;
            CloseFindButton.Text = "Close";
            // 
            // CodeEditor
            // 
            AutoScaleMode = AutoScaleMode.None;
            Controls.Add(SourceEditor);
            Controls.Add(LineNumberColumn);
            Controls.Add(ScrollMarks);
            Controls.Add(MinimapBox);
            Controls.Add(FindBar);
            Name = "CodeEditor";
            Disposed += CodeEditor_Disposed;
            Size = new Size(480, 320);
            FindBar.ResumeLayout(false);
            ReplaceRow.ResumeLayout(false);
            ReplaceRow.PerformLayout();
            FindRow.ResumeLayout(false);
            FindRow.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private SourceTextBox SourceEditor;
        private LineNumberGutter LineNumberColumn;
        private RichTextBox MinimapBox;
        private ScrollMarkStrip ScrollMarks;
        private Timer HighlightTimer;
        private Panel FindBar;
        private FlowLayoutPanel FindRow;
        private Label FindPromptLabel;
        private TextBox QueryBox;
        private Button FindNextButton;
        private Button FindPreviousButton;
        private CheckBox MatchCaseCheckBox;
        private Button CloseFindButton;
        private FlowLayoutPanel ReplaceRow;
        private Label ReplacePromptLabel;
        private TextBox ReplaceBox;
        private Button ReplaceButton;
        private Button ReplaceAllButton;
        private System.ComponentModel.IContainer components;
    }
}
