using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Architecture;

/// <summary>
/// Parses architecture-beta diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class ArchitectureParser : HeaderKindParser
{
    private static readonly string[] Headers = ["architecture-beta", "architecture"];

    private static readonly Regex GroupRegex = new Regex(
        @"^group\s+(?<id>[A-Za-z_][\w-]*)(?:\((?<label>[^)]*)\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ServiceRegex = new Regex(
        @"^service\s+(?<id>[A-Za-z_][\w-]*)(?:\((?<label>[^)]*)\))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JunctionRegex = new Regex(
        @"^junction\s+(?<id>[A-Za-z_][\w-]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EdgeRegex = new Regex(
        @"^(?<src>[A-Za-z_][\w-]*)\s*(?:-->|:)\s*(?<tgt>[A-Za-z_][\w-]*)",
        RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static ArchitectureParser Default { get; } = new ArchitectureParser();

    static ArchitectureParser()
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
        Match Group = GroupRegex.Match(Trimmed);
        if (Group.Success)
        {
            string Id = Group.Groups["id"].Value;
            string Label = Group.Groups["label"].Success ? Group.Groups["label"].Value.Trim() : Id;
            Item = Container(LineNumber, Id, Label, "group");
            return true;
        }

        Match Service = ServiceRegex.Match(Trimmed);
        if (Service.Success)
        {
            string Id = Service.Groups["id"].Value;
            string Label = Service.Groups["label"].Success ? Service.Groups["label"].Value.Trim() : Id;
            Item = Node(LineNumber, Id, Label, "service");
            return true;
        }

        Match Junction = JunctionRegex.Match(Trimmed);
        if (Junction.Success)
        {
            string Id = Junction.Groups["id"].Value;
            Item = Node(LineNumber, Id, Id, "junction");
            return true;
        }

        Match Edge = EdgeRegex.Match(Trimmed);
        if (Edge.Success)
        {
            Item = Relationship(LineNumber, Edge.Groups["src"].Value, Edge.Groups["tgt"].Value, string.Empty, "edge");
            return true;
        }

        return false;
    }
}
