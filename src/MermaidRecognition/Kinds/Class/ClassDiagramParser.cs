using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Parses class diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class ClassDiagramParser : HeaderKindParser
{
    private static readonly string[] Headers = ["classDiagram"];

    private static readonly Regex ClassDeclareRegex = new Regex(
        @"^class\s+(?<id>[A-Za-z_][\w-]*)(?:\s*~(?<label>[^~]*)~)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex = new Regex(
        @"^namespace\s+(?<id>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RelationRegex = new Regex(
        @"^(?<src>[A-Za-z_][\w-]*)\s*(?<arrow>(?:<\|--|-\-|>\|--|-\-|>|\.\.|<\|\.\.|\*\-\-|\<\-\-|o\-\-))\s*(?<tgt>[A-Za-z_][\w-]*)",
        RegexOptions.Compiled);

    private static readonly Regex BareClassRegex = new Regex(
        @"^(?<id>[A-Za-z_][\w-]*)\s*\{",
        RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static ClassDiagramParser Default { get; } = new ClassDiagramParser();

    static ClassDiagramParser()
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
        if (string.Equals(Trimmed, "end", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("direction ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("note ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("<<", StringComparison.Ordinal))
        {
            return false;
        }

        Match Namespace = NamespaceRegex.Match(Trimmed);
        if (Namespace.Success)
        {
            Item = Container(LineNumber, Namespace.Groups["id"].Value, Namespace.Groups["id"].Value, "namespace");
            return true;
        }

        Match Declared = ClassDeclareRegex.Match(Trimmed);
        if (Declared.Success)
        {
            string Id = Declared.Groups["id"].Value;
            string Label = Declared.Groups["label"].Success ? Declared.Groups["label"].Value.Trim() : Id;
            Item = Node(LineNumber, Id, Label, "class");
            return true;
        }

        Match Relation = RelationRegex.Match(Trimmed);
        if (Relation.Success)
        {
            Item = Relationship(LineNumber, Relation.Groups["src"].Value, Relation.Groups["tgt"].Value, string.Empty, "relation");
            return true;
        }

        Match Bare = BareClassRegex.Match(Trimmed);
        if (Bare.Success)
        {
            string Id = Bare.Groups["id"].Value;
            Item = Node(LineNumber, Id, Id, "class");
            return true;
        }

        return false;
    }
}
