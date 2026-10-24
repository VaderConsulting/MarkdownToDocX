using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class NewDiagramForm
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

        private void InitializeComponent()
        {
            _Components = new System.ComponentModel.Container();
            KindTabs = new TabControl();
            ButtonRow = new TableLayoutPanel();
            DialogButtons = new FlowLayoutPanel();
            KindDescriptionLabel = new Label();
            CreateDiagramButton = new Button();
            CancelDiagramButton = new Button();
            ButtonRow.SuspendLayout();
            DialogButtons.SuspendLayout();
            SuspendLayout();
            //
            // KindTabs
            //
            KindTabs.Dock = DockStyle.Fill;
            KindTabs.Name = "KindTabs";
            KindTabs.Padding = new Point(8, 6);
            KindTabs.TabIndex = 0;
            //
            // ButtonRow
            //
            ButtonRow.AutoSize = true;
            ButtonRow.ColumnCount = 2;
            ButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            ButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ButtonRow.Controls.Add(KindDescriptionLabel, 0, 0);
            ButtonRow.Controls.Add(DialogButtons, 1, 0);
            ButtonRow.Dock = DockStyle.Bottom;
            ButtonRow.Name = "ButtonRow";
            ButtonRow.Padding = new Padding(8);
            ButtonRow.RowCount = 1;
            ButtonRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            //
            // DialogButtons
            //
            DialogButtons.Anchor = AnchorStyles.Right;
            DialogButtons.AutoSize = true;
            DialogButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            DialogButtons.Controls.Add(CreateDiagramButton);
            DialogButtons.Controls.Add(CancelDiagramButton);
            DialogButtons.Name = "DialogButtons";
            DialogButtons.WrapContents = false;
            //
            // KindDescriptionLabel
            //
            KindDescriptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            KindDescriptionLabel.AutoSize = true;
            KindDescriptionLabel.Name = "KindDescriptionLabel";
            KindDescriptionLabel.Text = "Choose a diagram type.";
            KindDescriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // CreateDiagramButton
            //
            CreateDiagramButton.AutoSize = true;
            CreateDiagramButton.Margin = new Padding(0, 0, 8, 0);
            CreateDiagramButton.Name = "CreateDiagramButton";
            CreateDiagramButton.Text = "Create";
            CreateDiagramButton.UseVisualStyleBackColor = true;
            //
            // CancelDiagramButton
            //
            CancelDiagramButton.AutoSize = true;
            CancelDiagramButton.Margin = new Padding(0);
            CancelDiagramButton.Name = "CancelDiagramButton";
            CancelDiagramButton.Text = "Cancel";
            CancelDiagramButton.UseVisualStyleBackColor = true;
            //
            // NewDiagramForm
            //
            AcceptButton = CreateDiagramButton;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = CancelDiagramButton;
            ClientSize = new Size(720, 420);
            Controls.Add(KindTabs);
            Controls.Add(ButtonRow);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "NewDiagramForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "New diagram";
            DialogButtons.ResumeLayout(false);
            DialogButtons.PerformLayout();
            ButtonRow.ResumeLayout(false);
            ButtonRow.PerformLayout();
            ResumeLayout(false);
        }

        private TabControl KindTabs;

        private TableLayoutPanel ButtonRow;

        private FlowLayoutPanel DialogButtons;

        private Label KindDescriptionLabel;

        private Button CreateDiagramButton;

        private Button CancelDiagramButton;
    }
}
