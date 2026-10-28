using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Parses sequence diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class SequenceParser : HeaderKindParser
{
    private static readonly string[] Headers = ["sequenceDiagram"];

    private static readonly Regex NoteRegex = new Regex(
        @"^note\s+(?:left|right)\s+of\s+(?<id>\S+)\s*:\s*(?<label>.*)$|^note\s+over\s+(?<ids>[^:]+)\s*:\s*(?<label>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static SequenceParser Default { get; } = new SequenceParser();

    static SequenceParser()
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
        if (SequenceParticipantParser.TryParse(Trimmed, out SequenceParticipantParseResult Participant))
        {
            string Label = Participant.Label ?? Participant.Id;
            Item = Node(LineNumber, Participant.Id, Label, Participant.DrawType);
            return true;
        }

        if (SequenceMessageParser.TryParse(Trimmed, out SequenceMessageParseResult Message))
        {
            Item = Relationship(LineNumber, Message.SourceId, Message.TargetId, Message.Label ?? string.Empty, "message");
            return true;
        }

        Match Note = NoteRegex.Match(Trimmed);
        if (Note.Success)
        {
            string Name = Note.Groups["id"].Success ? Note.Groups["id"].Value : Note.Groups["ids"].Value.Trim();
            Item = Node(LineNumber, Name, Note.Groups["label"].Value.Trim(), "note");
            return true;
        }

        return false;
    }
}
