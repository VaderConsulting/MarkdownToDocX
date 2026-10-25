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
            ObjectPicture = new PictureBox();
            TypeRow = new Panel();
            TypeLabel = new Label();
            TypeCombo = new ComboBox();
            ChangeTypeButton = new Button();
            KeepRelationshipsRadio = new RadioButton();
            UpdateRelationshipsRadio = new RadioButton();
            FactsRow = new Panel();
            EditorLineCaption = new Label();
            EditorLineValue = new Label();
            FactsLabel = new Label();
            TitleLabel = new Label();
            DescriptionBox = new TextBox();
            ((System.ComponentModel.ISupportInitialize)ObjectPicture).BeginInit();
            TypeRow.SuspendLayout();
            FactsRow.SuspendLayout();
            SuspendLayout();
            // 
            // ObjectPicture
            // 
            ObjectPicture.AccessibleName = "Selected object";
            ObjectPicture.BackColor = Color.White;
            ObjectPicture.BorderStyle = BorderStyle.FixedSingle;
            ObjectPicture.Dock = DockStyle.Left;
            ObjectPicture.Location = new Point(0, 0);
            ObjectPicture.Margin = new Padding(0);
            ObjectPicture.Name = "ObjectPicture";
            ObjectPicture.Size = new Size(128, 176);
            ObjectPicture.SizeMode = PictureBoxSizeMode.Zoom;
            ObjectPicture.TabIndex = 0;
            ObjectPicture.TabStop = false;
            // 
            // TypeRow
            // 
            TypeRow.Controls.Add(UpdateRelationshipsRadio);
            TypeRow.Controls.Add(KeepRelationshipsRadio);
            TypeRow.Controls.Add(ChangeTypeButton);
            TypeRow.Controls.Add(TypeCombo);
            TypeRow.Controls.Add(TypeLabel);
            TypeRow.Dock = DockStyle.Bottom;
            TypeRow.Location = new Point(128, 124);
            TypeRow.Margin = new Padding(0);
            TypeRow.Name = "TypeRow";
            TypeRow.Size = new Size(472, 52);
            TypeRow.TabIndex = 1;
            // 
            // TypeLabel
            // 
            TypeLabel.AutoSize = true;
            TypeLabel.Location = new Point(8, 6);
            TypeLabel.Name = "TypeLabel";
            TypeLabel.Size = new Size(34, 15);
            TypeLabel.TabIndex = 0;
            TypeLabel.Text = "Type";
            // 
            // TypeCombo
            // 
            TypeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            TypeCombo.Location = new Point(48, 2);
            TypeCombo.Name = "TypeCombo";
            TypeCombo.Size = new Size(220, 23);
            TypeCombo.TabIndex = 1;
            // 
            // ChangeTypeButton
            // 
            ChangeTypeButton.Location = new Point(274, 1);
            ChangeTypeButton.Name = "ChangeTypeButton";
            ChangeTypeButton.Size = new Size(96, 25);
            ChangeTypeButton.TabIndex = 2;
            ChangeTypeButton.Text = "Change type";
            ChangeTypeButton.UseVisualStyleBackColor = true;
            // 
            // KeepRelationshipsRadio
            // 
            KeepRelationshipsRadio.AutoSize = true;
            KeepRelationshipsRadio.Checked = true;
            KeepRelationshipsRadio.Location = new Point(8, 30);
            KeepRelationshipsRadio.Name = "KeepRelationshipsRadio";
            KeepRelationshipsRadio.Size = new Size(122, 19);
            KeepRelationshipsRadio.TabIndex = 3;
            KeepRelationshipsRadio.TabStop = true;
            KeepRelationshipsRadio.Text = "Keep relationships";
            // 
            // UpdateRelationshipsRadio
            // 
            UpdateRelationshipsRadio.AutoSize = true;
            UpdateRelationshipsRadio.Location = new Point(148, 30);
            UpdateRelationshipsRadio.Name = "UpdateRelationshipsRadio";
            UpdateRelationshipsRadio.Size = new Size(154, 19);
            UpdateRelationshipsRadio.TabIndex = 4;
            UpdateRelationshipsRadio.Text = "Drop illegal relationships";
            // 
            // FactsRow
            // 
            FactsRow.Controls.Add(FactsLabel);
            FactsRow.Controls.Add(EditorLineValue);
            FactsRow.Controls.Add(EditorLineCaption);
            FactsRow.Dock = DockStyle.Bottom;
            FactsRow.Location = new Point(128, 84);
            FactsRow.Margin = new Padding(0);
            FactsRow.Name = "FactsRow";
            FactsRow.Size = new Size(472, 40);
            FactsRow.TabIndex = 2;
            // 
            // EditorLineCaption
            // 
            EditorLineCaption.AutoSize = true;
            EditorLineCaption.Location = new Point(8, 4);
            EditorLineCaption.Name = "EditorLineCaption";
            EditorLineCaption.Size = new Size(68, 15);
            EditorLineCaption.TabIndex = 0;
            EditorLineCaption.Text = "Editor Line:";
            // 
            // EditorLineValue
            // 
            EditorLineValue.AccessibleName = "Editor line number";
            EditorLineValue.BorderStyle = BorderStyle.FixedSingle;
            EditorLineValue.Location = new Point(88, 2);
            EditorLineValue.Name = "EditorLineValue";
            EditorLineValue.Size = new Size(48, 19);
            EditorLineValue.TabIndex = 1;
            EditorLineValue.TextAlign = ContentAlignment.MiddleRight;
            // 
            // FactsLabel
            // 
            FactsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            FactsLabel.Location = new Point(8, 22);
            FactsLabel.Name = "FactsLabel";
            FactsLabel.Size = new Size(460, 16);
            FactsLabel.TabIndex = 2;
            FactsLabel.Text = "Connection points";
            // 
            // TitleLabel
            // 
            TitleLabel.Dock = DockStyle.Top;
            TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            TitleLabel.Location = new Point(128, 0);
            TitleLabel.Name = "TitleLabel";
            TitleLabel.Padding = new Padding(8, 4, 4, 0);
            TitleLabel.Size = new Size(472, 24);
            TitleLabel.TabIndex = 3;
            TitleLabel.Text = "Selection";
            // 
            // DescriptionBox
            // 
            DescriptionBox.BorderStyle = BorderStyle.None;
            DescriptionBox.Dock = DockStyle.Fill;
            DescriptionBox.Location = new Point(128, 24);
            DescriptionBox.Multiline = true;
            DescriptionBox.Name = "DescriptionBox";
            DescriptionBox.ReadOnly = true;
            DescriptionBox.ScrollBars = ScrollBars.Vertical;
            DescriptionBox.Size = new Size(472, 60);
            DescriptionBox.TabIndex = 4;
            DescriptionBox.WordWrap = true;
            // 
            // SelectionPanel
            // 
            Controls.Add(DescriptionBox);
            Controls.Add(TitleLabel);
            Controls.Add(FactsRow);
            Controls.Add(TypeRow);
            Controls.Add(ObjectPicture);
            Name = "SelectionPanel";
            Size = new Size(600, 176);
            ((System.ComponentModel.ISupportInitialize)ObjectPicture).EndInit();
            TypeRow.ResumeLayout(false);
            TypeRow.PerformLayout();
            FactsRow.ResumeLayout(false);
            FactsRow.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private PictureBox ObjectPicture;
        private Panel TypeRow;
        private Label TypeLabel;
        private ComboBox TypeCombo;
        private Button ChangeTypeButton;
        private RadioButton KeepRelationshipsRadio;
        private RadioButton UpdateRelationshipsRadio;
        private Panel FactsRow;
        private Label EditorLineCaption;
        private Label EditorLineValue;
        private Label FactsLabel;
        private Label TitleLabel;
        private TextBox DescriptionBox;
    }
}
