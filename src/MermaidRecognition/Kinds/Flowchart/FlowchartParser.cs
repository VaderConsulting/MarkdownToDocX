using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Parses flowchart and graph scripts into line-number items for the LiveEditor UI.
/// </summary>
public sealed class FlowchartParser : Parser
{
    private static readonly Regex HeaderRegex = new Regex(
        @"^(flowchart|graph)(\s+(TB|TD|BT|LR|RL))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NodeLineRegex = new Regex(
        @"^(?<id>[A-Za-z][\w-]*)\s*(?<shape>\[\[(?<label>[^\]]*)\]\]|\[\((?<label>[^\)]*)\)\]|\(\[(?<label>[^\]]*)\]\)|\(\((?<label>[^\)]*)\)\)|\{(?<label>[^\}]*)\}|\{\{(?<label>[^\}]*)\}\}|\((?<label>[^\)]*)\)|\[(?<label>[^\]]*)\]|\>\s*(?<label>[^\]]*)\]|(?<label>))?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex LinkLineRegex = new Regex(
        @"^(?<src>[A-Za-z][\w-]*)\s*(?:\[.*?\]|\(.*?\)|\{.*?\})?\s*(?<arrow>--+|==+|-\.+|~~+)\s*(?:\|(?<elabel>[^\|]*)\|)?\s*(?<tgt>[A-Za-z][\w-]*)",
        RegexOptions.Compiled);

    private static readonly Regex SubgraphRegex = new Regex(
        @"^subgraph\s+(?<id>[A-Za-z][\w-]*)(?:\s*\[(?<label>[^\]]*)\])?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static FlowchartParser Default { get; } = new FlowchartParser();

    static FlowchartParser()
    {
        Parser.Register(Default);
    }

    /// <summary>
    /// Creates a flowchart parser.
    /// </summary>
    public FlowchartParser()
    {
    }

    /// <inheritdoc />
    public override bool Detect(string? Source)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return false;
        }

        string DetectionText = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        foreach (string Raw in DetectionText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            string Trimmed = StripComment(Raw).Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            return HeaderRegex.IsMatch(Trimmed);
        }

        return false;
    }

    /// <inheritdoc />
    public override DiagramParseResult Parse(string? Source)
    {
        List<DiagramItem> Items = new List<DiagramItem>();
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
            if (Trimmed.Length == 0 || HeaderRegex.IsMatch(Trimmed))
            {
                continue;
            }

            if (string.Equals(Trimmed, "end", StringComparison.OrdinalIgnoreCase)
                || Trimmed.StartsWith("direction ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Match Subgraph = SubgraphRegex.Match(Trimmed);
            if (Subgraph.Success)
            {
                string Id = Subgraph.Groups["id"].Value;
                string Label = Subgraph.Groups["label"].Success ? Subgraph.Groups["label"].Value : Id;
                Items.Add(new DiagramItem(LineNumber, DiagramItemKind.Container, Id, Label, "subgraph", string.Empty, string.Empty));
                continue;
            }

            Match Link = LinkLineRegex.Match(Trimmed);
            if (Link.Success)
            {
                string Label = Link.Groups["elabel"].Success ? Link.Groups["elabel"].Value : string.Empty;
                Items.Add(new DiagramItem(
                    LineNumber,
                    DiagramItemKind.Relationship,
                    string.Empty,
                    Label,
                    "link",
                    Link.Groups["src"].Value,
                    Link.Groups["tgt"].Value));
                continue;
            }

            Match Node = NodeLineRegex.Match(Trimmed);
            if (Node.Success && Node.Groups["id"].Success)
            {
                string Id = Node.Groups["id"].Value;
                string Label = Node.Groups["label"].Success ? Node.Groups["label"].Value : Id;
                string Shape = ShapeToken(Node.Groups["shape"].Value);
                Items.Add(new DiagramItem(LineNumber, DiagramItemKind.Node, Id, Label, Shape, string.Empty, string.Empty));
            }
        }

        return new DiagramParseResult(Items, Array.Empty<string>());
    }

    /// <inheritdoc />
    public override IReadOnlyList<string> ChangeTypeTokens(DiagramItem Item)
    {
        if (Item == null || Item.Kind != DiagramItemKind.Node)
        {
            return Array.Empty<string>();
        }

        return new string[]
        {
            "rectangle",
            "round",
            "stadium",
            "subroutine",
            "cylinder",
            "circle",
            "diamond",
            "hexagon"
        };
    }

    private static string ShapeToken(string ShapeMarkup)
    {
        if (string.IsNullOrEmpty(ShapeMarkup))
        {
            return "rectangle";
        }

        if (ShapeMarkup.StartsWith("[[", StringComparison.Ordinal))
        {
            return "subroutine";
        }

        if (ShapeMarkup.StartsWith("[(", StringComparison.Ordinal))
        {
            return "cylinder";
        }

        if (ShapeMarkup.StartsWith("([", StringComparison.Ordinal))
        {
            return "stadium";
        }

        if (ShapeMarkup.StartsWith("((", StringComparison.Ordinal))
        {
            return "circle";
        }

        if (ShapeMarkup.StartsWith("{{", StringComparison.Ordinal))
        {
            return "hexagon";
        }

        if (ShapeMarkup.StartsWith("{", StringComparison.Ordinal))
        {
            return "diamond";
        }

        if (ShapeMarkup.StartsWith("(", StringComparison.Ordinal))
        {
            return "round";
        }

        return "rectangle";
    }

    private static string StripComment(string Line)
    {
        int Comment = Line.IndexOf("%%", StringComparison.Ordinal);
        if (Comment < 0)
        {
            return Line;
        }

        return Line.Substring(0, Comment);
    }
}
