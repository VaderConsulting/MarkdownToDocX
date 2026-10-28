using System;
using System.Collections.Generic;

namespace MermaidRecognition.Core;

/// <summary>
/// Parser for Mermaid kinds that start with a fixed header line.
/// Subclasses map each body line into a line-number item and collect identifiers from those items.
/// </summary>
public abstract class HeaderKindParser : Parser
{
    /// <summary>Header prefixes that identify this kind, matched case-insensitively at the start of the first content line.</summary>
    protected abstract IReadOnlyList<string> HeaderPrefixes { get; }

    /// <inheritdoc />
    public override bool Detect(string? Source)
    {
        string? Header = FirstContentLine(Source);
        if (Header == null)
        {
            return false;
        }

        for (int Index = 0; Index < HeaderPrefixes.Count; Index++)
        {
            if (Header.StartsWith(HeaderPrefixes[Index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override DiagramParseResult Parse(string? Source)
    {
        List<DiagramItem> Items = [];
        if (string.IsNullOrWhiteSpace(Source))
        {
            return new DiagramParseResult(Items, Array.Empty<string>());
        }

        string Normalised = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Lines = Normalised.Split('\n');
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            int LineNumber = Index + 1;
            string Trimmed = StripComment(Lines[Index]).Trim();
            if (Trimmed.Length == 0 || IsHeaderLine(Trimmed))
            {
                continue;
            }

            if (TryMapLine(Trimmed, LineNumber, out DiagramItem? Item) && Item != null)
            {
                Items.Add(Item);
            }
        }

        return new DiagramParseResult(Items, Array.Empty<string>());
    }

    /// <summary>
    /// Maps one body line into an item when the line declares something selectable.
    /// </summary>
    /// <param name="Trimmed">Comment-stripped trimmed line.</param>
    /// <param name="LineNumber">1-based line number.</param>
    /// <param name="Item">The item when mapping succeeds.</param>
    /// <returns>True when the line produced an item.</returns>
    protected abstract bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item);

    /// <summary>
    /// True when the line is this kind's diagram header.
    /// </summary>
    /// <param name="Trimmed">Trimmed line.</param>
    /// <returns>True for a matching header.</returns>
    protected bool IsHeaderLine(string Trimmed)
    {
        for (int Index = 0; Index < HeaderPrefixes.Count; Index++)
        {
            if (Trimmed.StartsWith(HeaderPrefixes[Index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes a trailing <c>%%</c> comment from a line.
    /// </summary>
    /// <param name="Line">Raw line.</param>
    /// <returns>Text before the comment.</returns>
    protected static string StripComment(string Line)
    {
        int Comment = Line.IndexOf("%%", StringComparison.Ordinal);
        if (Comment < 0)
        {
            return Line;
        }

        return Line.Substring(0, Comment);
    }

    /// <summary>
    /// Returns the first non-empty, non-comment line, or null.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <returns>Trimmed first content line.</returns>
    protected static string? FirstContentLine(string? Source)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return null;
        }

        string DetectionText = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        foreach (string Raw in DetectionText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            string Trimmed = StripComment(Raw).Trim();
            if (Trimmed.Length > 0)
            {
                return Trimmed;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a node item for a named declaration.
    /// </summary>
    protected static DiagramItem Node(int LineNumber, string Name, string Label, string TypeToken)
    {
        return new DiagramItem(LineNumber, DiagramItemKind.Node, Name, Label, TypeToken, string.Empty, string.Empty);
    }

    /// <summary>
    /// Builds a container item.
    /// </summary>
    protected static DiagramItem Container(int LineNumber, string Name, string Label, string TypeToken)
    {
        return new DiagramItem(LineNumber, DiagramItemKind.Container, Name, Label, TypeToken, string.Empty, string.Empty);
    }

    /// <summary>
    /// Builds a relationship item.
    /// </summary>
    protected static DiagramItem Relationship(int LineNumber, string SourceName, string TargetName, string Label, string TypeToken)
    {
        return new DiagramItem(LineNumber, DiagramItemKind.Relationship, string.Empty, Label, TypeToken, SourceName, TargetName);
    }
}
