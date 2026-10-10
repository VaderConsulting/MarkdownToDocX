using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Writes an equivalent Mermaid flowchart for an enterprise document: same ids,
/// same edge directions, compounds and groups as nested subgraphs, junctions as
/// small circles. Declaration order matches <see cref="EnterpriseFlowchartContractMapper"/>.
/// </summary>
public static class EnterpriseFlowchartSourceWriter
{
    /// <summary>
    /// Builds <c>flowchart TB</c> or <c>flowchart LR</c> source for the document.
    /// </summary>
    /// <param name="Document">Validated enterprise document.</param>
    /// <returns>Mermaid flowchart source.</returns>
    public static string Write(EnterpriseDocument Document)
    {
        if (Document == null)
        {
            throw new ArgumentNullException(nameof(Document));
        }

        HashSet<string> Containers = EnterpriseFlowchartContractMapper.CollectContainers(Document);
        StringBuilder Builder = new StringBuilder();
        Builder.Append("flowchart ").Append(Document.Direction.ToString().ToUpperInvariant());
        Builder.Append('\n');

        HashSet<string> WrittenNodes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> WrittenContainers = new HashSet<string>(StringComparer.Ordinal);
        foreach (string ContainerId in Containers)
        {
            if (EnterpriseFlowchartContractMapper.ParentContainerId(Document, ContainerId, Containers) != null)
            {
                continue;
            }

            AppendSubgraph(Builder, Document, ContainerId, Containers, WrittenContainers, WrittenNodes, 1);
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (Containers.Contains(Element.Id) || WrittenNodes.Contains(Element.Id))
            {
                continue;
            }

            AppendNode(Builder, Element, 1);
            WrittenNodes.Add(Element.Id);
        }

        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            Builder.Append("  ");
            Builder.Append(Relationship.SourceId);
            string Label = EnterpriseFlowchartContractMapper.EdgeLabel(Relationship);
            Builder.Append(Label.Length == 0 ? " --> " : " -->|");

            if (Label.Length > 0)
            {
                Builder.Append(EscapeLabel(Label));
                Builder.Append("| ");
            }

            Builder.Append(Relationship.TargetId);
            Builder.Append('\n');
        }

        return Builder.ToString();
    }

    private static void AppendSubgraph(
        StringBuilder Builder,
        EnterpriseDocument Document,
        string ContainerId,
        HashSet<string> Containers,
        HashSet<string> WrittenContainers,
        HashSet<string> WrittenNodes,
        int Depth)
    {
        if (!WrittenContainers.Add(ContainerId))
        {
            return;
        }

        string Indent = new string(' ', Depth * 2);
        Builder.Append(Indent);
        Builder.Append("subgraph ");
        Builder.Append(ContainerId);
        Builder.Append("[\"");
        Builder.Append(EscapeLabel(EnterpriseFlowchartContractMapper.ContainerTitle(Document, ContainerId)));
        Builder.Append("\"]\n");
        foreach (string ChildId in EnterpriseFlowchartContractMapper.ChildIds(Document, ContainerId))
        {
            if (Containers.Contains(ChildId))
            {
                AppendSubgraph(Builder, Document, ChildId, Containers, WrittenContainers, WrittenNodes, Depth + 1);
            }
            else if (Document.Elements.TryGetValue(ChildId, out EnterpriseElement? Child))
            {
                AppendNode(Builder, Child, Depth + 1);
                WrittenNodes.Add(ChildId);
            }
        }

        Builder.Append(Indent);
        Builder.Append("end\n");
    }

    private static void AppendNode(StringBuilder Builder, EnterpriseElement Element, int Depth)
    {
        string Indent = new string(' ', Depth * 2);
        Builder.Append(Indent);
        Builder.Append(Element.Id);
        string Shape = EnterpriseFlowchartContractMapper.RoutingShape(Element);
        string Label = Element.ElementType == EnterpriseElementType.Junction ? " " : EscapeLabel(Element.Label);
        if (Shape == "circle")
        {
            Builder.Append("((\"");
            Builder.Append(Label);
            Builder.Append("\"))\n");
            return;
        }

        if (Shape == "rounded")
        {
            Builder.Append("(\"");
            Builder.Append(Label);
            Builder.Append("\")\n");
            return;
        }

        Builder.Append("[\"");
        Builder.Append(Label);
        Builder.Append("\"]\n");
    }

    private static string EscapeLabel(string Label)
    {
        return Label.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
