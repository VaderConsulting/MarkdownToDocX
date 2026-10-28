using System;
using System.Collections.Generic;

namespace MermaidRecognition.Core;

/// <summary>
/// Parses one Mermaid diagram kind into a shared item list for the LiveEditor UI.
/// Every supported kind subclasses this type. There is no separate interface.
/// </summary>
public abstract class Parser
{
    private static readonly List<Parser> _Registered = [];

    private static readonly object _RegisterLock = new object();

    private static bool _DefaultsReady;

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
    /// Prepares a script for the editor before it is shown.
    /// Flowchart sources expand inline-only nodes onto their own lines so each item has a line id.
    /// </summary>
    /// <param name="Source">File or sample text.</param>
    /// <returns>The text to load into the editor.</returns>
    public static string PrepareForEditor(string? Source)
    {
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(Source))
        {
            return Source ?? string.Empty;
        }

        Parser? Kind = ForSource(Source);
        if (Kind == null)
        {
            return Source;
        }

        return Kind.PrepareSource(Source);
    }

    /// <summary>
    /// Kind-specific rewrite before the editor shows the script.
    /// </summary>
    /// <param name="Source">Original script.</param>
    /// <returns>Script to display.</returns>
    public virtual string PrepareSource(string Source)
    {
        return Source ?? string.Empty;
    }

    /// <summary>
    /// Collects identifier names currently used in the script.
    /// Re-scan after the user adds, edits, or deletes lines so suggestions stay unique.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <returns>Used identifiers. Comparison is case-insensitive.</returns>
    public static HashSet<string> CollectUsedIdentifiers(string? Source)
    {
        HashSet<string> Used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        EnsureDefaults();
        if (string.IsNullOrWhiteSpace(Source))
        {
            return Used;
        }

        Parser? Kind = ForSource(Source);
        if (Kind == null)
        {
            return Used;
        }

        Kind.AddUsedIdentifiers(Source, Used);
        return Used;
    }

    /// <summary>
    /// Adds this kind's identifiers from the current script into <paramref name="Used"/>.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <param name="Used">Set to fill. Caller owns the set across add / edit / delete re-scans.</param>
    public virtual void AddUsedIdentifiers(string? Source, HashSet<string> Used)
    {
        if (Used == null || string.IsNullOrWhiteSpace(Source))
        {
            return;
        }

        DiagramParseResult Result = Parse(Source);
        foreach (DiagramItem Item in Result.Items)
        {
            if (Item.Name.Length > 0)
            {
                Used.Add(Item.Name);
            }

            if (Item.SourceName.Length > 0)
            {
                Used.Add(Item.SourceName);
            }

            if (Item.TargetName.Length > 0)
            {
                Used.Add(Item.TargetName);
            }
        }
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
            if (_DefaultsReady)
            {
                return;
            }

            _DefaultsReady = true;
        }

        Register(Kinds.Enterprise.EnterpriseParser.Default);
        Register(Kinds.Flowchart.FlowchartParser.Default);
        Register(Kinds.Sequence.SequenceParser.Default);
        Register(Kinds.Class.ClassDiagramParser.Default);
        Register(Kinds.State.StateDiagramParser.Default);
        Register(Kinds.Er.ErDiagramParser.Default);
        Register(Kinds.Architecture.ArchitectureParser.Default);
        Register(Kinds.Block.BlockDiagramParser.Default);
        Register(Kinds.C4.C4Parser.Default);
        Register(Kinds.Charts.GanttParser.Default);
        Register(Kinds.Charts.PieParser.Default);
        Register(Kinds.Charts.GitGraphParser.Default);
        Register(Kinds.Charts.MindmapParser.Default);
        Register(Kinds.Charts.TimelineParser.Default);
        Register(Kinds.Charts.JourneyParser.Default);
        Register(Kinds.Charts.QuadrantChartParser.Default);
        Register(Kinds.Charts.XyChartParser.Default);
        Register(Kinds.Charts.PacketParser.Default);
        Register(Kinds.Charts.RadarParser.Default);
        Register(Kinds.Charts.TreemapParser.Default);
    }
}
