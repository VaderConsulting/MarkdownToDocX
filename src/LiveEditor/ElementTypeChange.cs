using System;
using System.Collections.Generic;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Rewrites an element declaration to a new type, and can drop relationships that type no longer allows.
/// </summary>
internal static class ElementTypeChange
{
    /// <summary>
    /// Changes the type token on the element's declaration line.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <param name="ElementId">Element identifier.</param>
    /// <param name="NewKebab">New type token, such as application-service.</param>
    /// <param name="RemoveDisallowedRelationships">When true, relationship lines that the new type does not allow are removed.</param>
    /// <returns>The updated source, or the original source when nothing can be changed.</returns>
    public static string Apply(string Source, string ElementId, string NewKebab, bool RemoveDisallowedRelationships)
    {
        if (string.IsNullOrWhiteSpace(Source) || string.IsNullOrWhiteSpace(ElementId) || string.IsNullOrWhiteSpace(NewKebab))
        {
            return Source ?? string.Empty;
        }

        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(Source);
        if (!Parsed.Document.Elements.TryGetValue(ElementId, out EnterpriseElement? Element))
        {
            return Source;
        }

        string Current = EnterpriseElementCatalog.GetSourceToken(Element);
        if (string.Equals(Current, NewKebab, StringComparison.Ordinal) || Element.LineNumber < 1)
        {
            return Source;
        }

        string Replaced = ReplaceLineToken(Source, Element.LineNumber, Current, NewKebab);
        if (string.Equals(Replaced, Source, StringComparison.Ordinal))
        {
            foreach (string Alias in SourceAliases(Current))
            {
                Replaced = ReplaceLineToken(Source, Element.LineNumber, Alias, NewKebab);
                if (!string.Equals(Replaced, Source, StringComparison.Ordinal))
                {
                    break;
                }
            }
        }
        if (!RemoveDisallowedRelationships || string.Equals(Replaced, Source, StringComparison.Ordinal))
        {
            return Replaced;
        }

        EnterpriseParseResult Again = EnterpriseParser.ParseDocument(Replaced);
        List<int> Remove = [];
        foreach (EnterpriseRelationship Relationship in Again.Document.Relationships)
        {
            if (!string.Equals(Relationship.SourceId, ElementId, StringComparison.Ordinal)
                && !string.Equals(Relationship.TargetId, ElementId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!Again.Document.Elements.TryGetValue(Relationship.SourceId, out EnterpriseElement? SourceElement)
                || !Again.Document.Elements.TryGetValue(Relationship.TargetId, out EnterpriseElement? TargetElement))
            {
                continue;
            }

            string Letters = EnterpriseRulesCatalog.Instance.GetAllowedLetters(
                Again.Document.Version,
                EnterpriseElementCatalog.GetKebabName(SourceElement.ElementType),
                EnterpriseElementCatalog.GetKebabName(TargetElement.ElementType));
            char Letter = EnterpriseElementCatalog.GetLetter(Relationship.Kind);
            if (Letters.IndexOf(Letter) < 0)
            {
                Remove.Add(Relationship.LineNumber);
            }
        }

        return RemoveLines(Replaced, Remove);
    }

    private static IEnumerable<string> SourceAliases(string Token)
    {
        if (string.Equals(Token, "and-junction", StringComparison.Ordinal))
        {
            yield return "junction-and";
            yield return "andjunction";
            yield return "junction";
        }
        else if (string.Equals(Token, "or-junction", StringComparison.Ordinal))
        {
            yield return "junction-or";
            yield return "orjunction";
            yield return "junction";
        }
    }

    private static string ReplaceLineToken(string Source, int LineNumber, string OldToken, string NewToken)
    {
        bool Windows = Source.Contains("\r\n", StringComparison.Ordinal);
        string[] Lines = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        int Index = LineNumber - 1;
        if (Index < 0 || Index >= Lines.Length)
        {
            return Source;
        }

        string Line = Lines[Index];
        int Start = 0;
        while (Start < Line.Length && char.IsWhiteSpace(Line[Start]))
        {
            Start++;
        }

        if (Start >= Line.Length || string.CompareOrdinal(Line, Start, OldToken, 0, OldToken.Length) != 0)
        {
            return Source;
        }

        int After = Start + OldToken.Length;
        if (After < Line.Length && !char.IsWhiteSpace(Line[After]))
        {
            return Source;
        }

        Lines[Index] = Line.Substring(0, Start) + NewToken + Line.Substring(After);
        return string.Join(Windows ? "\r\n" : "\n", Lines);
    }

    private static string RemoveLines(string Source, List<int> LineNumbers)
    {
        if (LineNumbers.Count == 0)
        {
            return Source;
        }

        bool Windows = Source.Contains("\r\n", StringComparison.Ordinal);
        string[] Lines = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        LineNumbers.Sort();
        for (int Index = LineNumbers.Count - 1; Index >= 0; Index--)
        {
            int LineIndex = LineNumbers[Index] - 1;
            if (LineIndex < 0 || LineIndex >= Lines.Length)
            {
                continue;
            }

            string[] Next = new string[Lines.Length - 1];
            if (LineIndex > 0)
            {
                Array.Copy(Lines, 0, Next, 0, LineIndex);
            }

            if (LineIndex < Lines.Length - 1)
            {
                Array.Copy(Lines, LineIndex + 1, Next, LineIndex, Lines.Length - LineIndex - 1);
            }

            Lines = Next;
        }

        return string.Join(Windows ? "\r\n" : "\n", Lines);
    }
}
