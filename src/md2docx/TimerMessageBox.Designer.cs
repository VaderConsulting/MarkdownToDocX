using System.Drawing;
using System.Windows.Forms;

namespace md2docx
{
    partial class TimerMessageBox
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer _Components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (_Components != null))
            {
                _Components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            _messageLabel = new Label();
            _countdownLabel = new Label();
            _okButton = new Button();
            _countdownTimer = new Timer(components);
            SuspendLayout();
            // 
            // _messageLabel
            // 
            _messageLabel.Location = new Point(16, 16);
            _messageLabel.Name = "_messageLabel";
            _messageLabel.Size = new Size(488, 80);
            _messageLabel.TabIndex = 0;
            // 
            // _countdownLabel
            // 
            _countdownLabel.Location = new Point(16, 104);
            _countdownLabel.Name = "_countdownLabel";
            _countdownLabel.Size = new Size(370, 32);
            _countdownLabel.TabIndex = 1;
            _countdownLabel.Visible = false;
            // 
            // _okButton
            // 
            _okButton.Location = new Point(392, 104);
            _okButton.Name = "_okButton";
            _okButton.Size = new Size(112, 32);
            _okButton.TabIndex = 2;
            _okButton.Text = "OK";
            _okButton.UseVisualStyleBackColor = true;
            _okButton.Click += OkButton_Click;
            // 
            // _countdownTimer
            // 
            _countdownTimer.Interval = 1000;
            _countdownTimer.Tick += CountdownTimer_Tick;
            // 
            // TimerMessageBox
            // 
            AcceptButton = _okButton;
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 152);
            Controls.Add(_okButton);
            Controls.Add(_countdownLabel);
            Controls.Add(_messageLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "TimerMessageBox";
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            FormClosing += TimerMessageBox_FormClosing;
            Shown += TimerMessageBox_Shown;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label _messageLabel;
        private System.Windows.Forms.Label _countdownLabel;
        private System.Windows.Forms.Button _okButton;
        private System.Windows.Forms.Timer _countdownTimer;
        private System.ComponentModel.IContainer components;
    }
}
