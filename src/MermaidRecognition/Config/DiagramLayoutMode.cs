namespace MermaidRecognition.Config;

/// <summary>Controls placement only; routing rules apply in both modes.</summary>
public enum DiagramLayoutMode
{
    Grid,
    Open
}

/// <summary>Resolves the shared layout setting for every supported diagram kind.</summary>
public static class DiagramLayoutSettings
{
    public static bool TryParse(string? Value, out DiagramLayoutMode Mode)
    {
        Mode = DiagramLayoutMode.Grid;
        if (string.Equals(Value, "grid", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(Value, "open", StringComparison.OrdinalIgnoreCase)) { Mode = DiagramLayoutMode.Open; return true; }
        return false;
    }

    public static DiagramLayoutMode Resolve(IReadOnlyDictionary<string, string> Configuration, Action<string>? Warn = null,
        DiagramLayoutMode Default = DiagramLayoutMode.Grid)
    {
        if (!Configuration.TryGetValue("layout", out string? Value)) return Default;
        if (TryParse(Value, out var Mode)) return Mode;
        Warn?.Invoke($"Unknown layout value \"{Value}\"; using grid.");
        return DiagramLayoutMode.Grid;
    }
}
