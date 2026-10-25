using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class SettingsForm
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
            _Components = new System.ComponentModel.Container();
            SettingsLayout = new TableLayoutPanel();
            AutoIndentCheckBox = new CheckBox();
            InsertSpacesCheckBox = new CheckBox();
            IndentSizeLabel = new Label();
            IndentSizeNumeric = new NumericUpDown();
            WordWrapCheckBox = new CheckBox();
            FontSizeLabel = new Label();
            FontSizeNumeric = new NumericUpDown();
            RenderDelayLabel = new Label();
            RenderDelayNumeric = new NumericUpDown();
            HighlightDelayLabel = new Label();
            HighlightDelayNumeric = new NumericUpDown();
            SelectionColourLabel = new Label();
            SelectionColourPanel = new FlowLayoutPanel();
            SelectionColourSwatch = new Panel();
            SelectionColourValueLabel = new Label();
            SelectionColourButton = new Button();
            ToolbarGroup = new GroupBox();
            ToolbarOptions = new FlowLayoutPanel();
            WideToolbarRadio = new RadioButton();
            NarrowToolbarRadio = new RadioButton();
            EditorGroup = new GroupBox();
            EditorOptions = new FlowLayoutPanel();
            EditorLeftRadio = new RadioButton();
            EditorRightRadio = new RadioButton();
            ButtonPanel = new FlowLayoutPanel();
            CancelSettingsButton = new Button();
            OkSettingsButton = new Button();
            SettingsLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)IndentSizeNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)FontSizeNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)RenderDelayNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)HighlightDelayNumeric).BeginInit();
            SelectionColourPanel.SuspendLayout();
            ToolbarGroup.SuspendLayout();
            ToolbarOptions.SuspendLayout();
            EditorGroup.SuspendLayout();
            EditorOptions.SuspendLayout();
            ButtonPanel.SuspendLayout();
            SuspendLayout();
            //
            // SettingsLayout
            //
            SettingsLayout.AutoSize = true;
            SettingsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SettingsLayout.ColumnCount = 2;
            SettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            SettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            SettingsLayout.Controls.Add(AutoIndentCheckBox, 0, 0);
            SettingsLayout.SetColumnSpan(AutoIndentCheckBox, 2);
            SettingsLayout.Controls.Add(InsertSpacesCheckBox, 0, 1);
            SettingsLayout.SetColumnSpan(InsertSpacesCheckBox, 2);
            SettingsLayout.Controls.Add(IndentSizeLabel, 0, 2);
            SettingsLayout.Controls.Add(IndentSizeNumeric, 1, 2);
            SettingsLayout.Controls.Add(WordWrapCheckBox, 0, 3);
            SettingsLayout.SetColumnSpan(WordWrapCheckBox, 2);
            SettingsLayout.Controls.Add(FontSizeLabel, 0, 4);
            SettingsLayout.Controls.Add(FontSizeNumeric, 1, 4);
            SettingsLayout.Controls.Add(RenderDelayLabel, 0, 5);
            SettingsLayout.Controls.Add(RenderDelayNumeric, 1, 5);
            SettingsLayout.Controls.Add(HighlightDelayLabel, 0, 6);
            SettingsLayout.Controls.Add(HighlightDelayNumeric, 1, 6);
            SettingsLayout.Controls.Add(SelectionColourLabel, 0, 7);
            SettingsLayout.Controls.Add(SelectionColourPanel, 1, 7);
            SettingsLayout.Controls.Add(ToolbarGroup, 0, 8);
            SettingsLayout.SetColumnSpan(ToolbarGroup, 2);
            SettingsLayout.Controls.Add(EditorGroup, 0, 9);
            SettingsLayout.SetColumnSpan(EditorGroup, 2);
            SettingsLayout.Controls.Add(ButtonPanel, 0, 10);
            SettingsLayout.SetColumnSpan(ButtonPanel, 2);
            SettingsLayout.Location = new Point(12, 12);
            SettingsLayout.Margin = new Padding(0);
            SettingsLayout.Name = "SettingsLayout";
            SettingsLayout.RowCount = 11;
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            SettingsLayout.TabIndex = 0;
            //
            // AutoIndentCheckBox
            //
            AutoIndentCheckBox.Anchor = AnchorStyles.Left;
            AutoIndentCheckBox.AutoSize = true;
            AutoIndentCheckBox.Margin = new Padding(3, 4, 3, 4);
            AutoIndentCheckBox.Name = "AutoIndentCheckBox";
            AutoIndentCheckBox.TabIndex = 0;
            AutoIndentCheckBox.Text = "Preserve indentation on new lines";
            AutoIndentCheckBox.UseVisualStyleBackColor = true;
            //
            // InsertSpacesCheckBox
            //
            InsertSpacesCheckBox.Anchor = AnchorStyles.Left;
            InsertSpacesCheckBox.AutoSize = true;
            InsertSpacesCheckBox.Margin = new Padding(3, 4, 3, 4);
            InsertSpacesCheckBox.Name = "InsertSpacesCheckBox";
            InsertSpacesCheckBox.TabIndex = 1;
            InsertSpacesCheckBox.Text = "Insert spaces when Tab is pressed";
            InsertSpacesCheckBox.UseVisualStyleBackColor = true;
            //
            // IndentSizeLabel
            //
            IndentSizeLabel.Anchor = AnchorStyles.Left;
            IndentSizeLabel.AutoSize = true;
            IndentSizeLabel.Margin = new Padding(3, 4, 12, 4);
            IndentSizeLabel.Name = "IndentSizeLabel";
            IndentSizeLabel.Text = "Indent size";
            //
            // IndentSizeNumeric
            //
            IndentSizeNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            IndentSizeNumeric.Margin = new Padding(3, 4, 3, 4);
            IndentSizeNumeric.Maximum = 16;
            IndentSizeNumeric.Minimum = 1;
            IndentSizeNumeric.Name = "IndentSizeNumeric";
            IndentSizeNumeric.Size = new Size(90, 23);
            IndentSizeNumeric.TabIndex = 2;
            IndentSizeNumeric.Value = 2;
            //
            // WordWrapCheckBox
            //
            WordWrapCheckBox.Anchor = AnchorStyles.Left;
            WordWrapCheckBox.AutoSize = true;
            WordWrapCheckBox.Margin = new Padding(3, 4, 3, 4);
            WordWrapCheckBox.Name = "WordWrapCheckBox";
            WordWrapCheckBox.TabIndex = 3;
            WordWrapCheckBox.Text = "Wrap long editor lines";
            WordWrapCheckBox.UseVisualStyleBackColor = true;
            //
            // FontSizeLabel
            //
            FontSizeLabel.Anchor = AnchorStyles.Left;
            FontSizeLabel.AutoSize = true;
            FontSizeLabel.Margin = new Padding(3, 4, 12, 4);
            FontSizeLabel.Name = "FontSizeLabel";
            FontSizeLabel.Text = "Editor font size";
            //
            // FontSizeNumeric
            //
            FontSizeNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            FontSizeNumeric.DecimalPlaces = 1;
            FontSizeNumeric.Increment = 0.5M;
            FontSizeNumeric.Margin = new Padding(3, 4, 3, 4);
            FontSizeNumeric.Maximum = 32;
            FontSizeNumeric.Minimum = 8;
            FontSizeNumeric.Name = "FontSizeNumeric";
            FontSizeNumeric.Size = new Size(90, 23);
            FontSizeNumeric.TabIndex = 4;
            FontSizeNumeric.Value = 11;
            //
            // RenderDelayLabel
            //
            RenderDelayLabel.Anchor = AnchorStyles.Left;
            RenderDelayLabel.AutoSize = true;
            RenderDelayLabel.Margin = new Padding(3, 4, 12, 4);
            RenderDelayLabel.Name = "RenderDelayLabel";
            RenderDelayLabel.Text = "Preview delay (milliseconds)";
            //
            // RenderDelayNumeric
            //
            RenderDelayNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            RenderDelayNumeric.Increment = 50;
            RenderDelayNumeric.Margin = new Padding(3, 4, 3, 4);
            RenderDelayNumeric.Maximum = 5000;
            RenderDelayNumeric.Minimum = 50;
            RenderDelayNumeric.Name = "RenderDelayNumeric";
            RenderDelayNumeric.Size = new Size(90, 23);
            RenderDelayNumeric.TabIndex = 5;
            RenderDelayNumeric.Value = 350;
            //
            // HighlightDelayLabel
            //
            HighlightDelayLabel.Anchor = AnchorStyles.Left;
            HighlightDelayLabel.AutoSize = true;
            HighlightDelayLabel.Margin = new Padding(3, 4, 12, 4);
            HighlightDelayLabel.Name = "HighlightDelayLabel";
            HighlightDelayLabel.Text = "Syntax colour delay (milliseconds)";
            //
            // HighlightDelayNumeric
            //
            HighlightDelayNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            HighlightDelayNumeric.Increment = 50;
            HighlightDelayNumeric.Margin = new Padding(3, 4, 3, 4);
            HighlightDelayNumeric.Maximum = 5000;
            HighlightDelayNumeric.Minimum = 50;
            HighlightDelayNumeric.Name = "HighlightDelayNumeric";
            HighlightDelayNumeric.Size = new Size(90, 23);
            HighlightDelayNumeric.TabIndex = 6;
            HighlightDelayNumeric.Value = 250;
            //
            // SelectionColourLabel
            //
            SelectionColourLabel.Anchor = AnchorStyles.Left;
            SelectionColourLabel.AutoSize = true;
            SelectionColourLabel.Margin = new Padding(3, 4, 12, 4);
            SelectionColourLabel.Name = "SelectionColourLabel";
            SelectionColourLabel.Text = "Selection colour";
            //
            // SelectionColourPanel
            //
            SelectionColourPanel.Anchor = AnchorStyles.Left;
            SelectionColourPanel.AutoSize = true;
            SelectionColourPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SelectionColourPanel.Controls.Add(SelectionColourSwatch);
            SelectionColourPanel.Controls.Add(SelectionColourValueLabel);
            SelectionColourPanel.Controls.Add(SelectionColourButton);
            SelectionColourPanel.Margin = new Padding(0, 1, 0, 1);
            SelectionColourPanel.Name = "SelectionColourPanel";
            SelectionColourPanel.TabIndex = 7;
            SelectionColourPanel.WrapContents = false;
            //
            // SelectionColourSwatch
            //
            SelectionColourSwatch.Anchor = AnchorStyles.Left;
            SelectionColourSwatch.BorderStyle = BorderStyle.FixedSingle;
            SelectionColourSwatch.Cursor = Cursors.Hand;
            SelectionColourSwatch.Margin = new Padding(3, 4, 3, 4);
            SelectionColourSwatch.Name = "SelectionColourSwatch";
            SelectionColourSwatch.Size = new Size(22, 16);
            SelectionColourSwatch.Click += SelectionColourButton_Click;
            //
            // SelectionColourValueLabel
            //
            SelectionColourValueLabel.Anchor = AnchorStyles.Left;
            SelectionColourValueLabel.AutoSize = true;
            SelectionColourValueLabel.Margin = new Padding(3, 4, 6, 4);
            SelectionColourValueLabel.Name = "SelectionColourValueLabel";
            SelectionColourValueLabel.Text = "#1E1E1E";
            //
            // SelectionColourButton
            //
            SelectionColourButton.Anchor = AnchorStyles.Left;
            SelectionColourButton.AutoSize = true;
            SelectionColourButton.Margin = new Padding(3, 3, 3, 3);
            SelectionColourButton.Name = "SelectionColourButton";
            SelectionColourButton.TabIndex = 0;
            SelectionColourButton.Text = "Choose\u2026";
            SelectionColourButton.UseVisualStyleBackColor = true;
            SelectionColourButton.Click += SelectionColourButton_Click;
            //
            // ToolbarGroup
            //
            ToolbarGroup.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            ToolbarGroup.AutoSize = true;
            ToolbarGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ToolbarGroup.Controls.Add(ToolbarOptions);
            ToolbarGroup.Margin = new Padding(3, 8, 3, 4);
            ToolbarGroup.Name = "ToolbarGroup";
            ToolbarGroup.Padding = new Padding(8, 4, 8, 6);
            ToolbarGroup.TabIndex = 8;
            ToolbarGroup.TabStop = false;
            ToolbarGroup.Text = "Toolbar";
            //
            // ToolbarOptions
            //
            ToolbarOptions.AutoSize = true;
            ToolbarOptions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ToolbarOptions.Controls.Add(WideToolbarRadio);
            ToolbarOptions.Controls.Add(NarrowToolbarRadio);
            ToolbarOptions.Dock = DockStyle.Fill;
            ToolbarOptions.Margin = new Padding(0);
            ToolbarOptions.Name = "ToolbarOptions";
            ToolbarOptions.TabIndex = 0;
            ToolbarOptions.WrapContents = false;
            //
            // WideToolbarRadio
            //
            WideToolbarRadio.AutoSize = true;
            WideToolbarRadio.Checked = true;
            WideToolbarRadio.Margin = new Padding(3, 3, 24, 3);
            WideToolbarRadio.Name = "WideToolbarRadio";
            WideToolbarRadio.TabIndex = 0;
            WideToolbarRadio.TabStop = true;
            WideToolbarRadio.Text = "Wide";
            WideToolbarRadio.UseVisualStyleBackColor = true;
            //
            // NarrowToolbarRadio
            //
            NarrowToolbarRadio.AutoSize = true;
            NarrowToolbarRadio.Margin = new Padding(3, 3, 3, 3);
            NarrowToolbarRadio.Name = "NarrowToolbarRadio";
            NarrowToolbarRadio.TabIndex = 1;
            NarrowToolbarRadio.Text = "Narrow";
            NarrowToolbarRadio.UseVisualStyleBackColor = true;
            //
            // EditorGroup
            //
            EditorGroup.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            EditorGroup.AutoSize = true;
            EditorGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            EditorGroup.Controls.Add(EditorOptions);
            EditorGroup.Margin = new Padding(3, 4, 3, 4);
            EditorGroup.Name = "EditorGroup";
            EditorGroup.Padding = new Padding(8, 4, 8, 6);
            EditorGroup.TabIndex = 9;
            EditorGroup.TabStop = false;
            EditorGroup.Text = "Editor";
            //
            // EditorOptions
            //
            EditorOptions.AutoSize = true;
            EditorOptions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            EditorOptions.Controls.Add(EditorLeftRadio);
            EditorOptions.Controls.Add(EditorRightRadio);
            EditorOptions.Dock = DockStyle.Fill;
            EditorOptions.Margin = new Padding(0);
            EditorOptions.Name = "EditorOptions";
            EditorOptions.TabIndex = 0;
            EditorOptions.WrapContents = false;
            //
            // EditorLeftRadio
            //
            EditorLeftRadio.AutoSize = true;
            EditorLeftRadio.Checked = true;
            EditorLeftRadio.Margin = new Padding(3, 3, 24, 3);
            EditorLeftRadio.Name = "EditorLeftRadio";
            EditorLeftRadio.TabIndex = 0;
            EditorLeftRadio.TabStop = true;
            EditorLeftRadio.Text = "Left";
            EditorLeftRadio.UseVisualStyleBackColor = true;
            //
            // EditorRightRadio
            //
            EditorRightRadio.AutoSize = true;
            EditorRightRadio.Margin = new Padding(3, 3, 3, 3);
            EditorRightRadio.Name = "EditorRightRadio";
            EditorRightRadio.TabIndex = 1;
            EditorRightRadio.Text = "Right";
            EditorRightRadio.UseVisualStyleBackColor = true;
            //
            // ButtonPanel
            //
            ButtonPanel.Anchor = AnchorStyles.Right;
            ButtonPanel.AutoSize = true;
            ButtonPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ButtonPanel.Controls.Add(CancelSettingsButton);
            ButtonPanel.Controls.Add(OkSettingsButton);
            ButtonPanel.FlowDirection = FlowDirection.RightToLeft;
            ButtonPanel.Margin = new Padding(0, 12, 0, 0);
            ButtonPanel.Name = "ButtonPanel";
            ButtonPanel.TabIndex = 10;
            ButtonPanel.WrapContents = false;
            //
            // CancelSettingsButton
            //
            CancelSettingsButton.AutoSize = true;
            CancelSettingsButton.DialogResult = DialogResult.Cancel;
            CancelSettingsButton.Margin = new Padding(3, 3, 3, 3);
            CancelSettingsButton.MinimumSize = new Size(80, 0);
            CancelSettingsButton.Name = "CancelSettingsButton";
            CancelSettingsButton.TabIndex = 1;
            CancelSettingsButton.Text = "Cancel";
            CancelSettingsButton.UseVisualStyleBackColor = true;
            //
            // OkSettingsButton
            //
            OkSettingsButton.AutoSize = true;
            OkSettingsButton.DialogResult = DialogResult.OK;
            OkSettingsButton.Margin = new Padding(3, 3, 3, 3);
            OkSettingsButton.MinimumSize = new Size(80, 0);
            OkSettingsButton.Name = "OkSettingsButton";
            OkSettingsButton.TabIndex = 0;
            OkSettingsButton.Text = "OK";
            OkSettingsButton.UseVisualStyleBackColor = true;
            //
            // SettingsForm
            //
            AcceptButton = OkSettingsButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            CancelButton = CancelSettingsButton;
            ClientSize = new Size(360, 420);
            Controls.Add(SettingsLayout);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SettingsForm";
            Padding = new Padding(12);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "LiveEditor Settings";
            ((System.ComponentModel.ISupportInitialize)IndentSizeNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)FontSizeNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)RenderDelayNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)HighlightDelayNumeric).EndInit();
            SelectionColourPanel.ResumeLayout(false);
            SelectionColourPanel.PerformLayout();
            ToolbarOptions.ResumeLayout(false);
            ToolbarOptions.PerformLayout();
            ToolbarGroup.ResumeLayout(false);
            ToolbarGroup.PerformLayout();
            EditorOptions.ResumeLayout(false);
            EditorOptions.PerformLayout();
            EditorGroup.ResumeLayout(false);
            EditorGroup.PerformLayout();
            ButtonPanel.ResumeLayout(false);
            ButtonPanel.PerformLayout();
            SettingsLayout.ResumeLayout(false);
            SettingsLayout.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel SettingsLayout;
        private CheckBox AutoIndentCheckBox;
        private CheckBox InsertSpacesCheckBox;
        private Label IndentSizeLabel;
        private NumericUpDown IndentSizeNumeric;
        private CheckBox WordWrapCheckBox;
        private Label FontSizeLabel;
        private NumericUpDown FontSizeNumeric;
        private Label RenderDelayLabel;
        private NumericUpDown RenderDelayNumeric;
        private Label HighlightDelayLabel;
        private NumericUpDown HighlightDelayNumeric;
        private Label SelectionColourLabel;
        private FlowLayoutPanel SelectionColourPanel;
        private Panel SelectionColourSwatch;
        private Label SelectionColourValueLabel;
        private Button SelectionColourButton;
        private GroupBox ToolbarGroup;
        private FlowLayoutPanel ToolbarOptions;
        private RadioButton WideToolbarRadio;
        private RadioButton NarrowToolbarRadio;
        private GroupBox EditorGroup;
        private FlowLayoutPanel EditorOptions;
        private RadioButton EditorLeftRadio;
        private RadioButton EditorRightRadio;
        private FlowLayoutPanel ButtonPanel;
        private Button CancelSettingsButton;
        private Button OkSettingsButton;
    }
}
