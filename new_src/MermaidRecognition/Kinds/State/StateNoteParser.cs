using System.Text.RegularExpressions;

namespace MermaidRecognition.State;

/// <summary>
/// Parses Mermaid state diagram note directives.
/// </summary>
public static class StateNoteParser
{
    private static readonly Regex NoteSideRegex = new(
        @"^\s*note\s+(left|right)\s+of\s+(\S+)\s*:\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a classified state note line into the state database.
    /// </summary>
    public static bool TryParse(string Line, StateDb Database)
    {
        Match SideMatch = NoteSideRegex.Match(Line);
        if (!SideMatch.Success)
        {
            return false;
        }

        string Placement = SideMatch.Groups[1].Value.ToLowerInvariant();
        string AnchorId = SideMatch.Groups[2].Value;
        string Text = SideMatch.Groups[3].Value.Trim();
        Database.GetOrCreateState(AnchorId);
        Database.AddNote(Placement, AnchorId, Text);
        return true;
    }
}
