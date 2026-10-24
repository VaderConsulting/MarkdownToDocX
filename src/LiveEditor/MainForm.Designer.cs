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
            components = new System.ComponentModel.Container();
            MainMenu = new MenuStrip();
            FileMenu = new ToolStripMenuItem();
            NewMenuItem = new ToolStripMenuItem();
            OpenMenuItem = new ToolStripMenuItem();
            SaveMenuItem = new ToolStripMenuItem();
            SaveAsMenuItem = new ToolStripMenuItem();
            RecentFilesMenuItem = new ToolStripMenuItem();
            FileSeparator = new ToolStripSeparator();
            ExitMenuItem = new ToolStripMenuItem();
            EditMenu = new ToolStripMenuItem();
            UndoMenuItem = new ToolStripMenuItem();
            RedoMenuItem = new ToolStripMenuItem();
            EditHistorySeparator = new ToolStripSeparator();
            CutMenuItem = new ToolStripMenuItem();
            CopyMenuItem = new ToolStripMenuItem();
            PasteMenuItem = new ToolStripMenuItem();
            ClipboardSeparator = new ToolStripSeparator();
            FindMenuItem = new ToolStripMenuItem();
            ReplaceMenuItem = new ToolStripMenuItem();
            GoToLineMenuItem = new ToolStripMenuItem();
            FindNextMenuItem = new ToolStripMenuItem();
            FindPreviousMenuItem = new ToolStripMenuItem();
            FindSeparator = new ToolStripSeparator();
            CommentMenuItem = new ToolStripMenuItem();
            UncommentMenuItem = new ToolStripMenuItem();
            DuplicateLineMenuItem = new ToolStripMenuItem();
            DeleteLineMenuItem = new ToolStripMenuItem();
            IndentMenuItem = new ToolStripMenuItem();
            OutdentMenuItem = new ToolStripMenuItem();
            LineEditSeparator = new ToolStripSeparator();
            NextErrorMenuItem = new ToolStripMenuItem();
            PreviousErrorMenuItem = new ToolStripMenuItem();
            CopySourceMenuItem = new ToolStripMenuItem();
            ListMembersMenuItem = new ToolStripMenuItem();
            ParameterInfoMenuItem = new ToolStripMenuItem();
            QuickInfoMenuItem = new ToolStripMenuItem();
            RenameSymbolMenuItem = new ToolStripMenuItem();
            GoToDefinitionMenuItem = new ToolStripMenuItem();
            FindReferencesMenuItem = new ToolStripMenuItem();
            SelectNextOccurrenceMenuItem = new ToolStripMenuItem();
            InsertArchiSnippetMenuItem = new ToolStripMenuItem();
            CommandPaletteMenuItem = new ToolStripMenuItem();
            SettingsSeparator = new ToolStripSeparator();
            SettingsMenuItem = new ToolStripMenuItem();
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
            CodeView = new CodeEditor();
            ElementToolbar = new ElementPalette();
            PreviewHost = new Panel();
            PreviewCanvas = new Panel();
            PreviewImage = new PictureBox();
            PreviewBanner = new Label();
            PreviewZoomInButton = new Button();
            PreviewZoomOutButton = new Button();
            PreviewFitButton = new Button();
            DiagnosticsList = new ListBox();
            StatusBar = new StatusStrip();
            StatusLabel = new ToolStripStatusLabel();
            SignatureLabel = new ToolStripStatusLabel();
            TimingLabel = new ToolStripStatusLabel();
            CoArchiDirtyLabel = new ToolStripStatusLabel();
            RenderTimer = new Timer(components);
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
            MainMenu.Padding = new Padding(5, 2, 0, 2);
            MainMenu.Size = new Size(898, 28);
            MainMenu.TabIndex = 0;
            // 
            // FileMenu
            // 
            FileMenu.DropDownItems.AddRange(new ToolStripItem[] { NewMenuItem, OpenMenuItem, SaveMenuItem, SaveAsMenuItem, RecentFilesMenuItem, FileSeparator, ExitMenuItem });
            FileMenu.Name = "FileMenu";
            FileMenu.Size = new Size(46, 24);
            FileMenu.Text = "&File";
            // 
            // NewMenuItem
            // 
            NewMenuItem.Name = "NewMenuItem";
            NewMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            NewMenuItem.Size = new Size(190, 26);
            NewMenuItem.Text = "&New...";
            // 
            // OpenMenuItem
            // 
            OpenMenuItem.Name = "OpenMenuItem";
            OpenMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            OpenMenuItem.Size = new Size(190, 26);
            OpenMenuItem.Text = "&Open...";
            // 
            // SaveMenuItem
            // 
            SaveMenuItem.Name = "SaveMenuItem";
            SaveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            SaveMenuItem.Size = new Size(190, 26);
            SaveMenuItem.Text = "&Save";
            // 
            // SaveAsMenuItem
            // 
            SaveAsMenuItem.Name = "SaveAsMenuItem";
            SaveAsMenuItem.Size = new Size(190, 26);
            SaveAsMenuItem.Text = "Save &As...";
            // 
            // RecentFilesMenuItem
            // 
            RecentFilesMenuItem.Name = "RecentFilesMenuItem";
            RecentFilesMenuItem.Size = new Size(190, 26);
            RecentFilesMenuItem.Text = "&Recent files";
            // 
            // FileSeparator
            // 
            FileSeparator.Name = "FileSeparator";
            FileSeparator.Size = new Size(187, 6);
            // 
            // ExitMenuItem
            // 
            ExitMenuItem.Name = "ExitMenuItem";
            ExitMenuItem.Size = new Size(190, 26);
            ExitMenuItem.Text = "E&xit";
            // 
            // EditMenu
            // 
            EditMenu.DropDownItems.AddRange(new ToolStripItem[] { UndoMenuItem, RedoMenuItem, EditHistorySeparator, CutMenuItem, CopyMenuItem, PasteMenuItem, ClipboardSeparator, FindMenuItem, ReplaceMenuItem, GoToLineMenuItem, FindNextMenuItem, FindPreviousMenuItem, FindSeparator, CommentMenuItem, UncommentMenuItem, DuplicateLineMenuItem, DeleteLineMenuItem, IndentMenuItem, OutdentMenuItem, LineEditSeparator, NextErrorMenuItem, PreviousErrorMenuItem, CopySourceMenuItem, ListMembersMenuItem, ParameterInfoMenuItem, QuickInfoMenuItem, RenameSymbolMenuItem, GoToDefinitionMenuItem, FindReferencesMenuItem, SelectNextOccurrenceMenuItem, InsertArchiSnippetMenuItem, CommandPaletteMenuItem, SettingsSeparator, SettingsMenuItem });
            EditMenu.Name = "EditMenu";
            EditMenu.Size = new Size(49, 24);
            EditMenu.Text = "&Edit";
            // 
            // UndoMenuItem
            // 
            UndoMenuItem.Name = "UndoMenuItem";
            UndoMenuItem.ShortcutKeyDisplayString = "Ctrl+Z";
            UndoMenuItem.Size = new Size(328, 26);
            UndoMenuItem.Text = "&Undo";
            // 
            // RedoMenuItem
            // 
            RedoMenuItem.Name = "RedoMenuItem";
            RedoMenuItem.ShortcutKeyDisplayString = "Ctrl+Y";
            RedoMenuItem.Size = new Size(328, 26);
            RedoMenuItem.Text = "&Redo";
            // 
            // EditHistorySeparator
            // 
            EditHistorySeparator.Name = "EditHistorySeparator";
            EditHistorySeparator.Size = new Size(325, 6);
            // 
            // CutMenuItem
            // 
            CutMenuItem.Name = "CutMenuItem";
            CutMenuItem.ShortcutKeyDisplayString = "Ctrl+X";
            CutMenuItem.Size = new Size(328, 26);
            CutMenuItem.Text = "Cu&t";
            // 
            // CopyMenuItem
            // 
            CopyMenuItem.Name = "CopyMenuItem";
            CopyMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            CopyMenuItem.Size = new Size(328, 26);
            CopyMenuItem.Text = "&Copy";
            // 
            // PasteMenuItem
            // 
            PasteMenuItem.Name = "PasteMenuItem";
            PasteMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
            PasteMenuItem.Size = new Size(328, 26);
            PasteMenuItem.Text = "&Paste";
            // 
            // ClipboardSeparator
            // 
            ClipboardSeparator.Name = "ClipboardSeparator";
            ClipboardSeparator.Size = new Size(325, 6);
            // 
            // FindMenuItem
            // 
            FindMenuItem.Name = "FindMenuItem";
            FindMenuItem.ShortcutKeys = Keys.Control | Keys.F;
            FindMenuItem.Size = new Size(328, 26);
            FindMenuItem.Text = "&Find...";
            // 
            // ReplaceMenuItem
            // 
            ReplaceMenuItem.Name = "ReplaceMenuItem";
            ReplaceMenuItem.ShortcutKeys = Keys.Control | Keys.H;
            ReplaceMenuItem.Size = new Size(328, 26);
            ReplaceMenuItem.Text = "R&eplace...";
            // 
            // GoToLineMenuItem
            // 
            GoToLineMenuItem.Name = "GoToLineMenuItem";
            GoToLineMenuItem.ShortcutKeys = Keys.Control | Keys.G;
            GoToLineMenuItem.Size = new Size(328, 26);
            GoToLineMenuItem.Text = "&Go to line...";
            // 
            // FindNextMenuItem
            // 
            FindNextMenuItem.Name = "FindNextMenuItem";
            FindNextMenuItem.ShortcutKeys = Keys.F3;
            FindNextMenuItem.Size = new Size(328, 26);
            FindNextMenuItem.Text = "Find &next";
            // 
            // FindPreviousMenuItem
            // 
            FindPreviousMenuItem.Name = "FindPreviousMenuItem";
            FindPreviousMenuItem.ShortcutKeys = Keys.Shift | Keys.F3;
            FindPreviousMenuItem.Size = new Size(328, 26);
            FindPreviousMenuItem.Text = "Find &previous";
            // 
            // FindSeparator
            // 
            FindSeparator.Name = "FindSeparator";
            FindSeparator.Size = new Size(325, 6);
            // 
            // CommentMenuItem
            // 
            CommentMenuItem.Name = "CommentMenuItem";
            CommentMenuItem.ShortcutKeyDisplayString = "Ctrl+K, Ctrl+C";
            CommentMenuItem.Size = new Size(328, 26);
            CommentMenuItem.Text = "Co&mment selection";
            // 
            // UncommentMenuItem
            // 
            UncommentMenuItem.Name = "UncommentMenuItem";
            UncommentMenuItem.ShortcutKeyDisplayString = "Ctrl+K, Ctrl+U";
            UncommentMenuItem.Size = new Size(328, 26);
            UncommentMenuItem.Text = "Uncomment se&lection";
            // 
            // DuplicateLineMenuItem
            // 
            DuplicateLineMenuItem.Name = "DuplicateLineMenuItem";
            DuplicateLineMenuItem.ShortcutKeyDisplayString = "Ctrl+E, V";
            DuplicateLineMenuItem.Size = new Size(328, 26);
            DuplicateLineMenuItem.Text = "D&uplicate line";
            // 
            // DeleteLineMenuItem
            // 
            DeleteLineMenuItem.Name = "DeleteLineMenuItem";
            DeleteLineMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.L;
            DeleteLineMenuItem.Size = new Size(328, 26);
            DeleteLineMenuItem.Text = "&Delete line";
            // 
            // IndentMenuItem
            // 
            IndentMenuItem.Name = "IndentMenuItem";
            IndentMenuItem.ShortcutKeyDisplayString = "Tab";
            IndentMenuItem.Size = new Size(328, 26);
            IndentMenuItem.Text = "&Indent";
            // 
            // OutdentMenuItem
            // 
            OutdentMenuItem.Name = "OutdentMenuItem";
            OutdentMenuItem.ShortcutKeyDisplayString = "Shift+Tab";
            OutdentMenuItem.Size = new Size(328, 26);
            OutdentMenuItem.Text = "O&utdent";
            // 
            // LineEditSeparator
            // 
            LineEditSeparator.Name = "LineEditSeparator";
            LineEditSeparator.Size = new Size(325, 6);
            // 
            // NextErrorMenuItem
            // 
            NextErrorMenuItem.Name = "NextErrorMenuItem";
            NextErrorMenuItem.ShortcutKeys = Keys.F8;
            NextErrorMenuItem.Size = new Size(328, 26);
            NextErrorMenuItem.Text = "Next &error";
            // 
            // PreviousErrorMenuItem
            // 
            PreviousErrorMenuItem.Name = "PreviousErrorMenuItem";
            PreviousErrorMenuItem.ShortcutKeys = Keys.Shift | Keys.F8;
            PreviousErrorMenuItem.Size = new Size(328, 26);
            PreviousErrorMenuItem.Text = "Pre&vious error";
            // 
            // CopySourceMenuItem
            // 
            CopySourceMenuItem.Name = "CopySourceMenuItem";
            CopySourceMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;
            CopySourceMenuItem.Size = new Size(328, 26);
            CopySourceMenuItem.Text = "&Copy Mermaid source";
            // 
            // ListMembersMenuItem
            // 
            ListMembersMenuItem.Name = "ListMembersMenuItem";
            ListMembersMenuItem.ShortcutKeys = Keys.Control | Keys.J;
            ListMembersMenuItem.Size = new Size(328, 26);
            ListMembersMenuItem.Text = "&List Members";
            // 
            // ParameterInfoMenuItem
            // 
            ParameterInfoMenuItem.Name = "ParameterInfoMenuItem";
            ParameterInfoMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.Space;
            ParameterInfoMenuItem.Size = new Size(328, 26);
            ParameterInfoMenuItem.Text = "&Parameter Info";
            // 
            // QuickInfoMenuItem
            // 
            QuickInfoMenuItem.Name = "QuickInfoMenuItem";
            QuickInfoMenuItem.ShortcutKeyDisplayString = "Ctrl+K, Ctrl+I";
            QuickInfoMenuItem.Size = new Size(328, 26);
            QuickInfoMenuItem.Text = "&Quick Info";
            // 
            // RenameSymbolMenuItem
            // 
            RenameSymbolMenuItem.Name = "RenameSymbolMenuItem";
            RenameSymbolMenuItem.ShortcutKeys = Keys.F2;
            RenameSymbolMenuItem.Size = new Size(328, 26);
            RenameSymbolMenuItem.Text = "&Rename symbol...";
            // 
            // GoToDefinitionMenuItem
            // 
            GoToDefinitionMenuItem.Name = "GoToDefinitionMenuItem";
            GoToDefinitionMenuItem.ShortcutKeys = Keys.F12;
            GoToDefinitionMenuItem.Size = new Size(328, 26);
            GoToDefinitionMenuItem.Text = "&Go to definition";
            // 
            // FindReferencesMenuItem
            // 
            FindReferencesMenuItem.Name = "FindReferencesMenuItem";
            FindReferencesMenuItem.ShortcutKeys = Keys.Shift | Keys.F12;
            FindReferencesMenuItem.Size = new Size(328, 26);
            FindReferencesMenuItem.Text = "Find &references";
            // 
            // SelectNextOccurrenceMenuItem
            // 
            SelectNextOccurrenceMenuItem.Name = "SelectNextOccurrenceMenuItem";
            SelectNextOccurrenceMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            SelectNextOccurrenceMenuItem.Size = new Size(328, 26);
            SelectNextOccurrenceMenuItem.Text = "Select &next occurrence";
            // 
            // InsertArchiSnippetMenuItem
            // 
            InsertArchiSnippetMenuItem.Name = "InsertArchiSnippetMenuItem";
            InsertArchiSnippetMenuItem.Size = new Size(328, 26);
            InsertArchiSnippetMenuItem.Text = "Insert from &Archi exchange XML...";
            // 
            // CommandPaletteMenuItem
            // 
            CommandPaletteMenuItem.Name = "CommandPaletteMenuItem";
            CommandPaletteMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.P;
            CommandPaletteMenuItem.Size = new Size(328, 26);
            CommandPaletteMenuItem.Text = "Command &palette...";
            // 
            // SettingsSeparator
            // 
            SettingsSeparator.Name = "SettingsSeparator";
            SettingsSeparator.Size = new Size(325, 6);
            // 
            // SettingsMenuItem
            // 
            SettingsMenuItem.Name = "SettingsMenuItem";
            SettingsMenuItem.Size = new Size(328, 26);
            SettingsMenuItem.Text = "&Settings...";
            // 
            // ViewMenu
            // 
            ViewMenu.DropDownItems.AddRange(new ToolStripItem[] { ToggleThemeMenuItem, ToggleSplitOrientationMenuItem, ZoomInMenuItem, ZoomOutMenuItem, ZoomResetMenuItem, FitPreviewMenuItem, ToggleDiagnosticsMenuItem, ToggleMinimapMenuItem, ToggleLegacyPreviewMenuItem });
            ViewMenu.Name = "ViewMenu";
            ViewMenu.Size = new Size(55, 24);
            ViewMenu.Text = "&View";
            // 
            // ToggleThemeMenuItem
            // 
            ToggleThemeMenuItem.Name = "ToggleThemeMenuItem";
            ToggleThemeMenuItem.Size = new Size(285, 26);
            ToggleThemeMenuItem.Text = "Toggle &theme";
            // 
            // ToggleSplitOrientationMenuItem
            // 
            ToggleSplitOrientationMenuItem.Name = "ToggleSplitOrientationMenuItem";
            ToggleSplitOrientationMenuItem.Size = new Size(285, 26);
            ToggleSplitOrientationMenuItem.Text = "Toggle split &orientation";
            // 
            // ZoomInMenuItem
            // 
            ZoomInMenuItem.Name = "ZoomInMenuItem";
            ZoomInMenuItem.ShortcutKeys = Keys.Control | Keys.Oemplus;
            ZoomInMenuItem.Size = new Size(285, 26);
            ZoomInMenuItem.Text = "Zoom editor &in";
            // 
            // ZoomOutMenuItem
            // 
            ZoomOutMenuItem.Name = "ZoomOutMenuItem";
            ZoomOutMenuItem.ShortcutKeys = Keys.Control | Keys.OemMinus;
            ZoomOutMenuItem.Size = new Size(285, 26);
            ZoomOutMenuItem.Text = "Zoom editor &out";
            // 
            // ZoomResetMenuItem
            // 
            ZoomResetMenuItem.Name = "ZoomResetMenuItem";
            ZoomResetMenuItem.ShortcutKeys = Keys.Control | Keys.D0;
            ZoomResetMenuItem.Size = new Size(285, 26);
            ZoomResetMenuItem.Text = "&Reset editor zoom";
            // 
            // FitPreviewMenuItem
            // 
            FitPreviewMenuItem.Name = "FitPreviewMenuItem";
            FitPreviewMenuItem.Size = new Size(285, 26);
            FitPreviewMenuItem.Text = "&Fit preview";
            // 
            // ToggleDiagnosticsMenuItem
            // 
            ToggleDiagnosticsMenuItem.Name = "ToggleDiagnosticsMenuItem";
            ToggleDiagnosticsMenuItem.Size = new Size(285, 26);
            ToggleDiagnosticsMenuItem.Text = "Toggle &diagnostics panel";
            // 
            // ToggleMinimapMenuItem
            // 
            ToggleMinimapMenuItem.Name = "ToggleMinimapMenuItem";
            ToggleMinimapMenuItem.Size = new Size(285, 26);
            ToggleMinimapMenuItem.Text = "Toggle &minimap";
            // 
            // ToggleLegacyPreviewMenuItem
            // 
            ToggleLegacyPreviewMenuItem.Name = "ToggleLegacyPreviewMenuItem";
            ToggleLegacyPreviewMenuItem.Size = new Size(285, 26);
            ToggleLegacyPreviewMenuItem.Text = "Toggle &legacy Naiad preview";
            // 
            // ExportMenu
            // 
            ExportMenu.DropDownItems.AddRange(new ToolStripItem[] { ExportPngMenuItem, ExportSvgMenuItem });
            ExportMenu.Name = "ExportMenu";
            ExportMenu.Size = new Size(66, 24);
            ExportMenu.Text = "E&xport";
            // 
            // ExportPngMenuItem
            // 
            ExportPngMenuItem.Name = "ExportPngMenuItem";
            ExportPngMenuItem.Size = new Size(177, 26);
            ExportPngMenuItem.Text = "Export &PNG...";
            // 
            // ExportSvgMenuItem
            // 
            ExportSvgMenuItem.Name = "ExportSvgMenuItem";
            ExportSvgMenuItem.Size = new Size(177, 26);
            ExportSvgMenuItem.Text = "Export &SVG...";
            // 
            // SamplesMenu
            // 
            SamplesMenu.Name = "SamplesMenu";
            SamplesMenu.Size = new Size(79, 24);
            SamplesMenu.Text = "&Samples";
            // 
            // ArchiMenu
            // 
            ArchiMenu.DropDownItems.AddRange(new ToolStripItem[] { ImportExchangeMenuItem, ImportArchimateMenuItem, ArchiSeparator, ExportExchangeMenuItem, ExportArchimateMenuItem });
            ArchiMenu.Name = "ArchiMenu";
            ArchiMenu.Size = new Size(57, 24);
            ArchiMenu.Text = "&Archi";
            // 
            // ImportExchangeMenuItem
            // 
            ImportExchangeMenuItem.Name = "ImportExchangeMenuItem";
            ImportExchangeMenuItem.Size = new Size(260, 26);
            ImportExchangeMenuItem.Text = "Import Model Exchange...";
            // 
            // ImportArchimateMenuItem
            // 
            ImportArchimateMenuItem.Name = "ImportArchimateMenuItem";
            ImportArchimateMenuItem.Size = new Size(260, 26);
            ImportArchimateMenuItem.Text = "Import ArchiMate file...";
            // 
            // ArchiSeparator
            // 
            ArchiSeparator.Name = "ArchiSeparator";
            ArchiSeparator.Size = new Size(257, 6);
            // 
            // ExportExchangeMenuItem
            // 
            ExportExchangeMenuItem.Name = "ExportExchangeMenuItem";
            ExportExchangeMenuItem.Size = new Size(260, 26);
            ExportExchangeMenuItem.Text = "Export Model Exchange...";
            // 
            // ExportArchimateMenuItem
            // 
            ExportArchimateMenuItem.Name = "ExportArchimateMenuItem";
            ExportArchimateMenuItem.Size = new Size(260, 26);
            ExportArchimateMenuItem.Text = "Export ArchiMate file...";
            // 
            // CoArchiMenu
            // 
            CoArchiMenu.DropDownItems.AddRange(new ToolStripItem[] { OpenCoArchiFolderMenuItem, SaveCoArchiFolderMenuItem });
            CoArchiMenu.Name = "CoArchiMenu";
            CoArchiMenu.Size = new Size(73, 24);
            CoArchiMenu.Text = "&coArchi";
            // 
            // OpenCoArchiFolderMenuItem
            // 
            OpenCoArchiFolderMenuItem.Name = "OpenCoArchiFolderMenuItem";
            OpenCoArchiFolderMenuItem.Size = new Size(194, 26);
            OpenCoArchiFolderMenuItem.Text = "Open folder...";
            // 
            // SaveCoArchiFolderMenuItem
            // 
            SaveCoArchiFolderMenuItem.Name = "SaveCoArchiFolderMenuItem";
            SaveCoArchiFolderMenuItem.Size = new Size(194, 26);
            SaveCoArchiFolderMenuItem.Text = "Save to folder...";
            // 
            // BodySplit
            // 
            BodySplit.Dock = DockStyle.Fill;
            BodySplit.Location = new Point(0, 28);
            BodySplit.Margin = new Padding(2, 2, 2, 2);
            BodySplit.Name = "BodySplit";
            BodySplit.Orientation = Orientation.Horizontal;
            // 
            // BodySplit.Panel1
            // 
            BodySplit.Panel1.Controls.Add(EditorSplit);
            BodySplit.Panel1.Controls.Add(ElementToolbar);
            // 
            // BodySplit.Panel2
            // 
            BodySplit.Panel2.Controls.Add(DiagnosticsList);
            BodySplit.Panel2Collapsed = true;
            BodySplit.Size = new Size(898, 406);
            BodySplit.SplitterDistance = 336;
            BodySplit.SplitterWidth = 2;
            BodySplit.TabIndex = 1;
            // 
            // EditorSplit
            // 
            EditorSplit.Dock = DockStyle.Fill;
            EditorSplit.Location = new Point(0, 0);
            EditorSplit.Margin = new Padding(2, 2, 2, 2);
            EditorSplit.Name = "EditorSplit";
            // 
            // EditorSplit.Panel1
            // 
            EditorSplit.Panel1.Controls.Add(CodeView);
            EditorSplit.Panel1MinSize = 240;
            // 
            // EditorSplit.Panel2
            // 
            EditorSplit.Panel2.Controls.Add(PreviewHost);
            EditorSplit.Panel2MinSize = 240;
            EditorSplit.Size = new Size(898, 406);
            EditorSplit.SplitterDistance = 655;
            EditorSplit.SplitterWidth = 3;
            EditorSplit.TabIndex = 0;
            // 
            // CodeView
            // 
            CodeView.Dock = DockStyle.Fill;
            CodeView.Location = new Point(0, 0);
            CodeView.Margin = new Padding(0);
            CodeView.Name = "CodeView";
            CodeView.Size = new Size(655, 406);
            CodeView.TabIndex = 0;
            // 
            // PreviewHost
            // 
            PreviewHost.Controls.Add(PreviewCanvas);
            PreviewHost.Controls.Add(PreviewBanner);
            PreviewHost.Controls.Add(PreviewZoomInButton);
            PreviewHost.Controls.Add(PreviewZoomOutButton);
            PreviewHost.Controls.Add(PreviewFitButton);
            PreviewHost.Dock = DockStyle.Fill;
            PreviewHost.Location = new Point(0, 0);
            PreviewHost.Margin = new Padding(2, 2, 2, 2);
            PreviewHost.Name = "PreviewHost";
            PreviewHost.Size = new Size(240, 406);
            PreviewHost.TabIndex = 0;
            // 
            // PreviewCanvas
            // 
            PreviewCanvas.AccessibleName = "Diagram preview canvas";
            PreviewCanvas.AutoScroll = true;
            PreviewCanvas.BackColor = Color.White;
            PreviewCanvas.Controls.Add(PreviewImage);
            PreviewCanvas.Dock = DockStyle.Fill;
            PreviewCanvas.Location = new Point(0, 0);
            PreviewCanvas.Margin = new Padding(2, 2, 2, 2);
            PreviewCanvas.Name = "PreviewCanvas";
            PreviewCanvas.Size = new Size(240, 406);
            PreviewCanvas.TabIndex = 0;
            // 
            // PreviewImage
            // 
            PreviewImage.AccessibleName = "Diagram preview image";
            PreviewImage.BackColor = Color.White;
            PreviewImage.Dock = DockStyle.Fill;
            PreviewImage.Location = new Point(0, 0);
            PreviewImage.Margin = new Padding(2, 2, 2, 2);
            PreviewImage.Name = "PreviewImage";
            PreviewImage.Size = new Size(240, 406);
            PreviewImage.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewImage.TabIndex = 0;
            PreviewImage.TabStop = false;
            // 
            // PreviewBanner
            // 
            PreviewBanner.Dock = DockStyle.Top;
            PreviewBanner.Location = new Point(0, 0);
            PreviewBanner.Margin = new Padding(2, 0, 2, 0);
            PreviewBanner.Name = "PreviewBanner";
            PreviewBanner.Size = new Size(240, 0);
            PreviewBanner.TabIndex = 1;
            PreviewBanner.TextAlign = ContentAlignment.MiddleLeft;
            PreviewBanner.Visible = false;
            // 
            // PreviewZoomInButton
            // 
            PreviewZoomInButton.AccessibleName = "Zoom in";
            PreviewZoomInButton.FlatStyle = FlatStyle.Flat;
            PreviewZoomInButton.Location = new Point(0, 0);
            PreviewZoomInButton.Margin = new Padding(2, 2, 2, 2);
            PreviewZoomInButton.Name = "PreviewZoomInButton";
            PreviewZoomInButton.Size = new Size(23, 20);
            PreviewZoomInButton.TabIndex = 3;
            PreviewZoomInButton.Text = "+";
            PreviewZoomInButton.UseVisualStyleBackColor = true;
            // 
            // PreviewZoomOutButton
            // 
            PreviewZoomOutButton.AccessibleName = "Zoom out";
            PreviewZoomOutButton.FlatStyle = FlatStyle.Flat;
            PreviewZoomOutButton.Location = new Point(0, 0);
            PreviewZoomOutButton.Margin = new Padding(2, 2, 2, 2);
            PreviewZoomOutButton.Name = "PreviewZoomOutButton";
            PreviewZoomOutButton.Size = new Size(23, 20);
            PreviewZoomOutButton.TabIndex = 4;
            PreviewZoomOutButton.Text = "-";
            PreviewZoomOutButton.UseVisualStyleBackColor = true;
            // 
            // PreviewFitButton
            // 
            PreviewFitButton.AccessibleName = "Fit preview";
            PreviewFitButton.FlatStyle = FlatStyle.Flat;
            PreviewFitButton.Location = new Point(0, 0);
            PreviewFitButton.Margin = new Padding(2, 2, 2, 2);
            PreviewFitButton.Name = "PreviewFitButton";
            PreviewFitButton.Size = new Size(23, 20);
            PreviewFitButton.TabIndex = 5;
            PreviewFitButton.UseVisualStyleBackColor = true;
            // 
            // DiagnosticsList
            // 
            DiagnosticsList.AccessibleName = "Validation diagnostics";
            DiagnosticsList.Dock = DockStyle.Fill;
            DiagnosticsList.Font = new Font("Consolas", 7.20000029F);
            DiagnosticsList.HorizontalScrollbar = true;
            DiagnosticsList.Location = new Point(0, 0);
            DiagnosticsList.Margin = new Padding(2, 2, 2, 2);
            DiagnosticsList.Name = "DiagnosticsList";
            DiagnosticsList.Size = new Size(896, 74);
            DiagnosticsList.TabIndex = 0;
            // 
            // ElementToolbar
            // 
            ElementToolbar.Dock = DockStyle.Top;
            ElementToolbar.Name = "ElementToolbar";
            ElementToolbar.TabIndex = 2;
            // 
            // StatusBar
            // 
            StatusBar.ImageScalingSize = new Size(20, 20);
            StatusBar.Items.AddRange(new ToolStripItem[] { StatusLabel, SignatureLabel, TimingLabel, CoArchiDirtyLabel });
            StatusBar.Location = new Point(0, 434);
            StatusBar.Name = "StatusBar";
            StatusBar.Padding = new Padding(1, 0, 10, 0);
            StatusBar.Size = new Size(898, 26);
            StatusBar.TabIndex = 2;
            // 
            // StatusLabel
            // 
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(887, 20);
            StatusLabel.Spring = true;
            StatusLabel.Text = "Ready";
            StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // SignatureLabel
            // 
            SignatureLabel.Name = "SignatureLabel";
            SignatureLabel.Size = new Size(0, 20);
            // 
            // TimingLabel
            // 
            TimingLabel.Name = "TimingLabel";
            TimingLabel.Size = new Size(0, 20);
            // 
            // CoArchiDirtyLabel
            // 
            CoArchiDirtyLabel.Name = "CoArchiDirtyLabel";
            CoArchiDirtyLabel.Size = new Size(0, 20);
            // 
            // RenderTimer
            // 
            RenderTimer.Interval = 350;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(898, 460);
            Controls.Add(BodySplit);
            Controls.Add(StatusBar);
            Controls.Add(MainMenu);
            MainMenuStrip = MainMenu;
            Margin = new Padding(2, 2, 2, 2);
            MinimumSize = new Size(636, 355);
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
        private ToolStripMenuItem NewMenuItem;
        private ToolStripMenuItem OpenMenuItem;
        private ToolStripMenuItem SaveMenuItem;
        private ToolStripMenuItem SaveAsMenuItem;
        private ToolStripMenuItem RecentFilesMenuItem;
        private ToolStripSeparator FileSeparator;
        private ToolStripMenuItem ExitMenuItem;
        private ToolStripMenuItem EditMenu;
        private ToolStripMenuItem UndoMenuItem;
        private ToolStripMenuItem RedoMenuItem;
        private ToolStripSeparator EditHistorySeparator;
        private ToolStripMenuItem CutMenuItem;
        private ToolStripMenuItem CopyMenuItem;
        private ToolStripMenuItem PasteMenuItem;
        private ToolStripSeparator ClipboardSeparator;
        private ToolStripMenuItem FindMenuItem;
        private ToolStripMenuItem ReplaceMenuItem;
        private ToolStripMenuItem GoToLineMenuItem;
        private ToolStripMenuItem FindNextMenuItem;
        private ToolStripMenuItem FindPreviousMenuItem;
        private ToolStripSeparator FindSeparator;
        private ToolStripMenuItem CommentMenuItem;
        private ToolStripMenuItem UncommentMenuItem;
        private ToolStripMenuItem DuplicateLineMenuItem;
        private ToolStripMenuItem DeleteLineMenuItem;
        private ToolStripMenuItem IndentMenuItem;
        private ToolStripMenuItem OutdentMenuItem;
        private ToolStripSeparator LineEditSeparator;
        private ToolStripMenuItem NextErrorMenuItem;
        private ToolStripMenuItem PreviousErrorMenuItem;
        private ToolStripMenuItem CopySourceMenuItem;
        private ToolStripMenuItem ListMembersMenuItem;
        private ToolStripMenuItem ParameterInfoMenuItem;
        private ToolStripMenuItem QuickInfoMenuItem;
        private ToolStripMenuItem RenameSymbolMenuItem;
        private ToolStripMenuItem GoToDefinitionMenuItem;
        private ToolStripMenuItem FindReferencesMenuItem;
        private ToolStripMenuItem CommandPaletteMenuItem;
        private ToolStripMenuItem SelectNextOccurrenceMenuItem;
        private ToolStripMenuItem InsertArchiSnippetMenuItem;
        private ToolStripSeparator SettingsSeparator;
        private ToolStripMenuItem SettingsMenuItem;
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
        private CodeEditor CodeView;
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
        private ElementPalette ElementToolbar;
        private Button PreviewZoomInButton;
        private Button PreviewZoomOutButton;
        private Button PreviewFitButton;
        private System.ComponentModel.IContainer components;
    }
}
