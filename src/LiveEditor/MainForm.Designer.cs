using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer _Components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (_Components != null))
            {
                _Components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            _Components = new System.ComponentModel.Container();
            MainMenu = new MenuStrip();
            FileMenu = new ToolStripMenuItem();
            OpenMenuItem = new ToolStripMenuItem();
            SaveMenuItem = new ToolStripMenuItem();
            SaveAsMenuItem = new ToolStripMenuItem();
            FileSeparator = new ToolStripSeparator();
            ExitMenuItem = new ToolStripMenuItem();
            ArchiMenu = new ToolStripMenuItem();
            ImportExchangeMenuItem = new ToolStripMenuItem();
            ImportArchimateMenuItem = new ToolStripMenuItem();
            ArchiSeparator = new ToolStripSeparator();
            ExportExchangeMenuItem = new ToolStripMenuItem();
            ExportArchimateMenuItem = new ToolStripMenuItem();
            CoArchiMenu = new ToolStripMenuItem();
            OpenCoArchiFolderMenuItem = new ToolStripMenuItem();
            SaveCoArchiFolderMenuItem = new ToolStripMenuItem();
            MainSplit = new SplitContainer();
            SourceEditor = new TextBox();
            PreviewImage = new PictureBox();
            StatusBar = new StatusStrip();
            StatusLabel = new ToolStripStatusLabel();
            RenderTimer = new System.Windows.Forms.Timer(_Components);
            MainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)MainSplit).BeginInit();
            MainSplit.Panel1.SuspendLayout();
            MainSplit.Panel2.SuspendLayout();
            MainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PreviewImage).BeginInit();
            StatusBar.SuspendLayout();
            SuspendLayout();
            //
            // MainMenu
            //
            MainMenu.ImageScalingSize = new Size(24, 24);
            MainMenu.Items.AddRange(new ToolStripItem[] { FileMenu, ArchiMenu, CoArchiMenu });
            MainMenu.Location = new Point(0, 0);
            MainMenu.Name = "MainMenu";
            MainMenu.Size = new Size(1200, 33);
            MainMenu.TabIndex = 0;
            MainMenu.Text = "MainMenu";
            //
            // FileMenu
            //
            FileMenu.DropDownItems.AddRange(new ToolStripItem[] { OpenMenuItem, SaveMenuItem, SaveAsMenuItem, FileSeparator, ExitMenuItem });
            FileMenu.Name = "FileMenu";
            FileMenu.Size = new Size(50, 29);
            FileMenu.Text = "&File";
            //
            // OpenMenuItem
            //
            OpenMenuItem.Name = "OpenMenuItem";
            OpenMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            OpenMenuItem.Size = new Size(270, 34);
            OpenMenuItem.Text = "&Open...";
            //
            // SaveMenuItem
            //
            SaveMenuItem.Name = "SaveMenuItem";
            SaveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            SaveMenuItem.Size = new Size(270, 34);
            SaveMenuItem.Text = "&Save";
            //
            // SaveAsMenuItem
            //
            SaveAsMenuItem.Name = "SaveAsMenuItem";
            SaveAsMenuItem.Size = new Size(270, 34);
            SaveAsMenuItem.Text = "Save &As...";
            //
            // FileSeparator
            //
            FileSeparator.Name = "FileSeparator";
            FileSeparator.Size = new Size(267, 6);
            //
            // ExitMenuItem
            //
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Size = new Size(270, 34);
            ExitMenuItem.Text = "E&xit";
            //
            // ArchiMenu
            //
            ArchiMenu.DropDownItems.AddRange(new ToolStripItem[] { ImportExchangeMenuItem, ImportArchimateMenuItem, ArchiSeparator, ExportExchangeMenuItem, ExportArchimateMenuItem });
            ArchiMenu.Name = "ArchiMenu";
            ArchiMenu.Size = new Size(67, 29);
            ArchiMenu.Text = "&Archi";
            //
            // ImportExchangeMenuItem
            //
            ImportExchangeMenuItem.Name = "ImportExchangeMenuItem";
            ImportExchangeMenuItem.Size = new Size(340, 34);
            ImportExchangeMenuItem.Text = "Import Model Exchange...";
            //
            // ImportArchimateMenuItem
            //
            ImportArchimateMenuItem.Name = "ImportArchimateMenuItem";
            ImportArchimateMenuItem.Size = new Size(340, 34);
            ImportArchimateMenuItem.Text = "Import ArchiMate file...";
            //
            // ArchiSeparator
            //
            ArchiSeparator.Name = "ArchiSeparator";
            ArchiSeparator.Size = new Size(337, 6);
            //
            // ExportExchangeMenuItem
            //
            ExportExchangeMenuItem.Name = "ExportExchangeMenuItem";
            ExportExchangeMenuItem.Size = new Size(340, 34);
            ExportExchangeMenuItem.Text = "Export Model Exchange...";
            //
            // ExportArchimateMenuItem
            //
            ExportArchimateMenuItem.Name = "ExportArchimateMenuItem";
            ExportArchimateMenuItem.Size = new Size(340, 34);
            ExportArchimateMenuItem.Text = "Export ArchiMate file...";
            //
            // CoArchiMenu
            //
            CoArchiMenu.DropDownItems.AddRange(new ToolStripItem[] { OpenCoArchiFolderMenuItem, SaveCoArchiFolderMenuItem });
            CoArchiMenu.Name = "CoArchiMenu";
            CoArchiMenu.Size = new Size(91, 29);
            CoArchiMenu.Text = "&coArchi";
            //
            // OpenCoArchiFolderMenuItem
            //
            OpenCoArchiFolderMenuItem.Name = "OpenCoArchiFolderMenuItem";
            OpenCoArchiFolderMenuItem.Size = new Size(270, 34);
            OpenCoArchiFolderMenuItem.Text = "Open folder...";
            //
            // SaveCoArchiFolderMenuItem
            //
            SaveCoArchiFolderMenuItem.Name = "SaveCoArchiFolderMenuItem";
            SaveCoArchiFolderMenuItem.Size = new Size(270, 34);
            SaveCoArchiFolderMenuItem.Text = "Save to folder...";
            //
            // MainSplit
            //
            MainSplit.Dock = DockStyle.Fill;
            MainSplit.Location = new Point(0, 33);
            MainSplit.Name = "MainSplit";
            //
            // MainSplit.Panel1
            //
            MainSplit.Panel1.Controls.Add(SourceEditor);
            //
            // MainSplit.Panel2
            //
            MainSplit.Panel2.Controls.Add(PreviewImage);
            MainSplit.Size = new Size(1200, 687);
            MainSplit.SplitterDistance = 560;
            MainSplit.TabIndex = 1;
            //
            // SourceEditor
            //
            SourceEditor.AcceptsReturn = true;
            SourceEditor.AcceptsTab = true;
            SourceEditor.Dock = DockStyle.Fill;
            SourceEditor.Font = new Font("Consolas", 11F, FontStyle.Regular, GraphicsUnit.Point);
            SourceEditor.HideSelection = false;
            SourceEditor.Location = new Point(0, 0);
            SourceEditor.MaxLength = 0;
            SourceEditor.Multiline = true;
            SourceEditor.Name = "SourceEditor";
            SourceEditor.ScrollBars = ScrollBars.Both;
            SourceEditor.Size = new Size(560, 687);
            SourceEditor.TabIndex = 0;
            SourceEditor.WordWrap = false;
            //
            // PreviewImage
            //
            PreviewImage.BackColor = Color.White;
            PreviewImage.Dock = DockStyle.Fill;
            PreviewImage.Location = new Point(0, 0);
            PreviewImage.Name = "PreviewImage";
            PreviewImage.Size = new Size(636, 687);
            PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewImage.TabIndex = 0;
            PreviewImage.TabStop = false;
            //
            // StatusBar
            //
            StatusBar.ImageScalingSize = new Size(24, 24);
            StatusBar.Items.AddRange(new ToolStripItem[] { StatusLabel });
            StatusBar.Location = new Point(0, 720);
            StatusBar.Name = "StatusBar";
            StatusBar.Size = new Size(1200, 32);
            StatusBar.TabIndex = 2;
            StatusBar.Text = "StatusBar";
            //
            // StatusLabel
            //
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(1185, 25);
            StatusLabel.Spring = true;
            StatusLabel.Text = "Ready";
            StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // RenderTimer
            //
            RenderTimer.Interval = 350;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 752);
            Controls.Add(MainSplit);
            Controls.Add(StatusBar);
            Controls.Add(MainMenu);
            MainMenuStrip = MainMenu;
            MinimumSize = new Size(800, 500);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LiveEditor";
            MainMenu.ResumeLayout(false);
            MainMenu.PerformLayout();
            MainSplit.Panel1.ResumeLayout(false);
            MainSplit.Panel1.PerformLayout();
            MainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)MainSplit).EndInit();
            MainSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)PreviewImage).EndInit();
            StatusBar.ResumeLayout(false);
            StatusBar.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip MainMenu;
        private ToolStripMenuItem FileMenu;
        private ToolStripMenuItem OpenMenuItem;
        private ToolStripMenuItem SaveMenuItem;
        private ToolStripMenuItem SaveAsMenuItem;
        private ToolStripSeparator FileSeparator;
        private ToolStripMenuItem ExitMenuItem;
        private ToolStripMenuItem ArchiMenu;
        private ToolStripMenuItem ImportExchangeMenuItem;
        private ToolStripMenuItem ImportArchimateMenuItem;
        private ToolStripSeparator ArchiSeparator;
        private ToolStripMenuItem ExportExchangeMenuItem;
        private ToolStripMenuItem ExportArchimateMenuItem;
        private ToolStripMenuItem CoArchiMenu;
        private ToolStripMenuItem OpenCoArchiFolderMenuItem;
        private ToolStripMenuItem SaveCoArchiFolderMenuItem;
        private SplitContainer MainSplit;
        private TextBox SourceEditor;
        private PictureBox PreviewImage;
        private StatusStrip StatusBar;
        private ToolStripStatusLabel StatusLabel;
        private System.Windows.Forms.Timer RenderTimer;
    }
}
