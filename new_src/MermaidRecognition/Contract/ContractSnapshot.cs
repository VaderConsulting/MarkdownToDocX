using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MermaidRecognition.Contract;

/// <summary>
/// JSON-serialisable projection of <see cref="DiagramContract"/> for verified fixture tests.
/// </summary>
public sealed class ContractSnapshot
{
    public static ContractSnapshot FromDiagram(DiagramContract Contract)
    {
        ContractSnapshot Snapshot = new ContractSnapshot();
        Snapshot.Kind = Contract.Kind;
        Snapshot.Direction = Contract.Direction;
        if (Contract.RemarkStatus != MermaidDiagramRemarkStatus.Active)
        {
            Snapshot.RemarkStatus = Contract.RemarkStatus.ToString();
        }

        Snapshot.Nodes = Contract.Nodes
            .Select(
                Node => new NodeSnapshot
                {
                    Id = Node.Id,
                    Label = Node.Label,
                    LabelType = Node.LabelType.ToString(),
                    Shape = Node.Shape,
                    ShapeSource = Node.ShapeSource,
                    ParentGroupId = Node.ParentGroupId,
                    MetadataShape = Node.Metadata.Values
                        .OfType<ShapeMetadata>()
                        .Select(Shape => Shape.CanonicalShapeId)
                        .FirstOrDefault(),
                    SequenceActive = Node.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "sequenceActive", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    SequenceNotePlacement = Node.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "sequenceNotePlacement", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    SequenceNoteAnchor = Node.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "sequenceNoteAnchor", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    StateNotePlacement = Node.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "stateNotePlacement", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    StateNoteAnchor = Node.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "stateNoteAnchor", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    PieValue = Node.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "pieValue", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    Classes = Node.Classes.OrderBy(ClassId => ClassId, StringComparer.Ordinal).ToList(),
                    InlineStroke = Node.InlineStyle.Stroke,
                    Members = Node.Declarations
                        .Where(Declaration => string.Equals(Declaration.Kind, "member", StringComparison.Ordinal))
                        .Select(Declaration => Declaration.RawText.Trim())
                        .ToList(),
                    Attributes = Node.Declarations
                        .Where(Declaration => string.Equals(Declaration.Kind, "attribute", StringComparison.Ordinal))
                        .Select(Declaration => Declaration.RawText.Trim())
                        .ToList()
                })
            .OrderBy(Node => Node.Id, StringComparer.Ordinal)
            .ToList();
        Snapshot.Edges = Contract.Edges
            .Select(
                Edge => new EdgeSnapshot
                {
                    CreationIndex = Edge.CreationIndex,
                    Id = Edge.Id,
                    IsUserDefinedId = Edge.IsUserDefinedId,
                    SourceId = Edge.SourceId,
                    TargetId = Edge.TargetId,
                    MinLength = Edge.MinLength,
                    Stroke = Edge.Stroke,
                    MarkerStart = Edge.MarkerStart.ToString(),
                    MarkerEnd = Edge.MarkerEnd.ToString(),
                    Label = Edge.Label,
                    Curve = Edge.Curve ?? Edge.Metadata.Values
                        .OfType<CurveMetadata>()
                        .Select(Entry => Entry.Curve)
                        .FirstOrDefault(),
                    MetadataCurve = Edge.Metadata.Values
                        .OfType<CurveMetadata>()
                        .Select(Entry => Entry.Curve)
                        .FirstOrDefault(),
                    ClassRelation = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "classRelation", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    ClassSourceCardinality = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "classSourceCardinality", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    ClassTargetCardinality = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "classTargetCardinality", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    SequenceArrow = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "sequenceArrow", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    SequenceActivateTarget = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "sequenceActivateTarget", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    SequenceDeactivateSource = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "sequenceDeactivateSource", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    ErLeftMarker = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "erLeftMarker", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    ErLink = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "erLink", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    ErRightMarker = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "erRightMarker", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    ErRoleLabel = Edge.Metadata.Values
                        .OfType<RawMetadata>()
                        .Where(Entry => string.Equals(Entry.Key, "erRoleLabel", StringComparison.Ordinal))
                        .Select(Entry => Entry.RawValue)
                        .FirstOrDefault(),
                    MetadataAnimate = Edge.Metadata.Values
                        .OfType<AnimationMetadata>()
                        .Select(Entry => Entry.Animate)
                        .FirstOrDefault(),
                    MetadataAnimation = Edge.Metadata.Values
                        .OfType<AnimationMetadata>()
                        .Select(Entry => Entry.Animation)
                        .FirstOrDefault(),
                    Classes = Edge.Classes.OrderBy(ClassId => ClassId, StringComparer.Ordinal).ToList()
                })
            .OrderBy(Edge => Edge.CreationIndex)
            .ToList();
        if (Contract.Groups.Count == 0)
        {
            Snapshot.Groups = new List<GroupSnapshot>();
        }
        else
        {
            Snapshot.Groups = Contract.Groups
            .Select(
                Group => new GroupSnapshot
                {
                    Id = Group.Id,
                    Title = Group.Title,
                    ParentGroupId = Group.ParentGroupId,
                    Direction = Group.Direction,
                    ChildNodeIds = Group.ChildNodeIds.OrderBy(Id => Id, StringComparer.Ordinal).ToList(),
                    ChildGroupIds = Group.ChildGroupIds.OrderBy(Id => Id, StringComparer.Ordinal).ToList()
                })
            .OrderBy(Group => Group.Id, StringComparer.Ordinal)
            .ToList();
        }

        if (Contract.LinkStyles.Count == 0)
        {
            Snapshot.LinkStyles = new List<LinkStyleSnapshot>();
        }
        else
        {
            Snapshot.LinkStyles = Contract.LinkStyles
            .Select(
                LinkStyle => new LinkStyleSnapshot
                {
                    IsDefault = LinkStyle.IsDefault,
                    EdgeIndexes = LinkStyle.EdgeIndexes.ToList(),
                    Stroke = LinkStyle.Style.Stroke,
                    Curve = LinkStyle.Curve
                })
            .ToList();
        }

        if (Contract.Configuration.RawDirectiveValues.Count > 0)
        {
            Snapshot.Config = new ConfigSnapshot
            {
                Raw = Contract.Configuration.RawDirectiveValues
                    .OrderBy(Entry => Entry.Key, StringComparer.Ordinal)
                    .ToDictionary(Entry => Entry.Key, Entry => Entry.Value, StringComparer.Ordinal)
            };
        }

        if (Contract.ClassDefs.Count > 0)
        {
            Snapshot.ClassDefs = Contract.ClassDefs
                .Select(
                    ClassDef => new ClassDefSnapshot
                    {
                        ClassId = ClassDef.ClassId,
                        Stroke = ClassDef.Style.Stroke,
                        Animation = ClassDef.Style.Animation
                    })
                .OrderBy(ClassDef => ClassDef.ClassId, StringComparer.Ordinal)
                .ToList();
        }

        if (Contract.Accessibility.Title is not null || Contract.Accessibility.Description is not null)
        {
            Snapshot.Accessibility = new AccessibilitySnapshot
            {
                Title = Contract.Accessibility.Title,
                Description = Contract.Accessibility.Description
            };
        }

        if (Contract.Interactions.Count > 0)
        {
            Snapshot.Interactions = Contract.Interactions
                .Select(
                    Interaction => new InteractionSnapshot
                    {
                        TargetId = Interaction.TargetId,
                        Callback = Interaction.Callback,
                        Url = Interaction.Url
                    })
                .OrderBy(Interaction => Interaction.TargetId, StringComparer.Ordinal)
                .ToList();
        }

        if (Contract.Diagnostics.Count > 0)
        {
            Snapshot.Diagnostics = Contract.Diagnostics
                .Select(
                    Diagnostic => new DiagnosticSnapshot
                    {
                        Severity = Diagnostic.Severity.ToString(),
                        Code = Diagnostic.Code,
                        AffectedId = Diagnostic.AffectedId
                    })
                .OrderBy(Diagnostic => Diagnostic.Code, StringComparer.Ordinal)
                .ToList();
        }

        if (Contract.SourceStatements.Count > 0)
        {
            Snapshot.SourceStatements = Contract.SourceStatements
                .Select(
                    Statement => new SourceStatementSnapshot
                    {
                        LineNumber = Statement.LineNumber,
                        LineTypeId = Statement.LineTypeId,
                        RawLine = Statement.RawLine
                    })
                .ToList();
        }

        return Snapshot;
    }

    public string Kind { get; set; } = string.Empty;

    public string? Direction { get; set; }

    public string? RemarkStatus { get; set; }

    public List<NodeSnapshot> Nodes { get; set; } = new List<NodeSnapshot>();

    public List<EdgeSnapshot> Edges { get; set; } = new List<EdgeSnapshot>();

    public List<GroupSnapshot> Groups { get; set; } = new List<GroupSnapshot>();

    public List<LinkStyleSnapshot> LinkStyles { get; set; } = new List<LinkStyleSnapshot>();

    public ConfigSnapshot? Config { get; set; }

    public List<ClassDefSnapshot> ClassDefs { get; set; } = new List<ClassDefSnapshot>();

    public AccessibilitySnapshot? Accessibility { get; set; }

    public List<InteractionSnapshot> Interactions { get; set; } = new List<InteractionSnapshot>();

    public List<DiagnosticSnapshot> Diagnostics { get; set; } = new List<DiagnosticSnapshot>();

    public List<SourceStatementSnapshot> SourceStatements { get; set; } = new List<SourceStatementSnapshot>();

    public string ToJson()
    {
        JsonSerializerOptions Options = new JsonSerializerOptions();
        Options.WriteIndented = true;
        Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        Options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        return JsonSerializer.Serialize(this, Options);
    }

    public static ContractSnapshot FromJson(string Json)
    {
        JsonSerializerOptions Options = new JsonSerializerOptions();
        Options.PropertyNameCaseInsensitive = true;
        return JsonSerializer.Deserialize<ContractSnapshot>(Json, Options)
            ?? throw new InvalidOperationException("Invalid contract snapshot JSON.");
    }
}

public sealed class NodeSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string LabelType { get; set; } = "Text";

    public string? Shape { get; set; }

    public string? ShapeSource { get; set; }

    public string? ParentGroupId { get; set; }

    public string? MetadataShape { get; set; }

    public string? SequenceActive { get; set; }

    public string? SequenceNotePlacement { get; set; }

    public string? SequenceNoteAnchor { get; set; }

    public string? StateNotePlacement { get; set; }

    public string? StateNoteAnchor { get; set; }

    public string? PieValue { get; set; }

    public List<string> Classes { get; set; } = new List<string>();

    public string? InlineStroke { get; set; }

    public List<string> Members { get; set; } = new List<string>();

    public List<string> Attributes { get; set; } = new List<string>();
}

public sealed class EdgeSnapshot
{
    public int CreationIndex { get; set; }

    public string? Id { get; set; }

    public bool IsUserDefinedId { get; set; }

    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public int MinLength { get; set; }

    public string? Stroke { get; set; }

    public string MarkerStart { get; set; } = "None";

    public string MarkerEnd { get; set; } = "Point";

    public string? Label { get; set; }

    public string? Curve { get; set; }

    public string? MetadataCurve { get; set; }

    public string? ClassRelation { get; set; }

    public string? ClassSourceCardinality { get; set; }

    public string? ClassTargetCardinality { get; set; }

    public string? SequenceArrow { get; set; }

    public string? SequenceActivateTarget { get; set; }

    public string? SequenceDeactivateSource { get; set; }

    public string? ErLeftMarker { get; set; }

    public string? ErLink { get; set; }

    public string? ErRightMarker { get; set; }

    public string? ErRoleLabel { get; set; }

    public bool? MetadataAnimate { get; set; }

    public string? MetadataAnimation { get; set; }

    public List<string> Classes { get; set; } = new List<string>();
}

public sealed class SourceStatementSnapshot
{
    public int LineNumber { get; set; }

    public string LineTypeId { get; set; } = string.Empty;

    public string RawLine { get; set; } = string.Empty;
}

public sealed class GroupSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? ParentGroupId { get; set; }

    public string? Direction { get; set; }

    public List<string> ChildNodeIds { get; set; } = new List<string>();

    public List<string> ChildGroupIds { get; set; } = new List<string>();
}

public sealed class LinkStyleSnapshot
{
    public bool IsDefault { get; set; }

    public List<int> EdgeIndexes { get; set; } = new List<int>();

    public string? Stroke { get; set; }

    public string? Curve { get; set; }
}

public sealed class ConfigSnapshot
{
    public Dictionary<string, string> Raw { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed class ClassDefSnapshot
{
    public string ClassId { get; set; } = string.Empty;

    public string? Stroke { get; set; }

    public string? Animation { get; set; }
}

public sealed class AccessibilitySnapshot
{
    public string? Title { get; set; }

    public string? Description { get; set; }
}

public sealed class InteractionSnapshot
{
    public string TargetId { get; set; } = string.Empty;

    public string? Callback { get; set; }

    public string? Url { get; set; }
}

public sealed class DiagnosticSnapshot
{
    public string Severity { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? AffectedId { get; set; }
}
