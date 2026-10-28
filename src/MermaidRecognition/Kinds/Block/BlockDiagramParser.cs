using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Block;

/// <summary>
/// Parses block-beta diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class BlockDiagramParser : HeaderKindParser
{
    private static readonly string[] Headers = ["block-beta", "block"];

    private static readonly Regex BlockRegex = new Regex(
        @"^(?<id>[A-Za-z_][\w-]*)(?<shape>\[\[[^\]]*\]\]|\(\[[^\]]*\]\)|\(\([^\)]*\)\)|\([^\)]*\)|\[[^\]]*\]|\{[^\}]*\})?",
        RegexOptions.Compiled);

    private static readonly Regex LinkRegex = new Regex(
        @"^(?<src>[A-Za-z_][\w-]*)\s*-->\s*(?<tgt>[A-Za-z_][\w-]*)",
        RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static BlockDiagramParser Default { get; } = new BlockDiagramParser();

    static BlockDiagramParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (Trimmed.StartsWith("columns ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("space", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Trimmed, "end", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Match Link = LinkRegex.Match(Trimmed);
        if (Link.Success)
        {
            Item = Relationship(LineNumber, Link.Groups["src"].Value, Link.Groups["tgt"].Value, string.Empty, "link");
            return true;
        }

        Match Block = BlockRegex.Match(Trimmed);
        if (Block.Success)
        {
            string Id = Block.Groups["id"].Value;
            string Shape = Block.Groups["shape"].Success ? Block.Groups["shape"].Value : string.Empty;
            string Label = ExtractLabel(Shape, Id);
            Item = Node(LineNumber, Id, Label, "block");
            return true;
        }

        return false;
    }

    private static string ExtractLabel(string Shape, string Fallback)
    {
        if (string.IsNullOrEmpty(Shape) || Shape.Length < 2)
        {
            return Fallback;
        }

        string Inner = Shape.Substring(1, Shape.Length - 2).Trim();
        while (Inner.Length > 0 && (Inner[0] == '[' || Inner[0] == '(' || Inner[0] == '{'))
        {
            Inner = Inner.Substring(1);
        }

        while (Inner.Length > 0)
        {
            char Last = Inner[Inner.Length - 1];
            if (Last != ']' && Last != ')' && Last != '}')
            {
                break;
            }

            Inner = Inner.Substring(0, Inner.Length - 1);
        }

        if (Inner.Length >= 2 && Inner[0] == '"' && Inner[Inner.Length - 1] == '"')
        {
            Inner = Inner.Substring(1, Inner.Length - 2);
        }

        return Inner.Length == 0 ? Fallback : Inner;
    }
}
