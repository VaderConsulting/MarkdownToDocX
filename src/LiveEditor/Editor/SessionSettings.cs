using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace LiveEditor.Editor;

/// <summary>
/// Persists LiveEditor session preferences and recent files.
/// </summary>
internal sealed class SessionSettings
{
    private const int MAX_RECENT = 12;

    /// <summary>Default colour of the preview selection: the diagram's line colour.</summary>
    public const string DEFAULT_SELECTION_COLOUR = "#1E1E1E";

    public bool DarkTheme { get; set; }

    public bool DiagnosticsVisible { get; set; }

    public bool MinimapVisible { get; set; }

    public bool AutoIndent { get; set; } = true;

    public bool InsertSpaces { get; set; } = true;

    public int IndentSize { get; set; } = 2;

    public bool EditorWordWrap { get; set; }

    public float EditorFontSize { get; set; } = 11F;

    public int RenderDelayMilliseconds { get; set; } = 350;

    public int HighlightDelayMilliseconds { get; set; } = 250;

    public bool VerticalSplit { get; set; } = true;

    public bool WideToolbar { get; set; } = true;

    public bool EditorOnLeft { get; set; } = true;

    public bool LegacyPreview { get; set; }

    public bool PreviewFitToWindow { get; set; } = true;

    public double PreviewZoom { get; set; } = 1.0;

    /// <summary>Gets or sets the colour of the preview selection outline and connection points, as #RRGGBB.</summary>
    public string SelectionColour { get; set; } = DEFAULT_SELECTION_COLOUR;

    public string? LastFilePath { get; set; }

    public string? LastCoArchiFolder { get; set; }

    public int CaretIndex { get; set; }

    public List<string> RecentFiles { get; set; } = new List<string>();

    public static string StorePath
    {
        get
        {
            string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkdownToDocX", "LiveEditor");
            Directory.CreateDirectory(Folder);
            return Path.Combine(Folder, "session.json");
        }
    }

    public static SessionSettings Load()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return new SessionSettings();
            }

            return FromJson(File.ReadAllText(StorePath));
        }
        catch
        {
            return new SessionSettings();
        }
    }

    public void RememberFile(string PathValue)
    {
        if (string.IsNullOrWhiteSpace(PathValue))
        {
            return;
        }

        string Full = Path.GetFullPath(PathValue);
        RecentFiles.RemoveAll(Item => string.Equals(Item, Full, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, Full);
        while (RecentFiles.Count > MAX_RECENT)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }

        LastFilePath = Full;
    }

    public void Save()
    {
        File.WriteAllText(StorePath, ToJson());
    }

    /// <summary>
    /// Reads settings from the stored JSON.
    /// </summary>
    internal static SessionSettings FromJson(string Json)
    {
        SessionSettings? Loaded = JsonSerializer.Deserialize<SessionSettings>(Json);
        return Loaded ?? new SessionSettings();
    }

    /// <summary>
    /// Writes the settings as the stored JSON.
    /// </summary>
    internal string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads a #RRGGBB colour, falling back to the default selection colour when it is missing or malformed.
    /// </summary>
    public static Color ParseColour(string? Value)
    {
        string Text = (Value ?? string.Empty).Trim();
        if (Text.Length == 7 && Text[0] == '#'
            && int.TryParse(Text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int Rgb))
        {
            return Color.FromArgb((Rgb >> 16) & 0xFF, (Rgb >> 8) & 0xFF, Rgb & 0xFF);
        }

        return ParseColour(DEFAULT_SELECTION_COLOUR);
    }

    /// <summary>
    /// Writes a colour as #RRGGBB.
    /// </summary>
    public static string FormatColour(Color Value)
    {
        return "#" + Value.R.ToString("X2", CultureInfo.InvariantCulture)
            + Value.G.ToString("X2", CultureInfo.InvariantCulture)
            + Value.B.ToString("X2", CultureInfo.InvariantCulture);
    }
}
