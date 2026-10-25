using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor.Completion
{
    partial class CompletionAssistWindow
    {
        private System.ComponentModel.IContainer _Components = null;

        /// <summary>
        /// Disposes the window.
        /// </summary>
        /// <param name="Disposing">True when managed resources should be released.</param>
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
            SignatureBox = new RichTextBox();
            MemberList = new ListBox();
            DetailBox = new TextBox();
            SuspendLayout();
            //
            // SignatureBox
            //
            SignatureBox.BorderStyle = BorderStyle.None;
            SignatureBox.DetectUrls = false;
            SignatureBox.Dock = DockStyle.None;
            SignatureBox.Name = "SignatureBox";
            SignatureBox.ReadOnly = true;
            SignatureBox.ScrollBars = RichTextBoxScrollBars.None;
            SignatureBox.TabStop = false;
            SignatureBox.WordWrap = true;
            SignatureBox.MouseDown += Surface_MouseDown;
            //
            // MemberList
            //
            MemberList.BorderStyle = BorderStyle.None;
            MemberList.DrawMode = DrawMode.Normal;
            MemberList.HorizontalScrollbar = false;
            MemberList.IntegralHeight = false;
            MemberList.ItemHeight = 22;
            MemberList.Name = "MemberList";
            MemberList.TabStop = false;
            MemberList.Click += MemberList_Click;
            MemberList.SelectedIndexChanged += MemberList_SelectedIndexChanged;
            //
            // DetailBox
            //
            DetailBox.BorderStyle = BorderStyle.None;
            DetailBox.Multiline = true;
            DetailBox.Name = "DetailBox";
            DetailBox.ReadOnly = true;
            DetailBox.ScrollBars = ScrollBars.None;
            DetailBox.TabStop = false;
            DetailBox.WordWrap = false;
            DetailBox.MouseDown += Surface_MouseDown;
            //
            // CompletionAssistWindow
            //
            AutoScaleMode = AutoScaleMode.None;
            BackColor = SystemColors.ControlDark;
            ClientSize = new Size(320, 120);
            ControlBox = false;
            Controls.Add(SignatureBox);
            Controls.Add(MemberList);
            Controls.Add(DetailBox);
            FormBorderStyle = FormBorderStyle.None;
            Name = "CompletionAssistWindow";
            Padding = new Padding(1);
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ResumeLayout(false);
            PerformLayout();
        }

        private RichTextBox SignatureBox;

        private ListBox MemberList;

        private TextBox DetailBox;
    }
}
