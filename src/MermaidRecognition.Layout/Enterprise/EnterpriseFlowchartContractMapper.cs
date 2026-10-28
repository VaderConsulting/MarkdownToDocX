using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Maps an enterprise document onto a flowchart contract so layout and routing
/// use the same Sugiyama path as markdown2docx flowcharts. Nested compounds and
/// groups become subgraphs; junctions become circles.
/// </summary>
public static class EnterpriseFlowchartContractMapper
{
    /// <summary>
    /// Builds the flowchart contract used for enterprise layout and edge routing.
    /// </summary>
    /// <param name="Document">Validated enterprise document.</param>
    /// <returns>Flowchart contract.</returns>
    public static DiagramContract Build(EnterpriseDocument Document)
    {
        if (Document == null)
        {
            throw new ArgumentNullException(nameof(Document));
        }

        DiagramContract Contract = new DiagramContract
        {
            Kind = "flowchart",
            Header = "flowchart",
            LayoutMode = Document.LayoutMode,
            Direction = Document.Direction.ToString().ToUpperInvariant()
        };

        HashSet<string> Containers = CollectContainers(Document);
        HashSet<string> WrittenGroups = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> WrittenNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string ContainerId in Containers)
        {
            if (ParentContainerId(Document, ContainerId, Containers) != null)
            {
                continue;
            }

            AddGroupTree(Contract, Document, ContainerId, Containers, WrittenGroups, WrittenNodes);
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (Containers.Contains(Element.Id) || WrittenNodes.Contains(Element.Id))
            {
                continue;
            }

            AddLeafNode(Contract, Document, Element, Containers, WrittenNodes);
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            if (!WrittenNodes.Contains(Note.Id))
            {
                AddNoteNode(Contract, Document, Note, Containers, WrittenNodes);
            }
        }

        int CreationIndex = 0;
        HashSet<string> DeclaredEndpointIds = Document.Elements.Keys
            .Concat(Document.Groups.Keys)
            .Concat(Document.Notes.Keys)
            .ToHashSet(StringComparer.Ordinal);
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            // Invalid references are not drawable graph edges: omitting them avoids
            // creating model routes to nodes which have no geometry. Keep an
            // explicit contract diagnostic as well as the parser/validator error,
            // so a dropped link cannot disappear from layout output unnoticed.
            if (!Relationship.HiddenByNesting
                && (!DeclaredEndpointIds.Contains(Relationship.SourceId)
                    || !DeclaredEndpointIds.Contains(Relationship.TargetId)))
            {
                string LinkId = Relationship.SourceId + "->" + Relationship.TargetId + ":"
                    + CreationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string Missing = !DeclaredEndpointIds.Contains(Relationship.SourceId)
                    ? "source" : "target";
                string MissingId = Missing == "source" ? Relationship.SourceId : Relationship.TargetId;
                MermaidSourceSpan? Span = Relationship.LineNumber <= 0 ? null
                    : new MermaidSourceSpan(0, 0, Relationship.LineNumber, 1,
                        Relationship.LineNumber, 1);
                Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                    MermaidDiagnosticSeverity.Error,
                    "enterprise.dropped-link",
                    "Dropped relationship " + LinkId + ": undeclared " + Missing + " endpoint '" + MissingId + "'.",
                    Span,
                    LinkId));
                CreationIndex++;
                continue;
            }

            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            EdgeContract Edge = new EdgeContract
            {
                Id = Relationship.SourceId + "->" + Relationship.TargetId + ":"
                    + CreationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceId = Relationship.SourceId,
                TargetId = Relationship.TargetId,
                Label = EdgeLabel(Relationship),
                MarkerStart = EdgeEndpointMarker.None,
                MarkerEnd = EdgeEndpointMarker.None,
                CreationIndex = CreationIndex,
                MinLength = Relationship.MinLength,
                RoutingHint = Relationship.RoutingHint
            };
            Edge.MetadataMutable["enterpriseRelationshipKind"] = new RawMetadata("enterpriseRelationshipKind", Relationship.Kind.ToString());
            Contract.EdgesMutable.Add(Edge);
            CreationIndex++;
        }

        foreach (EnterprisePlainConnection Connection in Document.PlainConnections)
        {
            EdgeContract Edge = new EdgeContract
            {
                Id = Connection.SourceId + "---" + Connection.TargetId + ":"
                    + CreationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceId = Connection.SourceId,
                TargetId = Connection.TargetId,
                MarkerStart = EdgeEndpointMarker.None,
                MarkerEnd = EdgeEndpointMarker.None,
                CreationIndex = CreationIndex,
                MinLength = Connection.MinLength,
                RoutingHint = Connection.RoutingHint
            };
            Edge.MetadataMutable["enterprisePlainConnection"] = new RawMetadata("enterprisePlainConnection", "true");
            Contract.EdgesMutable.Add(Edge);
            CreationIndex++;
        }

        return Contract;
    }

    private static void AddGroupTree(
        DiagramContract Contract,
        EnterpriseDocument Document,
        string ContainerId,
        HashSet<string> Containers,
        HashSet<string> WrittenGroups,
        HashSet<string> WrittenNodes)
    {
        if (!WrittenGroups.Add(ContainerId))
        {
            return;
        }

        GroupContract Group = new GroupContract
        {
            Id = ContainerId,
            Title = string.Join("\n", EnterpriseNodeSizer.Wrap(ContainerTitle(Document, ContainerId))),
            ParentGroupId = ParentContainerId(Document, ContainerId, Containers),
            Direction = Document.Groups.TryGetValue(ContainerId, out EnterpriseGroup? DeclaredGroup)
                ? DeclaredGroup.Direction?.ToString().ToUpperInvariant() : null,
            PlacementHint = DeclaredGroup?.PlacementHint
        };
        Contract.GroupsMutable.Add(Group);
        foreach (string ChildId in ChildIds(Document, ContainerId))
        {
            if (Containers.Contains(ChildId))
            {
                Group.ChildGroupIdsMutable.Add(ChildId);
                AddGroupTree(Contract, Document, ChildId, Containers, WrittenGroups, WrittenNodes);
            }
            else if (Document.Elements.TryGetValue(ChildId, out EnterpriseElement? Child))
            {
                Group.ChildNodeIdsMutable.Add(ChildId);
                AddLeafNode(Contract, Document, Child, Containers, WrittenNodes);
            }
            else if (Document.Notes.TryGetValue(ChildId, out EnterpriseNote? Note))
            {
                Group.ChildNodeIdsMutable.Add(ChildId);
                AddNoteNode(Contract, Document, Note, Containers, WrittenNodes);
            }
        }
    }

    private static void AddNoteNode(
        DiagramContract Contract,
        EnterpriseDocument Document,
        EnterpriseNote Note,
        HashSet<string> Containers,
        HashSet<string> WrittenNodes)
    {
        if (!WrittenNodes.Add(Note.Id))
        {
            return;
        }

        (double Width, double Height) Size = EnterpriseNodeSizer.MeasureNote(Note);
        NodeContract Node = new NodeContract
        {
            Id = Note.Id,
            Label = Note.Label,
            Shape = "note",
            ParentGroupId = ParentContainerId(Document, Note.Id, Containers),
            LayoutWidth = Size.Width,
            LayoutHeight = Size.Height,
            PlacementHint = Note.PlacementHint
        };
        Node.MetadataMutable["enterpriseNote"] = new RawMetadata("enterpriseNote", "true");
        Contract.NodesMutable.Add(Node);
    }

    private static void AddLeafNode(
        DiagramContract Contract,
        EnterpriseDocument Document,
        EnterpriseElement Element,
        HashSet<string> Containers,
        HashSet<string> WrittenNodes)
    {
        if (!WrittenNodes.Add(Element.Id))
        {
            return;
        }

        (double Width, double Height) Size = EnterpriseNodeSizer.MeasureElement(Element);
        string Label = Element.ElementType == EnterpriseElementType.Junction ? " " : Element.Label;
        NodeContract NodeContract = new NodeContract
        {
            Id = Element.Id,
            Label = Label,
            Shape = RoutingShape(Element),
            ParentGroupId = ParentContainerId(Document, Element.Id, Containers),
            LayoutWidth = Size.Width,
            LayoutHeight = Size.Height,
            GeometryProvider = EnterpriseElementCatalog.Elements[Element.ElementType].BoxKind is
                EnterpriseBoxKind.Contract or EnterpriseBoxKind.Representation
                ? new EnterpriseDecoratedNodeGeometry(EnterpriseElementCatalog.Elements[Element.ElementType].BoxKind)
                : null,
            PlacementHint = Element.PlacementHint
        };
        NodeContract.MetadataMutable["enterpriseElementType"] = new RawMetadata(
            "enterpriseElementType", Element.ElementType.ToString());
        Contract.NodesMutable.Add(NodeContract);
    }

    internal static string EdgeLabel(EnterpriseRelationship Relationship)
    {
        string Label = Relationship.Label ?? string.Empty;
        if (Relationship.Kind == EnterpriseRelationshipKind.Influence && !string.IsNullOrEmpty(Relationship.InfluenceStrength))
        {
            return Label.Length == 0
                ? Relationship.InfluenceStrength
                : Label + " (" + Relationship.InfluenceStrength + ")";
        }

        return Label;
    }

    internal static HashSet<string> CollectContainers(EnterpriseDocument Document)
    {
        HashSet<string> Containers = new HashSet<string>(StringComparer.Ordinal);
        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            Containers.Add(Group.Id);
            if (!string.IsNullOrEmpty(Group.ParentId))
            {
                Containers.Add(Group.ParentId);
            }
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (!string.IsNullOrEmpty(Element.ParentId))
            {
                Containers.Add(Element.ParentId);
            }
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            if (!string.IsNullOrEmpty(Note.ParentId))
            {
                Containers.Add(Note.ParentId);
            }
        }

        return Containers;
    }

    internal static string ContainerTitle(EnterpriseDocument Document, string ContainerId)
    {
        if (Document.Groups.TryGetValue(ContainerId, out EnterpriseGroup? Group)
            && !string.IsNullOrEmpty(Group.Label))
        {
            return Group.Label;
        }

        if (Document.Elements.TryGetValue(ContainerId, out EnterpriseElement? Element))
        {
            return Element.Label;
        }

        return ContainerId;
    }

    internal static string? ParentContainerId(
        EnterpriseDocument Document,
        string Id,
        HashSet<string> Containers)
    {
        string? ParentId = null;
        if (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Group))
        {
            ParentId = Group.ParentId;
        }
        else if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            ParentId = Element.ParentId;
        }
        else if (Document.Notes.TryGetValue(Id, out EnterpriseNote? Note))
        {
            ParentId = Note.ParentId;
        }

        if (string.IsNullOrEmpty(ParentId) || !Containers.Contains(ParentId))
        {
            return null;
        }

        return ParentId;
    }

    internal static List<string> ChildIds(EnterpriseDocument Document, string ContainerId)
    {
        List<string> Children = [];
        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            if (string.Equals(Group.ParentId, ContainerId, StringComparison.Ordinal))
            {
                Children.Add(Group.Id);
            }
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (string.Equals(Element.ParentId, ContainerId, StringComparison.Ordinal)
                && !Children.Contains(Element.Id))
            {
                Children.Add(Element.Id);
            }
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            if (string.Equals(Note.ParentId, ContainerId, StringComparison.Ordinal)
                && !Children.Contains(Note.Id))
            {
                Children.Add(Note.Id);
            }
        }

        return Children;
    }

    internal static string RoutingShape(EnterpriseElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            return "circle";
        }

        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Element.ElementType];
        if (Metadata.BoxKind == EnterpriseBoxKind.Ellipse)
        {
            return "ellipse";
        }

        if (Metadata.Aspect == EnterpriseAspect.Behaviour)
        {
            // Behaviour elements have short rounded corners, not semicircular
            // ends. Side ports must stay on the drawn rectangle's flat edge.
            return "rounded";
        }

        return "rect";
    }
}
