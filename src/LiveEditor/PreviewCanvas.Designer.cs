using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class PreviewCanvas
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            Viewport = new Panel();
            DiagramPicture = new PictureBox();
            Banner = new Label();
            ZoomInButton = new Button();
            ZoomOutButton = new Button();
            FitButton = new Button();
            FocusSink = new PreviewFocusSink();
            DiagramMenu = new ContextMenuStrip(components);
            ClearMenuItem = new ToolStripMenuItem();
            RefreshMenuItem = new ToolStripMenuItem();
            CopyPngMenuItem = new ToolStripMenuItem();
            Viewport.SuspendLayout();
            DiagramMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)DiagramPicture).BeginInit();
            SuspendLayout();
            //
            // Viewport
            //
            Viewport.AccessibleName = "Diagram preview canvas";
            Viewport.AutoScroll = true;
            Viewport.BackColor = Color.White;
            Viewport.Controls.Add(DiagramPicture);
            Viewport.Dock = DockStyle.Fill;
            Viewport.Location = new Point(0, 0);
            Viewport.Margin = new Padding(2, 2, 2, 2);
            Viewport.ContextMenuStrip = DiagramMenu;
            Viewport.Name = "Viewport";
            Viewport.Size = new Size(240, 406);
            Viewport.TabIndex = 0;
            //
            // DiagramPicture
            //
            DiagramPicture.AccessibleName = "Diagram preview image";
            DiagramPicture.BackColor = Color.White;
            DiagramPicture.ContextMenuStrip = DiagramMenu;
            DiagramPicture.Dock = DockStyle.Fill;
            DiagramPicture.Location = new Point(0, 0);
            DiagramPicture.Margin = new Padding(2, 2, 2, 2);
            DiagramPicture.Name = "DiagramPicture";
            DiagramPicture.Size = new Size(240, 406);
            DiagramPicture.SizeMode = PictureBoxSizeMode.Zoom;
            DiagramPicture.TabIndex = 0;
            DiagramPicture.TabStop = false;
            //
            // Banner
            //
            Banner.Dock = DockStyle.Top;
            Banner.Location = new Point(0, 0);
            Banner.Margin = new Padding(2, 0, 2, 0);
            Banner.Name = "Banner";
            Banner.Size = new Size(240, 0);
            Banner.TabIndex = 1;
            Banner.TextAlign = ContentAlignment.MiddleLeft;
            Banner.Visible = false;
            //
            // ZoomInButton
            //
            ZoomInButton.AccessibleName = "Zoom in";
            ZoomInButton.FlatStyle = FlatStyle.Flat;
            ZoomInButton.Location = new Point(0, 0);
            ZoomInButton.Margin = new Padding(2, 2, 2, 2);
            ZoomInButton.Name = "ZoomInButton";
            ZoomInButton.Size = new Size(32, 32);
            ZoomInButton.TabIndex = 3;
            ZoomInButton.Text = "+";
            ZoomInButton.UseVisualStyleBackColor = true;
            //
            // ZoomOutButton
            //
            ZoomOutButton.AccessibleName = "Zoom out";
            ZoomOutButton.FlatStyle = FlatStyle.Flat;
            ZoomOutButton.Location = new Point(0, 0);
            ZoomOutButton.Margin = new Padding(2, 2, 2, 2);
            ZoomOutButton.Name = "ZoomOutButton";
            ZoomOutButton.Size = new Size(32, 32);
            ZoomOutButton.TabIndex = 4;
            ZoomOutButton.Text = "-";
            ZoomOutButton.UseVisualStyleBackColor = true;
            //
            // FitButton
            //
            FitButton.AccessibleName = "Fit preview";
            FitButton.FlatStyle = FlatStyle.Flat;
            FitButton.Location = new Point(0, 0);
            FitButton.Margin = new Padding(2, 2, 2, 2);
            FitButton.Name = "FitButton";
            FitButton.Size = new Size(32, 32);
            FitButton.TabIndex = 5;
            FitButton.UseVisualStyleBackColor = true;
            //
            // FocusSink
            //
            FocusSink.Name = "FocusSink";
            FocusSink.TabIndex = 6;
            //
            // DiagramMenu
            //
            DiagramMenu.Items.AddRange(new ToolStripItem[] { ClearMenuItem, RefreshMenuItem, CopyPngMenuItem });
            DiagramMenu.Name = "DiagramMenu";
            DiagramMenu.Size = new Size(240, 82);
            //
            // ClearMenuItem
            //
            ClearMenuItem.Name = "ClearMenuItem";
            ClearMenuItem.Size = new Size(239, 22);
            ClearMenuItem.Text = "Clear";
            //
            // RefreshMenuItem
            //
            RefreshMenuItem.Name = "RefreshMenuItem";
            RefreshMenuItem.Size = new Size(239, 22);
            RefreshMenuItem.Text = "Refresh";
            //
            // CopyPngMenuItem
            //
            CopyPngMenuItem.Name = "CopyPngMenuItem";
            CopyPngMenuItem.Size = new Size(239, 22);
            CopyPngMenuItem.Text = "Save to Clipboard (.png)";
            //
            // PreviewCanvas
            //
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(Viewport);
            Controls.Add(Banner);
            Controls.Add(FitButton);
            Controls.Add(ZoomOutButton);
            Controls.Add(ZoomInButton);
            Controls.Add(FocusSink);
            Name = "PreviewCanvas";
            Size = new Size(240, 406);
            DiagramMenu.ResumeLayout(false);
            Viewport.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)DiagramPicture).EndInit();
            ResumeLayout(false);
        }

        private Panel Viewport;
        private PictureBox DiagramPicture;
        private Label Banner;
        private Button ZoomInButton;
        private Button ZoomOutButton;
        private Button FitButton;
        private PreviewFocusSink FocusSink;
        private ContextMenuStrip DiagramMenu;
        private ToolStripMenuItem ClearMenuItem;
        private ToolStripMenuItem RefreshMenuItem;
        private ToolStripMenuItem CopyPngMenuItem;
    }
}
