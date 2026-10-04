using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Extracts state identifiers, descriptions, and pseudo-state types from state declaration lines.
/// </summary>
public static class StateDeclarationParser
{
    private static readonly Regex CompositeInlineRegex = new(
        @"^\s*state\s+(\S+)\s*\{\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForkJoinChoiceRegex = new(
        @"^\s*state\s+(\S+)\s+<<(\w+)>>\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DescriptionAsRegex = new(
        @"^\s*state\s+""([^""]*)""\s+as\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SimpleStateRegex = new(
        @"^\s*state\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a state declaration line.
    /// </summary>
    public static bool TryParse(string Line, out StateDeclarationParseResult Result)
    {
        Result = new StateDeclarationParseResult();

        Match CompositeMatch = CompositeInlineRegex.Match(Line);
        if (CompositeMatch.Success)
        {
            Result.StateId = CompositeMatch.Groups[1].Value;
            Result.OpensComposite = true;
            return true;
        }

        Match TypeMatch = ForkJoinChoiceRegex.Match(Line);
        if (TypeMatch.Success)
        {
            Result.StateId = TypeMatch.Groups[1].Value;
            Result.StateType = TypeMatch.Groups[2].Value.ToLowerInvariant();
            return true;
        }

        Match DescriptionMatch = DescriptionAsRegex.Match(Line);
        if (DescriptionMatch.Success)
        {
            Result.Label = DescriptionMatch.Groups[1].Value;
            Result.StateId = DescriptionMatch.Groups[2].Value;
            return true;
        }

        Match SimpleMatch = SimpleStateRegex.Match(Line);
        if (SimpleMatch.Success)
        {
            Result.StateId = SimpleMatch.Groups[1].Value;
            return true;
        }

        return false;
    }
}
