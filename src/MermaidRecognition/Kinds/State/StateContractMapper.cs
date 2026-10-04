using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Class;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Maps classified state diagram source statements into <see cref="DiagramContract"/> entities (L3 G4/G6/G8/G9).
/// </summary>
public static class StateContractMapper
{
    private static readonly Regex ClassDefRegex = new(
        @"^\s*classDef\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleRegex = new(
        @"^\s*style\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CssClassRegex = new(
        @"^\s*class\s+([^\s]+)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Populates nodes, edges, composite groups, styles, and accessibility on an L2-classified state diagram contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        StateDb Database = new StateDb();
        string? PendingCompositeId = null;
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
                else
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

            if (LineTypeId.EndsWith(".direction", StringComparison.Ordinal))
            {
                if (MermaidDirectionParser.TryParse(RawLine, out string Direction))
                {
                    Contract.Direction = Direction;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".state", StringComparison.Ordinal))
            {
                if (StateDeclarationParser.TryParse(RawLine, out StateDeclarationParseResult Declaration))
                {
                    if (Declaration.OpensComposite)
                    {
                        Database.PushComposite(Declaration.StateId);
                        PendingCompositeId = null;
                    }
                    else
                    {
                        StateNodeRecord State = Database.GetOrCreateState(Declaration.StateId);
                        if (Declaration.Label is not null)
                        {
                            State.Label = Declaration.Label;
                        }

                        if (Declaration.StateType is not null)
                        {
                            Database.SetStateType(Declaration.StateId, Declaration.StateType);
                        }

                        PendingCompositeId = Declaration.StateId;
                    }
                }

                continue;
            }

            if (LineTypeId.EndsWith(".structOpen", StringComparison.Ordinal))
            {
                if (PendingCompositeId is not null)
                {
                    Database.PushComposite(PendingCompositeId);
                    PendingCompositeId = null;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".structClose", StringComparison.Ordinal))
            {
                Database.PopComposite();
                PendingCompositeId = null;
                continue;
            }

            if (LineTypeId.EndsWith(".transition", StringComparison.Ordinal))
            {
                if (StateTransitionParser.TryParse(RawLine, out StateTransitionParseResult Transition))
                {
                    Database.AddTransition(Transition.SourceId, Transition.TargetId, Transition.Label);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".note", StringComparison.Ordinal))
            {
                StateNoteParser.TryParse(RawLine, Database);
                continue;
            }

            if (LineTypeId.EndsWith(".classDef", StringComparison.Ordinal))
            {
                Match Match = ClassDefRegex.Match(RawLine);
                if (Match.Success)
                {
                    Database.RegisterClassDef(Match.Groups[1].Value, FlowchartDirectiveParser.ParseStyleList(Match.Groups[2].Value));
                }

                continue;
            }

            if (LineTypeId.EndsWith(".style", StringComparison.Ordinal))
            {
                Match Match = StyleRegex.Match(RawLine);
                if (Match.Success)
                {
                    IEnumerable<string> Targets = SplitTargetIds(Match.Groups[1].Value);
                    Database.ApplyInlineStyleToTargets(Targets, FlowchartDirectiveParser.ParseStyleList(Match.Groups[2].Value));
                }

                continue;
            }

            if (LineTypeId.EndsWith(".cssClass", StringComparison.Ordinal))
            {
                Match Match = CssClassRegex.Match(RawLine);
                if (Match.Success)
                {
                    IEnumerable<string> Targets = SplitTargetIds(Match.Groups[1].Value);
                    Database.ApplyClassToTargets(Targets, Match.Groups[2].Value);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".click", StringComparison.Ordinal))
            {
                StateClickParser.TryParse(RawLine, Statement.LineNumber, Database);
            }
        }

        MapToContract(Database, Contract);
    }

    static IEnumerable<string> SplitTargetIds(string TargetList) => TargetList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static void MapToContract(StateDb Database, DiagramContract Contract)
    {
        foreach (KeyValuePair<string, StateNodeRecord> Entry in Database.StatesById)
        {
            StateNodeRecord Source = Entry.Value;
            NodeContract Node = new NodeContract
            {
                Id = Source.Id,
                Label = Source.Label,
                ParentGroupId = Source.ParentGroupId
            };

            if (Source.StateType is not null)
            {
                Node.Shape = Source.StateType;
                Node.MetadataMutable["stateType"] = new RawMetadata("stateType", Source.StateType);
            }

            foreach (string ClassId in Source.ClassesMutable)
            {
                Node.ClassesMutable.Add(ClassId);
            }

            RecognitionStyleMerge.Apply(Source.InlineStyle, Node.InlineStyle);
            Contract.NodesMutable.Add(Node);
        }

        foreach (StateNoteRecord Source in Database.Notes)
        {
            NodeContract NoteNode = new NodeContract
            {
                Id = Source.Id,
                Label = Source.Text,
                Shape = "note"
            };
            NoteNode.MetadataMutable["stateNotePlacement"] = new RawMetadata(
                "stateNotePlacement",
                Source.Placement);
            NoteNode.MetadataMutable["stateNoteAnchor"] = new RawMetadata(
                "stateNoteAnchor",
                Source.AnchorIds);
            Contract.NodesMutable.Add(NoteNode);
        }

        foreach (StateEdgeRecord Source in Database.Edges)
        {
            EdgeContract Edge = new EdgeContract
            {
                SourceId = Source.SourceId,
                TargetId = Source.TargetId,
                Label = Source.Label,
                CreationIndex = Source.CreationIndex,
                Id = GenerateEdgeId(Source.SourceId, Source.TargetId, Source.CreationIndex)
            };
            Contract.EdgesMutable.Add(Edge);
        }

        foreach (KeyValuePair<string, StateGroupRecord> Entry in Database.GroupsById)
        {
            StateGroupRecord Source = Entry.Value;
            GroupContract Group = new GroupContract
            {
                Id = Source.Id,
                Title = Source.Id,
                ParentGroupId = Source.ParentGroupId
            };
            foreach (string ChildNodeId in Source.ChildNodeIdsMutable)
            {
                Group.ChildNodeIdsMutable.Add(ChildNodeId);
            }

            Contract.GroupsMutable.Add(Group);
        }

        foreach (KeyValuePair<string, ClassDefContract> Entry in Database.ClassDefsById)
        {
            ClassDefContract Copy = new ClassDefContract
            {
                ClassId = Entry.Value.ClassId
            };
            RecognitionStyleMerge.Apply(Entry.Value.Style, Copy.Style);
            Contract.ClassDefsMutable.Add(Copy);
        }

        Contract.Accessibility.Title = Database.AccessibilityTitle;
        Contract.Accessibility.Description = Database.AccessibilityDescription;

        foreach (InteractionDirective Interaction in Database.InteractionsMutable)
        {
            Contract.InteractionsMutable.Add(Interaction);
        }

        foreach (MermaidDiagnostic Diagnostic in Database.DiagnosticsMutable)
        {
            Contract.DiagnosticsMutable.Add(Diagnostic);
        }
    }

    static string GenerateEdgeId(string SourceId, string TargetId, int CreationIndex) => "L-" + SourceId + "-" + TargetId + "-" + CreationIndex.ToString();

    enum AccDescrParseState
    {
        None,
        Multiline
    }
}
