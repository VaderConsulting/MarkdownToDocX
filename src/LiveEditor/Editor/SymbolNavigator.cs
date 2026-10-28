using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Editor;

/// <summary>
/// Symbol navigation helpers for enterprise ids in the editor buffer.
/// </summary>
internal static class SymbolNavigator
{
    private static readonly Regex IdTokenRegex = new Regex(
        @"[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*",
        RegexOptions.Compiled);

    public static string? IdAt(string Source, int CaretIndex)
    {
        if (string.IsNullOrEmpty(Source) || CaretIndex < 0 || CaretIndex > Source.Length)
        {
            return null;
        }

        int Start = CaretIndex;
        while (Start > 0 && IsIdChar(Source[Start - 1]))
        {
            Start--;
        }

        int End = CaretIndex;
        while (End < Source.Length && IsIdChar(Source[End]))
        {
            End++;
        }

        if (End <= Start)
        {
            return null;
        }

        string Token = Source.Substring(Start, End - Start);
        return IdTokenRegex.IsMatch(Token) ? Token : null;
    }

    public static int FindDefinition(string Source, string Id)
    {
        if (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(Id))
        {
            return -1;
        }

        Regex Pattern = new Regex(
            @"^\s*(?:[A-Za-z-]+|group|note)\s+" + Regex.Escape(Id) + @"\b",
            RegexOptions.Multiline);
        Match Match = Pattern.Match(Source);
        return Match.Success ? Match.Index : -1;
    }

    public static List<int> FindReferences(string Source, string Id)
    {
        List<int> Results = [];
        if (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(Id))
        {
            return Results;
        }

        Regex Pattern = new Regex(@"\b" + Regex.Escape(Id) + @"\b");
        foreach (Match Match in Pattern.Matches(Source))
        {
            Results.Add(Match.Index);
        }

        return Results;
    }

    public static string Rename(string Source, string OldId, string NewId)
    {
        if (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(OldId) || string.IsNullOrEmpty(NewId))
        {
            return Source;
        }

        if (!LiveEditor.Archi.ArchiObjectIds.IsValidMermaidId(NewId))
        {
            throw new ArgumentException("New id is not a valid Mermaid identifier.", nameof(NewId));
        }

        return Regex.Replace(Source, @"\b" + Regex.Escape(OldId) + @"\b", NewId);
    }

    public static int FindNextOccurrence(string Source, string Token, int FromIndex)
    {
        if (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(Token))
        {
            return -1;
        }

        int Start = Math.Max(0, FromIndex);
        int Found = Source.IndexOf(Token, Start, StringComparison.Ordinal);
        if (Found < 0 && Start > 0)
        {
            Found = Source.IndexOf(Token, 0, StringComparison.Ordinal);
        }

        return Found;
    }

    /// <summary>
    /// Finds each whole-token use of an identifier. A longer token that only starts with the same letters is left out.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <param name="Id">Identifier to find.</param>
    /// <returns>Start index of each match.</returns>
    public static List<int> FindExactIds(string Source, string Id)
    {
        List<int> Results = [];
        if (string.IsNullOrEmpty(Source) || string.IsNullOrEmpty(Id))
        {
            return Results;
        }

        int From = 0;
        while (From <= Source.Length - Id.Length)
        {
            int Found = Source.IndexOf(Id, From, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            bool LeftBound = Found == 0 || !IsIdChar(Source[Found - 1]);
            int End = Found + Id.Length;
            bool RightBound = End >= Source.Length || !IsIdChar(Source[End]);
            if (LeftBound && RightBound)
            {
                Results.Add(Found);
            }

            From = Found + Id.Length;
        }

        return Results;
    }

    /// <summary>
    /// Returns true when the token is an element identifier rather than a type, keyword, or relationship name.
    /// </summary>
    /// <param name="Token">Selected token.</param>
    /// <returns>True when other copies of this token should be marked.</returns>
    public static bool IsReferenceIdentifier(string Token)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            return false;
        }

        if (EnterpriseElementCatalog.TryResolveElementType(Token, out _))
        {
            return false;
        }

        if (Token.Length > 1 && EnterpriseElementCatalog.TryResolveRelationship(Token, out _))
        {
            return false;
        }

        if (ReservedWords.Contains(Token))
        {
            return false;
        }

        return true;
    }

    private static readonly HashSet<string> ReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "enterprise",
        "enterprise-beta",
        "title",
        "version",
        "direction",
        "group",
        "note",
        "in",
        "end",
        "class",
        "classDef",
        "style",
        "linkStyle",
        "accTitle",
        "accDescr",
        "TB",
        "TD",
        "BT",
        "LR",
        "RL"
    };

    private static bool IsIdChar(char Character)
    {
        return char.IsLetterOrDigit(Character)
            || Character == '_'
            || Character == '-'
            || Character == '.';
    }
}
