using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition.Class;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Er;

/// <summary>
/// Maps classified ER diagram source statements into <see cref="DiagramContract"/> entities (L3 G4/G6/G9).
/// </summary>
public static class ErContractMapper
{
    private static readonly Regex ClassDefRegex = new(
        @"^\s*classDef\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleRegex = new(
        @"^\s*style\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassApplyRegex = new(
        @"^\s*class\s+(\S+)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Populates entity nodes, attribute declarations, relationship edges, styles, and accessibility on an L2-classified ER contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        ErDb Database = new ErDb();
        string? CurrentEntityId = null;
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

            if (LineTypeId.EndsWith(".relationship", StringComparison.Ordinal))
            {
                if (ErRelationshipParser.TryParse(RawLine, out ErRelationshipParseResult Relationship))
                {
                    Database.AddRelationship(
                        Relationship.SourceId,
                        Relationship.TargetId,
                        Relationship.LeftMarker,
                        Relationship.Link,
                        Relationship.RightMarker,
                        Relationship.RoleLabel);
                }

                continue;
            }

            if (LineTypeId.EndsWith(".entityBlockOpen", StringComparison.Ordinal))
            {
                if (ErEntityBlockParser.TryParseEntityOpen(RawLine, out string EntityId))
                {
                    Database.GetOrCreateEntity(EntityId);
                    CurrentEntityId = EntityId;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".entityBlockClose", StringComparison.Ordinal))
            {
                CurrentEntityId = null;
                continue;
            }

            if (LineTypeId.EndsWith(".attribute", StringComparison.Ordinal))
            {
                if (CurrentEntityId is not null)
                {
                    ErNodeRecord Entity = Database.GetOrCreateEntity(CurrentEntityId);
                    MermaidDeclaration Declaration = new MermaidDeclaration(
                        "attribute",
                        RawLine.Trim(),
                        SourceSpan);
                    Entity.DeclarationsMutable.Add(Declaration);
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

            if (LineTypeId.EndsWith(".class", StringComparison.Ordinal))
            {
                Match Match = ClassApplyRegex.Match(RawLine);
                if (Match.Success)
                {
                    Database.ApplyClassToTargets(new[] { Match.Groups[1].Value }, Match.Groups[2].Value);
                }
            }
        }

        MapToContract(Database, Contract);
    }

    static IEnumerable<string> SplitTargetIds(string TargetList)
    {
        return TargetList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    static void MapToContract(ErDb Database, DiagramContract Contract)
    {
        foreach (KeyValuePair<string, ErNodeRecord> Entry in Database.EntitiesById)
        {
            ErNodeRecord Source = Entry.Value;
            NodeContract Node = new NodeContract();
            Node.Id = Source.Id;
            Node.Label = Source.Label;
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

        foreach (ErEdgeRecord Source in Database.Edges)
        {
            EdgeContract Edge = new EdgeContract();
            Edge.SourceId = Source.SourceId;
            Edge.TargetId = Source.TargetId;
            Edge.CreationIndex = Source.CreationIndex;
            Edge.Id = GenerateEdgeId(Source.SourceId, Source.TargetId, Source.CreationIndex);
            Edge.Label = Source.RoleLabel;
            Edge.MetadataMutable["erLeftMarker"] = new RawMetadata("erLeftMarker", Source.LeftMarker);
            Edge.MetadataMutable["erLink"] = new RawMetadata("erLink", Source.Link);
            Edge.MetadataMutable["erRightMarker"] = new RawMetadata("erRightMarker", Source.RightMarker);
            if (Source.RoleLabel is not null)
            {
                Edge.MetadataMutable["erRoleLabel"] = new RawMetadata("erRoleLabel", Source.RoleLabel);
            }

            Contract.EdgesMutable.Add(Edge);
        }

        foreach (KeyValuePair<string, ClassDefContract> Entry in Database.ClassDefsById)
        {
            ClassDefContract Copy = new ClassDefContract();
            Copy.ClassId = Entry.Value.ClassId;
            RecognitionStyleMerge.Apply(Entry.Value.Style, Copy.Style);
            Contract.ClassDefsMutable.Add(Copy);
        }

        Contract.Accessibility.Title = Database.AccessibilityTitle;
        Contract.Accessibility.Description = Database.AccessibilityDescription;
    }

    static string GenerateEdgeId(string SourceId, string TargetId, int CreationIndex)
    {
        return "L-" + SourceId + "-" + TargetId + "-" + CreationIndex.ToString();
    }

    enum AccDescrParseState
    {
        None,
        Multiline
    }
}
