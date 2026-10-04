using System.Text.RegularExpressions;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parses Mermaid sequence diagram note directives (left/right/over placement).
/// </summary>
public static class SequenceNoteParser
{
    private static readonly Regex NoteSideRegex = new(
        @"^\s*[Nn]ote\s+(left|right)\s+of\s+(\S+)\s*:\s*(.+)$",
        RegexOptions.Compiled);

    private static readonly Regex NoteOverRegex = new(
        @"^\s*[Nn]ote\s+over\s+([^:]+)\s*:\s*(.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a classified sequence note line into the sequence database.
    /// </summary>
    public static bool TryParse(string Line, int LineNumber, SequenceDb Database)
    {
        Match SideMatch = NoteSideRegex.Match(Line);
        if (SideMatch.Success)
        {
            string Placement = SideMatch.Groups[1].Value.ToLowerInvariant();
            string AnchorId = SideMatch.Groups[2].Value;
            string Text = SideMatch.Groups[3].Value.Trim();
            Database.EnsureImplicitParticipant(AnchorId);
            Database.AddNote(Placement, AnchorId, Text);
            return true;
        }

        Match OverMatch = NoteOverRegex.Match(Line);
        if (OverMatch.Success)
        {
            string AnchorIds = OverMatch.Groups[1].Value.Trim();
            string Text = OverMatch.Groups[2].Value.Trim();
            foreach (string AnchorId in AnchorIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                Database.EnsureImplicitParticipant(AnchorId);
            }

            Database.AddNote("over", AnchorIds, Text);
            return true;
        }

        return false;
    }
}
