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
            FindRow = new FlowLayoutPanel();
            FindPromptLabel = new Label();
            QueryBox = new TextBox();
            FindNextButton = new Button();
            FindPreviousButton = new Button();
            MatchCaseCheckBox = new CheckBox();
            CloseFindButton = new Button();
            ReplaceRow = new FlowLayoutPanel();
            ReplacePromptLabel = new Label();
            ReplaceBox = new TextBox();
            ReplaceButton = new Button();
            ReplaceAllButton = new Button();
            FindBar.SuspendLayout();
            FindRow.SuspendLayout();
            ReplaceRow.SuspendLayout();
            SuspendLayout();
            // 
            // SourceEditor
            // 
            SourceEditor.AcceptsTab = true;
            SourceEditor.AccessibleName = "Mermaid source editor";
            SourceEditor.DetectUrls = false;
            SourceEditor.Dock = DockStyle.Fill;
            SourceEditor.Font = new Font("Consolas", 8.8F);
            SourceEditor.HideSelection = false;
            SourceEditor.Location = new Point(43, 0);
            SourceEditor.Margin = new Padding(0);
            SourceEditor.Name = "SourceEditor";
            SourceEditor.Size = new Size(392, 320);
            SourceEditor.TabIndex = 0;
            SourceEditor.Text = "";
            SourceEditor.WordWrap = false;
            // 
            // LineNumberColumn
            // 
            LineNumberColumn.AccessibleName = "Line numbers";
            LineNumberColumn.Dock = DockStyle.Left;
            LineNumberColumn.Location = new Point(0, 0);
            LineNumberColumn.Margin = new Padding(0);
            LineNumberColumn.Name = "LineNumberColumn";
            LineNumberColumn.Size = new Size(43, 320);
            LineNumberColumn.TabIndex = 1;
            // 
            // MinimapBox
            // 
            MinimapBox.AccessibleName = "Source minimap";
            MinimapBox.Dock = DockStyle.Right;
            MinimapBox.Font = new Font("Consolas", 2.56000018F);
            MinimapBox.Location = new Point(435, 0);
            MinimapBox.Margin = new Padding(0);
            MinimapBox.Name = "MinimapBox";
            MinimapBox.ReadOnly = true;
            MinimapBox.ScrollBars = RichTextBoxScrollBars.None;
            MinimapBox.Size = new Size(45, 320);
            MinimapBox.TabIndex = 1;
            MinimapBox.Text = "";
            MinimapBox.Visible = false;
            MinimapBox.WordWrap = false;
            // 
            // ScrollMarks
            // 
            ScrollMarks.AccessibleName = "Scrollbar marks";
            ScrollMarks.Dock = DockStyle.Right;
            ScrollMarks.Location = new Point(421, 0);
            ScrollMarks.Margin = new Padding(0);
            ScrollMarks.Name = "ScrollMarks";
            ScrollMarks.Size = new Size(14, 320);
            ScrollMarks.TabIndex = 2;
            // 
            // HighlightTimer
            // 
            HighlightTimer.Interval = 250;
            // 
            // FindBar
            // 
            FindBar.Controls.Add(ReplaceRow);
            FindBar.Controls.Add(FindRow);
            FindBar.Dock = DockStyle.Top;
            FindBar.Name = "FindBar";
            FindBar.Padding = new Padding(4, 4, 4, 0);
            FindBar.Size = new Size(480, 60);
            FindBar.TabIndex = 2;
            FindBar.Visible = false;
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
            FindRow.Name = "FindRow";
            FindRow.Size = new Size(472, 28);
            FindRow.TabIndex = 0;
            FindRow.WrapContents = false;
            // 
            // FindPromptLabel
            // 
            FindPromptLabel.AutoSize = true;
            FindPromptLabel.Margin = new Padding(0, 6, 6, 0);
            FindPromptLabel.Name = "FindPromptLabel";
            FindPromptLabel.Text = "Find";
            // 
            // QueryBox
            // 
            QueryBox.Margin = new Padding(0, 2, 6, 0);
            QueryBox.Name = "QueryBox";
            QueryBox.Size = new Size(180, 23);
            QueryBox.TabIndex = 0;
            // 
            // FindNextButton
            // 
            FindNextButton.AutoSize = true;
            FindNextButton.Margin = new Padding(0, 1, 4, 0);
            FindNextButton.Name = "FindNextButton";
            FindNextButton.TabIndex = 1;
            FindNextButton.Text = "Next";
            // 
            // FindPreviousButton
            // 
            FindPreviousButton.AutoSize = true;
            FindPreviousButton.Margin = new Padding(0, 1, 4, 0);
            FindPreviousButton.Name = "FindPreviousButton";
            FindPreviousButton.TabIndex = 2;
            FindPreviousButton.Text = "Previous";
            // 
            // MatchCaseCheckBox
            // 
            MatchCaseCheckBox.AutoSize = true;
            MatchCaseCheckBox.Margin = new Padding(8, 5, 4, 0);
            MatchCaseCheckBox.Name = "MatchCaseCheckBox";
            MatchCaseCheckBox.TabIndex = 3;
            MatchCaseCheckBox.Text = "Match case";
            // 
            // CloseFindButton
            // 
            CloseFindButton.AutoSize = true;
            CloseFindButton.Margin = new Padding(8, 1, 0, 0);
            CloseFindButton.Name = "CloseFindButton";
            CloseFindButton.TabIndex = 4;
            CloseFindButton.Text = "Close";
            // 
            // ReplaceRow
            // 
            ReplaceRow.Controls.Add(ReplacePromptLabel);
            ReplaceRow.Controls.Add(ReplaceBox);
            ReplaceRow.Controls.Add(ReplaceButton);
            ReplaceRow.Controls.Add(ReplaceAllButton);
            ReplaceRow.Dock = DockStyle.Top;
            ReplaceRow.Name = "ReplaceRow";
            ReplaceRow.Size = new Size(472, 28);
            ReplaceRow.TabIndex = 1;
            ReplaceRow.Visible = false;
            ReplaceRow.WrapContents = false;
            // 
            // ReplacePromptLabel
            // 
            ReplacePromptLabel.AutoSize = true;
            ReplacePromptLabel.Margin = new Padding(0, 6, 6, 0);
            ReplacePromptLabel.Name = "ReplacePromptLabel";
            ReplacePromptLabel.Text = "Replace";
            // 
            // ReplaceBox
            // 
            ReplaceBox.Margin = new Padding(0, 2, 6, 0);
            ReplaceBox.Name = "ReplaceBox";
            ReplaceBox.Size = new Size(180, 23);
            ReplaceBox.TabIndex = 0;
            // 
            // ReplaceButton
            // 
            ReplaceButton.AutoSize = true;
            ReplaceButton.Margin = new Padding(0, 1, 4, 0);
            ReplaceButton.Name = "ReplaceButton";
            ReplaceButton.TabIndex = 1;
            ReplaceButton.Text = "Replace";
            // 
            // ReplaceAllButton
            // 
            ReplaceAllButton.AutoSize = true;
            ReplaceAllButton.Margin = new Padding(0, 1, 4, 0);
            ReplaceAllButton.Name = "ReplaceAllButton";
            ReplaceAllButton.TabIndex = 2;
            ReplaceAllButton.Text = "Replace all";
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
            Size = new Size(480, 320);
            FindRow.ResumeLayout(false);
            FindRow.PerformLayout();
            ReplaceRow.ResumeLayout(false);
            ReplaceRow.PerformLayout();
            FindBar.ResumeLayout(false);
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
