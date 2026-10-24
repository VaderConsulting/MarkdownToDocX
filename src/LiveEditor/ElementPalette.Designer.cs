using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class ElementPalette
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
            LayerTabs = new TabControl();
            PromptPanel = new FlowLayoutPanel();
            PromptGlyph = new Panel();
            PromptTypeLabel = new Label();
            PromptLabelCaption = new Label();
            PromptLabelBox = new TextBox();
            PromptIdCaption = new Label();
            PromptIdBox = new TextBox();
            InsertLineButton = new Button();
            CancelPromptButton = new Button();
            PromptErrorLabel = new Label();
            PromptPanel.SuspendLayout();
            SuspendLayout();
            //
            // LayerTabs
            //
            LayerTabs.Dock = DockStyle.Fill;
            LayerTabs.Name = "LayerTabs";
            LayerTabs.Padding = new Point(6, 4);
            LayerTabs.Size = new Size(800, 148);
            LayerTabs.TabIndex = 0;
            //
            // PromptPanel
            //
            PromptPanel.AutoSize = true;
            PromptPanel.Controls.Add(PromptGlyph);
            PromptPanel.Controls.Add(PromptTypeLabel);
            PromptPanel.Controls.Add(PromptLabelCaption);
            PromptPanel.Controls.Add(PromptLabelBox);
            PromptPanel.Controls.Add(PromptIdCaption);
            PromptPanel.Controls.Add(PromptIdBox);
            PromptPanel.Controls.Add(InsertLineButton);
            PromptPanel.Controls.Add(CancelPromptButton);
            PromptPanel.Controls.Add(PromptErrorLabel);
            PromptPanel.Dock = DockStyle.Bottom;
            PromptPanel.Name = "PromptPanel";
            PromptPanel.Padding = new Padding(4, 4, 4, 2);
            PromptPanel.Visible = false;
            PromptPanel.WrapContents = true;
            //
            // PromptGlyph
            //
            PromptGlyph.Margin = new Padding(0, 4, 6, 0);
            PromptGlyph.Name = "PromptGlyph";
            PromptGlyph.Size = new Size(34, 22);
            //
            // PromptTypeLabel
            //
            PromptTypeLabel.AutoSize = true;
            PromptTypeLabel.Margin = new Padding(0, 8, 8, 0);
            PromptTypeLabel.Name = "PromptTypeLabel";
            PromptTypeLabel.Text = "Element";
            //
            // PromptLabelCaption
            //
            PromptLabelCaption.AutoSize = true;
            PromptLabelCaption.Margin = new Padding(0, 8, 4, 0);
            PromptLabelCaption.Name = "PromptLabelCaption";
            PromptLabelCaption.Text = "Label";
            //
            // PromptLabelBox
            //
            PromptLabelBox.Margin = new Padding(0, 4, 8, 0);
            PromptLabelBox.Name = "PromptLabelBox";
            PromptLabelBox.Size = new Size(140, 23);
            //
            // PromptIdCaption
            //
            PromptIdCaption.AutoSize = true;
            PromptIdCaption.Margin = new Padding(0, 8, 4, 0);
            PromptIdCaption.Name = "PromptIdCaption";
            PromptIdCaption.Text = "Identifier";
            //
            // PromptIdBox
            //
            PromptIdBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            PromptIdBox.Margin = new Padding(0, 4, 8, 0);
            PromptIdBox.MaxLength = 8;
            PromptIdBox.Name = "PromptIdBox";
            PromptIdBox.Size = new Size(64, 23);
            //
            // InsertLineButton
            //
            InsertLineButton.AutoSize = true;
            InsertLineButton.Margin = new Padding(0, 3, 4, 0);
            InsertLineButton.Name = "InsertLineButton";
            InsertLineButton.Text = "Insert line";
            //
            // CancelPromptButton
            //
            CancelPromptButton.AutoSize = true;
            CancelPromptButton.Margin = new Padding(0, 3, 4, 0);
            CancelPromptButton.Name = "CancelPromptButton";
            CancelPromptButton.Text = "Cancel";
            //
            // PromptErrorLabel
            //
            PromptErrorLabel.AutoSize = true;
            PromptErrorLabel.ForeColor = Color.DarkRed;
            PromptErrorLabel.Margin = new Padding(0, 6, 0, 0);
            PromptErrorLabel.Name = "PromptErrorLabel";
            PromptErrorLabel.Visible = false;
            //
            // ElementPalette
            //
            AutoScaleMode = AutoScaleMode.Inherit;
            Controls.Add(LayerTabs);
            Controls.Add(PromptPanel);
            Name = "ElementPalette";
            Size = new Size(800, 148);
            PromptPanel.ResumeLayout(false);
            PromptPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private TabControl LayerTabs;

        private FlowLayoutPanel PromptPanel;

        private Panel PromptGlyph;

        private Label PromptTypeLabel;

        private Label PromptLabelCaption;

        private TextBox PromptLabelBox;

        private Label PromptIdCaption;

        private TextBox PromptIdBox;

        private Button InsertLineButton;

        private Button CancelPromptButton;

        private Label PromptErrorLabel;
    }
}
