using System.Text.RegularExpressions;
using MermaidRecognition.Contract;

namespace MermaidRecognition.State;

/// <summary>
/// Parses Mermaid state diagram <c>click</c> interaction directives (href, call, tooltip, link target).
/// </summary>
public static class StateClickParser
{
    private static readonly Regex ClickHeadRegex = new(
        @"^\s*(?:click|link|callback)\s+(\S+)\s+(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CallFunctionRegex = new(
        @"^(\S+)\((.*)\)\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CallbackFormRegex = new(
        @"^(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a classified <c>click</c> line. Always returns true for click/link/callback input.
    /// </summary>
    public static bool TryParse(string Line, int LineNumber, StateDb Database)
    {
        Match HeadMatch = ClickHeadRegex.Match(Line);
        if (!HeadMatch.Success)
        {
            AddUnparseableDiagnostic(Line, LineNumber, Database, "click");
            return true;
        }

        string TargetId = HeadMatch.Groups[1].Value;
        string Remainder = HeadMatch.Groups[2].Value.Trim();
        Database.GetOrCreateState(TargetId);

        if (Remainder.StartsWith("href", StringComparison.OrdinalIgnoreCase))
        {
            ParseHrefForm(TargetId, Remainder[4..].Trim(), Line, LineNumber, Database);
            return true;
        }

        if (Remainder.StartsWith("call", StringComparison.OrdinalIgnoreCase))
        {
            ParseCallForm(TargetId, Remainder[4..].Trim(), Line, LineNumber, Database);
            return true;
        }

        List<string> QuotedStrings = ExtractQuotedStrings(Remainder);
        if (QuotedStrings.Count > 0 && Remainder.TrimStart().StartsWith('"'))
        {
            string? Tooltip = QuotedStrings.Count > 1 ? QuotedStrings[1] : null;
            string? TargetWindow = ParseLinkTarget(Remainder, QuotedStrings);
            Database.InteractionsMutable.Add(
                new InteractionDirective(TargetId, null, QuotedStrings[0], Tooltip, TargetWindow));
            return true;
        }

        Match CallbackOnlyMatch = CallbackFormRegex.Match(Remainder);
        if (CallbackOnlyMatch.Success
            && !string.Equals(CallbackOnlyMatch.Groups[1].Value, "href", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(CallbackOnlyMatch.Groups[1].Value, "call", StringComparison.OrdinalIgnoreCase))
        {
            AddCallbackInteraction(TargetId, CallbackOnlyMatch.Groups[1].Value, null, Line, LineNumber, Database);
            return true;
        }

        AddUnparseableDiagnostic(Line, LineNumber, Database, TargetId);
        return true;
    }

    static void ParseHrefForm(string TargetId, string AfterHref, string Line, int LineIndex, StateDb Database)
    {
        List<string> QuotedStrings = ExtractQuotedStrings(AfterHref);
        if (QuotedStrings.Count == 0)
        {
            AddUnparseableDiagnostic(Line, LineIndex, Database, TargetId);
            return;
        }

        string? Tooltip = QuotedStrings.Count > 1 ? QuotedStrings[1] : null;
        string? TargetWindow = ParseLinkTarget(AfterHref, QuotedStrings);
        Database.InteractionsMutable.Add(
            new InteractionDirective(TargetId, null, QuotedStrings[0], Tooltip, TargetWindow));
    }

    static void ParseCallForm(string TargetId, string AfterCall, string Line, int LineIndex, StateDb Database)
    {
        Match CallMatch = CallFunctionRegex.Match(AfterCall);
        if (!CallMatch.Success)
        {
            AddUnparseableDiagnostic(Line, LineIndex, Database, TargetId);
            return;
        }

        string Callback = CallMatch.Groups[1].Value + "(" + CallMatch.Groups[2].Value + ")";
        string? Tooltip = ExtractTrailingTooltip(CallMatch.Groups[3].Value);
        AddCallbackInteraction(TargetId, Callback, Tooltip, Line, LineIndex, Database);
    }

    static void AddCallbackInteraction(
        string TargetId,
        string Callback,
        string? Tooltip,
        string Line,
        int LineIndex,
        StateDb Database)
    {
        Database.InteractionsMutable.Add(
            new InteractionDirective(TargetId, Callback, null, Tooltip, null));
        Database.DiagnosticsMutable.Add(
            new MermaidDiagnostic(
                MermaidDiagnosticSeverity.Warning,
                "state.click-callback-not-exported",
                "Click callback recognised but not exportable to DOCX.",
                new MermaidSourceSpan(0, 0, LineIndex, 0, LineIndex, Line.Length),
                TargetId));
    }

    static string? ExtractTrailingTooltip(string Tail)
    {
        List<string> QuotedStrings = ExtractQuotedStrings(Tail);
        if (QuotedStrings.Count == 0)
        {
            return null;
        }

        return QuotedStrings[0];
    }

    static string? ParseLinkTarget(string Text, List<string> QuotedStrings)
    {
        string Remaining = Text;
        foreach (string Quoted in QuotedStrings)
        {
            Remaining = Remaining.Replace("\"" + Quoted + "\"", string.Empty, StringComparison.Ordinal);
        }

        Remaining = Remaining.Trim();
        if (Remaining.Length == 0)
        {
            return null;
        }

        string[] Parts = Remaining.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (string Part in Parts)
        {
            if (Part.StartsWith('_'))
            {
                return Part;
            }
        }

        return null;
    }

    static List<string> ExtractQuotedStrings(string Text)
    {
        List<string> Results = new List<string>();
        int Index = 0;
        while (Index < Text.Length)
        {
            if (Text[Index] != '"')
            {
                Index++;
                continue;
            }

            int Start = Index + 1;
            int End = Start;
            while (End < Text.Length)
            {
                if (Text[End] == '"' && (End == Start || Text[End - 1] != '\\'))
                {
                    break;
                }

                End++;
            }

            if (End >= Text.Length)
            {
                break;
            }

            Results.Add(Text[Start..End]);
            Index = End + 1;
        }

        return Results;
    }

    static void AddUnparseableDiagnostic(string Line, int LineIndex, StateDb Database, string TargetId)
    {
        Database.DiagnosticsMutable.Add(
            new MermaidDiagnostic(
                MermaidDiagnosticSeverity.Warning,
                "state.click-unparseable",
                "Click directive could not be parsed; interaction ignored for export.",
                new MermaidSourceSpan(0, 0, LineIndex, 0, LineIndex, Line.Length),
                TargetId));
    }
}

