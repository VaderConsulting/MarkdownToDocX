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
            SamplesArchitectureMenu = new ToolStripMenuItem();
            SamplesArchitectureSimpleMenuItem = new ToolStripMenuItem();
            SamplesArchitectureCompleteMenuItem = new ToolStripMenuItem();
            SamplesBlockMenu = new ToolStripMenuItem();
            SamplesBlockSimpleMenuItem = new ToolStripMenuItem();
            SamplesBlockCompleteMenuItem = new ToolStripMenuItem();
            SamplesC4Menu = new ToolStripMenuItem();
            SamplesC4SimpleMenuItem = new ToolStripMenuItem();
            SamplesC4CompleteMenuItem = new ToolStripMenuItem();
            SamplesClassMenu = new ToolStripMenuItem();
            SamplesClassSimpleMenuItem = new ToolStripMenuItem();
            SamplesClassCompleteMenuItem = new ToolStripMenuItem();
            SamplesEnterpriseMenu = new ToolStripMenuItem();
            SamplesEnterpriseSimpleMenuItem = new ToolStripMenuItem();
            SamplesEnterpriseCompleteMenuItem = new ToolStripMenuItem();
            SamplesEntityRelationshipMenu = new ToolStripMenuItem();
            SamplesEntityRelationshipSimpleMenuItem = new ToolStripMenuItem();
            SamplesEntityRelationshipCompleteMenuItem = new ToolStripMenuItem();
            SamplesFlowchartMenu = new ToolStripMenuItem();
            SamplesFlowchartSimpleMenuItem = new ToolStripMenuItem();
            SamplesFlowchartCompleteMenuItem = new ToolStripMenuItem();
            SamplesGanttMenu = new ToolStripMenuItem();
            SamplesGanttSimpleMenuItem = new ToolStripMenuItem();
            SamplesGanttCompleteMenuItem = new ToolStripMenuItem();
            SamplesGitGraphMenu = new ToolStripMenuItem();
            SamplesGitGraphSimpleMenuItem = new ToolStripMenuItem();
            SamplesGitGraphCompleteMenuItem = new ToolStripMenuItem();
            SamplesJourneyMenu = new ToolStripMenuItem();
            SamplesJourneySimpleMenuItem = new ToolStripMenuItem();
            SamplesJourneyCompleteMenuItem = new ToolStripMenuItem();
            SamplesMindmapMenu = new ToolStripMenuItem();
            SamplesMindmapSimpleMenuItem = new ToolStripMenuItem();
            SamplesMindmapCompleteMenuItem = new ToolStripMenuItem();
            SamplesPacketMenu = new ToolStripMenuItem();
            SamplesPacketSimpleMenuItem = new ToolStripMenuItem();
            SamplesPacketCompleteMenuItem = new ToolStripMenuItem();
            SamplesPieMenu = new ToolStripMenuItem();
            SamplesPieSimpleMenuItem = new ToolStripMenuItem();
            SamplesPieCompleteMenuItem = new ToolStripMenuItem();
            SamplesQuadrantChartMenu = new ToolStripMenuItem();
            SamplesQuadrantChartSimpleMenuItem = new ToolStripMenuItem();
            SamplesQuadrantChartCompleteMenuItem = new ToolStripMenuItem();
            SamplesRadarMenu = new ToolStripMenuItem();
            SamplesRadarSimpleMenuItem = new ToolStripMenuItem();
            SamplesRadarCompleteMenuItem = new ToolStripMenuItem();
            SamplesSequenceMenu = new ToolStripMenuItem();
            SamplesSequenceSimpleMenuItem = new ToolStripMenuItem();
            SamplesSequenceCompleteMenuItem = new ToolStripMenuItem();
            SamplesStateMenu = new ToolStripMenuItem();
            SamplesStateSimpleMenuItem = new ToolStripMenuItem();
            SamplesStateCompleteMenuItem = new ToolStripMenuItem();
            SamplesTimelineMenu = new ToolStripMenuItem();
            SamplesTimelineSimpleMenuItem = new ToolStripMenuItem();
            SamplesTimelineCompleteMenuItem = new ToolStripMenuItem();
            SamplesTreemapMenu = new ToolStripMenuItem();
            SamplesTreemapSimpleMenuItem = new ToolStripMenuItem();
            SamplesTreemapCompleteMenuItem = new ToolStripMenuItem();
            SamplesXyChartMenu = new ToolStripMenuItem();
            SamplesXyChartSimpleMenuItem = new ToolStripMenuItem();
            SamplesXyChartCompleteMenuItem = new ToolStripMenuItem();
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
            SelectionDetails = new SelectionPanel();
            Preview = new PreviewCanvas();
            ElementToolbar = new ElementPalette();
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
            StatusBar.SuspendLayout();
            SuspendLayout();
            // 
            // MainMenu
            // 
            MainMenu.ImageScalingSize = new Size(24, 24);
            MainMenu.Items.AddRange(new ToolStripItem[] { FileMenu, EditMenu, ViewMenu, ExportMenu, SamplesMenu, ArchiMenu, CoArchiMenu });
            MainMenu.Location = new Point(0, 0);
            MainMenu.Name = "MainMenu";
            MainMenu.Padding = new Padding(6, 3, 0, 3);
            MainMenu.Size = new Size(1317, 30);
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
            NewMenuItem.Click += NewMenuItem_Click;
            // 
            // OpenMenuItem
            // 
            OpenMenuItem.Name = "OpenMenuItem";
            OpenMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            OpenMenuItem.Size = new Size(190, 26);
            OpenMenuItem.Text = "&Open...";
            OpenMenuItem.Click += OpenMenuItem_Click;
            // 
            // SaveMenuItem
            // 
            SaveMenuItem.Name = "SaveMenuItem";
            SaveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            SaveMenuItem.Size = new Size(190, 26);
            SaveMenuItem.Text = "&Save";
            SaveMenuItem.Click += SaveMenuItem_Click;
            // 
            // SaveAsMenuItem
            // 
            SaveAsMenuItem.Name = "SaveAsMenuItem";
            SaveAsMenuItem.Size = new Size(190, 26);
            SaveAsMenuItem.Text = "Save &As...";
            SaveAsMenuItem.Click += SaveAsMenuItem_Click;
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
            ExitMenuItem.Click += ExitMenuItem_Click;
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
            UndoMenuItem.Size = new Size(337, 26);
            UndoMenuItem.Text = "&Undo";
            UndoMenuItem.Click += UndoMenuItem_Click;
            // 
            // RedoMenuItem
            // 
            RedoMenuItem.Name = "RedoMenuItem";
            RedoMenuItem.ShortcutKeyDisplayString = "Ctrl+Y";
            RedoMenuItem.Size = new Size(337, 26);
            RedoMenuItem.Text = "&Redo";
            RedoMenuItem.Click += RedoMenuItem_Click;
            // 
            // EditHistorySeparator
            // 
            EditHistorySeparator.Name = "EditHistorySeparator";
            EditHistorySeparator.Size = new Size(334, 6);
            // 
            // CutMenuItem
            // 
            CutMenuItem.Name = "CutMenuItem";
            CutMenuItem.ShortcutKeyDisplayString = "Ctrl+X";
            CutMenuItem.Size = new Size(337, 26);
            CutMenuItem.Text = "Cu&t";
            CutMenuItem.Click += CutMenuItem_Click;
            // 
            // CopyMenuItem
            // 
            CopyMenuItem.Name = "CopyMenuItem";
            CopyMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            CopyMenuItem.Size = new Size(337, 26);
            CopyMenuItem.Text = "&Copy";
            CopyMenuItem.Click += CopyMenuItem_Click;
            // 
            // PasteMenuItem
            // 
            PasteMenuItem.Name = "PasteMenuItem";
            PasteMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
            PasteMenuItem.Size = new Size(337, 26);
            PasteMenuItem.Text = "&Paste";
            PasteMenuItem.Click += PasteMenuItem_Click;
            // 
            // ClipboardSeparator
            // 
            ClipboardSeparator.Name = "ClipboardSeparator";
            ClipboardSeparator.Size = new Size(334, 6);
            // 
            // FindMenuItem
            // 
            FindMenuItem.Name = "FindMenuItem";
            FindMenuItem.ShortcutKeys = Keys.Control | Keys.F;
            FindMenuItem.Size = new Size(337, 26);
            FindMenuItem.Text = "&Find...";
            FindMenuItem.Click += FindMenuItem_Click;
            // 
            // ReplaceMenuItem
            // 
            ReplaceMenuItem.Name = "ReplaceMenuItem";
            ReplaceMenuItem.ShortcutKeys = Keys.Control | Keys.H;
            ReplaceMenuItem.Size = new Size(337, 26);
            ReplaceMenuItem.Text = "R&eplace...";
            ReplaceMenuItem.Click += ReplaceMenuItem_Click;
            // 
            // GoToLineMenuItem
            // 
            GoToLineMenuItem.Name = "GoToLineMenuItem";
            GoToLineMenuItem.ShortcutKeys = Keys.Control | Keys.G;
            GoToLineMenuItem.Size = new Size(337, 26);
            GoToLineMenuItem.Text = "&Go to line...";
            GoToLineMenuItem.Click += GoToLineMenuItem_Click;
            // 
            // FindNextMenuItem
            // 
            FindNextMenuItem.Name = "FindNextMenuItem";
            FindNextMenuItem.ShortcutKeys = Keys.F3;
            FindNextMenuItem.Size = new Size(337, 26);
            FindNextMenuItem.Text = "Find &next";
            FindNextMenuItem.Click += FindNextMenuItem_Click;
            // 
            // FindPreviousMenuItem
            // 
            FindPreviousMenuItem.Name = "FindPreviousMenuItem";
            FindPreviousMenuItem.ShortcutKeys = Keys.Shift | Keys.F3;
            FindPreviousMenuItem.Size = new Size(337, 26);
            FindPreviousMenuItem.Text = "Find &previous";
            FindPreviousMenuItem.Click += FindPreviousMenuItem_Click;
            // 
            // FindSeparator
            // 
            FindSeparator.Name = "FindSeparator";
            FindSeparator.Size = new Size(334, 6);
            // 
            // CommentMenuItem
            // 
            CommentMenuItem.Name = "CommentMenuItem";
            CommentMenuItem.ShortcutKeyDisplayString = "Ctrl+K, Ctrl+C";
            CommentMenuItem.Size = new Size(337, 26);
            CommentMenuItem.Text = "Co&mment selection";
            CommentMenuItem.Click += CommentMenuItem_Click;
            // 
            // UncommentMenuItem
            // 
            UncommentMenuItem.Name = "UncommentMenuItem";
            UncommentMenuItem.ShortcutKeyDisplayString = "Ctrl+K, Ctrl+U";
            UncommentMenuItem.Size = new Size(337, 26);
            UncommentMenuItem.Text = "Uncomment se&lection";
            UncommentMenuItem.Click += UncommentMenuItem_Click;
            // 
            // DuplicateLineMenuItem
            // 
            DuplicateLineMenuItem.Name = "DuplicateLineMenuItem";
            DuplicateLineMenuItem.ShortcutKeyDisplayString = "Ctrl+E, V";
            DuplicateLineMenuItem.Size = new Size(337, 26);
            DuplicateLineMenuItem.Text = "D&uplicate line";
            DuplicateLineMenuItem.Click += DuplicateLineMenuItem_Click;
            // 
            // DeleteLineMenuItem
            // 
            DeleteLineMenuItem.Name = "DeleteLineMenuItem";
            DeleteLineMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.L;
            DeleteLineMenuItem.Size = new Size(337, 26);
            DeleteLineMenuItem.Text = "&Delete line";
            DeleteLineMenuItem.Click += DeleteLineMenuItem_Click;
            // 
            // IndentMenuItem
            // 
            IndentMenuItem.Name = "IndentMenuItem";
            IndentMenuItem.ShortcutKeyDisplayString = "Tab";
            IndentMenuItem.Size = new Size(337, 26);
            IndentMenuItem.Text = "&Indent";
            IndentMenuItem.Click += IndentMenuItem_Click;
            // 
            // OutdentMenuItem
            // 
            OutdentMenuItem.Name = "OutdentMenuItem";
            OutdentMenuItem.ShortcutKeyDisplayString = "Shift+Tab";
            OutdentMenuItem.Size = new Size(337, 26);
            OutdentMenuItem.Text = "O&utdent";
            OutdentMenuItem.Click += OutdentMenuItem_Click;
            // 
            // LineEditSeparator
            // 
            LineEditSeparator.Name = "LineEditSeparator";
            LineEditSeparator.Size = new Size(334, 6);
            // 
            // NextErrorMenuItem
            // 
            NextErrorMenuItem.Name = "NextErrorMenuItem";
            NextErrorMenuItem.ShortcutKeys = Keys.F8;
            NextErrorMenuItem.Size = new Size(337, 26);
            NextErrorMenuItem.Text = "Next &error";
            NextErrorMenuItem.Click += NextErrorMenuItem_Click;
            // 
            // PreviousErrorMenuItem
            // 
            PreviousErrorMenuItem.Name = "PreviousErrorMenuItem";
            PreviousErrorMenuItem.ShortcutKeys = Keys.Shift | Keys.F8;
            PreviousErrorMenuItem.Size = new Size(337, 26);
            PreviousErrorMenuItem.Text = "Pre&vious error";
            PreviousErrorMenuItem.Click += PreviousErrorMenuItem_Click;
            // 
            // CopySourceMenuItem
            // 
            CopySourceMenuItem.Name = "CopySourceMenuItem";
            CopySourceMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;
            CopySourceMenuItem.Size = new Size(337, 26);
            CopySourceMenuItem.Text = "&Copy Mermaid source";
            CopySourceMenuItem.Click += CopySourceMenuItem_Click;
            // 
            // ListMembersMenuItem
            // 
            ListMembersMenuItem.Name = "ListMembersMenuItem";
            ListMembersMenuItem.ShortcutKeys = Keys.Control | Keys.J;
            ListMembersMenuItem.Size = new Size(337, 26);
            ListMembersMenuItem.Text = "&List Members";
            ListMembersMenuItem.Click += ListMembersMenuItem_Click;
            // 
            // ParameterInfoMenuItem
            // 
            ParameterInfoMenuItem.Name = "ParameterInfoMenuItem";
            ParameterInfoMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.Space;
            ParameterInfoMenuItem.Size = new Size(337, 26);
            ParameterInfoMenuItem.Text = "&Parameter Info";
            ParameterInfoMenuItem.Click += ParameterInfoMenuItem_Click;
            // 
            // QuickInfoMenuItem
            // 
            QuickInfoMenuItem.Name = "QuickInfoMenuItem";
            QuickInfoMenuItem.ShortcutKeyDisplayString = "Ctrl+K, Ctrl+I";
            QuickInfoMenuItem.Size = new Size(337, 26);
            QuickInfoMenuItem.Text = "&Quick Info";
            QuickInfoMenuItem.Click += QuickInfoMenuItem_Click;
            // 
            // RenameSymbolMenuItem
            // 
            RenameSymbolMenuItem.Name = "RenameSymbolMenuItem";
            RenameSymbolMenuItem.ShortcutKeys = Keys.F2;
            RenameSymbolMenuItem.Size = new Size(337, 26);
            RenameSymbolMenuItem.Text = "&Rename symbol...";
            RenameSymbolMenuItem.Click += RenameSymbolMenuItem_Click;
            // 
            // GoToDefinitionMenuItem
            // 
            GoToDefinitionMenuItem.Name = "GoToDefinitionMenuItem";
            GoToDefinitionMenuItem.ShortcutKeys = Keys.F12;
            GoToDefinitionMenuItem.Size = new Size(337, 26);
            GoToDefinitionMenuItem.Text = "&Go to definition";
            GoToDefinitionMenuItem.Click += GoToDefinitionMenuItem_Click;
            // 
            // FindReferencesMenuItem
            // 
            FindReferencesMenuItem.Name = "FindReferencesMenuItem";
            FindReferencesMenuItem.ShortcutKeys = Keys.Shift | Keys.F12;
            FindReferencesMenuItem.Size = new Size(337, 26);
            FindReferencesMenuItem.Text = "Find &references";
            FindReferencesMenuItem.Click += FindReferencesMenuItem_Click;
            // 
            // SelectNextOccurrenceMenuItem
            // 
            SelectNextOccurrenceMenuItem.Name = "SelectNextOccurrenceMenuItem";
            SelectNextOccurrenceMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            SelectNextOccurrenceMenuItem.Size = new Size(337, 26);
            SelectNextOccurrenceMenuItem.Text = "Select &next occurrence";
            SelectNextOccurrenceMenuItem.Click += SelectNextOccurrenceMenuItem_Click;
            // 
            // InsertArchiSnippetMenuItem
            // 
            InsertArchiSnippetMenuItem.Name = "InsertArchiSnippetMenuItem";
            InsertArchiSnippetMenuItem.Size = new Size(337, 26);
            InsertArchiSnippetMenuItem.Text = "Insert from &Archi exchange XML...";
            InsertArchiSnippetMenuItem.Click += InsertArchiSnippetMenuItem_Click;
            // 
            // CommandPaletteMenuItem
            // 
            CommandPaletteMenuItem.Name = "CommandPaletteMenuItem";
            CommandPaletteMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.P;
            CommandPaletteMenuItem.Size = new Size(337, 26);
            CommandPaletteMenuItem.Text = "Command &palette...";
            CommandPaletteMenuItem.Click += CommandPaletteMenuItem_Click;
            // 
            // SettingsSeparator
            // 
            SettingsSeparator.Name = "SettingsSeparator";
            SettingsSeparator.Size = new Size(334, 6);
            // 
            // SettingsMenuItem
            // 
            SettingsMenuItem.Name = "SettingsMenuItem";
            SettingsMenuItem.Size = new Size(337, 26);
            SettingsMenuItem.Text = "&Settings...";
            SettingsMenuItem.Click += SettingsMenuItem_Click;
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
            ToggleThemeMenuItem.Size = new Size(315, 26);
            ToggleThemeMenuItem.Text = "Toggle &theme";
            ToggleThemeMenuItem.Click += ToggleThemeMenuItem_Click;
            // 
            // ToggleSplitOrientationMenuItem
            // 
            ToggleSplitOrientationMenuItem.Name = "ToggleSplitOrientationMenuItem";
            ToggleSplitOrientationMenuItem.Size = new Size(315, 26);
            ToggleSplitOrientationMenuItem.Text = "Toggle split &orientation";
            ToggleSplitOrientationMenuItem.Click += ToggleSplitOrientationMenuItem_Click;
            // 
            // ZoomInMenuItem
            // 
            ZoomInMenuItem.Name = "ZoomInMenuItem";
            ZoomInMenuItem.ShortcutKeys = Keys.Control | Keys.Oemplus;
            ZoomInMenuItem.Size = new Size(315, 26);
            ZoomInMenuItem.Text = "Zoom editor &in";
            ZoomInMenuItem.Click += ZoomInMenuItem_Click;
            // 
            // ZoomOutMenuItem
            // 
            ZoomOutMenuItem.Name = "ZoomOutMenuItem";
            ZoomOutMenuItem.ShortcutKeys = Keys.Control | Keys.OemMinus;
            ZoomOutMenuItem.Size = new Size(315, 26);
            ZoomOutMenuItem.Text = "Zoom editor &out";
            ZoomOutMenuItem.Click += ZoomOutMenuItem_Click;
            // 
            // ZoomResetMenuItem
            // 
            ZoomResetMenuItem.Name = "ZoomResetMenuItem";
            ZoomResetMenuItem.ShortcutKeys = Keys.Control | Keys.D0;
            ZoomResetMenuItem.Size = new Size(315, 26);
            ZoomResetMenuItem.Text = "&Reset editor zoom";
            ZoomResetMenuItem.Click += ZoomResetMenuItem_Click;
            // 
            // FitPreviewMenuItem
            // 
            FitPreviewMenuItem.Name = "FitPreviewMenuItem";
            FitPreviewMenuItem.Size = new Size(315, 26);
            FitPreviewMenuItem.Text = "&Fit preview";
            FitPreviewMenuItem.Click += FitPreviewMenuItem_Click;
            // 
            // ToggleDiagnosticsMenuItem
            // 
            ToggleDiagnosticsMenuItem.Name = "ToggleDiagnosticsMenuItem";
            ToggleDiagnosticsMenuItem.Size = new Size(315, 26);
            ToggleDiagnosticsMenuItem.Text = "Toggle &diagnostics panel";
            ToggleDiagnosticsMenuItem.Click += ToggleDiagnosticsMenuItem_Click;
            // 
            // ToggleMinimapMenuItem
            // 
            ToggleMinimapMenuItem.Name = "ToggleMinimapMenuItem";
            ToggleMinimapMenuItem.Size = new Size(315, 26);
            ToggleMinimapMenuItem.Text = "Toggle &minimap";
            ToggleMinimapMenuItem.Click += ToggleMinimapMenuItem_Click;
            // 
            // ToggleLegacyPreviewMenuItem
            // 
            ToggleLegacyPreviewMenuItem.Name = "ToggleLegacyPreviewMenuItem";
            ToggleLegacyPreviewMenuItem.Size = new Size(315, 26);
            ToggleLegacyPreviewMenuItem.Text = "Toggle &legacy Naiad preview";
            ToggleLegacyPreviewMenuItem.Click += ToggleLegacyPreviewMenuItem_Click;
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
            ExportPngMenuItem.Click += ExportPngMenuItem_Click;
            // 
            // ExportSvgMenuItem
            // 
            ExportSvgMenuItem.Name = "ExportSvgMenuItem";
            ExportSvgMenuItem.Size = new Size(177, 26);
            ExportSvgMenuItem.Text = "Export &SVG...";
            ExportSvgMenuItem.Click += ExportSvgMenuItem_Click;
            // 
            // SamplesMenu
            // 
            SamplesMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesArchitectureMenu, SamplesBlockMenu, SamplesC4Menu, SamplesClassMenu, SamplesEnterpriseMenu, SamplesEntityRelationshipMenu, SamplesFlowchartMenu, SamplesGanttMenu, SamplesGitGraphMenu, SamplesJourneyMenu, SamplesMindmapMenu, SamplesPacketMenu, SamplesPieMenu, SamplesQuadrantChartMenu, SamplesRadarMenu, SamplesSequenceMenu, SamplesStateMenu, SamplesTimelineMenu, SamplesTreemapMenu, SamplesXyChartMenu });
            SamplesMenu.Name = "SamplesMenu";
            SamplesMenu.Size = new Size(79, 24);
            SamplesMenu.Text = "&Samples";
            // 
            // SamplesArchitectureMenu
            // 
            SamplesArchitectureMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesArchitectureSimpleMenuItem, SamplesArchitectureCompleteMenuItem });
            SamplesArchitectureMenu.Name = "SamplesArchitectureMenu";
            SamplesArchitectureMenu.Size = new Size(211, 26);
            SamplesArchitectureMenu.Text = "Architecture";
            // 
            // SamplesArchitectureSimpleMenuItem
            // 
            SamplesArchitectureSimpleMenuItem.Name = "SamplesArchitectureSimpleMenuItem";
            SamplesArchitectureSimpleMenuItem.Size = new Size(296, 26);
            SamplesArchitectureSimpleMenuItem.Text = "One service (Simple)";
            SamplesArchitectureSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesArchitectureCompleteMenuItem
            // 
            SamplesArchitectureCompleteMenuItem.Name = "SamplesArchitectureCompleteMenuItem";
            SamplesArchitectureCompleteMenuItem.Size = new Size(296, 26);
            SamplesArchitectureCompleteMenuItem.Text = "Group and services (Complete)";
            SamplesArchitectureCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesBlockMenu
            // 
            SamplesBlockMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesBlockSimpleMenuItem, SamplesBlockCompleteMenuItem });
            SamplesBlockMenu.Name = "SamplesBlockMenu";
            SamplesBlockMenu.Size = new Size(211, 26);
            SamplesBlockMenu.Text = "Block";
            // 
            // SamplesBlockSimpleMenuItem
            // 
            SamplesBlockSimpleMenuItem.Name = "SamplesBlockSimpleMenuItem";
            SamplesBlockSimpleMenuItem.Size = new Size(304, 26);
            SamplesBlockSimpleMenuItem.Text = "One block (Simple)";
            SamplesBlockSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesBlockCompleteMenuItem
            // 
            SamplesBlockCompleteMenuItem.Name = "SamplesBlockCompleteMenuItem";
            SamplesBlockCompleteMenuItem.Size = new Size(304, 26);
            SamplesBlockCompleteMenuItem.Text = "Columns and a span (Complete)";
            SamplesBlockCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesC4Menu
            // 
            SamplesC4Menu.DropDownItems.AddRange(new ToolStripItem[] { SamplesC4SimpleMenuItem, SamplesC4CompleteMenuItem });
            SamplesC4Menu.Name = "SamplesC4Menu";
            SamplesC4Menu.Size = new Size(211, 26);
            SamplesC4Menu.Text = "C4";
            // 
            // SamplesC4SimpleMenuItem
            // 
            SamplesC4SimpleMenuItem.Name = "SamplesC4SimpleMenuItem";
            SamplesC4SimpleMenuItem.Size = new Size(377, 26);
            SamplesC4SimpleMenuItem.Text = "Person and system (Simple)";
            SamplesC4SimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesC4CompleteMenuItem
            // 
            SamplesC4CompleteMenuItem.Name = "SamplesC4CompleteMenuItem";
            SamplesC4CompleteMenuItem.Size = new Size(377, 26);
            SamplesC4CompleteMenuItem.Text = "Context and an external system (Complete)";
            SamplesC4CompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesClassMenu
            // 
            SamplesClassMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesClassSimpleMenuItem, SamplesClassCompleteMenuItem });
            SamplesClassMenu.Name = "SamplesClassMenu";
            SamplesClassMenu.Size = new Size(211, 26);
            SamplesClassMenu.Text = "Class";
            // 
            // SamplesClassSimpleMenuItem
            // 
            SamplesClassSimpleMenuItem.Name = "SamplesClassSimpleMenuItem";
            SamplesClassSimpleMenuItem.Size = new Size(323, 26);
            SamplesClassSimpleMenuItem.Text = "Inheritance (Simple)";
            SamplesClassSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesClassCompleteMenuItem
            // 
            SamplesClassCompleteMenuItem.Name = "SamplesClassCompleteMenuItem";
            SamplesClassCompleteMenuItem.Size = new Size(323, 26);
            SamplesClassCompleteMenuItem.Text = "Members and relations (Complete)";
            SamplesClassCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesEnterpriseMenu
            // 
            SamplesEnterpriseMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesEnterpriseSimpleMenuItem, SamplesEnterpriseCompleteMenuItem });
            SamplesEnterpriseMenu.Name = "SamplesEnterpriseMenu";
            SamplesEnterpriseMenu.Size = new Size(211, 26);
            SamplesEnterpriseMenu.Text = "Enterprise";
            // 
            // SamplesEnterpriseSimpleMenuItem
            // 
            SamplesEnterpriseSimpleMenuItem.Name = "SamplesEnterpriseSimpleMenuItem";
            SamplesEnterpriseSimpleMenuItem.Size = new Size(337, 26);
            SamplesEnterpriseSimpleMenuItem.Text = "Web shop (Simple)";
            SamplesEnterpriseSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesEnterpriseCompleteMenuItem
            // 
            SamplesEnterpriseCompleteMenuItem.Name = "SamplesEnterpriseCompleteMenuItem";
            SamplesEnterpriseCompleteMenuItem.Size = new Size(337, 26);
            SamplesEnterpriseCompleteMenuItem.Text = "Clauses and relationships (Complete)";
            SamplesEnterpriseCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesEntityRelationshipMenu
            // 
            SamplesEntityRelationshipMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesEntityRelationshipSimpleMenuItem, SamplesEntityRelationshipCompleteMenuItem });
            SamplesEntityRelationshipMenu.Name = "SamplesEntityRelationshipMenu";
            SamplesEntityRelationshipMenu.Size = new Size(211, 26);
            SamplesEntityRelationshipMenu.Text = "Entity relationship";
            // 
            // SamplesEntityRelationshipSimpleMenuItem
            // 
            SamplesEntityRelationshipSimpleMenuItem.Name = "SamplesEntityRelationshipSimpleMenuItem";
            SamplesEntityRelationshipSimpleMenuItem.Size = new Size(321, 26);
            SamplesEntityRelationshipSimpleMenuItem.Text = "Customer places an order (Simple)";
            SamplesEntityRelationshipSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesEntityRelationshipCompleteMenuItem
            // 
            SamplesEntityRelationshipCompleteMenuItem.Name = "SamplesEntityRelationshipCompleteMenuItem";
            SamplesEntityRelationshipCompleteMenuItem.Size = new Size(321, 26);
            SamplesEntityRelationshipCompleteMenuItem.Text = "Keys and cardinalities (Complete)";
            SamplesEntityRelationshipCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesFlowchartMenu
            // 
            SamplesFlowchartMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesFlowchartSimpleMenuItem, SamplesFlowchartCompleteMenuItem });
            SamplesFlowchartMenu.Name = "SamplesFlowchartMenu";
            SamplesFlowchartMenu.Size = new Size(211, 26);
            SamplesFlowchartMenu.Text = "Flowchart";
            // 
            // SamplesFlowchartSimpleMenuItem
            // 
            SamplesFlowchartSimpleMenuItem.Name = "SamplesFlowchartSimpleMenuItem";
            SamplesFlowchartSimpleMenuItem.Size = new Size(336, 26);
            SamplesFlowchartSimpleMenuItem.Text = "Start to done (Simple)";
            SamplesFlowchartSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesFlowchartCompleteMenuItem
            // 
            SamplesFlowchartCompleteMenuItem.Name = "SamplesFlowchartCompleteMenuItem";
            SamplesFlowchartCompleteMenuItem.Size = new Size(336, 26);
            SamplesFlowchartCompleteMenuItem.Text = "Shapes, links, and groups (Complete)";
            SamplesFlowchartCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesGanttMenu
            // 
            SamplesGanttMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesGanttSimpleMenuItem, SamplesGanttCompleteMenuItem });
            SamplesGanttMenu.Name = "SamplesGanttMenu";
            SamplesGanttMenu.Size = new Size(211, 26);
            SamplesGanttMenu.Text = "Gantt";
            // 
            // SamplesGanttSimpleMenuItem
            // 
            SamplesGanttSimpleMenuItem.Name = "SamplesGanttSimpleMenuItem";
            SamplesGanttSimpleMenuItem.Size = new Size(295, 26);
            SamplesGanttSimpleMenuItem.Text = "One task (Simple)";
            SamplesGanttSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesGanttCompleteMenuItem
            // 
            SamplesGanttCompleteMenuItem.Name = "SamplesGanttCompleteMenuItem";
            SamplesGanttCompleteMenuItem.Size = new Size(295, 26);
            SamplesGanttCompleteMenuItem.Text = "Dates and sections (Complete)";
            SamplesGanttCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesGitGraphMenu
            // 
            SamplesGitGraphMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesGitGraphSimpleMenuItem, SamplesGitGraphCompleteMenuItem });
            SamplesGitGraphMenu.Name = "SamplesGitGraphMenu";
            SamplesGitGraphMenu.Size = new Size(211, 26);
            SamplesGitGraphMenu.Text = "Git graph";
            // 
            // SamplesGitGraphSimpleMenuItem
            // 
            SamplesGitGraphSimpleMenuItem.Name = "SamplesGitGraphSimpleMenuItem";
            SamplesGitGraphSimpleMenuItem.Size = new Size(292, 26);
            SamplesGitGraphSimpleMenuItem.Text = "Two commits (Simple)";
            SamplesGitGraphSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesGitGraphCompleteMenuItem
            // 
            SamplesGitGraphCompleteMenuItem.Name = "SamplesGitGraphCompleteMenuItem";
            SamplesGitGraphCompleteMenuItem.Size = new Size(292, 26);
            SamplesGitGraphCompleteMenuItem.Text = "Branch and merge (Complete)";
            SamplesGitGraphCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesJourneyMenu
            // 
            SamplesJourneyMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesJourneySimpleMenuItem, SamplesJourneyCompleteMenuItem });
            SamplesJourneyMenu.Name = "SamplesJourneyMenu";
            SamplesJourneyMenu.Size = new Size(211, 26);
            SamplesJourneyMenu.Text = "Journey";
            // 
            // SamplesJourneySimpleMenuItem
            // 
            SamplesJourneySimpleMenuItem.Name = "SamplesJourneySimpleMenuItem";
            SamplesJourneySimpleMenuItem.Size = new Size(267, 26);
            SamplesJourneySimpleMenuItem.Text = "Sign in (Simple)";
            SamplesJourneySimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesJourneyCompleteMenuItem
            // 
            SamplesJourneyCompleteMenuItem.Name = "SamplesJourneyCompleteMenuItem";
            SamplesJourneyCompleteMenuItem.Size = new Size(267, 26);
            SamplesJourneyCompleteMenuItem.Text = "Arrive and buy (Complete)";
            SamplesJourneyCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesMindmapMenu
            // 
            SamplesMindmapMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesMindmapSimpleMenuItem, SamplesMindmapCompleteMenuItem });
            SamplesMindmapMenu.Name = "SamplesMindmapMenu";
            SamplesMindmapMenu.Size = new Size(211, 26);
            SamplesMindmapMenu.Text = "Mindmap";
            // 
            // SamplesMindmapSimpleMenuItem
            // 
            SamplesMindmapSimpleMenuItem.Name = "SamplesMindmapSimpleMenuItem";
            SamplesMindmapSimpleMenuItem.Size = new Size(262, 26);
            SamplesMindmapSimpleMenuItem.Text = "Shop and orders (Simple)";
            SamplesMindmapSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesMindmapCompleteMenuItem
            // 
            SamplesMindmapCompleteMenuItem.Name = "SamplesMindmapCompleteMenuItem";
            SamplesMindmapCompleteMenuItem.Size = new Size(262, 26);
            SamplesMindmapCompleteMenuItem.Text = "Nested topics (Complete)";
            SamplesMindmapCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesPacketMenu
            // 
            SamplesPacketMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesPacketSimpleMenuItem, SamplesPacketCompleteMenuItem });
            SamplesPacketMenu.Name = "SamplesPacketMenu";
            SamplesPacketMenu.Size = new Size(211, 26);
            SamplesPacketMenu.Text = "Packet";
            // 
            // SamplesPacketSimpleMenuItem
            // 
            SamplesPacketSimpleMenuItem.Name = "SamplesPacketSimpleMenuItem";
            SamplesPacketSimpleMenuItem.Size = new Size(248, 26);
            SamplesPacketSimpleMenuItem.Text = "One field (Simple)";
            SamplesPacketSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesPacketCompleteMenuItem
            // 
            SamplesPacketCompleteMenuItem.Name = "SamplesPacketCompleteMenuItem";
            SamplesPacketCompleteMenuItem.Size = new Size(248, 26);
            SamplesPacketCompleteMenuItem.Text = "Three fields (Complete)";
            SamplesPacketCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesPieMenu
            // 
            SamplesPieMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesPieSimpleMenuItem, SamplesPieCompleteMenuItem });
            SamplesPieMenu.Name = "SamplesPieMenu";
            SamplesPieMenu.Size = new Size(211, 26);
            SamplesPieMenu.Text = "Pie";
            // 
            // SamplesPieSimpleMenuItem
            // 
            SamplesPieSimpleMenuItem.Name = "SamplesPieSimpleMenuItem";
            SamplesPieSimpleMenuItem.Size = new Size(325, 26);
            SamplesPieSimpleMenuItem.Text = "Two slices (Simple)";
            SamplesPieSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesPieCompleteMenuItem
            // 
            SamplesPieCompleteMenuItem.Name = "SamplesPieCompleteMenuItem";
            SamplesPieCompleteMenuItem.Size = new Size(325, 26);
            SamplesPieCompleteMenuItem.Text = "Values beside the slices (Complete)";
            SamplesPieCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesQuadrantChartMenu
            // 
            SamplesQuadrantChartMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesQuadrantChartSimpleMenuItem, SamplesQuadrantChartCompleteMenuItem });
            SamplesQuadrantChartMenu.Name = "SamplesQuadrantChartMenu";
            SamplesQuadrantChartMenu.Size = new Size(211, 26);
            SamplesQuadrantChartMenu.Text = "Quadrant chart";
            // 
            // SamplesQuadrantChartSimpleMenuItem
            // 
            SamplesQuadrantChartSimpleMenuItem.Name = "SamplesQuadrantChartSimpleMenuItem";
            SamplesQuadrantChartSimpleMenuItem.Size = new Size(301, 26);
            SamplesQuadrantChartSimpleMenuItem.Text = "One item (Simple)";
            SamplesQuadrantChartSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesQuadrantChartCompleteMenuItem
            // 
            SamplesQuadrantChartCompleteMenuItem.Name = "SamplesQuadrantChartCompleteMenuItem";
            SamplesQuadrantChartCompleteMenuItem.Size = new Size(301, 26);
            SamplesQuadrantChartCompleteMenuItem.Text = "Axes and quadrants (Complete)";
            SamplesQuadrantChartCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesRadarMenu
            // 
            SamplesRadarMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesRadarSimpleMenuItem, SamplesRadarCompleteMenuItem });
            SamplesRadarMenu.Name = "SamplesRadarMenu";
            SamplesRadarMenu.Size = new Size(211, 26);
            SamplesRadarMenu.Text = "Radar";
            // 
            // SamplesRadarSimpleMenuItem
            // 
            SamplesRadarSimpleMenuItem.Name = "SamplesRadarSimpleMenuItem";
            SamplesRadarSimpleMenuItem.Size = new Size(243, 26);
            SamplesRadarSimpleMenuItem.Text = "Axes (Simple)";
            SamplesRadarSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesRadarCompleteMenuItem
            // 
            SamplesRadarCompleteMenuItem.Name = "SamplesRadarCompleteMenuItem";
            SamplesRadarCompleteMenuItem.Size = new Size(243, 26);
            SamplesRadarCompleteMenuItem.Text = "Two curves (Complete)";
            SamplesRadarCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesSequenceMenu
            // 
            SamplesSequenceMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesSequenceSimpleMenuItem, SamplesSequenceCompleteMenuItem });
            SamplesSequenceMenu.Name = "SamplesSequenceMenu";
            SamplesSequenceMenu.Size = new Size(211, 26);
            SamplesSequenceMenu.Text = "Sequence";
            // 
            // SamplesSequenceSimpleMenuItem
            // 
            SamplesSequenceSimpleMenuItem.Name = "SamplesSequenceSimpleMenuItem";
            SamplesSequenceSimpleMenuItem.Size = new Size(310, 26);
            SamplesSequenceSimpleMenuItem.Text = "Request and response (Simple)";
            SamplesSequenceSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesSequenceCompleteMenuItem
            // 
            SamplesSequenceCompleteMenuItem.Name = "SamplesSequenceCompleteMenuItem";
            SamplesSequenceCompleteMenuItem.Size = new Size(310, 26);
            SamplesSequenceCompleteMenuItem.Text = "Messages and blocks (Complete)";
            SamplesSequenceCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesStateMenu
            // 
            SamplesStateMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesStateSimpleMenuItem, SamplesStateCompleteMenuItem });
            SamplesStateMenu.Name = "SamplesStateMenu";
            SamplesStateMenu.Size = new Size(211, 26);
            SamplesStateMenu.Text = "State";
            // 
            // SamplesStateSimpleMenuItem
            // 
            SamplesStateSimpleMenuItem.Name = "SamplesStateSimpleMenuItem";
            SamplesStateSimpleMenuItem.Size = new Size(307, 26);
            SamplesStateSimpleMenuItem.Text = "Start and end (Simple)";
            SamplesStateSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesStateCompleteMenuItem
            // 
            SamplesStateCompleteMenuItem.Name = "SamplesStateCompleteMenuItem";
            SamplesStateCompleteMenuItem.Size = new Size(307, 26);
            SamplesStateCompleteMenuItem.Text = "Direction and a note (Complete)";
            SamplesStateCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesTimelineMenu
            // 
            SamplesTimelineMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesTimelineSimpleMenuItem, SamplesTimelineCompleteMenuItem });
            SamplesTimelineMenu.Name = "SamplesTimelineMenu";
            SamplesTimelineMenu.Size = new Size(211, 26);
            SamplesTimelineMenu.Text = "Timeline";
            // 
            // SamplesTimelineSimpleMenuItem
            // 
            SamplesTimelineSimpleMenuItem.Name = "SamplesTimelineSimpleMenuItem";
            SamplesTimelineSimpleMenuItem.Size = new Size(255, 26);
            SamplesTimelineSimpleMenuItem.Text = "One year (Simple)";
            SamplesTimelineSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesTimelineCompleteMenuItem
            // 
            SamplesTimelineCompleteMenuItem.Name = "SamplesTimelineCompleteMenuItem";
            SamplesTimelineCompleteMenuItem.Size = new Size(255, 26);
            SamplesTimelineCompleteMenuItem.Text = "Two sections (Complete)";
            SamplesTimelineCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesTreemapMenu
            // 
            SamplesTreemapMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesTreemapSimpleMenuItem, SamplesTreemapCompleteMenuItem });
            SamplesTreemapMenu.Name = "SamplesTreemapMenu";
            SamplesTreemapMenu.Size = new Size(211, 26);
            SamplesTreemapMenu.Text = "Treemap";
            // 
            // SamplesTreemapSimpleMenuItem
            // 
            SamplesTreemapSimpleMenuItem.Name = "SamplesTreemapSimpleMenuItem";
            SamplesTreemapSimpleMenuItem.Size = new Size(237, 26);
            SamplesTreemapSimpleMenuItem.Text = "One area (Simple)";
            SamplesTreemapSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesTreemapCompleteMenuItem
            // 
            SamplesTreemapCompleteMenuItem.Name = "SamplesTreemapCompleteMenuItem";
            SamplesTreemapCompleteMenuItem.Size = new Size(237, 26);
            SamplesTreemapCompleteMenuItem.Text = "Two areas (Complete)";
            SamplesTreemapCompleteMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesXyChartMenu
            // 
            SamplesXyChartMenu.DropDownItems.AddRange(new ToolStripItem[] { SamplesXyChartSimpleMenuItem, SamplesXyChartCompleteMenuItem });
            SamplesXyChartMenu.Name = "SamplesXyChartMenu";
            SamplesXyChartMenu.Size = new Size(211, 26);
            SamplesXyChartMenu.Text = "XY chart";
            // 
            // SamplesXyChartSimpleMenuItem
            // 
            SamplesXyChartSimpleMenuItem.Name = "SamplesXyChartSimpleMenuItem";
            SamplesXyChartSimpleMenuItem.Size = new Size(268, 26);
            SamplesXyChartSimpleMenuItem.Text = "Bars (Simple)";
            SamplesXyChartSimpleMenuItem.Click += SampleMenuItem_Click;
            // 
            // SamplesXyChartCompleteMenuItem
            // 
            SamplesXyChartCompleteMenuItem.Name = "SamplesXyChartCompleteMenuItem";
            SamplesXyChartCompleteMenuItem.Size = new Size(268, 26);
            SamplesXyChartCompleteMenuItem.Text = "Bars and a line (Complete)";
            SamplesXyChartCompleteMenuItem.Click += SampleMenuItem_Click;
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
            ImportExchangeMenuItem.Click += ImportExchangeMenuItem_Click;
            // 
            // ImportArchimateMenuItem
            // 
            ImportArchimateMenuItem.Name = "ImportArchimateMenuItem";
            ImportArchimateMenuItem.Size = new Size(260, 26);
            ImportArchimateMenuItem.Text = "Import ArchiMate file...";
            ImportArchimateMenuItem.Click += ImportArchimateMenuItem_Click;
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
            ExportExchangeMenuItem.Click += ExportExchangeMenuItem_Click;
            // 
            // ExportArchimateMenuItem
            // 
            ExportArchimateMenuItem.Name = "ExportArchimateMenuItem";
            ExportArchimateMenuItem.Size = new Size(260, 26);
            ExportArchimateMenuItem.Text = "Export ArchiMate file...";
            ExportArchimateMenuItem.Click += ExportArchimateMenuItem_Click;
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
            OpenCoArchiFolderMenuItem.Click += OpenCoArchiFolderMenuItem_Click;
            // 
            // SaveCoArchiFolderMenuItem
            // 
            SaveCoArchiFolderMenuItem.Name = "SaveCoArchiFolderMenuItem";
            SaveCoArchiFolderMenuItem.Size = new Size(194, 26);
            SaveCoArchiFolderMenuItem.Text = "Save to folder...";
            SaveCoArchiFolderMenuItem.Click += SaveCoArchiFolderMenuItem_Click;
            // 
            // BodySplit
            // 
            BodySplit.Dock = DockStyle.Fill;
            BodySplit.Location = new Point(0, 30);
            BodySplit.Margin = new Padding(2, 3, 2, 3);
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
            BodySplit.Size = new Size(1317, 919);
            BodySplit.SplitterDistance = 448;
            BodySplit.SplitterWidth = 3;
            BodySplit.TabIndex = 1;
            // 
            // EditorSplit
            // 
            EditorSplit.Dock = DockStyle.Fill;
            EditorSplit.FixedPanel = FixedPanel.Panel1;
            EditorSplit.Location = new Point(0, 92);
            EditorSplit.Margin = new Padding(2, 3, 2, 3);
            EditorSplit.Name = "EditorSplit";
            // 
            // EditorSplit.Panel1
            // 
            EditorSplit.Panel1.Controls.Add(CodeView);
            EditorSplit.Panel1MinSize = 240;
            // 
            // EditorSplit.Panel2
            // 
            EditorSplit.Panel2.Controls.Add(SelectionDetails);
            EditorSplit.Panel2.Controls.Add(Preview);
            EditorSplit.Panel2MinSize = 240;
            EditorSplit.Size = new Size(1317, 827);
            EditorSplit.SplitterDistance = 390;
            EditorSplit.SplitterWidth = 3;
            EditorSplit.TabIndex = 0;
            EditorSplit.SplitterMoved += EditorSplit_SplitterMoved;
            // 
            // CodeView
            // 
            CodeView.Dock = DockStyle.Fill;
            CodeView.Location = new Point(0, 0);
            CodeView.Margin = new Padding(0);
            CodeView.Name = "CodeView";
            CodeView.Size = new Size(390, 827);
            CodeView.TabIndex = 0;
            // 
            // SelectionDetails
            // 
            SelectionDetails.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            SelectionDetails.BackColor = SystemColors.ControlDark;
            SelectionDetails.Location = new Point(11, 523);
            SelectionDetails.Margin = new Padding(0);
            SelectionDetails.MinimumSize = new Size(440, 292);
            SelectionDetails.Name = "SelectionDetails";
            SelectionDetails.Padding = new Padding(1);
            SelectionDetails.Size = new Size(440, 292);
            SelectionDetails.TabIndex = 6;
            SelectionDetails.Visible = true;
            SelectionDetails.ChangeTypeClicked += SelectionDetails_ChangeTypeClicked;
            // 
            // Preview
            // 
            Preview.Dock = DockStyle.Fill;
            Preview.Location = new Point(0, 0);
            Preview.Margin = new Padding(2, 3, 2, 3);
            Preview.Name = "Preview";
            Preview.Size = new Size(924, 827);
            Preview.TabIndex = 0;
            Preview.PictureMouseMove += Preview_PictureMouseMove;
            Preview.PictureMouseLeave += Preview_PictureMouseLeave;
            Preview.EscapePressed += Preview_EscapePressed;
            Preview.LineStepRequested += Preview_LineStepRequested;
            Preview.ZoomChanged += Preview_ZoomChanged;
            Preview.RefreshRequested += Preview_RefreshRequested;
            Preview.SelectedItemsChanged += Preview_SelectedItemsChanged;
            // 
            // ElementToolbar
            // 
            ElementToolbar.Dock = DockStyle.Top;
            ElementToolbar.Location = new Point(0, 0);
            ElementToolbar.Margin = new Padding(3, 4, 3, 4);
            ElementToolbar.Name = "ElementToolbar";
            ElementToolbar.Size = new Size(1317, 92);
            ElementToolbar.TabIndex = 2;
            ElementToolbar.InsertRequested += ElementToolbar_InsertRequested;
            ElementToolbar.PromptClosed += ElementToolbar_PromptClosed;
            // 
            // DiagnosticsList
            // 
            DiagnosticsList.AccessibleName = "Validation diagnostics";
            DiagnosticsList.Dock = DockStyle.Fill;
            DiagnosticsList.Font = new Font("Consolas", 0.0111414632F);
            DiagnosticsList.HorizontalScrollbar = true;
            DiagnosticsList.Location = new Point(0, 0);
            DiagnosticsList.Margin = new Padding(2, 3, 2, 3);
            DiagnosticsList.Name = "DiagnosticsList";
            DiagnosticsList.Size = new Size(171, 61);
            DiagnosticsList.TabIndex = 0;
            DiagnosticsList.DoubleClick += DiagnosticsList_DoubleClick;
            // 
            // StatusBar
            // 
            StatusBar.ImageScalingSize = new Size(20, 20);
            StatusBar.Items.AddRange(new ToolStripItem[] { StatusLabel, SignatureLabel, TimingLabel, CoArchiDirtyLabel });
            StatusBar.Location = new Point(0, 949);
            StatusBar.Name = "StatusBar";
            StatusBar.Padding = new Padding(1, 0, 11, 0);
            StatusBar.Size = new Size(1317, 26);
            StatusBar.TabIndex = 2;
            // 
            // StatusLabel
            // 
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(1305, 20);
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
            RenderTimer.Tick += RenderTimer_Tick;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1317, 975);
            Controls.Add(BodySplit);
            Controls.Add(StatusBar);
            Controls.Add(MainMenu);
            MainMenuStrip = MainMenu;
            Margin = new Padding(2, 3, 2, 3);
            MinimumSize = new Size(724, 458);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LiveEditor";
            Deactivate += MainForm_Deactivate;
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            Shown += MainForm_Shown;
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
        private ToolStripMenuItem SamplesArchitectureMenu;
        private ToolStripMenuItem SamplesArchitectureSimpleMenuItem;
        private ToolStripMenuItem SamplesArchitectureCompleteMenuItem;
        private ToolStripMenuItem SamplesBlockMenu;
        private ToolStripMenuItem SamplesBlockSimpleMenuItem;
        private ToolStripMenuItem SamplesBlockCompleteMenuItem;
        private ToolStripMenuItem SamplesC4Menu;
        private ToolStripMenuItem SamplesC4SimpleMenuItem;
        private ToolStripMenuItem SamplesC4CompleteMenuItem;
        private ToolStripMenuItem SamplesClassMenu;
        private ToolStripMenuItem SamplesClassSimpleMenuItem;
        private ToolStripMenuItem SamplesClassCompleteMenuItem;
        private ToolStripMenuItem SamplesEnterpriseMenu;
        private ToolStripMenuItem SamplesEnterpriseSimpleMenuItem;
        private ToolStripMenuItem SamplesEnterpriseCompleteMenuItem;
        private ToolStripMenuItem SamplesEntityRelationshipMenu;
        private ToolStripMenuItem SamplesEntityRelationshipSimpleMenuItem;
        private ToolStripMenuItem SamplesEntityRelationshipCompleteMenuItem;
        private ToolStripMenuItem SamplesFlowchartMenu;
        private ToolStripMenuItem SamplesFlowchartSimpleMenuItem;
        private ToolStripMenuItem SamplesFlowchartCompleteMenuItem;
        private ToolStripMenuItem SamplesGanttMenu;
        private ToolStripMenuItem SamplesGanttSimpleMenuItem;
        private ToolStripMenuItem SamplesGanttCompleteMenuItem;
        private ToolStripMenuItem SamplesGitGraphMenu;
        private ToolStripMenuItem SamplesGitGraphSimpleMenuItem;
        private ToolStripMenuItem SamplesGitGraphCompleteMenuItem;
        private ToolStripMenuItem SamplesJourneyMenu;
        private ToolStripMenuItem SamplesJourneySimpleMenuItem;
        private ToolStripMenuItem SamplesJourneyCompleteMenuItem;
        private ToolStripMenuItem SamplesMindmapMenu;
        private ToolStripMenuItem SamplesMindmapSimpleMenuItem;
        private ToolStripMenuItem SamplesMindmapCompleteMenuItem;
        private ToolStripMenuItem SamplesPacketMenu;
        private ToolStripMenuItem SamplesPacketSimpleMenuItem;
        private ToolStripMenuItem SamplesPacketCompleteMenuItem;
        private ToolStripMenuItem SamplesPieMenu;
        private ToolStripMenuItem SamplesPieSimpleMenuItem;
        private ToolStripMenuItem SamplesPieCompleteMenuItem;
        private ToolStripMenuItem SamplesQuadrantChartMenu;
        private ToolStripMenuItem SamplesQuadrantChartSimpleMenuItem;
        private ToolStripMenuItem SamplesQuadrantChartCompleteMenuItem;
        private ToolStripMenuItem SamplesRadarMenu;
        private ToolStripMenuItem SamplesRadarSimpleMenuItem;
        private ToolStripMenuItem SamplesRadarCompleteMenuItem;
        private ToolStripMenuItem SamplesSequenceMenu;
        private ToolStripMenuItem SamplesSequenceSimpleMenuItem;
        private ToolStripMenuItem SamplesSequenceCompleteMenuItem;
        private ToolStripMenuItem SamplesStateMenu;
        private ToolStripMenuItem SamplesStateSimpleMenuItem;
        private ToolStripMenuItem SamplesStateCompleteMenuItem;
        private ToolStripMenuItem SamplesTimelineMenu;
        private ToolStripMenuItem SamplesTimelineSimpleMenuItem;
        private ToolStripMenuItem SamplesTimelineCompleteMenuItem;
        private ToolStripMenuItem SamplesTreemapMenu;
        private ToolStripMenuItem SamplesTreemapSimpleMenuItem;
        private ToolStripMenuItem SamplesTreemapCompleteMenuItem;
        private ToolStripMenuItem SamplesXyChartMenu;
        private ToolStripMenuItem SamplesXyChartSimpleMenuItem;
        private ToolStripMenuItem SamplesXyChartCompleteMenuItem;
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
        private PreviewCanvas Preview;
        private ListBox DiagnosticsList;
        private StatusStrip StatusBar;
        private ToolStripStatusLabel StatusLabel;
        private ToolStripStatusLabel TimingLabel;
        private ToolStripStatusLabel CoArchiDirtyLabel;
        private ToolStripStatusLabel SignatureLabel;
        private System.Windows.Forms.Timer RenderTimer;
        private ElementPalette ElementToolbar;
        private SelectionPanel SelectionDetails;
        private System.ComponentModel.IContainer components;
    }
}
