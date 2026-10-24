using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class PreviewHoverWindow
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
            HoverText = new Label();
            SuspendLayout();
            //
            // HoverText
            //
            HoverText.BackColor = SystemColors.Info;
            HoverText.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            HoverText.ForeColor = SystemColors.InfoText;
            HoverText.Location = new Point(8, 6);
            HoverText.Name = "HoverText";
            HoverText.Size = new Size(64, 15);
            HoverText.TabIndex = 0;
            //
            // PreviewHoverWindow
            //
            AutoScaleMode = AutoScaleMode.None;
            BackColor = SystemColors.Info;
            ClientSize = new Size(80, 28);
            ControlBox = false;
            Controls.Add(HoverText);
            FormBorderStyle = FormBorderStyle.None;
            Name = "PreviewHoverWindow";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ResumeLayout(false);
        }

        #endregion

        private Label HoverText;
    }
}
