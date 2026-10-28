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
            RootLayout = new TableLayoutPanel();
            SettingsTabs = new TabControl();
            GeneralTabPage = new TabPage();
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
            DiagramLayoutTabPage = new TabPage();
            DiagramLayoutPanel = new TableLayoutPanel();
            LayoutFeatureIntroLabel = new Label();
            IncrementalRoutingCheckBox = new CheckBox();
            RouteAwareConstructionCheckBox = new CheckBox();
            WidthConstrainedLayoutCheckBox = new CheckBox();
            TargetWidthLabel = new Label();
            TargetWidthNumeric = new NumericUpDown();
            WidthHelpLabel = new Label();
            PendingFeaturesLabel = new Label();
            ButtonPanel = new FlowLayoutPanel();
            CancelSettingsButton = new Button();
            OkSettingsButton = new Button();
            RootLayout.SuspendLayout();
            SettingsTabs.SuspendLayout();
            GeneralTabPage.SuspendLayout();
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
            DiagramLayoutTabPage.SuspendLayout();
            DiagramLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)TargetWidthNumeric).BeginInit();
            ButtonPanel.SuspendLayout();
            SuspendLayout();
            // 
            // RootLayout
            // 
            RootLayout.ColumnCount = 1;
            RootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            RootLayout.Controls.Add(SettingsTabs, 0, 0);
            RootLayout.Controls.Add(ButtonPanel, 0, 1);
            RootLayout.Dock = DockStyle.Fill;
            RootLayout.Location = new Point(14, 16);
            RootLayout.Margin = new Padding(0);
            RootLayout.Name = "RootLayout";
            RootLayout.RowCount = 2;
            RootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            RootLayout.RowStyles.Add(new RowStyle());
            RootLayout.Size = new Size(576, 584);
            RootLayout.TabIndex = 0;
            // 
            // SettingsTabs
            // 
            SettingsTabs.Controls.Add(GeneralTabPage);
            SettingsTabs.Controls.Add(DiagramLayoutTabPage);
            SettingsTabs.Dock = DockStyle.Fill;
            SettingsTabs.Location = new Point(0, 0);
            SettingsTabs.Margin = new Padding(0);
            SettingsTabs.Name = "SettingsTabs";
            SettingsTabs.SelectedIndex = 0;
            SettingsTabs.Size = new Size(576, 520);
            SettingsTabs.TabIndex = 0;
            // 
            // GeneralTabPage
            // 
            GeneralTabPage.AutoScroll = true;
            GeneralTabPage.Controls.Add(SettingsLayout);
            GeneralTabPage.Location = new Point(4, 29);
            GeneralTabPage.Name = "GeneralTabPage";
            GeneralTabPage.Padding = new Padding(12);
            GeneralTabPage.Size = new Size(568, 487);
            GeneralTabPage.TabIndex = 0;
            GeneralTabPage.Text = "General";
            GeneralTabPage.UseVisualStyleBackColor = true;
            // 
            // SettingsLayout
            // 
            SettingsLayout.AutoSize = true;
            SettingsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SettingsLayout.ColumnCount = 2;
            SettingsLayout.ColumnStyles.Add(new ColumnStyle());
            SettingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            SettingsLayout.Controls.Add(AutoIndentCheckBox, 0, 0);
            SettingsLayout.Controls.Add(InsertSpacesCheckBox, 0, 1);
            SettingsLayout.Controls.Add(IndentSizeLabel, 0, 2);
            SettingsLayout.Controls.Add(IndentSizeNumeric, 1, 2);
            SettingsLayout.Controls.Add(WordWrapCheckBox, 0, 3);
            SettingsLayout.Controls.Add(FontSizeLabel, 0, 4);
            SettingsLayout.Controls.Add(FontSizeNumeric, 1, 4);
            SettingsLayout.Controls.Add(RenderDelayLabel, 0, 5);
            SettingsLayout.Controls.Add(RenderDelayNumeric, 1, 5);
            SettingsLayout.Controls.Add(HighlightDelayLabel, 0, 6);
            SettingsLayout.Controls.Add(HighlightDelayNumeric, 1, 6);
            SettingsLayout.Controls.Add(SelectionColourLabel, 0, 7);
            SettingsLayout.Controls.Add(SelectionColourPanel, 1, 7);
            SettingsLayout.Controls.Add(ToolbarGroup, 0, 8);
            SettingsLayout.Controls.Add(EditorGroup, 0, 9);
            SettingsLayout.Dock = DockStyle.Top;
            SettingsLayout.Location = new Point(12, 12);
            SettingsLayout.Margin = new Padding(0);
            SettingsLayout.Name = "SettingsLayout";
            SettingsLayout.RowCount = 10;
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.RowStyles.Add(new RowStyle());
            SettingsLayout.Size = new Size(544, 433);
            SettingsLayout.TabIndex = 0;
            // 
            // AutoIndentCheckBox
            // 
            AutoIndentCheckBox.Anchor = AnchorStyles.Left;
            AutoIndentCheckBox.AutoSize = true;
            SettingsLayout.SetColumnSpan(AutoIndentCheckBox, 2);
            AutoIndentCheckBox.Location = new Point(3, 5);
            AutoIndentCheckBox.Margin = new Padding(3, 5, 3, 5);
            AutoIndentCheckBox.Name = "AutoIndentCheckBox";
            AutoIndentCheckBox.Size = new Size(222, 21);
            AutoIndentCheckBox.TabIndex = 0;
            AutoIndentCheckBox.Text = "Preserve indentation on new lines";
            AutoIndentCheckBox.UseVisualStyleBackColor = true;
            // 
            // InsertSpacesCheckBox
            // 
            InsertSpacesCheckBox.Anchor = AnchorStyles.Left;
            InsertSpacesCheckBox.AutoSize = true;
            SettingsLayout.SetColumnSpan(InsertSpacesCheckBox, 2);
            InsertSpacesCheckBox.Location = new Point(3, 36);
            InsertSpacesCheckBox.Margin = new Padding(3, 5, 3, 5);
            InsertSpacesCheckBox.Name = "InsertSpacesCheckBox";
            InsertSpacesCheckBox.Size = new Size(226, 21);
            InsertSpacesCheckBox.TabIndex = 1;
            InsertSpacesCheckBox.Text = "Insert spaces when Tab is pressed";
            InsertSpacesCheckBox.UseVisualStyleBackColor = true;
            // 
            // IndentSizeLabel
            // 
            IndentSizeLabel.Anchor = AnchorStyles.Left;
            IndentSizeLabel.AutoSize = true;
            IndentSizeLabel.Location = new Point(3, 70);
            IndentSizeLabel.Margin = new Padding(3, 5, 14, 5);
            IndentSizeLabel.Name = "IndentSizeLabel";
            IndentSizeLabel.Size = new Size(80, 20);
            IndentSizeLabel.TabIndex = 2;
            IndentSizeLabel.Text = "Indent size";
            // 
            // IndentSizeNumeric
            // 
            IndentSizeNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            IndentSizeNumeric.Location = new Point(254, 67);
            IndentSizeNumeric.Margin = new Padding(3, 5, 3, 5);
            IndentSizeNumeric.Maximum = new decimal(new int[] { 16, 0, 0, 0 });
            IndentSizeNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            IndentSizeNumeric.Name = "IndentSizeNumeric";
            IndentSizeNumeric.Size = new Size(287, 23);
            IndentSizeNumeric.TabIndex = 2;
            IndentSizeNumeric.Value = new decimal(new int[] { 2, 0, 0, 0 });
            // 
            // WordWrapCheckBox
            // 
            WordWrapCheckBox.Anchor = AnchorStyles.Left;
            WordWrapCheckBox.AutoSize = true;
            SettingsLayout.SetColumnSpan(WordWrapCheckBox, 2);
            WordWrapCheckBox.Location = new Point(3, 104);
            WordWrapCheckBox.Margin = new Padding(3, 5, 3, 5);
            WordWrapCheckBox.Name = "WordWrapCheckBox";
            WordWrapCheckBox.Size = new Size(158, 21);
            WordWrapCheckBox.TabIndex = 3;
            WordWrapCheckBox.Text = "Wrap long editor lines";
            WordWrapCheckBox.UseVisualStyleBackColor = true;
            // 
            // FontSizeLabel
            // 
            FontSizeLabel.Anchor = AnchorStyles.Left;
            FontSizeLabel.AutoSize = true;
            FontSizeLabel.Location = new Point(3, 138);
            FontSizeLabel.Margin = new Padding(3, 5, 14, 5);
            FontSizeLabel.Name = "FontSizeLabel";
            FontSizeLabel.Size = new Size(109, 20);
            FontSizeLabel.TabIndex = 4;
            FontSizeLabel.Text = "Editor font size";
            // 
            // FontSizeNumeric
            // 
            FontSizeNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            FontSizeNumeric.DecimalPlaces = 1;
            FontSizeNumeric.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            FontSizeNumeric.Location = new Point(254, 135);
            FontSizeNumeric.Margin = new Padding(3, 5, 3, 5);
            FontSizeNumeric.Maximum = new decimal(new int[] { 32, 0, 0, 0 });
            FontSizeNumeric.Minimum = new decimal(new int[] { 8, 0, 0, 0 });
            FontSizeNumeric.Name = "FontSizeNumeric";
            FontSizeNumeric.Size = new Size(287, 23);
            FontSizeNumeric.TabIndex = 4;
            FontSizeNumeric.Value = new decimal(new int[] { 11, 0, 0, 0 });
            // 
            // RenderDelayLabel
            // 
            RenderDelayLabel.Anchor = AnchorStyles.Left;
            RenderDelayLabel.AutoSize = true;
            RenderDelayLabel.Location = new Point(3, 175);
            RenderDelayLabel.Margin = new Padding(3, 5, 14, 5);
            RenderDelayLabel.Name = "RenderDelayLabel";
            RenderDelayLabel.Size = new Size(196, 20);
            RenderDelayLabel.TabIndex = 5;
            RenderDelayLabel.Text = "Preview delay (milliseconds)";
            // 
            // RenderDelayNumeric
            // 
            RenderDelayNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            RenderDelayNumeric.Increment = new decimal(new int[] { 50, 0, 0, 0 });
            RenderDelayNumeric.Location = new Point(254, 172);
            RenderDelayNumeric.Margin = new Padding(3, 5, 3, 5);
            RenderDelayNumeric.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
            RenderDelayNumeric.Minimum = new decimal(new int[] { 50, 0, 0, 0 });
            RenderDelayNumeric.Name = "RenderDelayNumeric";
            RenderDelayNumeric.Size = new Size(287, 23);
            RenderDelayNumeric.TabIndex = 5;
            RenderDelayNumeric.Value = new decimal(new int[] { 350, 0, 0, 0 });
            // 
            // HighlightDelayLabel
            // 
            HighlightDelayLabel.Anchor = AnchorStyles.Left;
            HighlightDelayLabel.AutoSize = true;
            HighlightDelayLabel.Location = new Point(3, 212);
            HighlightDelayLabel.Margin = new Padding(3, 5, 14, 5);
            HighlightDelayLabel.Name = "HighlightDelayLabel";
            HighlightDelayLabel.Size = new Size(234, 20);
            HighlightDelayLabel.TabIndex = 6;
            HighlightDelayLabel.Text = "Syntax colour delay (milliseconds)";
            // 
            // HighlightDelayNumeric
            // 
            HighlightDelayNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            HighlightDelayNumeric.Increment = new decimal(new int[] { 50, 0, 0, 0 });
            HighlightDelayNumeric.Location = new Point(254, 209);
            HighlightDelayNumeric.Margin = new Padding(3, 5, 3, 5);
            HighlightDelayNumeric.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
            HighlightDelayNumeric.Minimum = new decimal(new int[] { 50, 0, 0, 0 });
            HighlightDelayNumeric.Name = "HighlightDelayNumeric";
            HighlightDelayNumeric.Size = new Size(287, 23);
            HighlightDelayNumeric.TabIndex = 6;
            HighlightDelayNumeric.Value = new decimal(new int[] { 250, 0, 0, 0 });
            // 
            // SelectionColourLabel
            // 
            SelectionColourLabel.Anchor = AnchorStyles.Left;
            SelectionColourLabel.AutoSize = true;
            SelectionColourLabel.Location = new Point(3, 256);
            SelectionColourLabel.Margin = new Padding(3, 5, 14, 5);
            SelectionColourLabel.Name = "SelectionColourLabel";
            SelectionColourLabel.Size = new Size(116, 20);
            SelectionColourLabel.TabIndex = 7;
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
            SelectionColourPanel.Location = new Point(251, 242);
            SelectionColourPanel.Margin = new Padding(0, 1, 0, 1);
            SelectionColourPanel.Name = "SelectionColourPanel";
            SelectionColourPanel.Size = new Size(203, 48);
            SelectionColourPanel.TabIndex = 7;
            SelectionColourPanel.WrapContents = false;
            // 
            // SelectionColourSwatch
            // 
            SelectionColourSwatch.Anchor = AnchorStyles.Left;
            SelectionColourSwatch.BorderStyle = BorderStyle.FixedSingle;
            SelectionColourSwatch.Cursor = Cursors.Hand;
            SelectionColourSwatch.Location = new Point(3, 13);
            SelectionColourSwatch.Margin = new Padding(3, 5, 3, 5);
            SelectionColourSwatch.Name = "SelectionColourSwatch";
            SelectionColourSwatch.Size = new Size(25, 21);
            SelectionColourSwatch.TabIndex = 0;
            SelectionColourSwatch.Click += SelectionColourButton_Click;
            // 
            // SelectionColourValueLabel
            // 
            SelectionColourValueLabel.Anchor = AnchorStyles.Left;
            SelectionColourValueLabel.AutoSize = true;
            SelectionColourValueLabel.Location = new Point(34, 14);
            SelectionColourValueLabel.Margin = new Padding(3, 5, 7, 5);
            SelectionColourValueLabel.Name = "SelectionColourValueLabel";
            SelectionColourValueLabel.Size = new Size(66, 20);
            SelectionColourValueLabel.TabIndex = 1;
            SelectionColourValueLabel.Text = "#1E1E1E";
            // 
            // SelectionColourButton
            // 
            SelectionColourButton.Anchor = AnchorStyles.Left;
            SelectionColourButton.AutoSize = true;
            SelectionColourButton.Location = new Point(110, 4);
            SelectionColourButton.Margin = new Padding(3, 4, 3, 4);
            SelectionColourButton.Name = "SelectionColourButton";
            SelectionColourButton.Size = new Size(90, 40);
            SelectionColourButton.TabIndex = 0;
            SelectionColourButton.Text = "Choose…";
            SelectionColourButton.UseVisualStyleBackColor = true;
            SelectionColourButton.Click += SelectionColourButton_Click;
            // 
            // ToolbarGroup
            // 
            ToolbarGroup.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            ToolbarGroup.AutoSize = true;
            ToolbarGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SettingsLayout.SetColumnSpan(ToolbarGroup, 2);
            ToolbarGroup.Controls.Add(ToolbarOptions);
            ToolbarGroup.Location = new Point(3, 302);
            ToolbarGroup.Margin = new Padding(3, 11, 3, 5);
            ToolbarGroup.Name = "ToolbarGroup";
            ToolbarGroup.Padding = new Padding(9, 5, 9, 8);
            ToolbarGroup.Size = new Size(538, 58);
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
            ToolbarOptions.Location = new Point(9, 21);
            ToolbarOptions.Margin = new Padding(0);
            ToolbarOptions.Name = "ToolbarOptions";
            ToolbarOptions.Size = new Size(520, 29);
            ToolbarOptions.TabIndex = 0;
            ToolbarOptions.WrapContents = false;
            // 
            // WideToolbarRadio
            // 
            WideToolbarRadio.AutoSize = true;
            WideToolbarRadio.Checked = true;
            WideToolbarRadio.Location = new Point(3, 4);
            WideToolbarRadio.Margin = new Padding(3, 4, 27, 4);
            WideToolbarRadio.Name = "WideToolbarRadio";
            WideToolbarRadio.Size = new Size(56, 21);
            WideToolbarRadio.TabIndex = 0;
            WideToolbarRadio.TabStop = true;
            WideToolbarRadio.Text = "Wide";
            WideToolbarRadio.UseVisualStyleBackColor = true;
            // 
            // NarrowToolbarRadio
            // 
            NarrowToolbarRadio.AutoSize = true;
            NarrowToolbarRadio.Location = new Point(89, 4);
            NarrowToolbarRadio.Margin = new Padding(3, 4, 3, 4);
            NarrowToolbarRadio.Name = "NarrowToolbarRadio";
            NarrowToolbarRadio.Size = new Size(70, 21);
            NarrowToolbarRadio.TabIndex = 1;
            NarrowToolbarRadio.Text = "Narrow";
            NarrowToolbarRadio.UseVisualStyleBackColor = true;
            // 
            // EditorGroup
            // 
            EditorGroup.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            EditorGroup.AutoSize = true;
            EditorGroup.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SettingsLayout.SetColumnSpan(EditorGroup, 2);
            EditorGroup.Controls.Add(EditorOptions);
            EditorGroup.Location = new Point(3, 370);
            EditorGroup.Margin = new Padding(3, 5, 3, 5);
            EditorGroup.Name = "EditorGroup";
            EditorGroup.Padding = new Padding(9, 5, 9, 8);
            EditorGroup.Size = new Size(538, 58);
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
            EditorOptions.Location = new Point(9, 21);
            EditorOptions.Margin = new Padding(0);
            EditorOptions.Name = "EditorOptions";
            EditorOptions.Size = new Size(520, 29);
            EditorOptions.TabIndex = 0;
            EditorOptions.WrapContents = false;
            // 
            // EditorLeftRadio
            // 
            EditorLeftRadio.AutoSize = true;
            EditorLeftRadio.Checked = true;
            EditorLeftRadio.Location = new Point(3, 4);
            EditorLeftRadio.Margin = new Padding(3, 4, 27, 4);
            EditorLeftRadio.Name = "EditorLeftRadio";
            EditorLeftRadio.Size = new Size(47, 21);
            EditorLeftRadio.TabIndex = 0;
            EditorLeftRadio.TabStop = true;
            EditorLeftRadio.Text = "Left";
            EditorLeftRadio.UseVisualStyleBackColor = true;
            // 
            // EditorRightRadio
            // 
            EditorRightRadio.AutoSize = true;
            EditorRightRadio.Location = new Point(80, 4);
            EditorRightRadio.Margin = new Padding(3, 4, 3, 4);
            EditorRightRadio.Name = "EditorRightRadio";
            EditorRightRadio.Size = new Size(56, 21);
            EditorRightRadio.TabIndex = 1;
            EditorRightRadio.Text = "Right";
            EditorRightRadio.UseVisualStyleBackColor = true;
            // 
            // DiagramLayoutTabPage
            // 
            DiagramLayoutTabPage.AutoScroll = true;
            DiagramLayoutTabPage.Controls.Add(DiagramLayoutPanel);
            DiagramLayoutTabPage.Location = new Point(4, 29);
            DiagramLayoutTabPage.Name = "DiagramLayoutTabPage";
            DiagramLayoutTabPage.Padding = new Padding(12);
            DiagramLayoutTabPage.Size = new Size(566, 479);
            DiagramLayoutTabPage.TabIndex = 1;
            DiagramLayoutTabPage.Text = "Diagram layout";
            DiagramLayoutTabPage.UseVisualStyleBackColor = true;
            // 
            // DiagramLayoutPanel
            // 
            DiagramLayoutPanel.AutoSize = true;
            DiagramLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            DiagramLayoutPanel.ColumnCount = 2;
            DiagramLayoutPanel.ColumnStyles.Add(new ColumnStyle());
            DiagramLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            DiagramLayoutPanel.Controls.Add(LayoutFeatureIntroLabel, 0, 0);
            DiagramLayoutPanel.Controls.Add(IncrementalRoutingCheckBox, 0, 1);
            DiagramLayoutPanel.Controls.Add(RouteAwareConstructionCheckBox, 0, 2);
            DiagramLayoutPanel.Controls.Add(WidthConstrainedLayoutCheckBox, 0, 3);
            DiagramLayoutPanel.Controls.Add(TargetWidthLabel, 0, 4);
            DiagramLayoutPanel.Controls.Add(TargetWidthNumeric, 1, 4);
            DiagramLayoutPanel.Controls.Add(WidthHelpLabel, 0, 5);
            DiagramLayoutPanel.Controls.Add(PendingFeaturesLabel, 0, 6);
            DiagramLayoutPanel.Dock = DockStyle.Top;
            DiagramLayoutPanel.Location = new Point(12, 12);
            DiagramLayoutPanel.Margin = new Padding(0);
            DiagramLayoutPanel.Name = "DiagramLayoutPanel";
            DiagramLayoutPanel.RowCount = 7;
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.RowStyles.Add(new RowStyle());
            DiagramLayoutPanel.Size = new Size(542, 312);
            DiagramLayoutPanel.TabIndex = 0;
            // 
            // LayoutFeatureIntroLabel
            // 
            LayoutFeatureIntroLabel.AutoSize = true;
            DiagramLayoutPanel.SetColumnSpan(LayoutFeatureIntroLabel, 2);
            LayoutFeatureIntroLabel.Location = new Point(3, 5);
            LayoutFeatureIntroLabel.Margin = new Padding(3, 5, 3, 16);
            LayoutFeatureIntroLabel.MaximumSize = new Size(500, 0);
            LayoutFeatureIntroLabel.Name = "LayoutFeatureIntroLabel";
            LayoutFeatureIntroLabel.Size = new Size(466, 40);
            LayoutFeatureIntroLabel.TabIndex = 0;
            LayoutFeatureIntroLabel.Text = "Experimental layout switches for native Enterprise previews. Changes apply when you choose OK.";
            // 
            // IncrementalRoutingCheckBox
            // 
            IncrementalRoutingCheckBox.AutoSize = true;
            DiagramLayoutPanel.SetColumnSpan(IncrementalRoutingCheckBox, 2);
            IncrementalRoutingCheckBox.Location = new Point(3, 66);
            IncrementalRoutingCheckBox.Margin = new Padding(3, 5, 3, 5);
            IncrementalRoutingCheckBox.Name = "IncrementalRoutingCheckBox";
            IncrementalRoutingCheckBox.Size = new Size(252, 24);
            IncrementalRoutingCheckBox.TabIndex = 1;
            IncrementalRoutingCheckBox.Text = "Incremental diagram routing (AF)";
            IncrementalRoutingCheckBox.UseVisualStyleBackColor = true;
            // 
            // RouteAwareConstructionCheckBox
            // 
            RouteAwareConstructionCheckBox.AutoSize = true;
            DiagramLayoutPanel.SetColumnSpan(RouteAwareConstructionCheckBox, 2);
            RouteAwareConstructionCheckBox.Location = new Point(3, 100);
            RouteAwareConstructionCheckBox.Margin = new Padding(3, 5, 3, 5);
            RouteAwareConstructionCheckBox.Name = "RouteAwareConstructionCheckBox";
            RouteAwareConstructionCheckBox.Size = new Size(295, 24);
            RouteAwareConstructionCheckBox.TabIndex = 2;
            RouteAwareConstructionCheckBox.Text = "Route-aware diagram construction (AG)";
            RouteAwareConstructionCheckBox.UseVisualStyleBackColor = true;
            // 
            // WidthConstrainedLayoutCheckBox
            // 
            WidthConstrainedLayoutCheckBox.AutoSize = true;
            DiagramLayoutPanel.SetColumnSpan(WidthConstrainedLayoutCheckBox, 2);
            WidthConstrainedLayoutCheckBox.Location = new Point(3, 134);
            WidthConstrainedLayoutCheckBox.Margin = new Padding(3, 5, 3, 5);
            WidthConstrainedLayoutCheckBox.Name = "WidthConstrainedLayoutCheckBox";
            WidthConstrainedLayoutCheckBox.Size = new Size(234, 24);
            WidthConstrainedLayoutCheckBox.TabIndex = 3;
            WidthConstrainedLayoutCheckBox.Text = "Width-constrained layout (AH)";
            WidthConstrainedLayoutCheckBox.UseVisualStyleBackColor = true;
            WidthConstrainedLayoutCheckBox.CheckedChanged += WidthConstrainedLayoutCheckBox_CheckedChanged;
            // 
            // TargetWidthLabel
            // 
            TargetWidthLabel.Anchor = AnchorStyles.Left;
            TargetWidthLabel.AutoSize = true;
            TargetWidthLabel.Location = new Point(3, 171);
            TargetWidthLabel.Margin = new Padding(3, 5, 14, 5);
            TargetWidthLabel.Name = "TargetWidthLabel";
            TargetWidthLabel.Size = new Size(188, 20);
            TargetWidthLabel.TabIndex = 4;
            TargetWidthLabel.Text = "Target width (layout pixels)";
            // 
            // TargetWidthNumeric
            // 
            TargetWidthNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            TargetWidthNumeric.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            TargetWidthNumeric.Location = new Point(208, 168);
            TargetWidthNumeric.Margin = new Padding(3, 5, 3, 5);
            TargetWidthNumeric.Maximum = new decimal(new int[] { 20000, 0, 0, 0 });
            TargetWidthNumeric.Minimum = new decimal(new int[] { 200, 0, 0, 0 });
            TargetWidthNumeric.Name = "TargetWidthNumeric";
            TargetWidthNumeric.Size = new Size(331, 27);
            TargetWidthNumeric.TabIndex = 4;
            TargetWidthNumeric.ThousandsSeparator = true;
            TargetWidthNumeric.Value = new decimal(new int[] { 1200, 0, 0, 0 });
            // 
            // WidthHelpLabel
            // 
            WidthHelpLabel.AutoSize = true;
            DiagramLayoutPanel.SetColumnSpan(WidthHelpLabel, 2);
            WidthHelpLabel.Location = new Point(3, 211);
            WidthHelpLabel.Margin = new Padding(3, 11, 3, 5);
            WidthHelpLabel.MaximumSize = new Size(500, 0);
            WidthHelpLabel.Name = "WidthHelpLabel";
            WidthHelpLabel.Size = new Size(493, 40);
            WidthHelpLabel.TabIndex = 5;
            WidthHelpLabel.Text = "Prefer a readable width by wrapping eligible rows. Groups, direction and placement hints can require a wider diagram.";
            // 
            // PendingFeaturesLabel
            // 
            PendingFeaturesLabel.AutoSize = true;
            DiagramLayoutPanel.SetColumnSpan(PendingFeaturesLabel, 2);
            PendingFeaturesLabel.Location = new Point(3, 267);
            PendingFeaturesLabel.Margin = new Padding(3, 11, 3, 5);
            PendingFeaturesLabel.MaximumSize = new Size(500, 0);
            PendingFeaturesLabel.Name = "PendingFeaturesLabel";
            PendingFeaturesLabel.Size = new Size(463, 40);
            PendingFeaturesLabel.TabIndex = 6;
            PendingFeaturesLabel.Text = "AF and AG choices are saved for upcoming routing and construction operations, which are still in development.";
            // 
            // ButtonPanel
            // 
            ButtonPanel.Anchor = AnchorStyles.Right;
            ButtonPanel.AutoSize = true;
            ButtonPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ButtonPanel.Controls.Add(CancelSettingsButton);
            ButtonPanel.Controls.Add(OkSettingsButton);
            ButtonPanel.FlowDirection = FlowDirection.RightToLeft;
            ButtonPanel.Location = new Point(382, 536);
            ButtonPanel.Margin = new Padding(0, 16, 0, 0);
            ButtonPanel.Name = "ButtonPanel";
            ButtonPanel.Size = new Size(194, 48);
            ButtonPanel.TabIndex = 1;
            ButtonPanel.WrapContents = false;
            // 
            // CancelSettingsButton
            // 
            CancelSettingsButton.AutoSize = true;
            CancelSettingsButton.DialogResult = DialogResult.Cancel;
            CancelSettingsButton.Location = new Point(100, 4);
            CancelSettingsButton.Margin = new Padding(3, 4, 3, 4);
            CancelSettingsButton.MinimumSize = new Size(91, 0);
            CancelSettingsButton.Name = "CancelSettingsButton";
            CancelSettingsButton.Size = new Size(91, 40);
            CancelSettingsButton.TabIndex = 1;
            CancelSettingsButton.Text = "Cancel";
            CancelSettingsButton.UseVisualStyleBackColor = true;
            // 
            // OkSettingsButton
            // 
            OkSettingsButton.AutoSize = true;
            OkSettingsButton.DialogResult = DialogResult.OK;
            OkSettingsButton.Location = new Point(3, 4);
            OkSettingsButton.Margin = new Padding(3, 4, 3, 4);
            OkSettingsButton.MinimumSize = new Size(91, 0);
            OkSettingsButton.Name = "OkSettingsButton";
            OkSettingsButton.Size = new Size(91, 40);
            OkSettingsButton.TabIndex = 0;
            OkSettingsButton.Text = "OK";
            OkSettingsButton.UseVisualStyleBackColor = true;
            // 
            // SettingsForm
            // 
            AcceptButton = OkSettingsButton;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = CancelSettingsButton;
            ClientSize = new Size(604, 616);
            Controls.Add(RootLayout);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(3, 4, 3, 4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SettingsForm";
            Padding = new Padding(14, 16, 14, 16);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "LiveEditor Settings";
            RootLayout.ResumeLayout(false);
            RootLayout.PerformLayout();
            SettingsTabs.ResumeLayout(false);
            GeneralTabPage.ResumeLayout(false);
            GeneralTabPage.PerformLayout();
            SettingsLayout.ResumeLayout(false);
            SettingsLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)IndentSizeNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)FontSizeNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)RenderDelayNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)HighlightDelayNumeric).EndInit();
            SelectionColourPanel.ResumeLayout(false);
            SelectionColourPanel.PerformLayout();
            ToolbarGroup.ResumeLayout(false);
            ToolbarGroup.PerformLayout();
            ToolbarOptions.ResumeLayout(false);
            ToolbarOptions.PerformLayout();
            EditorGroup.ResumeLayout(false);
            EditorGroup.PerformLayout();
            EditorOptions.ResumeLayout(false);
            EditorOptions.PerformLayout();
            DiagramLayoutTabPage.ResumeLayout(false);
            DiagramLayoutTabPage.PerformLayout();
            DiagramLayoutPanel.ResumeLayout(false);
            DiagramLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)TargetWidthNumeric).EndInit();
            ButtonPanel.ResumeLayout(false);
            ButtonPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel RootLayout;
        private TabControl SettingsTabs;
        private TabPage GeneralTabPage;
        private TabPage DiagramLayoutTabPage;
        private TableLayoutPanel DiagramLayoutPanel;
        private Label LayoutFeatureIntroLabel;
        private CheckBox IncrementalRoutingCheckBox;
        private CheckBox RouteAwareConstructionCheckBox;
        private CheckBox WidthConstrainedLayoutCheckBox;
        private Label TargetWidthLabel;
        private NumericUpDown TargetWidthNumeric;
        private Label WidthHelpLabel;
        private Label PendingFeaturesLabel;
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
