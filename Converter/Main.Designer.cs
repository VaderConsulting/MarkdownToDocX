namespace Converter
{
    public partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer _Components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            label2 = new Label();
            Input = new TextBox();
            Output = new TextBox();
            Start = new Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 12);
            label1.Name = "label1";
            label1.Size = new Size(126, 25);
            label1.TabIndex = 0;
            label1.Text = "Input filename";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 49);
            label2.Name = "label2";
            label2.Size = new Size(141, 25);
            label2.TabIndex = 1;
            label2.Text = "Output filename";
            // 
            // Input
            // 
            Input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Input.Location = new Point(159, 12);
            Input.Name = "Input";
            Input.Size = new Size(607, 31);
            Input.TabIndex = 2;
            // 
            // Output
            // 
            Output.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Output.Location = new Point(159, 49);
            Output.Name = "Output";
            Output.Size = new Size(607, 31);
            Output.TabIndex = 3;
            // 
            // Start
            // 
            Start.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Start.Location = new Point(654, 86);
            Start.Name = "Start";
            Start.Size = new Size(112, 34);
            Start.TabIndex = 4;
            Start.Text = "Start";
            Start.UseVisualStyleBackColor = true;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(778, 144);
            Controls.Add(Start);
            Controls.Add(Output);
            Controls.Add(Input);
            Controls.Add(label2);
            Controls.Add(label1);
            MaximizeBox = false;
            MaximumSize = new Size(10000, 200);
            MinimumSize = new Size(640, 200);
            Name = "Main";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Markdown to DocX converter";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label label2;
        private TextBox Input;
        private TextBox Output;
        private Button Start;
    }
}
