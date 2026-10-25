using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class SelectionPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                if (_OwnedPicture != null)
                {
                    _OwnedPicture.Dispose();
                    _OwnedPicture = null;
                }
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            DetailTable = new TableLayoutPanel();
            TitleLabel = new Label();
            ObjectPicture = new PictureBox();
            DescriptionBox = new TextBox();
            EditorLineRow = new TableLayoutPanel();
            EditorLineCaption = new Label();
            EditorLineValue = new Label();
            EditorLineSpacer = new Label();
            FactsBox = new TextBox();
            TypeRow = new TableLayoutPanel();
            TypeLabel = new Label();
            TypeCombo = new ComboBox();
            ChangeTypeButton = new Button();
            RadiosRow = new FlowLayoutPanel();
            KeepRelationshipsRadio = new RadioButton();
            UpdateRelationshipsRadio = new RadioButton();
            DetailTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)ObjectPicture).BeginInit();
            EditorLineRow.SuspendLayout();
            TypeRow.SuspendLayout();
            RadiosRow.SuspendLayout();
            SuspendLayout();
            // 
            // DetailTable
            // 
            DetailTable.BackColor = SystemColors.Control;
            DetailTable.ColumnCount = 2;
            DetailTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            DetailTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            DetailTable.Controls.Add(TitleLabel, 0, 0);
            DetailTable.Controls.Add(ObjectPicture, 0, 1);
            DetailTable.Controls.Add(DescriptionBox, 1, 1);
            DetailTable.Controls.Add(EditorLineRow, 1, 2);
            DetailTable.Controls.Add(FactsBox, 1, 3);
            DetailTable.Controls.Add(TypeRow, 0, 4);
            DetailTable.Controls.Add(RadiosRow, 0, 5);
            DetailTable.Dock = DockStyle.Fill;
            DetailTable.Location = new Point(1, 1);
            DetailTable.Name = "DetailTable";
            DetailTable.RowCount = 6;
            DetailTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            DetailTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            DetailTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            DetailTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            DetailTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            DetailTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            DetailTable.Size = new Size(438, 278);
            DetailTable.TabIndex = 0;
            // 
            // TitleLabel
            // 
            DetailTable.SetColumnSpan(TitleLabel, 2);
            TitleLabel.Dock = DockStyle.Fill;
            TitleLabel.Font = new Font("Segoe UI", 7.20000029F, FontStyle.Bold, GraphicsUnit.Point, 0);
            TitleLabel.Location = new Point(0, 0);
            TitleLabel.Margin = new Padding(0);
            TitleLabel.Name = "TitleLabel";
            TitleLabel.Padding = new Padding(8, 4, 4, 0);
            TitleLabel.Size = new Size(438, 25);
            TitleLabel.TabIndex = 3;
            TitleLabel.Text = "Selection";
            // 
            // ObjectPicture
            // 
            ObjectPicture.AccessibleName = "Selected object";
            ObjectPicture.BackColor = Color.White;
            ObjectPicture.BorderStyle = BorderStyle.FixedSingle;
            ObjectPicture.Dock = DockStyle.Fill;
            ObjectPicture.Location = new Point(4, 29);
            ObjectPicture.Margin = new Padding(4);
            ObjectPicture.Name = "ObjectPicture";
            ObjectPicture.Size = new Size(104, 72);
            ObjectPicture.SizeMode = PictureBoxSizeMode.Zoom;
            ObjectPicture.TabIndex = 0;
            ObjectPicture.TabStop = false;
            // 
            // DescriptionBox
            // 
            DescriptionBox.BorderStyle = BorderStyle.None;
            DescriptionBox.Dock = DockStyle.Fill;
            DescriptionBox.Location = new Point(115, 28);
            DescriptionBox.Multiline = true;
            DescriptionBox.Name = "DescriptionBox";
            DescriptionBox.ReadOnly = true;
            DescriptionBox.ScrollBars = ScrollBars.Vertical;
            DescriptionBox.Size = new Size(320, 74);
            DescriptionBox.TabIndex = 4;
            // 
            // EditorLineRow
            // 
            EditorLineRow.ColumnCount = 3;
            EditorLineRow.ColumnStyles.Add(new ColumnStyle());
            EditorLineRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
            EditorLineRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            EditorLineRow.Controls.Add(EditorLineCaption, 0, 0);
            EditorLineRow.Controls.Add(EditorLineValue, 1, 0);
            EditorLineRow.Controls.Add(EditorLineSpacer, 2, 0);
            EditorLineRow.Dock = DockStyle.Fill;
            EditorLineRow.Location = new Point(112, 105);
            EditorLineRow.Margin = new Padding(0);
            EditorLineRow.Name = "EditorLineRow";
            EditorLineRow.RowCount = 1;
            EditorLineRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            EditorLineRow.Size = new Size(326, 32);
            EditorLineRow.TabIndex = 2;
            // 
            // EditorLineCaption
            // 
            EditorLineCaption.Anchor = AnchorStyles.None;
            EditorLineCaption.AutoSize = true;
            EditorLineCaption.Font = new Font("Segoe UI", 5.76F, FontStyle.Regular, GraphicsUnit.Point, 0);
            EditorLineCaption.Location = new Point(4, 10);
            EditorLineCaption.Margin = new Padding(4, 0, 8, 0);
            EditorLineCaption.Name = "EditorLineCaption";
            EditorLineCaption.Size = new Size(48, 12);
            EditorLineCaption.TabIndex = 0;
            EditorLineCaption.Text = "Editor line";
            // 
            // EditorLineValue
            // 
            EditorLineValue.AccessibleName = "Editor line number";
            EditorLineValue.Anchor = AnchorStyles.None;
            EditorLineValue.BorderStyle = BorderStyle.FixedSingle;
            EditorLineValue.Location = new Point(68, 4);
            EditorLineValue.Margin = new Padding(4, 2, 0, 2);
            EditorLineValue.Name = "EditorLineValue";
            EditorLineValue.Size = new Size(40, 23);
            EditorLineValue.TabIndex = 1;
            EditorLineValue.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // EditorLineSpacer
            // 
            EditorLineSpacer.Dock = DockStyle.Fill;
            EditorLineSpacer.Location = new Point(112, 0);
            EditorLineSpacer.Margin = new Padding(0);
            EditorLineSpacer.Name = "EditorLineSpacer";
            EditorLineSpacer.Size = new Size(214, 32);
            EditorLineSpacer.TabIndex = 2;
            // 
            // FactsBox
            // 
            FactsBox.BackColor = SystemColors.Control;
            FactsBox.BorderStyle = BorderStyle.None;
            FactsBox.Dock = DockStyle.Fill;
            FactsBox.Location = new Point(116, 141);
            FactsBox.Margin = new Padding(4);
            FactsBox.Multiline = true;
            FactsBox.Name = "FactsBox";
            FactsBox.ReadOnly = true;
            FactsBox.Size = new Size(318, 44);
            FactsBox.TabIndex = 2;
            FactsBox.TabStop = false;
            FactsBox.Text = "Connection points";
            // 
            // TypeRow
            // 
            TypeRow.ColumnCount = 3;
            DetailTable.SetColumnSpan(TypeRow, 2);
            TypeRow.ColumnStyles.Add(new ColumnStyle());
            TypeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            TypeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            TypeRow.Controls.Add(TypeLabel, 0, 0);
            TypeRow.Controls.Add(TypeCombo, 1, 0);
            TypeRow.Controls.Add(ChangeTypeButton, 2, 0);
            TypeRow.Dock = DockStyle.Fill;
            TypeRow.Location = new Point(0, 189);
            TypeRow.Margin = new Padding(0);
            TypeRow.Name = "TypeRow";
            TypeRow.RowCount = 1;
            TypeRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            TypeRow.Size = new Size(438, 52);
            TypeRow.TabIndex = 1;
            // 
            // TypeLabel
            // 
            TypeLabel.Anchor = AnchorStyles.None;
            TypeLabel.AutoSize = true;
            TypeLabel.Location = new Point(4, 16);
            TypeLabel.Margin = new Padding(4, 0, 8, 0);
            TypeLabel.Name = "TypeLabel";
            TypeLabel.Size = new Size(40, 20);
            TypeLabel.TabIndex = 0;
            TypeLabel.Text = "Type";
            // 
            // TypeCombo
            // 
            TypeCombo.Dock = DockStyle.Fill;
            TypeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            TypeCombo.Location = new Point(56, 12);
            TypeCombo.Margin = new Padding(4, 12, 8, 6);
            TypeCombo.Name = "TypeCombo";
            TypeCombo.Size = new Size(264, 28);
            TypeCombo.TabIndex = 1;
            // 
            // ChangeTypeButton
            // 
            ChangeTypeButton.Anchor = AnchorStyles.None;
            ChangeTypeButton.Location = new Point(332, 12);
            ChangeTypeButton.Margin = new Padding(0, 0, 4, 0);
            ChangeTypeButton.Name = "ChangeTypeButton";
            ChangeTypeButton.Size = new Size(98, 27);
            ChangeTypeButton.TabIndex = 2;
            ChangeTypeButton.Text = "Change type";
            ChangeTypeButton.UseCompatibleTextRendering = true;
            ChangeTypeButton.UseVisualStyleBackColor = true;
            ChangeTypeButton.Click += ChangeTypeButton_Click;
            // 
            // RadiosRow
            // 
            DetailTable.SetColumnSpan(RadiosRow, 2);
            RadiosRow.Controls.Add(KeepRelationshipsRadio);
            RadiosRow.Controls.Add(UpdateRelationshipsRadio);
            RadiosRow.Dock = DockStyle.Fill;
            RadiosRow.Location = new Point(0, 241);
            RadiosRow.Margin = new Padding(0);
            RadiosRow.Name = "RadiosRow";
            RadiosRow.Padding = new Padding(4, 4, 0, 4);
            RadiosRow.Size = new Size(438, 37);
            RadiosRow.TabIndex = 5;
            RadiosRow.WrapContents = false;
            // 
            // KeepRelationshipsRadio
            // 
            KeepRelationshipsRadio.AutoSize = true;
            KeepRelationshipsRadio.Checked = true;
            KeepRelationshipsRadio.Location = new Point(7, 7);
            KeepRelationshipsRadio.Margin = new Padding(3, 3, 12, 3);
            KeepRelationshipsRadio.Name = "KeepRelationshipsRadio";
            KeepRelationshipsRadio.Size = new Size(134, 21);
            KeepRelationshipsRadio.TabIndex = 3;
            KeepRelationshipsRadio.TabStop = true;
            KeepRelationshipsRadio.Text = "Keep relationships";
            // 
            // UpdateRelationshipsRadio
            // 
            UpdateRelationshipsRadio.AutoSize = true;
            UpdateRelationshipsRadio.Location = new Point(156, 7);
            UpdateRelationshipsRadio.Margin = new Padding(3, 3, 12, 3);
            UpdateRelationshipsRadio.Name = "UpdateRelationshipsRadio";
            UpdateRelationshipsRadio.Size = new Size(172, 21);
            UpdateRelationshipsRadio.TabIndex = 4;
            UpdateRelationshipsRadio.Text = "Drop illegal relationships";
            // 
            // SelectionPanel
            // 
            AutoScaleMode = AutoScaleMode.Inherit;
            BackColor = SystemColors.ControlDark;
            Controls.Add(DetailTable);
            MinimumSize = new Size(440, 280);
            Name = "SelectionPanel";
            Padding = new Padding(1);
            Size = new Size(440, 280);
            DetailTable.ResumeLayout(false);
            DetailTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)ObjectPicture).EndInit();
            EditorLineRow.ResumeLayout(false);
            EditorLineRow.PerformLayout();
            TypeRow.ResumeLayout(false);
            TypeRow.PerformLayout();
            RadiosRow.ResumeLayout(false);
            RadiosRow.PerformLayout();
            ResumeLayout(false);
        }

        private TableLayoutPanel DetailTable;
        private PictureBox ObjectPicture;
        private TableLayoutPanel TypeRow;
        private Label TypeLabel;
        private ComboBox TypeCombo;
        private Button ChangeTypeButton;
        private RadioButton KeepRelationshipsRadio;
        private RadioButton UpdateRelationshipsRadio;
        private FlowLayoutPanel RadiosRow;
        private TableLayoutPanel EditorLineRow;
        private Label EditorLineCaption;
        private Label EditorLineValue;
        private Label EditorLineSpacer;
        private TextBox FactsBox;
        private Label TitleLabel;
        private TextBox DescriptionBox;
    }
}
