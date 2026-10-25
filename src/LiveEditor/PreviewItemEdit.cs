using System;
using System.Text.RegularExpressions;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Rewrites diagram source for one preview item. The item does not need to be selected.
/// </summary>
internal static class PreviewItemEdit
{
    private static readonly Regex RELATIONSHIP_KIND = new Regex(
        @"-(?<kind>[A-Za-z]+)(?<tail>(?:\([^)]*\))?-+>)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Replaces the item's declaration line.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <param name="Item">Item whose line is replaced. A node with no line number is found by its id.</param>
    /// <param name="Declaration">The new line, without a line break.</param>
    /// <returns>The updated source, or the original source when the line cannot be replaced.</returns>
    public static string ReplaceDeclaration(string? Source, PreviewObject Item, string? Declaration)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (string.IsNullOrEmpty(Source) || Declaration == null || Declaration.IndexOf('\n') >= 0 || Declaration.IndexOf('\r') >= 0)
        {
            return Source ?? string.Empty;
        }

        int LineNumber = ResolveLine(Source, Item);
        return ReplaceLine(Source, LineNumber, Declaration);
    }

    /// <summary>
    /// Removes the item's declaration line.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <param name="Item">Item to remove. A node with no line number is found by its id. A relationship must already know its line.</param>
    /// <returns>The updated source, or the original source when the line cannot be removed.</returns>
    public static string Remove(string? Source, PreviewObject Item)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (string.IsNullOrEmpty(Source))
        {
            return Source ?? string.Empty;
        }

        int LineNumber = ResolveLine(Source, Item);
        if (LineNumber < 1)
        {
            return Source;
        }

        bool Windows = Source.Contains("\r\n", StringComparison.Ordinal);
        string[] Lines = SplitLines(Source);
        int Index = LineNumber - 1;
        if (Index < 0 || Index >= Lines.Length)
        {
            return Source;
        }

        string[] Next = new string[Lines.Length - 1];
        if (Index > 0)
        {
            Array.Copy(Lines, 0, Next, 0, Index);
        }

        if (Index < Lines.Length - 1)
        {
            Array.Copy(Lines, Index + 1, Next, Index, Lines.Length - Index - 1);
        }

        return string.Join(Windows ? "\r\n" : "\n", Next);
    }

    /// <summary>
    /// Changes an element's type token, or a relationship's kind, on that item's declaration.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <param name="Item">Item to change.</param>
    /// <param name="NewKebab">New element token or relationship kind.</param>
    /// <param name="RemoveDisallowedRelationships">When true, relationships the new element type does not allow are removed.</param>
    /// <returns>The updated source, or the original source when nothing can be changed.</returns>
    public static string ChangeType(string? Source, PreviewObject Item, string? NewKebab, bool RemoveDisallowedRelationships)
    {
        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (string.IsNullOrEmpty(Source) || string.IsNullOrWhiteSpace(NewKebab))
        {
            return Source ?? string.Empty;
        }

        string Token = NewKebab.Trim();
        if (Item.Kind == PreviewObjectKind.Relationship)
        {
            return ReplaceRelationshipKind(Source, Item, Token);
        }

        return ElementTypeChange.Apply(Source, Item.Id, Token, RemoveDisallowedRelationships);
    }

    private static string ReplaceRelationshipKind(string Source, PreviewObject Item, string NewKind)
    {
        if (string.Equals(Item.RelationshipKind, "connection", StringComparison.Ordinal)
            || string.Equals(Item.RelationshipKind, NewKind, StringComparison.Ordinal)
            || Item.LineNumber < 1)
        {
            return Source;
        }

        bool Windows = Source.Contains("\r\n", StringComparison.Ordinal);
        string[] Lines = SplitLines(Source);
        int Index = Item.LineNumber - 1;
        if (Index < 0 || Index >= Lines.Length)
        {
            return Source;
        }

        string Line = Lines[Index];
        Match Match = RELATIONSHIP_KIND.Match(Line);
        if (!Match.Success || !string.Equals(Match.Groups["kind"].Value, Item.RelationshipKind, StringComparison.OrdinalIgnoreCase))
        {
            return Source;
        }

        Group Kind = Match.Groups["kind"];
        Lines[Index] = Line.Substring(0, Kind.Index) + NewKind + Line.Substring(Kind.Index + Kind.Length);
        return string.Join(Windows ? "\r\n" : "\n", Lines);
    }

    private static int ResolveLine(string Source, PreviewObject Item)
    {
        if (Item.LineNumber >= 1)
        {
            return Item.LineNumber;
        }

        if (Item.Kind == PreviewObjectKind.Relationship || Item.Id.Length == 0)
        {
            return 0;
        }

        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Source);
        if (Parsed.Document.Elements.TryGetValue(Item.Id, out EnterpriseElement? Element))
        {
            return Element.LineNumber;
        }

        if (Parsed.Document.Groups.TryGetValue(Item.Id, out EnterpriseGroup? Group))
        {
            return Group.LineNumber;
        }

        if (Parsed.Document.Notes.TryGetValue(Item.Id, out EnterpriseNote? Note))
        {
            return Note.LineNumber;
        }

        return 0;
    }

    private static string ReplaceLine(string Source, int LineNumber, string Declaration)
    {
        if (LineNumber < 1)
        {
            return Source;
        }

        bool Windows = Source.Contains("\r\n", StringComparison.Ordinal);
        string[] Lines = SplitLines(Source);
        int Index = LineNumber - 1;
        if (Index < 0 || Index >= Lines.Length || string.Equals(Lines[Index], Declaration, StringComparison.Ordinal))
        {
            return Source;
        }

        Lines[Index] = Declaration;
        return string.Join(Windows ? "\r\n" : "\n", Lines);
    }

    private static string[] SplitLines(string Source)
    {
        return Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }
}
