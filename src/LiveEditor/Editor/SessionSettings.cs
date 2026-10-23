using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LiveEditor.Editor;

/// <summary>
/// Persists LiveEditor session preferences and recent files.
/// </summary>
internal sealed class SessionSettings
{
    private const int MAX_RECENT = 12;

    public bool DarkTheme { get; set; }

    public bool DiagnosticsVisible { get; set; }

    public bool MinimapVisible { get; set; }

    public bool VerticalSplit { get; set; } = true;

    public bool LegacyPreview { get; set; }

    public bool PreviewFitToWindow { get; set; } = true;

    public double PreviewZoom { get; set; } = 1.0;

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

            string Json = File.ReadAllText(StorePath);
            SessionSettings? Loaded = JsonSerializer.Deserialize<SessionSettings>(Json);
            return Loaded ?? new SessionSettings();
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
        string Json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StorePath, Json);
    }
}
