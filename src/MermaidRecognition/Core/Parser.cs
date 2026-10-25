using System;
using System.Collections.Generic;

namespace MermaidRecognition.Core;

/// <summary>
/// Parses one Mermaid diagram kind into a shared item list for the LiveEditor UI.
/// Every supported kind subclasses this type. There is no separate interface.
/// </summary>
public abstract class Parser
{
    private static readonly List<Parser> _Registered = new List<Parser>();

    private static readonly object _RegisterLock = new object();

    /// <summary>
    /// Registers a parser so <see cref="ForSource"/> can find it.
    /// </summary>
    /// <param name="Instance">Parser instance. Null is ignored.</param>
    public static void Register(Parser? Instance)
    {
        if (Instance == null)
        {
            return;
        }

        lock (_RegisterLock)
        {
            foreach (Parser Existing in _Registered)
            {
                if (ReferenceEquals(Existing, Instance))
                {
                    return;
                }
            }

            _Registered.Add(Instance);
        }
    }

    /// <summary>
    /// Returns the parser that accepts the source, or null when none matches.
    /// </summary>
    /// <param name="Source">Editor script.</param>
    /// <returns>The matching parser, or null.</returns>
    public static Parser? ForSource(string? Source)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(Source))
        {
            return null;
        }

        lock (_RegisterLock)
        {
            foreach (Parser Candidate in _Registered)
            {
                if (Candidate.Detect(Source))
                {
                    return Candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// True when this parser handles the source.
    /// </summary>
    /// <param name="Source">Editor script.</param>
    /// <returns>True when Detect succeeds.</returns>
    public abstract bool Detect(string? Source);

    /// <summary>
    /// Parses the source into items keyed by line number.
    /// </summary>
    /// <param name="Source">Editor script.</param>
    /// <returns>Items and diagnostics.</returns>
    public abstract DiagramParseResult Parse(string? Source);

    /// <summary>
    /// True when the caret sits on a line that already declares an item.
    /// </summary>
    /// <param name="Source">Editor script.</param>
    /// <param name="CaretIndex">Caret character index.</param>
    /// <returns>True when that line has an item.</returns>
    public virtual bool IsDeclaredLine(string? Source, int CaretIndex)
    {
        if (string.IsNullOrEmpty(Source))
        {
            return false;
        }

        int Line = LineNumberAt(Source, CaretIndex);
        return Parse(Source).ItemAtLine(Line) != null;
    }

    /// <summary>
    /// Builds a short description for the details panel.
    /// </summary>
    /// <param name="Item">Item from a previous parse of the same kind.</param>
    /// <returns>One or two sentences.</returns>
    public virtual string Describe(DiagramItem Item)
    {
        if (Item == null)
        {
            return string.Empty;
        }

        if (Item.Kind == DiagramItemKind.Relationship)
        {
            string SourceName = Item.SourceName.Length == 0 ? "?" : Item.SourceName;
            string TargetName = Item.TargetName.Length == 0 ? "?" : Item.TargetName;
            if (Item.TypeToken.Length > 0)
            {
                return Item.TypeToken + " from " + SourceName + " to " + TargetName + ".";
            }

            return "Connects " + SourceName + " to " + TargetName + ".";
        }

        if (Item.Label.Length > 0)
        {
            return Item.Label;
        }

        if (Item.Name.Length > 0)
        {
            return Item.Name;
        }

        return "Line " + Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Alternate type tokens the details panel may offer for this item.
    /// </summary>
    /// <param name="Item">Selected item.</param>
    /// <returns>Tokens, or empty when the type cannot change.</returns>
    public virtual IReadOnlyList<string> ChangeTypeTokens(DiagramItem Item)
    {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Returns the 1-based line that holds a character index. Only line feeds count.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <param name="CharacterIndex">Caret or selection index.</param>
    /// <returns>1-based line number.</returns>
    public static int LineNumberAt(string Source, int CharacterIndex)
    {
        int Limit = Math.Max(0, Math.Min(CharacterIndex, Source.Length));
        int Line = 1;
        for (int Index = 0; Index < Limit; Index++)
        {
            if (Source[Index] == '\n')
            {
                Line++;
            }
        }

        return Line;
    }

    private static void EnsureDefaults()
    {
        lock (_RegisterLock)
        {
            if (_Registered.Count > 0)
            {
                return;
            }
        }

        Register(Kinds.Enterprise.EnterpriseParser.Default);
        Register(Kinds.Flowchart.FlowchartParser.Default);
    }
}
