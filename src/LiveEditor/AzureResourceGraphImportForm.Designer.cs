using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class AzureResourceGraphImportForm
    {
        private System.ComponentModel.IContainer _Components = null;

        protected override void Dispose(bool Disposing)
        {
            if (Disposing && (_Components != null))
            {
                _Components.Dispose();
            }

            base.Dispose(Disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            MainLayout = new TableLayoutPanel();
            SummaryLabel = new Label();
            TitleLabel = new Label();
            TitleTextBox = new TextBox();
            CategoryList = new CheckedListBox();
            SelectedCountLabel = new Label();
            PresetButtonRow = new FlowLayoutPanel();
            EnterpriseDefaultsButton = new Button();
            SelectAllButton = new Button();
            SelectNoneButton = new Button();
            LayoutOptionsGroup = new GroupBox();
            LayoutOptionsFlow = new FlowLayoutPanel();
            ShowInfrastructureDetailCheckBox = new CheckBox();
            GroupBySubscriptionCheckBox = new CheckBox();
            LinkNestedResourcesCheckBox = new CheckBox();
            LinkHiddenTagsCheckBox = new CheckBox();
            DialogButtonRow = new FlowLayoutPanel();
            OkButton = new Button();
            CancelImportButton = new Button();
            MainLayout.SuspendLayout();
            PresetButtonRow.SuspendLayout();
            LayoutOptionsGroup.SuspendLayout();
            LayoutOptionsFlow.SuspendLayout();
            DialogButtonRow.SuspendLayout();
            SuspendLayout();
            // 
            // MainLayout
            // 
            MainLayout.ColumnCount = 1;
            MainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            MainLayout.Controls.Add(SummaryLabel, 0, 0);
            MainLayout.Controls.Add(TitleLabel, 0, 1);
            MainLayout.Controls.Add(TitleTextBox, 0, 2);
            MainLayout.Controls.Add(CategoryList, 0, 3);
            MainLayout.Controls.Add(SelectedCountLabel, 0, 4);
            MainLayout.Controls.Add(PresetButtonRow, 0, 5);
            MainLayout.Controls.Add(LayoutOptionsGroup, 0, 6);
            MainLayout.Controls.Add(DialogButtonRow, 0, 7);
            MainLayout.Dock = DockStyle.Fill;
            MainLayout.Name = "MainLayout";
            MainLayout.Padding = new Padding(0, 0, 0, 8);
            MainLayout.RowCount = 8;
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.RowStyles.Add(new RowStyle());
            MainLayout.TabIndex = 0;
            // 
            // SummaryLabel
            // 
            SummaryLabel.AutoSize = true;
            SummaryLabel.Dock = DockStyle.Fill;
            SummaryLabel.Location = new Point(3, 0);
            SummaryLabel.Margin = new Padding(3, 0, 3, 8);
            SummaryLabel.Name = "SummaryLabel";
            SummaryLabel.Size = new Size(554, 20);
            SummaryLabel.TabIndex = 0;
            SummaryLabel.Text = "Choose which resource types to include in the enterprise diagram.";
            // 
            // TitleLabel
            // 
            TitleLabel.AutoSize = true;
            TitleLabel.Location = new Point(3, 28);
            TitleLabel.Margin = new Padding(3, 0, 3, 4);
            TitleLabel.Name = "TitleLabel";
            TitleLabel.Size = new Size(88, 20);
            TitleLabel.TabIndex = 1;
            TitleLabel.Text = "Diagram title";
            // 
            // TitleTextBox
            // 
            TitleTextBox.Dock = DockStyle.Fill;
            TitleTextBox.Location = new Point(3, 52);
            TitleTextBox.Margin = new Padding(3, 0, 3, 8);
            TitleTextBox.Name = "TitleTextBox";
            TitleTextBox.Size = new Size(554, 27);
            TitleTextBox.TabIndex = 2;
            // 
            // CategoryList
            // 
            CategoryList.CheckOnClick = true;
            CategoryList.Dock = DockStyle.Fill;
            CategoryList.FormattingEnabled = true;
            CategoryList.Location = new Point(3, 87);
            CategoryList.Margin = new Padding(3, 0, 3, 4);
            CategoryList.Name = "CategoryList";
            CategoryList.Size = new Size(554, 244);
            CategoryList.TabIndex = 3;
            CategoryList.ItemCheck += CategoryList_ItemCheck;
            // 
            // SelectedCountLabel
            // 
            SelectedCountLabel.AutoSize = true;
            SelectedCountLabel.Location = new Point(3, 335);
            SelectedCountLabel.Margin = new Padding(3, 0, 3, 4);
            SelectedCountLabel.Name = "SelectedCountLabel";
            SelectedCountLabel.Size = new Size(168, 20);
            SelectedCountLabel.TabIndex = 4;
            SelectedCountLabel.Text = "0 categories selected (0 rows).";
            // 
            // PresetButtonRow
            // 
            PresetButtonRow.AutoSize = true;
            PresetButtonRow.Controls.Add(EnterpriseDefaultsButton);
            PresetButtonRow.Controls.Add(SelectAllButton);
            PresetButtonRow.Controls.Add(SelectNoneButton);
            PresetButtonRow.Dock = DockStyle.Fill;
            PresetButtonRow.Location = new Point(3, 359);
            PresetButtonRow.Margin = new Padding(3, 0, 3, 8);
            PresetButtonRow.Name = "PresetButtonRow";
            PresetButtonRow.Size = new Size(554, 35);
            PresetButtonRow.TabIndex = 5;
            // 
            // EnterpriseDefaultsButton
            // 
            EnterpriseDefaultsButton.AutoSize = true;
            EnterpriseDefaultsButton.Location = new Point(0, 0);
            EnterpriseDefaultsButton.Margin = new Padding(0, 0, 8, 0);
            EnterpriseDefaultsButton.Name = "EnterpriseDefaultsButton";
            EnterpriseDefaultsButton.Size = new Size(150, 35);
            EnterpriseDefaultsButton.TabIndex = 0;
            EnterpriseDefaultsButton.Text = "Enterprise defaults";
            EnterpriseDefaultsButton.UseVisualStyleBackColor = true;
            EnterpriseDefaultsButton.Click += EnterpriseDefaultsButton_Click;
            // 
            // SelectAllButton
            // 
            SelectAllButton.AutoSize = true;
            SelectAllButton.Location = new Point(158, 0);
            SelectAllButton.Margin = new Padding(0, 0, 8, 0);
            SelectAllButton.Name = "SelectAllButton";
            SelectAllButton.Size = new Size(82, 35);
            SelectAllButton.TabIndex = 1;
            SelectAllButton.Text = "Select all";
            SelectAllButton.UseVisualStyleBackColor = true;
            SelectAllButton.Click += SelectAllButton_Click;
            // 
            // SelectNoneButton
            // 
            SelectNoneButton.AutoSize = true;
            SelectNoneButton.Location = new Point(248, 0);
            SelectNoneButton.Margin = new Padding(0);
            SelectNoneButton.Name = "SelectNoneButton";
            SelectNoneButton.Size = new Size(96, 35);
            SelectNoneButton.TabIndex = 2;
            SelectNoneButton.Text = "Select none";
            SelectNoneButton.UseVisualStyleBackColor = true;
            SelectNoneButton.Click += SelectNoneButton_Click;
            // 
            // LayoutOptionsGroup
            // 
            LayoutOptionsGroup.Controls.Add(LayoutOptionsFlow);
            LayoutOptionsGroup.Dock = DockStyle.Fill;
            LayoutOptionsGroup.Location = new Point(3, 402);
            LayoutOptionsGroup.Margin = new Padding(3, 0, 3, 8);
            LayoutOptionsGroup.Name = "LayoutOptionsGroup";
            LayoutOptionsGroup.Padding = new Padding(8, 4, 8, 8);
            LayoutOptionsGroup.Size = new Size(554, 142);
            LayoutOptionsGroup.TabIndex = 6;
            LayoutOptionsGroup.TabStop = false;
            LayoutOptionsGroup.Text = "Diagram structure";
            // 
            // LayoutOptionsFlow
            // 
            LayoutOptionsFlow.Controls.Add(ShowInfrastructureDetailCheckBox);
            LayoutOptionsFlow.Controls.Add(GroupBySubscriptionCheckBox);
            LayoutOptionsFlow.Controls.Add(LinkNestedResourcesCheckBox);
            LayoutOptionsFlow.Controls.Add(LinkHiddenTagsCheckBox);
            LayoutOptionsFlow.Dock = DockStyle.Fill;
            LayoutOptionsFlow.FlowDirection = FlowDirection.TopDown;
            LayoutOptionsFlow.Location = new Point(8, 24);
            LayoutOptionsFlow.Name = "LayoutOptionsFlow";
            LayoutOptionsFlow.Size = new Size(538, 110);
            LayoutOptionsFlow.TabIndex = 0;
            LayoutOptionsFlow.WrapContents = false;
            // 
            // ShowInfrastructureDetailCheckBox
            // 
            ShowInfrastructureDetailCheckBox.AutoSize = true;
            ShowInfrastructureDetailCheckBox.Location = new Point(0, 0);
            ShowInfrastructureDetailCheckBox.Margin = new Padding(0, 0, 0, 6);
            ShowInfrastructureDetailCheckBox.Name = "ShowInfrastructureDetailCheckBox";
            ShowInfrastructureDetailCheckBox.Size = new Size(420, 24);
            ShowInfrastructureDetailCheckBox.TabIndex = 0;
            ShowInfrastructureDetailCheckBox.Text = "Show infrastructure detail (disks, NICs, alerts, backup, VNets, NSGs, …)";
            ShowInfrastructureDetailCheckBox.UseVisualStyleBackColor = true;
            ShowInfrastructureDetailCheckBox.CheckedChanged += ShowInfrastructureDetailCheckBox_CheckedChanged;
            // 
            // GroupBySubscriptionCheckBox
            // 
            GroupBySubscriptionCheckBox.AutoSize = true;
            GroupBySubscriptionCheckBox.Checked = true;
            GroupBySubscriptionCheckBox.CheckState = CheckState.Checked;
            GroupBySubscriptionCheckBox.Location = new Point(0, 0);
            GroupBySubscriptionCheckBox.Margin = new Padding(0, 0, 16, 0);
            GroupBySubscriptionCheckBox.Name = "GroupBySubscriptionCheckBox";
            GroupBySubscriptionCheckBox.Size = new Size(158, 24);
            GroupBySubscriptionCheckBox.TabIndex = 0;
            GroupBySubscriptionCheckBox.Text = "Group by subscription";
            GroupBySubscriptionCheckBox.UseVisualStyleBackColor = true;
            // 
            // LinkNestedResourcesCheckBox
            // 
            LinkNestedResourcesCheckBox.AutoSize = true;
            LinkNestedResourcesCheckBox.Checked = true;
            LinkNestedResourcesCheckBox.CheckState = CheckState.Checked;
            LinkNestedResourcesCheckBox.Location = new Point(174, 0);
            LinkNestedResourcesCheckBox.Margin = new Padding(0, 0, 16, 0);
            LinkNestedResourcesCheckBox.Name = "LinkNestedResourcesCheckBox";
            LinkNestedResourcesCheckBox.Size = new Size(158, 24);
            LinkNestedResourcesCheckBox.TabIndex = 1;
            LinkNestedResourcesCheckBox.Text = "Link nested resources";
            LinkNestedResourcesCheckBox.UseVisualStyleBackColor = true;
            // 
            // LinkHiddenTagsCheckBox
            // 
            LinkHiddenTagsCheckBox.AutoSize = true;
            LinkHiddenTagsCheckBox.Checked = true;
            LinkHiddenTagsCheckBox.CheckState = CheckState.Checked;
            LinkHiddenTagsCheckBox.Location = new Point(348, 0);
            LinkHiddenTagsCheckBox.Margin = new Padding(0);
            LinkHiddenTagsCheckBox.Name = "LinkHiddenTagsCheckBox";
            LinkHiddenTagsCheckBox.Size = new Size(168, 24);
            LinkHiddenTagsCheckBox.TabIndex = 2;
            LinkHiddenTagsCheckBox.Text = "Link hidden-link tags";
            LinkHiddenTagsCheckBox.UseVisualStyleBackColor = true;
            // 
            // DialogButtonRow
            // 
            DialogButtonRow.Anchor = AnchorStyles.Right;
            DialogButtonRow.AutoSize = true;
            DialogButtonRow.Controls.Add(OkButton);
            DialogButtonRow.Controls.Add(CancelImportButton);
            DialogButtonRow.Dock = DockStyle.Fill;
            DialogButtonRow.FlowDirection = FlowDirection.RightToLeft;
            DialogButtonRow.Location = new Point(3, 488);
            DialogButtonRow.Margin = new Padding(3, 0, 3, 0);
            DialogButtonRow.Name = "DialogButtonRow";
            DialogButtonRow.Size = new Size(554, 35);
            DialogButtonRow.TabIndex = 7;
            // 
            // OkButton
            // 
            OkButton.AutoSize = true;
            OkButton.DialogResult = DialogResult.None;
            OkButton.Location = new Point(451, 0);
            OkButton.Margin = new Padding(8, 0, 0, 0);
            OkButton.Name = "OkButton";
            OkButton.Size = new Size(103, 35);
            OkButton.TabIndex = 0;
            OkButton.Text = "Import";
            OkButton.UseVisualStyleBackColor = true;
            OkButton.Click += OkButton_Click;
            // 
            // CancelImportButton
            // 
            CancelImportButton.AutoSize = true;
            CancelImportButton.DialogResult = DialogResult.Cancel;
            CancelImportButton.Location = new Point(360, 0);
            CancelImportButton.Margin = new Padding(0);
            CancelImportButton.Name = "CancelImportButton";
            CancelImportButton.Size = new Size(83, 35);
            CancelImportButton.TabIndex = 1;
            CancelImportButton.Text = "Cancel";
            CancelImportButton.UseVisualStyleBackColor = true;
            // 
            // AzureResourceGraphImportForm
            // 
            AcceptButton = OkButton;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = CancelImportButton;
            ClientSize = new Size(584, 544);
            Controls.Add(MainLayout);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(3, 4, 3, 4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AzureResourceGraphImportForm";
            Padding = new Padding(14, 16, 14, 16);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Import Azure Resource Graph";
            MainLayout.ResumeLayout(false);
            MainLayout.PerformLayout();
            PresetButtonRow.ResumeLayout(false);
            PresetButtonRow.PerformLayout();
            LayoutOptionsGroup.ResumeLayout(false);
            LayoutOptionsFlow.ResumeLayout(false);
            LayoutOptionsFlow.PerformLayout();
            DialogButtonRow.ResumeLayout(false);
            DialogButtonRow.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel MainLayout;
        private Label SummaryLabel;
        private Label TitleLabel;
        private TextBox TitleTextBox;
        private CheckedListBox CategoryList;
        private Label SelectedCountLabel;
        private FlowLayoutPanel PresetButtonRow;
        private Button EnterpriseDefaultsButton;
        private Button SelectAllButton;
        private Button SelectNoneButton;
        private GroupBox LayoutOptionsGroup;
        private FlowLayoutPanel LayoutOptionsFlow;
        private CheckBox ShowInfrastructureDetailCheckBox;
        private CheckBox GroupBySubscriptionCheckBox;
        private CheckBox LinkNestedResourcesCheckBox;
        private CheckBox LinkHiddenTagsCheckBox;
        private FlowLayoutPanel DialogButtonRow;
        private Button OkButton;
        private Button CancelImportButton;
    }
}
