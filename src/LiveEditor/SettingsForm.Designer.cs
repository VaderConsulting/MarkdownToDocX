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
            ToolbarGroup = new GroupBox();
            WideToolbarRadio = new RadioButton();
            NarrowToolbarRadio = new RadioButton();
            EditorGroup = new GroupBox();
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
            ButtonPanel.SuspendLayout();
            SuspendLayout();
            //
            // SettingsLayout
            //
            SettingsLayout.ColumnCount = 2;
            SettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            SettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
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
            SettingsLayout.Controls.Add(ToolbarGroup, 0, 7);
            SettingsLayout.SetColumnSpan(ToolbarGroup, 2);
            SettingsLayout.Controls.Add(EditorGroup, 0, 8);
            SettingsLayout.SetColumnSpan(EditorGroup, 2);
            SettingsLayout.Dock = DockStyle.Fill;
            SettingsLayout.Location = new Point(12, 12);
            SettingsLayout.Name = "SettingsLayout";
            SettingsLayout.RowCount = 10;
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            SettingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            SettingsLayout.Size = new Size(476, 408);
            SettingsLayout.TabIndex = 0;
            //
            // AutoIndentCheckBox
            //
            AutoIndentCheckBox.Anchor = AnchorStyles.Left;
            AutoIndentCheckBox.AutoSize = true;
            AutoIndentCheckBox.Name = "AutoIndentCheckBox";
            AutoIndentCheckBox.Text = "Preserve indentation on new lines";
            AutoIndentCheckBox.UseVisualStyleBackColor = true;
            //
            // InsertSpacesCheckBox
            //
            InsertSpacesCheckBox.Anchor = AnchorStyles.Left;
            InsertSpacesCheckBox.AutoSize = true;
            InsertSpacesCheckBox.Name = "InsertSpacesCheckBox";
            InsertSpacesCheckBox.Text = "Insert spaces when Tab is pressed";
            InsertSpacesCheckBox.UseVisualStyleBackColor = true;
            //
            // IndentSizeLabel
            //
            IndentSizeLabel.Anchor = AnchorStyles.Left;
            IndentSizeLabel.AutoSize = true;
            IndentSizeLabel.Name = "IndentSizeLabel";
            IndentSizeLabel.Text = "Indent size";
            //
            // IndentSizeNumeric
            //
            IndentSizeNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            IndentSizeNumeric.Maximum = 16;
            IndentSizeNumeric.Minimum = 1;
            IndentSizeNumeric.Name = "IndentSizeNumeric";
            IndentSizeNumeric.TabIndex = 2;
            IndentSizeNumeric.Value = 2;
            //
            // WordWrapCheckBox
            //
            WordWrapCheckBox.Anchor = AnchorStyles.Left;
            WordWrapCheckBox.AutoSize = true;
            WordWrapCheckBox.Name = "WordWrapCheckBox";
            WordWrapCheckBox.Text = "Wrap long editor lines";
            WordWrapCheckBox.UseVisualStyleBackColor = true;
            //
            // FontSizeLabel
            //
            FontSizeLabel.Anchor = AnchorStyles.Left;
            FontSizeLabel.AutoSize = true;
            FontSizeLabel.Name = "FontSizeLabel";
            FontSizeLabel.Text = "Editor font size";
            //
            // FontSizeNumeric
            //
            FontSizeNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            FontSizeNumeric.DecimalPlaces = 1;
            FontSizeNumeric.Increment = 0.5M;
            FontSizeNumeric.Maximum = 32;
            FontSizeNumeric.Minimum = 8;
            FontSizeNumeric.Name = "FontSizeNumeric";
            FontSizeNumeric.TabIndex = 4;
            FontSizeNumeric.Value = 11;
            //
            // RenderDelayLabel
            //
            RenderDelayLabel.Anchor = AnchorStyles.Left;
            RenderDelayLabel.AutoSize = true;
            RenderDelayLabel.Name = "RenderDelayLabel";
            RenderDelayLabel.Text = "Preview delay (milliseconds)";
            //
            // RenderDelayNumeric
            //
            RenderDelayNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            RenderDelayNumeric.Increment = 50;
            RenderDelayNumeric.Maximum = 5000;
            RenderDelayNumeric.Minimum = 50;
            RenderDelayNumeric.Name = "RenderDelayNumeric";
            RenderDelayNumeric.TabIndex = 5;
            RenderDelayNumeric.Value = 350;
            //
            // HighlightDelayLabel
            //
            HighlightDelayLabel.Anchor = AnchorStyles.Left;
            HighlightDelayLabel.AutoSize = true;
            HighlightDelayLabel.Name = "HighlightDelayLabel";
            HighlightDelayLabel.Text = "Syntax colour delay (milliseconds)";
            //
            // HighlightDelayNumeric
            //
            HighlightDelayNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            HighlightDelayNumeric.Increment = 50;
            HighlightDelayNumeric.Maximum = 5000;
            HighlightDelayNumeric.Minimum = 50;
            HighlightDelayNumeric.Name = "HighlightDelayNumeric";
            HighlightDelayNumeric.TabIndex = 6;
            HighlightDelayNumeric.Value = 250;
            //
            // ToolbarGroup
            //
            ToolbarGroup.Controls.Add(WideToolbarRadio);
            ToolbarGroup.Controls.Add(NarrowToolbarRadio);
            ToolbarGroup.Dock = DockStyle.Fill;
            ToolbarGroup.Name = "ToolbarGroup";
            ToolbarGroup.TabIndex = 7;
            ToolbarGroup.Text = "Toolbar";
            //
            // WideToolbarRadio
            //
            WideToolbarRadio.AutoSize = true;
            WideToolbarRadio.Checked = true;
            WideToolbarRadio.Location = new Point(12, 22);
            WideToolbarRadio.Name = "WideToolbarRadio";
            WideToolbarRadio.TabIndex = 0;
            WideToolbarRadio.TabStop = true;
            WideToolbarRadio.Text = "Wide";
            WideToolbarRadio.UseVisualStyleBackColor = true;
            //
            // NarrowToolbarRadio
            //
            NarrowToolbarRadio.AutoSize = true;
            NarrowToolbarRadio.Location = new Point(90, 22);
            NarrowToolbarRadio.Name = "NarrowToolbarRadio";
            NarrowToolbarRadio.TabIndex = 1;
            NarrowToolbarRadio.Text = "Narrow";
            NarrowToolbarRadio.UseVisualStyleBackColor = true;
            //
            // EditorGroup
            //
            EditorGroup.Controls.Add(EditorLeftRadio);
            EditorGroup.Controls.Add(EditorRightRadio);
            EditorGroup.Dock = DockStyle.Fill;
            EditorGroup.Name = "EditorGroup";
            EditorGroup.TabIndex = 8;
            EditorGroup.Text = "Editor";
            //
            // EditorLeftRadio
            //
            EditorLeftRadio.AutoSize = true;
            EditorLeftRadio.Checked = true;
            EditorLeftRadio.Location = new Point(12, 22);
            EditorLeftRadio.Name = "EditorLeftRadio";
            EditorLeftRadio.TabIndex = 0;
            EditorLeftRadio.TabStop = true;
            EditorLeftRadio.Text = "Left";
            EditorLeftRadio.UseVisualStyleBackColor = true;
            //
            // EditorRightRadio
            //
            EditorRightRadio.AutoSize = true;
            EditorRightRadio.Location = new Point(90, 22);
            EditorRightRadio.Name = "EditorRightRadio";
            EditorRightRadio.TabIndex = 1;
            EditorRightRadio.Text = "Right";
            EditorRightRadio.UseVisualStyleBackColor = true;
            //
            // ButtonPanel
            //
            ButtonPanel.AutoSize = true;
            ButtonPanel.Controls.Add(CancelSettingsButton);
            ButtonPanel.Controls.Add(OkSettingsButton);
            ButtonPanel.Dock = DockStyle.Bottom;
            ButtonPanel.FlowDirection = FlowDirection.RightToLeft;
            ButtonPanel.Location = new Point(12, 420);
            ButtonPanel.Name = "ButtonPanel";
            ButtonPanel.Padding = new Padding(0, 10, 0, 0);
            ButtonPanel.Size = new Size(476, 51);
            ButtonPanel.TabIndex = 1;
            //
            // CancelSettingsButton
            //
            CancelSettingsButton.AutoSize = true;
            CancelSettingsButton.DialogResult = DialogResult.Cancel;
            CancelSettingsButton.Name = "CancelSettingsButton";
            CancelSettingsButton.TabIndex = 1;
            CancelSettingsButton.Text = "Cancel";
            CancelSettingsButton.UseVisualStyleBackColor = true;
            //
            // OkSettingsButton
            //
            OkSettingsButton.AutoSize = true;
            OkSettingsButton.DialogResult = DialogResult.OK;
            OkSettingsButton.Name = "OkSettingsButton";
            OkSettingsButton.TabIndex = 0;
            OkSettingsButton.Text = "OK";
            OkSettingsButton.UseVisualStyleBackColor = true;
            //
            // SettingsForm
            //
            AcceptButton = OkSettingsButton;
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = CancelSettingsButton;
            ClientSize = new Size(500, 483);
            Controls.Add(SettingsLayout);
            Controls.Add(ButtonPanel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SettingsForm";
            Padding = new Padding(12);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "LiveEditor Settings";
            SettingsLayout.ResumeLayout(false);
            SettingsLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)IndentSizeNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)FontSizeNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)RenderDelayNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)HighlightDelayNumeric).EndInit();
            ButtonPanel.ResumeLayout(false);
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
        private GroupBox ToolbarGroup;
        private RadioButton WideToolbarRadio;
        private RadioButton NarrowToolbarRadio;
        private GroupBox EditorGroup;
        private RadioButton EditorLeftRadio;
        private RadioButton EditorRightRadio;
        private FlowLayoutPanel ButtonPanel;
        private Button CancelSettingsButton;
        private Button OkSettingsButton;
    }
}
