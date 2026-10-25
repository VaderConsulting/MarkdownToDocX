using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Session settings, the settings dialog, and the way the editor applies them.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class SettingsFunctionalityTests
{
    [Test]
    public void ANewSessionUsesTheDiagramDefaults()
    {
        SessionSettings Settings = new SessionSettings();

        Assert.That(Settings.AutoIndent, Is.True);
        Assert.That(Settings.InsertSpaces, Is.True);
        Assert.That(Settings.IndentSize, Is.EqualTo(2));
        Assert.That(Settings.EditorWordWrap, Is.False);
        Assert.That(Settings.EditorFontSize, Is.EqualTo(11F));
        Assert.That(Settings.RenderDelayMilliseconds, Is.EqualTo(350));
        Assert.That(Settings.HighlightDelayMilliseconds, Is.EqualTo(250));
        Assert.That(Settings.VerticalSplit, Is.True);
        Assert.That(Settings.WideToolbar, Is.True);
        Assert.That(Settings.EditorOnLeft, Is.True);
        Assert.That(Settings.PreviewFitToWindow, Is.True);
        Assert.That(Settings.PreviewZoom, Is.EqualTo(1.0));
        Assert.That(Settings.SelectionColour, Is.EqualTo(SessionSettings.DEFAULT_SELECTION_COLOUR));
        Assert.That(Settings.DarkTheme, Is.False);
        Assert.That(Settings.DiagnosticsVisible, Is.False);
        Assert.That(Settings.MinimapVisible, Is.False);
        Assert.That(Settings.LegacyPreview, Is.False);
        Assert.That(Settings.RecentFiles, Is.Empty);
    }

    [Test]
    public void ColoursRoundTripAndMalformedTextUsesTheDiagramLine()
    {
        Color Orange = Color.FromArgb(0x80, 0xFF, 0x80, 0x00);
        Assert.That(SessionSettings.FormatColour(Orange), Is.EqualTo("#FF8000"));
        Assert.That(SessionSettings.ParseColour(" #ff8000 ").ToArgb(), Is.EqualTo(Color.FromArgb(0xFF, 0x80, 0x00).ToArgb()));
        Assert.That(SessionSettings.ParseColour(null).ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
        Assert.That(SessionSettings.ParseColour(string.Empty).ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
        Assert.That(SessionSettings.ParseColour("FF8000").ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
        Assert.That(SessionSettings.ParseColour("#GG0000").ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
        Assert.That(SessionSettings.ParseColour("#11223").ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
    }

    [Test]
    public void JsonKeepsEverySettingAndIgnoresABlankDocument()
    {
        SessionSettings Settings = new SessionSettings
        {
            DarkTheme = true,
            DiagnosticsVisible = true,
            MinimapVisible = true,
            AutoIndent = false,
            InsertSpaces = false,
            IndentSize = 4,
            EditorWordWrap = true,
            EditorFontSize = 14.5F,
            RenderDelayMilliseconds = 800,
            HighlightDelayMilliseconds = 120,
            VerticalSplit = false,
            WideToolbar = false,
            EditorOnLeft = false,
            LegacyPreview = true,
            PreviewFitToWindow = false,
            PreviewZoom = 2.5,
            SelectionColour = "#112233",
            LastFilePath = "C:\\notes\\shop.md",
            LastCoArchiFolder = "C:\\models\\shop",
            CaretIndex = 42
        };
        Settings.RecentFiles.Add("C:\\notes\\shop.md");
        Settings.RecentFiles.Add("C:\\notes\\older.md");

        SessionSettings Loaded = SessionSettings.FromJson(Settings.ToJson());

        Assert.That(Loaded.DarkTheme, Is.True);
        Assert.That(Loaded.DiagnosticsVisible, Is.True);
        Assert.That(Loaded.MinimapVisible, Is.True);
        Assert.That(Loaded.AutoIndent, Is.False);
        Assert.That(Loaded.InsertSpaces, Is.False);
        Assert.That(Loaded.IndentSize, Is.EqualTo(4));
        Assert.That(Loaded.EditorWordWrap, Is.True);
        Assert.That(Loaded.EditorFontSize, Is.EqualTo(14.5F));
        Assert.That(Loaded.RenderDelayMilliseconds, Is.EqualTo(800));
        Assert.That(Loaded.HighlightDelayMilliseconds, Is.EqualTo(120));
        Assert.That(Loaded.VerticalSplit, Is.False);
        Assert.That(Loaded.WideToolbar, Is.False);
        Assert.That(Loaded.EditorOnLeft, Is.False);
        Assert.That(Loaded.LegacyPreview, Is.True);
        Assert.That(Loaded.PreviewFitToWindow, Is.False);
        Assert.That(Loaded.PreviewZoom, Is.EqualTo(2.5));
        Assert.That(Loaded.SelectionColour, Is.EqualTo("#112233"));
        Assert.That(Loaded.LastFilePath, Is.EqualTo("C:\\notes\\shop.md"));
        Assert.That(Loaded.LastCoArchiFolder, Is.EqualTo("C:\\models\\shop"));
        Assert.That(Loaded.CaretIndex, Is.EqualTo(42));
        Assert.That(Loaded.RecentFiles, Is.EqualTo(new string[] { "C:\\notes\\shop.md", "C:\\notes\\older.md" }));

        SessionSettings Blank = SessionSettings.FromJson("{}");
        Assert.That(Blank.AutoIndent, Is.True);
        Assert.That(Blank.EditorFontSize, Is.EqualTo(11F));
        Assert.That(Blank.SelectionColour, Is.EqualTo(SessionSettings.DEFAULT_SELECTION_COLOUR));
        Assert.Throws<JsonException>(() => SessionSettings.FromJson("{"));
    }

    [Test]
    public void RememberFileKeepsTheNewestTwelvePaths()
    {
        SessionSettings Settings = new SessionSettings();
        Settings.RememberFile("  ");
        Settings.RememberFile(null!);
        Assert.That(Settings.RecentFiles, Is.Empty);

        string Folder = Path.Combine(Path.GetTempPath(), "liveeditor-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        try
        {
            string First = Path.Combine(Folder, "A.mmd");
            Settings.RememberFile(First);
            Settings.RememberFile(Path.Combine(Folder, "b.mmd"));
            Settings.RememberFile(First.ToLowerInvariant());

            Assert.That(Settings.RecentFiles.Count, Is.EqualTo(2));
            Assert.That(Settings.RecentFiles[0], Is.EqualTo(Path.GetFullPath(First)).IgnoreCase);
            Assert.That(Settings.LastFilePath, Is.EqualTo(Settings.RecentFiles[0]));

            for (int Index = 0; Index < 13; Index++)
            {
                Settings.RememberFile(Path.Combine(Folder, "file-" + Index.ToString() + ".mmd"));
            }

            Assert.That(Settings.RecentFiles.Count, Is.EqualTo(12));
            Assert.That(Settings.RecentFiles[0], Does.EndWith("file-12.mmd"));
            Assert.That(Settings.RecentFiles, Has.None.EndsWith("file-0.mmd"));
            Assert.That(Settings.LastFilePath, Is.EqualTo(Settings.RecentFiles[0]));
        }
        finally
        {
            Directory.Delete(Folder, true);
        }
    }

    [Test]
    public void TheDialogLoadsAndStoresEveryEditableSetting()
    {
        SessionSettings Settings = new SessionSettings
        {
            AutoIndent = false,
            InsertSpaces = false,
            EditorWordWrap = true,
            IndentSize = 6,
            EditorFontSize = 14.5F,
            RenderDelayMilliseconds = 800,
            HighlightDelayMilliseconds = 120,
            WideToolbar = false,
            EditorOnLeft = false,
            SelectionColour = "#00A0C0",
            DarkTheme = true,
            LastFilePath = "kept.md",
            PreviewZoom = 3
        };
        Settings.RecentFiles.Add("kept.md");

        using SettingsForm Dialog = new SettingsForm(Settings);
        Assert.That(Check(Dialog, "AutoIndentCheckBox").Checked, Is.False);
        Assert.That(Check(Dialog, "InsertSpacesCheckBox").Checked, Is.False);
        Assert.That(Check(Dialog, "WordWrapCheckBox").Checked, Is.True);
        Assert.That(Number(Dialog, "IndentSizeNumeric").Value, Is.EqualTo(6));
        Assert.That(Number(Dialog, "FontSizeNumeric").Value, Is.EqualTo(14.5M));
        Assert.That(Number(Dialog, "RenderDelayNumeric").Value, Is.EqualTo(800));
        Assert.That(Number(Dialog, "HighlightDelayNumeric").Value, Is.EqualTo(120));
        Assert.That(Radio(Dialog, "WideToolbarRadio").Checked, Is.False);
        Assert.That(Radio(Dialog, "NarrowToolbarRadio").Checked, Is.True);
        Assert.That(Radio(Dialog, "EditorLeftRadio").Checked, Is.False);
        Assert.That(Radio(Dialog, "EditorRightRadio").Checked, Is.True);
        Assert.That(Dialog.SelectionColour.ToArgb(), Is.EqualTo(Color.FromArgb(0x00, 0xA0, 0xC0).ToArgb()));

        Check(Dialog, "AutoIndentCheckBox").Checked = true;
        Check(Dialog, "InsertSpacesCheckBox").Checked = true;
        Check(Dialog, "WordWrapCheckBox").Checked = false;
        Number(Dialog, "IndentSizeNumeric").Value = 3;
        Number(Dialog, "FontSizeNumeric").Value = 18;
        Number(Dialog, "RenderDelayNumeric").Value = 500;
        Number(Dialog, "HighlightDelayNumeric").Value = 90;
        Radio(Dialog, "WideToolbarRadio").Checked = true;
        Radio(Dialog, "EditorLeftRadio").Checked = true;
        Dialog.SelectionColour = Color.FromArgb(0x40, 0x10, 0x20, 0x30);

        SessionSettings Stored = new SessionSettings();
        Dialog.ApplyTo(Stored);

        Assert.That(Stored.AutoIndent, Is.True);
        Assert.That(Stored.InsertSpaces, Is.True);
        Assert.That(Stored.EditorWordWrap, Is.False);
        Assert.That(Stored.IndentSize, Is.EqualTo(3));
        Assert.That(Stored.EditorFontSize, Is.EqualTo(18F));
        Assert.That(Stored.RenderDelayMilliseconds, Is.EqualTo(500));
        Assert.That(Stored.HighlightDelayMilliseconds, Is.EqualTo(90));
        Assert.That(Stored.WideToolbar, Is.True);
        Assert.That(Stored.EditorOnLeft, Is.True);
        Assert.That(Stored.SelectionColour, Is.EqualTo("#102030"));
        Assert.That(Settings.DarkTheme, Is.True);
        Assert.That(Settings.LastFilePath, Is.EqualTo("kept.md"));
        Assert.That(Settings.PreviewZoom, Is.EqualTo(3));
        Assert.That(Settings.RecentFiles, Is.EqualTo(new string[] { "kept.md" }));
    }

    [Test]
    public void TheDialogClampsNumbersAndRejectsAMissingSession()
    {
        SessionSettings Settings = new SessionSettings
        {
            IndentSize = 0,
            EditorFontSize = 1F,
            RenderDelayMilliseconds = 1,
            HighlightDelayMilliseconds = 9000
        };
        using SettingsForm Dialog = new SettingsForm(Settings);

        Assert.That(Number(Dialog, "IndentSizeNumeric").Value, Is.EqualTo(1));
        Assert.That(Number(Dialog, "FontSizeNumeric").Value, Is.EqualTo(8));
        Assert.That(Number(Dialog, "RenderDelayNumeric").Value, Is.EqualTo(50));
        Assert.That(Number(Dialog, "HighlightDelayNumeric").Value, Is.EqualTo(5000));

        Settings.IndentSize = 99;
        Settings.EditorFontSize = 80F;
        using SettingsForm High = new SettingsForm(Settings);
        Assert.That(Number(High, "IndentSizeNumeric").Value, Is.EqualTo(16));
        Assert.That(Number(High, "FontSizeNumeric").Value, Is.EqualTo(32));

        Assert.Throws<ArgumentNullException>(() => new SettingsForm(null!));
        Assert.Throws<ArgumentNullException>(() => Dialog.ApplyTo(null!));
    }

    [Test]
    public void TheEditorAppliesTheSessionAndTheViewCommands()
    {
        string Folder = Path.Combine(Path.GetTempPath(), "liveeditor-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        string SourcePath = Path.Combine(Folder, "diagram.mmd");
        string MissingPath = Path.Combine(Folder, "missing.mmd");
        File.WriteAllText(SourcePath, "enterprise\napplication-component kept \"Kept\"\n");
        SessionSettings Settings = new SessionSettings
        {
            DarkTheme = true,
            DiagnosticsVisible = false,
            MinimapVisible = false,
            AutoIndent = false,
            InsertSpaces = false,
            IndentSize = 7,
            EditorWordWrap = true,
            EditorFontSize = 14.5F,
            RenderDelayMilliseconds = 800,
            HighlightDelayMilliseconds = 180,
            VerticalSplit = false,
            WideToolbar = false,
            EditorOnLeft = true,
            LegacyPreview = true,
            PreviewFitToWindow = false,
            PreviewZoom = 2.5,
            SelectionColour = "#FF8000",
            LastFilePath = SourcePath,
            CaretIndex = 4
        };
        Settings.RecentFiles.Add(SourcePath);
        Settings.RecentFiles.Add(MissingPath);

        using MainForm Form = new MainForm(Settings);
        Form.ShowInTaskbar = false;
        Form.StartPosition = FormStartPosition.Manual;
        Form.Location = new Point(-20000, -20000);
        try
        {
            Form.Show();
            Application.DoEvents();

            RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
            PreviewCanvas Preview = Require<PreviewCanvas>(Form, "Preview");
            SplitContainer EditorSplit = Require<SplitContainer>(Form, "EditorSplit");
            SplitContainer BodySplit = Require<SplitContainer>(Form, "BodySplit");
            Control Toolbar = Require<Control>(Form, "ElementToolbar");
            MenuStrip Menu = Require<MenuStrip>(Form, "MainMenu");

            Assert.That(Editor.Text, Does.Contain("application-component kept"));
            Assert.That(Editor.SelectionStart, Is.EqualTo(4));
            Assert.That(Editor.WordWrap, Is.True);
            Assert.That(Editor.ScrollBars, Is.EqualTo(RichTextBoxScrollBars.Vertical));
            Assert.That(Editor.Font.Size, Is.EqualTo(14.5F).Within(0.01F));
            Assert.That(Editor.BackColor.ToArgb(), Is.EqualTo(Color.FromArgb(30, 30, 30).ToArgb()));
            CodeEditor Code = Require<CodeEditor>(Form, "CodeView");
            Assert.That(Code.AutoIndent, Is.False);
            Assert.That(Code.InsertSpaces, Is.False);
            Assert.That(Code.IndentSize, Is.EqualTo(7));
            Assert.That(Code.HighlightDelayMilliseconds, Is.EqualTo(180));
            Assert.That(Code.MinimapVisible, Is.False);
            Assert.That(RenderInterval(Form), Is.EqualTo(800));
            Assert.That(Preview.SelectionColour.ToArgb(), Is.EqualTo(Color.FromArgb(0xFF, 0x80, 0x00).ToArgb()));
            Assert.That(Preview.Zoom, Is.EqualTo(2.5));
            Assert.That(Preview.FitToWindow, Is.False);
            Assert.That(EditorSplit.Orientation, Is.EqualTo(Orientation.Horizontal));
            Assert.That(Code.Parent, Is.SameAs(EditorSplit.Panel1));
            Assert.That(Preview.Parent, Is.SameAs(EditorSplit.Panel2));
            Assert.That(Toolbar.Parent, Is.SameAs(EditorSplit.Panel2));
            Assert.That(BodySplit.Panel2Collapsed, Is.True);
            Assert.That(Command(Menu, "ToggleThemeMenuItem").Checked, Is.True);
            Assert.That(Command(Menu, "ToggleMinimapMenuItem").Checked, Is.False);
            Assert.That(Command(Menu, "ToggleDiagnosticsMenuItem").Checked, Is.False);
            Assert.That(Command(Menu, "ToggleLegacyPreviewMenuItem").Checked, Is.True);
            ToolStripMenuItem Recent = Command(Menu, "RecentFilesMenuItem");
            Assert.That(Recent.DropDownItems[0].Text, Is.EqualTo(SourcePath));
            Assert.That(Recent.DropDownItems[0].Enabled, Is.True);
            Assert.That(Recent.DropDownItems[1].Text, Is.EqualTo(MissingPath));
            Assert.That(Recent.DropDownItems[1].Enabled, Is.False);

            Command(Menu, "ToggleThemeMenuItem").PerformClick();
            Assert.That(Editor.BackColor.ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            Assert.That(Command(Menu, "ToggleThemeMenuItem").Checked, Is.False);

            Command(Menu, "ToggleSplitOrientationMenuItem").PerformClick();
            Assert.That(EditorSplit.Orientation, Is.EqualTo(Orientation.Vertical));

            Command(Menu, "ToggleDiagnosticsMenuItem").PerformClick();
            Assert.That(BodySplit.Panel2Collapsed, Is.False);
            Assert.That(Command(Menu, "ToggleDiagnosticsMenuItem").Checked, Is.True);

            Command(Menu, "ToggleMinimapMenuItem").PerformClick();
            Assert.That(Code.MinimapVisible, Is.True);
            Assert.That(Command(Menu, "ToggleMinimapMenuItem").Checked, Is.True);

            Command(Menu, "ToggleLegacyPreviewMenuItem").PerformClick();
            Assert.That(Command(Menu, "ToggleLegacyPreviewMenuItem").Checked, Is.False);

            Command(Menu, "ZoomInMenuItem").PerformClick();
            Assert.That(Editor.Font.Size, Is.EqualTo(15.5F).Within(0.01F));
            Assert.That(Preview.Zoom, Is.EqualTo(2.5));
            Command(Menu, "ZoomResetMenuItem").PerformClick();
            Assert.That(Editor.Font.Size, Is.EqualTo(14.5F).Within(0.01F));

            Command(Menu, "FitPreviewMenuItem").PerformClick();
            Assert.That(Preview.FitToWindow, Is.False);
        }
        finally
        {
            Form.Close();
            Directory.Delete(Folder, true);
        }
    }

    private static CheckBox Check(Control Root, string Name)
    {
        return Require<CheckBox>(Root, Name);
    }

    private static RadioButton Radio(Control Root, string Name)
    {
        return Require<RadioButton>(Root, Name);
    }

    private static NumericUpDown Number(Control Root, string Name)
    {
        return Require<NumericUpDown>(Root, Name);
    }

    private static T Require<T>(Control Root, string Name) where T : Control
    {
        Control[] Found = Root.Controls.Find(Name, true);
        Assert.That(Found, Has.Length.EqualTo(1), Name);
        return (T)Found[0];
    }

    private static ToolStripMenuItem Command(ToolStrip Menu, string Name)
    {
        ToolStripItem[] Found = Menu.Items.Find(Name, true);
        Assert.That(Found, Has.Length.EqualTo(1), Name);
        return (ToolStripMenuItem)Found[0];
    }

    private static int RenderInterval(MainForm Form)
    {
        FieldInfo? Field = typeof(MainForm).GetField("RenderTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(Field, Is.Not.Null);
        System.Windows.Forms.Timer Clock = (System.Windows.Forms.Timer)Field!.GetValue(Form)!;
        return Clock.Interval;
    }
}
