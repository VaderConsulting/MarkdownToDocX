using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Kinds.Class;

/// <summary>
/// Maps classified class diagram source statements into <see cref="DiagramContract"/> entities (L3 G4/G5/G6/G9).
/// </summary>
public static class ClassContractMapper
{
    private static readonly Regex ClassWithBraceRegex = new(
        @"^\s*class\s+(\S+)\s*\{\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassApplyRegex = new(
        @"^\s*class\s+(\S+)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassSimpleRegex = new(
        @"^\s*class\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassDefRegex = new(
        @"^\s*classDef\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleRegex = new(
        @"^\s*style\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Populates nodes, edges, groups, accessibility, and class definitions on an L2-classified class diagram contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        ClassDb Database = new ClassDb();
        string? CurrentClassId = null;
        string? PendingClassId = null;
        string? PendingNamespaceId = null;
        string? PendingNamespaceTitle = null;
        AccDescrParseState AccState = AccDescrParseState.None;
        StringBuilder AccDescrBuilder = new StringBuilder();

        foreach (SourceStatementContract Statement in Contract.SourceStatements)
        {
            string LineTypeId = Statement.LineTypeId;
            string RawLine = Statement.RawLine;
            int LineNumber = Statement.LineNumber;
            MermaidSourceSpan SourceSpan = new MermaidSourceSpan(0, 0, LineNumber, 0, LineNumber, RawLine.Length);

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

            if (LineTypeId.EndsWith(".direction", StringComparison.Ordinal))
            {
                if (MermaidDirectionParser.TryParse(RawLine, out string Direction))
                {
                    Contract.Direction = Direction;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".namespace", StringComparison.Ordinal))
            {
                if (ClassNamespaceParser.TryParse(RawLine, out string NamespaceId, out string? Title, out bool OpensBlock))
                {
                    if (OpensBlock)
                    {
                        Database.PushNamespace(NamespaceId, Title);
                        PendingNamespaceId = null;
                        PendingNamespaceTitle = null;
                    }
                    else
                    {
                        PendingNamespaceId = NamespaceId;
                        PendingNamespaceTitle = Title;
                    }
                }

                continue;
            }

            if (LineTypeId.EndsWith(".class", StringComparison.Ordinal))
            {
                ProcessClassLine(Database, RawLine, ref CurrentClassId, ref PendingClassId);
                continue;
            }

            if (LineTypeId.EndsWith(".structOpen", StringComparison.Ordinal))
            {
                if (PendingNamespaceId is not null)
                {
                    Database.PushNamespace(PendingNamespaceId, PendingNamespaceTitle);
                    PendingNamespaceId = null;
                    PendingNamespaceTitle = null;
                    continue;
                }

                if (PendingClassId is not null)
                {
                    CurrentClassId = PendingClassId;
                    PendingClassId = null;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".structClose", StringComparison.Ordinal))
            {
                if (CurrentClassId is not null)
                {
                    CurrentClassId = null;
                    PendingClassId = null;
                    continue;
                }

                Database.PopNamespace();
                PendingNamespaceId = null;
                PendingNamespaceTitle = null;
                continue;
            }

            if (LineTypeId.EndsWith(".member", StringComparison.Ordinal))
            {
                if (CurrentClassId is not null)
                {
                    Database.AddMember(CurrentClassId, RawLine.Trim(), SourceSpan);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".annotation", StringComparison.Ordinal))
            {
                if (CurrentClassId is not null)
                {
                    Database.AddStereotype(CurrentClassId, RawLine.Trim(), SourceSpan);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".relation", StringComparison.Ordinal))
            {
                if (ClassRelationParser.TryParse(RawLine, out ClassRelationParseResult Relation))
                {
                    Database.AddRelation(
                        Relation.SourceId,
                        Relation.TargetId,
                        Relation.RelationToken,
                        Relation.SourceCardinality,
                        Relation.TargetCardinality,
                        Relation.RelationLabel);
                }

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

            if (LineTypeId.EndsWith(".note", StringComparison.Ordinal))
            {
                ClassNoteParser.TryParse(RawLine, LineNumber, Database);
                continue;
            }

            if (LineTypeId.EndsWith(".click", StringComparison.Ordinal))
            {
                ClassClickParser.TryParse(RawLine, LineNumber, Database);
            }
        }

        MapToContract(Database, Contract);
    }

    static IEnumerable<string> SplitTargetIds(string TargetList) => TargetList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static void ProcessClassLine(
        ClassDb Database,
        string RawLine,
        ref string? CurrentClassId,
        ref string? PendingClassId)
    {
        Match BraceMatch = ClassWithBraceRegex.Match(RawLine);
        if (BraceMatch.Success)
        {
            string ClassId = BraceMatch.Groups[1].Value;
            Database.GetOrCreateClass(ClassId);
            CurrentClassId = ClassId;
            PendingClassId = null;
            return;
        }

        Match ApplyMatch = ClassApplyRegex.Match(RawLine);
        if (ApplyMatch.Success)
        {
            string ClassId = ApplyMatch.Groups[1].Value;
            string StyleClassId = ApplyMatch.Groups[2].Value;
            Database.ApplyClassToTargets([ClassId], StyleClassId);
            PendingClassId = null;
            CurrentClassId = null;
            return;
        }

        Match SimpleMatch = ClassSimpleRegex.Match(RawLine);
        if (SimpleMatch.Success)
        {
            string ClassId = SimpleMatch.Groups[1].Value;
            Database.GetOrCreateClass(ClassId);
            PendingClassId = ClassId;
            CurrentClassId = null;
        }
    }

    static void MapToContract(ClassDb Database, DiagramContract Contract)
    {
        foreach (KeyValuePair<string, ClassNodeRecord> Entry in Database.ClassesById)
        {
            ClassNodeRecord Source = Entry.Value;
            NodeContract Node = new NodeContract
            {
                Id = Source.Id,
                Label = Source.Label,
                ParentGroupId = Source.ParentNamespaceId
            };
            foreach (MermaidDeclaration Declaration in Source.DeclarationsMutable)
            {
                Node.DeclarationsMutable.Add(Declaration);
            }

            foreach (string ClassId in Source.ClassesMutable)
            {
                Node.ClassesMutable.Add(ClassId);
            }

            RecognitionStyleMerge.Apply(Source.InlineStyle, Node.InlineStyle);
            Contract.NodesMutable.Add(Node);
        }

        foreach (KeyValuePair<string, ClassNamespaceRecord> Entry in Database.NamespacesById)
        {
            ClassNamespaceRecord Source = Entry.Value;
            GroupContract Group = new GroupContract
            {
                Id = Source.Id,
                Title = Source.Title,
                ParentGroupId = Source.ParentNamespaceId
            };
            foreach (string NodeId in Source.ChildNodeIdsMutable)
            {
                Group.ChildNodeIdsMutable.Add(NodeId);
            }

            foreach (string ChildNamespaceId in Source.ChildNamespaceIdsMutable)
            {
                Group.ChildGroupIdsMutable.Add(ChildNamespaceId);
            }

            Contract.GroupsMutable.Add(Group);
        }

        foreach (ClassEdgeRecord Source in Database.Edges)
        {
            EdgeContract Edge = new EdgeContract
            {
                SourceId = Source.SourceId,
                TargetId = Source.TargetId,
                CreationIndex = Source.CreationIndex,
                Id = GenerateEdgeId(Source.SourceId, Source.TargetId, Source.CreationIndex),
                Label = Source.RelationLabel
            };
            Edge.MetadataMutable["classRelation"] = new RawMetadata("classRelation", Source.RelationToken);
            if (Source.SourceCardinality is not null)
            {
                Edge.MetadataMutable["classSourceCardinality"] = new RawMetadata("classSourceCardinality", Source.SourceCardinality);
            }

            if (Source.TargetCardinality is not null)
            {
                Edge.MetadataMutable["classTargetCardinality"] = new RawMetadata("classTargetCardinality", Source.TargetCardinality);
            }

            Contract.EdgesMutable.Add(Edge);
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
