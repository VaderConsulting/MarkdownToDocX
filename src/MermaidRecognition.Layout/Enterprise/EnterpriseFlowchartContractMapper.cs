using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
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
            Direction = Document.Direction == EnterpriseDiagramDirection.Lr ? "LR" : "TB"
        };

        HashSet<string> Containers = CollectContainers(Document);
        foreach (string ContainerId in Containers)
        {
            GroupContract Group = new GroupContract
            {
                Id = ContainerId,
                Title = ContainerTitle(Document, ContainerId),
                ParentGroupId = ParentContainerId(Document, ContainerId, Containers)
            };
            foreach (string ChildId in ChildIds(Document, ContainerId))
            {
                if (Containers.Contains(ChildId))
                {
                    Group.ChildGroupIdsMutable.Add(ChildId);
                }
                else
                {
                    Group.ChildNodeIdsMutable.Add(ChildId);
                }
            }

            Contract.GroupsMutable.Add(Group);
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (Containers.Contains(Element.Id))
            {
                continue;
            }

            NodeContract NodeContract = new NodeContract
            {
                Id = Element.Id,
                Label = Element.Label,
                Shape = RoutingShape(Element),
                ParentGroupId = ParentContainerId(Document, Element.Id, Containers)
            };
            Contract.NodesMutable.Add(NodeContract);
        }

        int CreationIndex = 0;
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
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
                Stroke = FlowchartStroke(Relationship),
                MarkerStart = EdgeEndpointMarker.None,
                MarkerEnd = EdgeEndpointMarker.None,
                CreationIndex = CreationIndex
            };
            Contract.EdgesMutable.Add(Edge);
            CreationIndex++;
        }

        return Contract;
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

    internal static string? FlowchartStroke(EnterpriseRelationship Relationship)
    {
        string Style = EnterpriseElementCatalog.Relationships[Relationship.Kind].LineStyle;
        if (Style == "dotted")
        {
            return "dotted";
        }

        if (Style == "dashed")
        {
            return "dashed";
        }

        return null;
    }

    private static HashSet<string> CollectContainers(EnterpriseDocument Document)
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

        return Containers;
    }

    private static string ContainerTitle(EnterpriseDocument Document, string ContainerId)
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

    private static string? ParentContainerId(
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

        if (string.IsNullOrEmpty(ParentId) || !Containers.Contains(ParentId))
        {
            return null;
        }

        return ParentId;
    }

    private static List<string> ChildIds(EnterpriseDocument Document, string ContainerId)
    {
        List<string> Children = new List<string>();
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

        return Children;
    }

    private static string RoutingShape(EnterpriseElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            return "circle";
        }

        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Element.ElementType];
        if (Metadata.BoxKind == EnterpriseBoxKind.Ellipse)
        {
            return "circle";
        }

        if (Metadata.Aspect == EnterpriseAspect.Behaviour)
        {
            return "stadium";
        }

        return "rect";
    }
}
