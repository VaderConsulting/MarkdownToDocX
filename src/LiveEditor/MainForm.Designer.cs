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
            RecentFilesMenuItem = new ToolStripMenuItem();
            FileSeparator = new ToolStripSeparator();
            ExitMenuItem = new ToolStripMenuItem();
            EditMenu = new ToolStripMenuItem();
            CopySourceMenuItem = new ToolStripMenuItem();
            RenameSymbolMenuItem = new ToolStripMenuItem();
            GoToDefinitionMenuItem = new ToolStripMenuItem();
            FindReferencesMenuItem = new ToolStripMenuItem();
            CommandPaletteMenuItem = new ToolStripMenuItem();
            SelectNextOccurrenceMenuItem = new ToolStripMenuItem();
            InsertArchiSnippetMenuItem = new ToolStripMenuItem();
            ViewMenu = new ToolStripMenuItem();
            ToggleThemeMenuItem = new ToolStripMenuItem();
            ToggleSplitOrientationMenuItem = new ToolStripMenuItem();
            ZoomInMenuItem = new ToolStripMenuItem();
            ZoomOutMenuItem = new ToolStripMenuItem();
            ZoomResetMenuItem = new ToolStripMenuItem();
            FitPreviewMenuItem = new ToolStripMenuItem();
            ToggleDiagnosticsMenuItem = new ToolStripMenuItem();
            ToggleMinimapMenuItem = new ToolStripMenuItem();
            ToggleLegacyPreviewMenuItem = new ToolStripMenuItem();
            ExportMenu = new ToolStripMenuItem();
            ExportPngMenuItem = new ToolStripMenuItem();
            ExportSvgMenuItem = new ToolStripMenuItem();
            SamplesMenu = new ToolStripMenuItem();
            ArchiMenu = new ToolStripMenuItem();
            ImportExchangeMenuItem = new ToolStripMenuItem();
            ImportArchimateMenuItem = new ToolStripMenuItem();
            ArchiSeparator = new ToolStripSeparator();
            ExportExchangeMenuItem = new ToolStripMenuItem();
            ExportArchimateMenuItem = new ToolStripMenuItem();
            CoArchiMenu = new ToolStripMenuItem();
            OpenCoArchiFolderMenuItem = new ToolStripMenuItem();
            SaveCoArchiFolderMenuItem = new ToolStripMenuItem();
            BodySplit = new SplitContainer();
            EditorSplit = new SplitContainer();
            SourceEditor = new RichTextBox();
            MinimapBox = new RichTextBox();
            PreviewHost = new Panel();
            PreviewCanvas = new Panel();
            PreviewImage = new PictureBox();
            PreviewBanner = new Label();
            DiagnosticsList = new ListBox();
            StatusBar = new StatusStrip();
            StatusLabel = new ToolStripStatusLabel();
            TimingLabel = new ToolStripStatusLabel();
            CoArchiDirtyLabel = new ToolStripStatusLabel();
            SignatureLabel = new ToolStripStatusLabel();
            RenderTimer = new System.Windows.Forms.Timer(_Components);
            HighlightTimer = new System.Windows.Forms.Timer(_Components);
            MainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)BodySplit).BeginInit();
            BodySplit.Panel1.SuspendLayout();
            BodySplit.Panel2.SuspendLayout();
            BodySplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)EditorSplit).BeginInit();
            EditorSplit.Panel1.SuspendLayout();
            EditorSplit.Panel2.SuspendLayout();
            EditorSplit.SuspendLayout();
            PreviewHost.SuspendLayout();
            PreviewCanvas.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PreviewImage).BeginInit();
            StatusBar.SuspendLayout();
            SuspendLayout();
            //
            // MainMenu
            //
            MainMenu.ImageScalingSize = new Size(24, 24);
            MainMenu.Items.AddRange(new ToolStripItem[] { FileMenu, EditMenu, ViewMenu, ExportMenu, SamplesMenu, ArchiMenu, CoArchiMenu });
            MainMenu.Location = new Point(0, 0);
            MainMenu.Name = "MainMenu";
            MainMenu.Size = new Size(1280, 33);
            MainMenu.TabIndex = 0;
            //
            // FileMenu
            //
            FileMenu.DropDownItems.AddRange(new ToolStripItem[] { OpenMenuItem, SaveMenuItem, SaveAsMenuItem, RecentFilesMenuItem, FileSeparator, ExitMenuItem });
            FileMenu.Name = "FileMenu";
            FileMenu.Text = "&File";
            OpenMenuItem.Name = "OpenMenuItem";
            OpenMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            OpenMenuItem.Text = "&Open...";
            SaveMenuItem.Name = "SaveMenuItem";
            SaveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            SaveMenuItem.Text = "&Save";
            SaveAsMenuItem.Name = "SaveAsMenuItem";
            SaveAsMenuItem.Text = "Save &As...";
            RecentFilesMenuItem.Name = "RecentFilesMenuItem";
            RecentFilesMenuItem.Text = "&Recent files";
            FileSeparator.Name = "FileSeparator";
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Text = "E&xit";
            //
            // EditMenu
            //
            EditMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                CopySourceMenuItem,
                RenameSymbolMenuItem,
                GoToDefinitionMenuItem,
                FindReferencesMenuItem,
                SelectNextOccurrenceMenuItem,
                InsertArchiSnippetMenuItem,
                CommandPaletteMenuItem
            });
            EditMenu.Name = "EditMenu";
            EditMenu.Text = "&Edit";
            CopySourceMenuItem.Name = "CopySourceMenuItem";
            CopySourceMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;
            CopySourceMenuItem.Text = "&Copy Mermaid source";
            RenameSymbolMenuItem.Name = "RenameSymbolMenuItem";
            RenameSymbolMenuItem.ShortcutKeys = Keys.F2;
            RenameSymbolMenuItem.Text = "&Rename symbol...";
            GoToDefinitionMenuItem.Name = "GoToDefinitionMenuItem";
            GoToDefinitionMenuItem.ShortcutKeys = Keys.F12;
            GoToDefinitionMenuItem.Text = "&Go to definition";
            FindReferencesMenuItem.Name = "FindReferencesMenuItem";
            FindReferencesMenuItem.ShortcutKeys = Keys.Shift | Keys.F12;
            FindReferencesMenuItem.Text = "Find &references";
            SelectNextOccurrenceMenuItem.Name = "SelectNextOccurrenceMenuItem";
            SelectNextOccurrenceMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            SelectNextOccurrenceMenuItem.Text = "Select &next occurrence";
            InsertArchiSnippetMenuItem.Name = "InsertArchiSnippetMenuItem";
            InsertArchiSnippetMenuItem.Text = "Insert from &Archi exchange XML...";
            CommandPaletteMenuItem.Name = "CommandPaletteMenuItem";
            CommandPaletteMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.P;
            CommandPaletteMenuItem.Text = "Command &palette...";
            //
            // ViewMenu
            //
            ViewMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                ToggleThemeMenuItem,
                ToggleSplitOrientationMenuItem,
                ZoomInMenuItem,
                ZoomOutMenuItem,
                ZoomResetMenuItem,
                FitPreviewMenuItem,
                ToggleDiagnosticsMenuItem,
                ToggleMinimapMenuItem,
                ToggleLegacyPreviewMenuItem
            });
            ViewMenu.Name = "ViewMenu";
            ViewMenu.Text = "&View";
            ToggleThemeMenuItem.Name = "ToggleThemeMenuItem";
            ToggleThemeMenuItem.Text = "Toggle &theme";
            ToggleSplitOrientationMenuItem.Name = "ToggleSplitOrientationMenuItem";
            ToggleSplitOrientationMenuItem.Text = "Toggle split &orientation";
            ZoomInMenuItem.Name = "ZoomInMenuItem";
            ZoomInMenuItem.ShortcutKeys = Keys.Control | Keys.Oemplus;
            ZoomInMenuItem.Text = "Zoom &in";
            ZoomOutMenuItem.Name = "ZoomOutMenuItem";
            ZoomOutMenuItem.ShortcutKeys = Keys.Control | Keys.OemMinus;
            ZoomOutMenuItem.Text = "Zoom &out";
            ZoomResetMenuItem.Name = "ZoomResetMenuItem";
            ZoomResetMenuItem.ShortcutKeys = Keys.Control | Keys.D0;
            ZoomResetMenuItem.Text = "&Reset zoom";
            FitPreviewMenuItem.Name = "FitPreviewMenuItem";
            FitPreviewMenuItem.Text = "&Fit preview";
            ToggleDiagnosticsMenuItem.Name = "ToggleDiagnosticsMenuItem";
            ToggleDiagnosticsMenuItem.Text = "Toggle &diagnostics panel";
            ToggleMinimapMenuItem.Name = "ToggleMinimapMenuItem";
            ToggleMinimapMenuItem.Text = "Toggle &minimap";
            ToggleLegacyPreviewMenuItem.Name = "ToggleLegacyPreviewMenuItem";
            ToggleLegacyPreviewMenuItem.Text = "Toggle &legacy Naiad preview";
            //
            // ExportMenu
            //
            ExportMenu.DropDownItems.AddRange(new ToolStripItem[] { ExportPngMenuItem, ExportSvgMenuItem });
            ExportMenu.Name = "ExportMenu";
            ExportMenu.Text = "E&xport";
            ExportPngMenuItem.Name = "ExportPngMenuItem";
            ExportPngMenuItem.Text = "Export &PNG...";
            ExportSvgMenuItem.Name = "ExportSvgMenuItem";
            ExportSvgMenuItem.Text = "Export &SVG...";
            //
            // SamplesMenu
            //
            SamplesMenu.Name = "SamplesMenu";
            SamplesMenu.Text = "&Samples";
            //
            // ArchiMenu
            //
            ArchiMenu.DropDownItems.AddRange(new ToolStripItem[] { ImportExchangeMenuItem, ImportArchimateMenuItem, ArchiSeparator, ExportExchangeMenuItem, ExportArchimateMenuItem });
            ArchiMenu.Name = "ArchiMenu";
            ArchiMenu.Text = "&Archi";
            ImportExchangeMenuItem.Name = "ImportExchangeMenuItem";
            ImportExchangeMenuItem.Text = "Import Model Exchange...";
            ImportArchimateMenuItem.Name = "ImportArchimateMenuItem";
            ImportArchimateMenuItem.Text = "Import ArchiMate file...";
            ArchiSeparator.Name = "ArchiSeparator";
            ExportExchangeMenuItem.Name = "ExportExchangeMenuItem";
            ExportExchangeMenuItem.Text = "Export Model Exchange...";
            ExportArchimateMenuItem.Name = "ExportArchimateMenuItem";
            ExportArchimateMenuItem.Text = "Export ArchiMate file...";
            //
            // CoArchiMenu
            //
            CoArchiMenu.DropDownItems.AddRange(new ToolStripItem[] { OpenCoArchiFolderMenuItem, SaveCoArchiFolderMenuItem });
            CoArchiMenu.Name = "CoArchiMenu";
            CoArchiMenu.Text = "&coArchi";
            OpenCoArchiFolderMenuItem.Name = "OpenCoArchiFolderMenuItem";
            OpenCoArchiFolderMenuItem.Text = "Open folder...";
            SaveCoArchiFolderMenuItem.Name = "SaveCoArchiFolderMenuItem";
            SaveCoArchiFolderMenuItem.Text = "Save to folder...";
            //
            // BodySplit
            //
            BodySplit.Dock = DockStyle.Fill;
            BodySplit.Location = new Point(0, 33);
            BodySplit.Name = "BodySplit";
            BodySplit.Orientation = Orientation.Horizontal;
            BodySplit.Panel1.Controls.Add(EditorSplit);
            BodySplit.Panel2.Controls.Add(DiagnosticsList);
            BodySplit.Size = new Size(1280, 687);
            BodySplit.SplitterDistance = 560;
            BodySplit.Panel2Collapsed = true;
            BodySplit.TabIndex = 1;
            //
            // EditorSplit
            //
            EditorSplit.Dock = DockStyle.Fill;
            EditorSplit.Name = "EditorSplit";
            EditorSplit.Panel1.Controls.Add(SourceEditor);
            EditorSplit.Panel1.Controls.Add(MinimapBox);
            EditorSplit.Panel2.Controls.Add(PreviewHost);
            EditorSplit.SplitterDistance = 600;
            //
            // SourceEditor
            //
            SourceEditor.AcceptsTab = true;
            SourceEditor.DetectUrls = false;
            SourceEditor.Dock = DockStyle.Fill;
            SourceEditor.Font = new Font("Consolas", 11F, FontStyle.Regular, GraphicsUnit.Point);
            SourceEditor.HideSelection = false;
            SourceEditor.Name = "SourceEditor";
            SourceEditor.ScrollBars = RichTextBoxScrollBars.Both;
            SourceEditor.WordWrap = false;
            SourceEditor.AccessibleName = "Mermaid source editor";
            //
            // MinimapBox
            //
            MinimapBox.Dock = DockStyle.Right;
            MinimapBox.Font = new Font("Consolas", 4F, FontStyle.Regular, GraphicsUnit.Point);
            MinimapBox.Name = "MinimapBox";
            MinimapBox.ReadOnly = true;
            MinimapBox.ScrollBars = RichTextBoxScrollBars.None;
            MinimapBox.Width = 90;
            MinimapBox.WordWrap = false;
            MinimapBox.Visible = false;
            MinimapBox.AccessibleName = "Source minimap";
            //
            // PreviewHost
            //
            PreviewHost.Controls.Add(PreviewCanvas);
            PreviewHost.Controls.Add(PreviewBanner);
            PreviewHost.Dock = DockStyle.Fill;
            PreviewHost.Name = "PreviewHost";
            //
            // PreviewBanner
            //
            PreviewBanner.Dock = DockStyle.Top;
            PreviewBanner.Height = 0;
            PreviewBanner.Name = "PreviewBanner";
            PreviewBanner.TextAlign = ContentAlignment.MiddleLeft;
            PreviewBanner.Visible = false;
            //
            // PreviewCanvas
            //
            PreviewCanvas.AutoScroll = true;
            PreviewCanvas.BackColor = Color.White;
            PreviewCanvas.Controls.Add(PreviewImage);
            PreviewCanvas.Dock = DockStyle.Fill;
            PreviewCanvas.Name = "PreviewCanvas";
            PreviewCanvas.AccessibleName = "Diagram preview canvas";
            //
            // PreviewImage
            //
            PreviewImage.BackColor = Color.White;
            PreviewImage.Location = new Point(0, 0);
            PreviewImage.Name = "PreviewImage";
            PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewImage.Size = new Size(640, 480);
            PreviewImage.TabStop = false;
            PreviewImage.AccessibleName = "Diagram preview image";
            //
            // DiagnosticsList
            //
            DiagnosticsList.Dock = DockStyle.Fill;
            DiagnosticsList.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            DiagnosticsList.HorizontalScrollbar = true;
            DiagnosticsList.Name = "DiagnosticsList";
            DiagnosticsList.AccessibleName = "Validation diagnostics";
            //
            // StatusBar
            //
            StatusBar.Items.AddRange(new ToolStripItem[] { StatusLabel, SignatureLabel, TimingLabel, CoArchiDirtyLabel });
            StatusBar.Location = new Point(0, 720);
            StatusBar.Name = "StatusBar";
            StatusBar.Size = new Size(1280, 32);
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Spring = true;
            StatusLabel.Text = "Ready";
            StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            SignatureLabel.Name = "SignatureLabel";
            SignatureLabel.Text = string.Empty;
            TimingLabel.Name = "TimingLabel";
            TimingLabel.Text = string.Empty;
            CoArchiDirtyLabel.Name = "CoArchiDirtyLabel";
            CoArchiDirtyLabel.Text = string.Empty;
            //
            // Timers
            //
            RenderTimer.Interval = 350;
            HighlightTimer.Interval = 250;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1280, 752);
            Controls.Add(BodySplit);
            Controls.Add(StatusBar);
            Controls.Add(MainMenu);
            MainMenuStrip = MainMenu;
            MinimumSize = new Size(900, 560);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LiveEditor";
            MainMenu.ResumeLayout(false);
            MainMenu.PerformLayout();
            BodySplit.Panel1.ResumeLayout(false);
            BodySplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)BodySplit).EndInit();
            BodySplit.ResumeLayout(false);
            EditorSplit.Panel1.ResumeLayout(false);
            EditorSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)EditorSplit).EndInit();
            EditorSplit.ResumeLayout(false);
            PreviewHost.ResumeLayout(false);
            PreviewCanvas.ResumeLayout(false);
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
        private ToolStripMenuItem RecentFilesMenuItem;
        private ToolStripSeparator FileSeparator;
        private ToolStripMenuItem ExitMenuItem;
        private ToolStripMenuItem EditMenu;
        private ToolStripMenuItem CopySourceMenuItem;
        private ToolStripMenuItem RenameSymbolMenuItem;
        private ToolStripMenuItem GoToDefinitionMenuItem;
        private ToolStripMenuItem FindReferencesMenuItem;
        private ToolStripMenuItem CommandPaletteMenuItem;
        private ToolStripMenuItem SelectNextOccurrenceMenuItem;
        private ToolStripMenuItem InsertArchiSnippetMenuItem;
        private ToolStripMenuItem ViewMenu;
        private ToolStripMenuItem ToggleThemeMenuItem;
        private ToolStripMenuItem ToggleSplitOrientationMenuItem;
        private ToolStripMenuItem ZoomInMenuItem;
        private ToolStripMenuItem ZoomOutMenuItem;
        private ToolStripMenuItem ZoomResetMenuItem;
        private ToolStripMenuItem FitPreviewMenuItem;
        private ToolStripMenuItem ToggleDiagnosticsMenuItem;
        private ToolStripMenuItem ToggleMinimapMenuItem;
        private ToolStripMenuItem ToggleLegacyPreviewMenuItem;
        private ToolStripMenuItem ExportMenu;
        private ToolStripMenuItem ExportPngMenuItem;
        private ToolStripMenuItem ExportSvgMenuItem;
        private ToolStripMenuItem SamplesMenu;
        private ToolStripMenuItem ArchiMenu;
        private ToolStripMenuItem ImportExchangeMenuItem;
        private ToolStripMenuItem ImportArchimateMenuItem;
        private ToolStripSeparator ArchiSeparator;
        private ToolStripMenuItem ExportExchangeMenuItem;
        private ToolStripMenuItem ExportArchimateMenuItem;
        private ToolStripMenuItem CoArchiMenu;
        private ToolStripMenuItem OpenCoArchiFolderMenuItem;
        private ToolStripMenuItem SaveCoArchiFolderMenuItem;
        private SplitContainer BodySplit;
        private SplitContainer EditorSplit;
        private RichTextBox SourceEditor;
        private RichTextBox MinimapBox;
        private Panel PreviewHost;
        private Panel PreviewCanvas;
        private PictureBox PreviewImage;
        private Label PreviewBanner;
        private ListBox DiagnosticsList;
        private StatusStrip StatusBar;
        private ToolStripStatusLabel StatusLabel;
        private ToolStripStatusLabel TimingLabel;
        private ToolStripStatusLabel CoArchiDirtyLabel;
        private ToolStripStatusLabel SignatureLabel;
        private System.Windows.Forms.Timer RenderTimer;
        private System.Windows.Forms.Timer HighlightTimer;
    }
}
