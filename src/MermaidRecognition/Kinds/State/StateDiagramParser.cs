using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Parses state diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class StateDiagramParser : HeaderKindParser
{
    private static readonly string[] Headers = ["stateDiagram-v2", "stateDiagram"];

    private static readonly Regex TransitionRegex = new Regex(
        @"^(?<src>\S+)\s*-->\s*(?<tgt>\S+)(?:\s*:\s*(?<label>.*))?$",
        RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static StateDiagramParser Default { get; } = new StateDiagramParser();

    static StateDiagramParser()
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
            || string.Equals(Trimmed, "}", StringComparison.Ordinal)
            || Trimmed.StartsWith("direction ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("note ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (StateDeclarationParser.TryParse(Trimmed, out StateDeclarationParseResult Declared))
        {
            string Id = Declared.StateId;
            string Label = string.IsNullOrEmpty(Declared.Label) ? Id : Declared.Label;
            DiagramItemKind Kind = Declared.OpensComposite ? DiagramItemKind.Container : DiagramItemKind.Node;
            Item = new DiagramItem(LineNumber, Kind, Id, Label, Declared.StateType ?? "state", string.Empty, string.Empty);
            return true;
        }

        Match Transition = TransitionRegex.Match(Trimmed);
        if (Transition.Success)
        {
            string Label = Transition.Groups["label"].Success ? Transition.Groups["label"].Value.Trim() : string.Empty;
            Item = Relationship(LineNumber, Transition.Groups["src"].Value, Transition.Groups["tgt"].Value, Label, "transition");
            return true;
        }

        return false;
    }
}
