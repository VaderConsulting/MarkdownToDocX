using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Class;

namespace MermaidRecognition.Kinds.Sequence;

/// <summary>
/// Maps classified sequence diagram source statements into <see cref="DiagramContract"/> entities (L3 G4/G5/G7/G9).
/// </summary>
public static class SequenceContractMapper
{
    /// <summary>
    /// Populates participants, messages, notes, control frames, and accessibility on an L2-classified sequence diagram contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        SequenceDb Database = new SequenceDb();
        AccDescrParseState AccState = AccDescrParseState.None;
        StringBuilder AccDescrBuilder = new StringBuilder();

        foreach (SourceStatementContract Statement in Contract.SourceStatements)
        {
            string LineTypeId = Statement.LineTypeId;
            string RawLine = Statement.RawLine;

            if (AccState == AccDescrParseState.Multiline)
            {
                if (LineTypeId.EndsWith(".structClose", StringComparison.Ordinal))
                {
                    Database.AccessibilityDescription = AccDescrBuilder.ToString().Trim();
                    AccState = AccDescrParseState.None;
                    AccDescrBuilder.Clear();
                }
                else if (LineTypeId.EndsWith(".body", StringComparison.Ordinal))
                {
                    if (AccDescrBuilder.Length > 0)
                    {
                        AccDescrBuilder.AppendLine();
                    }

                    AccDescrBuilder.Append(RawLine.Trim());
                }

                continue;
            }

            if (LineTypeId.EndsWith(".accTitle", StringComparison.Ordinal))
            {
                if (ClassAccessibilityParser.TryParseAccTitle(RawLine, out string Title))
                {
                    Database.AccessibilityTitle = Title;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".accDescr", StringComparison.Ordinal))
            {
                if (ClassAccessibilityParser.IsAccDescrBlockStart(RawLine))
                {
                    AccState = AccDescrParseState.Multiline;
                    AccDescrBuilder.Clear();
                }
                else if (ClassAccessibilityParser.TryParseAccDescrInline(RawLine, out string Description))
                {
                    Database.AccessibilityDescription = Description;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".title", StringComparison.Ordinal))
            {
                if (SequenceTitleParser.TryParse(RawLine, out string Title))
                {
                    Contract.Configuration.RawDirectiveValuesMutable["sequence.title"] = Title;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".autonumber", StringComparison.Ordinal))
            {
                Contract.Configuration.RawDirectiveValuesMutable["sequence.autonumber"] = "true";
                continue;
            }

            if (LineTypeId.EndsWith(".note", StringComparison.Ordinal))
            {
                SequenceNoteParser.TryParse(RawLine, Statement.LineNumber, Database);
                continue;
            }

            if (LineTypeId.EndsWith(".participant", StringComparison.Ordinal)
                || LineTypeId.EndsWith(".lifecycle", StringComparison.Ordinal))
            {
                if (SequenceParticipantParser.TryParse(RawLine, out SequenceParticipantParseResult Participant))
                {
                    Database.GetOrCreateParticipant(
                        Participant.Id,
                        Participant.Label,
                        Participant.DrawType);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".message", StringComparison.Ordinal))
            {
                if (SequenceMessageParser.TryParse(RawLine, out SequenceMessageParseResult Message))
                {
                    Database.AddMessage(
                        Message.SourceId,
                        Message.TargetId,
                        Message.Label,
                        Message.ArrowToken,
                        Message.ActivateTargetId,
                        Message.DeactivateSourceId);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".activate", StringComparison.Ordinal))
            {
                if (SequenceActivateParser.TryParseActivate(RawLine, out string ActivateId))
                {
                    Database.ActivateParticipant(ActivateId);
                }
                else if (SequenceActivateParser.TryParseDeactivate(RawLine, out string DeactivateId))
                {
                    Database.DeactivateParticipant(DeactivateId);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".block", StringComparison.Ordinal))
            {
                if (SequenceBlockParser.TryParse(RawLine, out SequenceBlockParseResult Block))
                {
                    Database.OpenBlock(Block);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".box", StringComparison.Ordinal))
            {
                if (SequenceBoxParser.TryParse(RawLine, out SequenceBoxParseResult Box))
                {
                    Database.OpenBox(Box);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".end", StringComparison.Ordinal))
            {
                Database.CloseBlock();
            }
        }

        MapToContract(Database, Contract);
    }

    static void MapToContract(SequenceDb Database, DiagramContract Contract)
    {
        foreach (KeyValuePair<string, SequenceNodeRecord> Entry in Database.ParticipantsById)
        {
            SequenceNodeRecord Source = Entry.Value;
            NodeContract Node = new NodeContract
            {
                Id = Source.Id,
                Label = Source.Label,
                Shape = Source.DrawType,
                ShapeSource = "sequence",
                ParentGroupId = Source.ParentGroupId
            };

            if (Source.IsActive.HasValue)
            {
                Node.MetadataMutable["sequenceActive"] = new RawMetadata(
                    "sequenceActive",
                    Source.IsActive.Value ? "true" : "false");
            }

            Contract.NodesMutable.Add(Node);
        }

        foreach (SequenceNoteRecord Source in Database.Notes)
        {
            NodeContract NoteNode = new NodeContract
            {
                Id = Source.Id,
                Label = Source.Text,
                Shape = "note",
                ShapeSource = "sequence"
            };
            NoteNode.MetadataMutable["sequenceNotePlacement"] = new RawMetadata(
                "sequenceNotePlacement",
                Source.Placement);
            NoteNode.MetadataMutable["sequenceNoteAnchor"] = new RawMetadata(
                "sequenceNoteAnchor",
                Source.AnchorIds);
            Contract.NodesMutable.Add(NoteNode);
        }

        foreach (SequenceEdgeRecord Source in Database.Messages)
        {
            EdgeContract Edge = new EdgeContract
            {
                SourceId = Source.SourceId,
                TargetId = Source.TargetId,
                Label = Source.Label,
                CreationIndex = Source.CreationIndex,
                Id = GenerateEdgeId(Source.SourceId, Source.TargetId, Source.CreationIndex)
            };
            Edge.MetadataMutable["sequenceArrow"] = new RawMetadata("sequenceArrow", Source.ArrowToken);

            if (Source.ActivateTargetId is not null)
            {
                Edge.MetadataMutable["sequenceActivateTarget"] = new RawMetadata(
                    "sequenceActivateTarget",
                    Source.ActivateTargetId);
            }

            if (Source.DeactivateSourceId is not null)
            {
                Edge.MetadataMutable["sequenceDeactivateSource"] = new RawMetadata(
                    "sequenceDeactivateSource",
                    Source.DeactivateSourceId);
            }

            if (Source.ParentGroupId is not null)
            {
                Edge.MetadataMutable["sequenceParentGroup"] = new RawMetadata(
                    "sequenceParentGroup",
                    Source.ParentGroupId);
            }

            Contract.EdgesMutable.Add(Edge);
        }

        foreach (KeyValuePair<string, SequenceGroupRecord> Entry in Database.GroupsById)
        {
            SequenceGroupRecord Source = Entry.Value;
            GroupContract Group = new GroupContract
            {
                Id = Source.Id,
                Title = Source.Title ?? Source.BlockKeyword,
                ParentGroupId = Source.ParentGroupId
            };

            foreach (string ChildNodeId in Source.ChildNodeIdsMutable)
            {
                Group.ChildNodeIdsMutable.Add(ChildNodeId);
            }

            foreach (string ChildGroupId in Source.ChildGroupIdsMutable)
            {
                Group.ChildGroupIdsMutable.Add(ChildGroupId);
            }

            Contract.GroupsMutable.Add(Group);
        }

        Contract.Accessibility.Title = Database.AccessibilityTitle;
        Contract.Accessibility.Description = Database.AccessibilityDescription;
    }

    static string GenerateEdgeId(string SourceId, string TargetId, int CreationIndex) => "L-" + SourceId + "-" + TargetId + "-" + CreationIndex.ToString();

    enum AccDescrParseState
    {
        None,
        Multiline
    }
}
