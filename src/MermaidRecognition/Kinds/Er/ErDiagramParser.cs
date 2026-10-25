using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Er;

/// <summary>
/// Parses entity-relationship diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class ErDiagramParser : HeaderKindParser
{
    private static readonly string[] Headers = new string[] { "erDiagram" };

    private static readonly Regex RelationshipRegex = new Regex(
        @"^(?<src>\S+)\s+(?<card>[|\}o]{1,2}\-\-?[|\{o]{1,2})\s+(?<tgt>\S+)(?:\s*:\s*(?<label>.*))?$",
        RegexOptions.Compiled);

    private static readonly Regex AliasRegex = new Regex(
        @"^(?<id>\S+)\s+\[\s*(?<label>[^\]]*)\s*\]",
        RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static ErDiagramParser Default { get; } = new ErDiagramParser();

    static ErDiagramParser()
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
        if (string.Equals(Trimmed, "}", StringComparison.Ordinal)
            || Trimmed.StartsWith("direction ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ErEntityBlockParser.TryParseEntityOpen(Trimmed, out string EntityId))
        {
            Item = Node(LineNumber, EntityId, EntityId, "entity");
            return true;
        }

        Match Alias = AliasRegex.Match(Trimmed);
        if (Alias.Success)
        {
            Item = Node(LineNumber, Alias.Groups["id"].Value, Alias.Groups["label"].Value.Trim(), "entity");
            return true;
        }

        Match Relation = RelationshipRegex.Match(Trimmed);
        if (Relation.Success)
        {
            string Label = Relation.Groups["label"].Success ? Relation.Groups["label"].Value.Trim() : string.Empty;
            Item = Relationship(LineNumber, Relation.Groups["src"].Value, Relation.Groups["tgt"].Value, Label, "relationship");
            return true;
        }

        return false;
    }
}
